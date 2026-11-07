using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using ARM.Models;

namespace ARM.Services.Sync;

// Messaggio scambiato tra telefono e PC
public class SyncMessage
{
    public string Type { get; set; }
    public string DeviceId { get; set; }
    public string DeviceName { get; set; }
    public string Token { get; set; }
    public SyncPayload Payload { get; set; }
    public List<string> SongIds { get; set; }
    public string SongId { get; set; }          // id del file richiesto (brano o copertina)
    public string Kind { get; set; }            // "song" o "cover"
    public long Size { get; set; }
    public string Error { get; set; }
}

// Serializzazione generata a compilazione: funziona anche con il "trimming" delle build Android
[JsonSourceGenerationOptions(DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(SyncMessage))]
[JsonSerializable(typeof(Note))]
[JsonSerializable(typeof(TaskItem))]
[JsonSerializable(typeof(CalendarEvent))]
[JsonSerializable(typeof(Ordine))]
[JsonSerializable(typeof(Playlist))]
[JsonSerializable(typeof(Song))]
public partial class SyncJson : JsonSerializerContext { }

// Canale TCP cifrato: ogni pacchetto è [lunghezza][IV | dati cifrati AES-CBC | HMAC-SHA256]
public sealed class SyncChannel : IDisposable
{
    public const int Port = 47800;
    public const int DiscoveryPort = 47801;
    public const byte ModePairing = (byte)'P';
    public const byte ModeSession = (byte)'S';
    public const byte ModeDevice = (byte)'D';   // sessione di un dispositivo associato: segue il suo id in chiaro
    private const int MaxFrame = 16 * 1024 * 1024;

    private readonly TcpClient _client;
    private readonly NetworkStream _stream;
    private readonly byte[] _encKey;
    private readonly byte[] _macKey;

    public SyncChannel(TcpClient client, byte[] secret)
    {
        _client = client;
        _stream = client.GetStream();
        var h = SHA512.HashData(secret);
        _encKey = h[..32];
        _macKey = h[32..];
    }

    public NetworkStream Stream => _stream;

    // Id del dispositivo all'inizio della sessione: [lunghezza][testo], così il PC sa quale chiave usare
    public static async Task WriteIdAsync(NetworkStream stream, string id)
    {
        var bytes = Encoding.UTF8.GetBytes(id);
        await stream.WriteAsync(new[] { (byte)bytes.Length });
        await stream.WriteAsync(bytes);
    }

    public static async Task<string> ReadIdAsync(NetworkStream stream)
    {
        var len = new byte[1];
        if (await stream.ReadAsync(len) != 1 || len[0] == 0) throw new IOException("Id mancante");
        var buffer = new byte[len[0]];
        int read = 0;
        while (read < buffer.Length)
        {
            int n = await stream.ReadAsync(buffer.AsMemory(read));
            if (n == 0) throw new IOException("Connessione chiusa");
            read += n;
        }
        return Encoding.UTF8.GetString(buffer);
    }

    // Chiave temporanea usata solo durante l'abbinamento (deriva dal codice a 6 cifre)
    public static byte[] KeyFromCode(string code) => SHA256.HashData(Encoding.UTF8.GetBytes("ARM-abbinamento:" + code.Trim()));

    public async Task SendBytesAsync(byte[] plain, CancellationToken ct)
    {
        using var aes = Aes.Create();
        aes.Key = _encKey;
        aes.GenerateIV();
        var cipher = aes.EncryptCbc(plain, aes.IV);

        var frame = new byte[16 + cipher.Length + 32];
        aes.IV.CopyTo(frame, 0);
        cipher.CopyTo(frame, 16);
        HMACSHA256.HashData(_macKey, frame.AsSpan(0, 16 + cipher.Length)).CopyTo(frame, 16 + cipher.Length);

        var len = new byte[4];
        System.Buffers.Binary.BinaryPrimitives.WriteInt32BigEndian(len, frame.Length);
        await _stream.WriteAsync(len, ct);
        await _stream.WriteAsync(frame, ct);
    }

    public async Task<byte[]> ReceiveBytesAsync(CancellationToken ct)
    {
        var len = await ReadExactAsync(4, ct);
        int size = System.Buffers.Binary.BinaryPrimitives.ReadInt32BigEndian(len);
        if (size < 48 || size > MaxFrame) throw new IOException("Pacchetto non valido");

        var frame = await ReadExactAsync(size, ct);
        var expected = HMACSHA256.HashData(_macKey, frame.AsSpan(0, size - 32));
        if (!CryptographicOperations.FixedTimeEquals(expected, frame.AsSpan(size - 32)))
            throw new CryptographicException("Chiave errata");

        using var aes = Aes.Create();
        aes.Key = _encKey;
        return aes.DecryptCbc(frame.AsSpan(16, size - 48), frame.AsSpan(0, 16));
    }

    public Task SendAsync(SyncMessage m, CancellationToken ct) =>
        SendBytesAsync(JsonSerializer.SerializeToUtf8Bytes(m, SyncJson.Default.SyncMessage), ct);

    public async Task<SyncMessage> ReceiveAsync(CancellationToken ct)
    {
        var msg = JsonSerializer.Deserialize(await ReceiveBytesAsync(ct), SyncJson.Default.SyncMessage);
        if (msg?.Type == "error") throw new IOException(msg.Error ?? "Errore dall'altro dispositivo");
        return msg;
    }

    // Invia una richiesta e aspetta la risposta (timeout 30 secondi)
    public async Task<SyncMessage> RequestAsync(SyncMessage m, int timeoutSeconds = 30)
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds));
        await SendAsync(m, cts.Token);
        return await ReceiveAsync(cts.Token);
    }

    // ---- File (brani) a pezzi da 256 KB ----
    public async Task SendFileAsync(string path, CancellationToken ct)
    {
        using var file = File.OpenRead(path);
        var buffer = new byte[256 * 1024];
        int read;
        while ((read = await file.ReadAsync(buffer, ct)) > 0)
            await SendBytesAsync(buffer[..read], ct);
    }

    public async Task ReceiveFileAsync(string path, long size, CancellationToken ct)
    {
        using var file = File.Create(path);
        long received = 0;
        while (received < size)
        {
            var chunk = await ReceiveBytesAsync(ct);
            await file.WriteAsync(chunk, ct);
            received += chunk.Length;
        }
    }

    private async Task<byte[]> ReadExactAsync(int count, CancellationToken ct)
    {
        var buffer = new byte[count];
        int offset = 0;
        while (offset < count)
        {
            int n = await _stream.ReadAsync(buffer.AsMemory(offset, count - offset), ct);
            if (n == 0) throw new IOException("Connessione chiusa");
            offset += n;
        }
        return buffer;
    }

    public void Dispose()
    {
        _stream.Dispose();
        _client.Dispose();
    }
}
