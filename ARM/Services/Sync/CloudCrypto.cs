using System.Security.Cryptography;
using System.Text;

namespace ARM.Services.Sync;

// Cifratura dei dati sincronizzati: il server vede solo blocchi illeggibili.
// - una chiave principale casuale da 32 byte, creata sul primo dispositivo e passata agli altri con un codice
// - dalla chiave principale (HKDF) una chiave per cifrare (AES-GCM) e una per nascondere gli identificativi (HMAC)
public class CloudCrypto
{
    private const int NonceSize = 12;
    private const int TagSize = 16;
    private const int PairingIterations = 300_000;
    private static readonly byte[] PairingAad = Encoding.UTF8.GetBytes("arm-pairing-v1");

    private readonly byte[] _enc;
    private readonly byte[] _mac;

    public CloudCrypto(byte[] master)
    {
        _enc = HKDF.DeriveKey(HashAlgorithmName.SHA256, master, 32, info: Encoding.UTF8.GetBytes("arm-enc-v1"));
        _mac = HKDF.DeriveKey(HashAlgorithmName.SHA256, master, 32, info: Encoding.UTF8.GetBytes("arm-mac-v1"));
    }

    public static byte[] NewMasterKey() => RandomNumberGenerator.GetBytes(32);

    // Identificativo sul server: non rivela tipo né id dell'elemento (es. quale brano di YouTube)
    public string ServerKey(string entryKey) =>
        Base64Url(HMACSHA256.HashData(_mac, Encoding.UTF8.GetBytes(entryKey)));

    // nonce (12) | testo cifrato | tag (16), in base64. aad lega il contenuto al suo identificativo.
    public string Encrypt(byte[] plain, string aad) => Convert.ToBase64String(Seal(_enc, plain, Encoding.UTF8.GetBytes(aad)));

    public byte[] Decrypt(string payload, string aad) => Open(_enc, Convert.FromBase64String(payload), Encoding.UTF8.GetBytes(aad));

    public byte[] EncryptBytes(byte[] plain, string aad) => Seal(_enc, plain, Encoding.UTF8.GetBytes(aad));

    public byte[] DecryptBytes(byte[] data, string aad) => Open(_enc, data, Encoding.UTF8.GetBytes(aad));

    // ---- Abbinamento: la chiave principale cifrata con una chiave derivata dal codice ----

    // Codice da 10 caratteri senza simboli ambigui (niente 0/O, 1/I/L): circa 49 bit
    public static string NewPairingCode()
    {
        const string alfabeto = "ABCDEFGHJKMNPQRSTUVWXYZ23456789";
        var chars = new char[10];
        for (int i = 0; i < chars.Length; i++) chars[i] = alfabeto[RandomNumberGenerator.GetInt32(alfabeto.Length)];
        return new string(chars);
    }

    public static string NormalizeCode(string code) =>
        new string((code ?? "").ToUpperInvariant().Where(char.IsLetterOrDigit).ToArray());

    public static (string Wrapped, string Salt) WrapMaster(byte[] master, string code)
    {
        var salt = RandomNumberGenerator.GetBytes(16);
        var key = Rfc2898DeriveBytes.Pbkdf2(NormalizeCode(code), salt, PairingIterations, HashAlgorithmName.SHA256, 32);
        return (Convert.ToBase64String(Seal(key, master, PairingAad)), Convert.ToBase64String(salt));
    }

    // null = codice sbagliato
    public static byte[] UnwrapMaster(string wrapped, string salt, string code)
    {
        try
        {
            var key = Rfc2898DeriveBytes.Pbkdf2(NormalizeCode(code), Convert.FromBase64String(salt), PairingIterations, HashAlgorithmName.SHA256, 32);
            return Open(key, Convert.FromBase64String(wrapped), PairingAad);
        }
        catch (CryptographicException) { return null; }
    }

    private static byte[] Seal(byte[] key, byte[] plain, byte[] aad)
    {
        var output = new byte[NonceSize + plain.Length + TagSize];
        var nonce = output.AsSpan(0, NonceSize);
        RandomNumberGenerator.Fill(nonce);
        using var aes = new AesGcm(key, TagSize);
        aes.Encrypt(nonce, plain, output.AsSpan(NonceSize, plain.Length), output.AsSpan(NonceSize + plain.Length, TagSize), aad);
        return output;
    }

    private static byte[] Open(byte[] key, byte[] data, byte[] aad)
    {
        if (data.Length < NonceSize + TagSize) throw new CryptographicException("Dati troppo corti");
        int len = data.Length - NonceSize - TagSize;
        var plain = new byte[len];
        using var aes = new AesGcm(key, TagSize);
        aes.Decrypt(data.AsSpan(0, NonceSize), data.AsSpan(NonceSize, len), data.AsSpan(NonceSize + len, TagSize), plain, aad);
        return plain;
    }

    private static string Base64Url(byte[] b) => Convert.ToBase64String(b).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
