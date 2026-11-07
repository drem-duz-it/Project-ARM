using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using ARMusic.Data;

namespace ARMusic.Services.Sync;

// Un dispositivo (telefono, tablet...) associato al PC
public class PairedDevice
{
    public string Id;
    public string Name;
    public DateTime? LastSync;
}

// Sincronizzazione via Wi-Fi tra i dispositivi Android e il PC.
// - Il PC fa da "base": resta in ascolto mentre ARM è aperto e risponde alla ricerca in rete.
//   Può avere più dispositivi associati (es. telefono e tablet), ognuno con la sua chiave.
// - Ogni dispositivo trova il PC, gli manda le sue modifiche, riceve quelle del PC (comprese quelle
//   arrivate dagli altri dispositivi) e le unisce. Se la stessa cosa è cambiata su entrambi, chiede all'utente.
public class SyncService
{
    private const string DiscoverRequest = "ARM-CERCA";
    private const string DiscoverReply = "ARM-PC";

    private readonly DatabaseService _db;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private TcpListener _listener;
    private UdpClient _discovery;
    private IDispatcherTimer _timer;
    private CancellationTokenSource _debounce;

    // Abbinamento in corso sul PC
    private string _pairingCode;
    private DateTime _pairingExpires;

    public SyncService(DatabaseService db)
    {
        _db = db;
        // Sul dispositivo Android: quando modifichi qualcosa, prova a sincronizzare dopo qualche secondo
        _db.DataChanged += () =>
        {
            if (IsHost || !IsPaired || _lock.CurrentCount == 0) return;
            _debounce?.Cancel();
            var cts = _debounce = new CancellationTokenSource();
            _ = Task.Delay(3000, cts.Token).ContinueWith(t => { if (!t.IsCanceled) _ = SyncAsync(null); });
        };
    }

    // ---------- Stato (per la schermata e la pagina Oggi) ----------
    public event Action StatusChanged;   // testo di stato aggiornato
    public event Action DataSynced;      // dati cambiati o conflitti trovati: ridisegnare le pagine

    public string Status { get; private set; } = "";
    public bool IsBusy => _lock.CurrentCount == 0;
    public int PendingConflicts { get; private set; }

    public bool IsHost
    {
        get
        {
#if DEBUG
            // Solo per prove: fa comportare un'istanza Windows come se fosse il telefono
            if (Environment.GetEnvironmentVariable("ARM_SYNC_ROLE") == "client") return false;
#endif
            return DeviceInfo.Platform == DevicePlatform.WinUI;
        }
    }

    // Le impostazioni hanno un prefisso per ruolo, così due istanze di prova sullo stesso PC non si confondono
    private string K(string name) => $"{Prefix}.{(IsHost ? "pc" : "tel")}.{name}";

    // Nelle prove (versione di debug con database di prova) le impostazioni di sincronizzazione sono separate:
    // così un test non tocca mai l'abbinamento vero con i tuoi dispositivi
    private static string Prefix
    {
        get
        {
#if DEBUG
            if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("ARMUSIC_DB_PATH"))) return "SyncTest";
#endif
            return "Sync";
        }
    }

    public string DeviceId
    {
        get
        {
            var id = Preferences.Default.Get(K("DeviceId"), "");
            if (id == "") { id = Guid.NewGuid().ToString("N"); Preferences.Default.Set(K("DeviceId"), id); }
            return id;
        }
    }

    public string DeviceName => string.IsNullOrWhiteSpace(DeviceInfo.Name) ? (IsHost ? "PC" : "Dispositivo Android") : DeviceInfo.Name;

    public bool IsPaired => IsHost ? DeviceIds.Count > 0 : Preferences.Default.Get(K("Token"), "") != "";

    // Telefono: nome del PC. PC: nomi dei dispositivi associati.
    public string PeerName => IsHost ? string.Join(", ", Devices.Select(d => d.Name)) : Preferences.Default.Get(K("PeerName"), "");

    public DateTime? LastSync => IsHost ? Devices.Max(d => d.LastSync) : ReadTime(K("LastOk"));

    private static DateTime? ReadTime(string key)
    {
        var t = Preferences.Default.Get(key, 0L);
        return t == 0 ? null : new DateTime(t, DateTimeKind.Utc).ToLocalTime();
    }

    private void SetStatus(string text)
    {
        Status = text;
        MainThread.BeginInvokeOnMainThread(() => StatusChanged?.Invoke());
    }

    // Chiamato quando qualcun altro (es. un assistente esterno) ha cambiato i dati: le pagine aperte si ridisegnano
    public void RefreshPages() => RaiseDataSynced();

    private void RaiseDataSynced() => MainThread.BeginInvokeOnMainThread(() => DataSynced?.Invoke());

    // ---------- Dispositivi associati al PC ----------
    private string DK(string deviceId, string name) => K($"dev.{deviceId}.{name}");

    private List<string> DeviceIds =>
        Preferences.Default.Get(K("Devices"), "").Split(';', StringSplitOptions.RemoveEmptyEntries).ToList();

    public List<PairedDevice> Devices => DeviceIds.Select(id => new PairedDevice
    {
        Id = id,
        Name = Preferences.Default.Get(DK(id, "Name"), "Dispositivo"),
        LastSync = ReadTime(DK(id, "LastOk"))
    }).ToList();

    private void AddDevice(string id, string name, string token)
    {
        var ids = DeviceIds;
        if (!ids.Contains(id)) ids.Add(id);
        Preferences.Default.Set(K("Devices"), string.Join(";", ids));
        Preferences.Default.Set(DK(id, "Name"), name);
        Preferences.Default.Set(DK(id, "Token"), token);
        Preferences.Default.Remove(DK(id, "Mark"));     // la prima sincronizzazione confronta tutto
        Preferences.Default.Remove(DK(id, "LastOk"));
    }

    public void UnpairDevice(string id)
    {
        Preferences.Default.Set(K("Devices"), string.Join(";", DeviceIds.Where(x => x != id)));
        foreach (var k in new[] { "Name", "Token", "Mark", "LastOk" }) Preferences.Default.Remove(DK(id, k));
        SetStatus(IsPaired ? Loc.T("Dispositivo dissociato") : Loc.T("Nessun dispositivo associato"));
        RaiseDataSynced();
    }

    // Dalla 2.1 (un solo telefono) alla 2.1.1 (più dispositivi): sposta il telefono già associato nell'elenco
    private void MigrateSinglePeer()
    {
        var token = Preferences.Default.Get(K("Token"), "");
        if (token == "") return;
        var id = Preferences.Default.Get(K("PeerId"), Guid.NewGuid().ToString("N"));
        AddDevice(id, Preferences.Default.Get(K("PeerName"), "Telefono"), token);
        Preferences.Default.Set(K("LegacyDevice"), id);   // può ancora collegarsi con ARM 2.1 finché non lo aggiorni
        if (Preferences.Default.ContainsKey(K("Mark"))) Preferences.Default.Set(DK(id, "Mark"), Preferences.Default.Get(K("Mark"), 0L));
        if (Preferences.Default.ContainsKey(K("LastOk"))) Preferences.Default.Set(DK(id, "LastOk"), Preferences.Default.Get(K("LastOk"), 0L));
        foreach (var k in new[] { "PeerId", "PeerName", "Token", "Mark", "LastOk" }) Preferences.Default.Remove(K(k));
    }

    // ---------- Dispositivo Android: il PC associato ----------
    private long? ClientMark
    {
        get => Preferences.Default.ContainsKey(K("Mark")) ? Preferences.Default.Get(K("Mark"), 0L) : null;
        set { if (value == null) Preferences.Default.Remove(K("Mark")); else Preferences.Default.Set(K("Mark"), value.Value); }
    }

    private byte[] ClientToken => Convert.FromBase64String(Preferences.Default.Get(K("Token"), ""));

    private void SavePc(string id, string name, string token, string host)
    {
        Preferences.Default.Set(K("PeerId"), id);
        Preferences.Default.Set(K("PeerName"), name);
        Preferences.Default.Set(K("Token"), token);
        if (host != null) Preferences.Default.Set(K("LastHost"), host);
        ClientMark = null;   // la prima sincronizzazione confronta tutto
        Preferences.Default.Remove(K("LastOk"));
    }

    public void Unpair()
    {
        if (IsHost)
        {
            foreach (var id in DeviceIds) UnpairDevice(id);
            return;
        }
        foreach (var k in new[] { "PeerId", "PeerName", "Token", "LastHost", "Mark", "LastOk" })
            Preferences.Default.Remove(K(k));
        PendingConflicts = 0;
        SetStatus(Loc.T("Non associato a nessun PC"));
        RaiseDataSynced();
    }

    // ---------- Avvio ----------
    public void Start()
    {
        if (IsHost)
        {
            MigrateSinglePeer();
            if (IsPaired) StartServer();
            SetStatus(IsPaired ? Loc.T("In attesa dei dispositivi sulla rete Wi-Fi") : Loc.T("Nessun dispositivo associato"));
        }
        else if (IsPaired)
        {
            // Dispositivo Android: controlla ogni minuto mentre l'app è aperta
            if (_timer == null)
            {
                _timer = Application.Current.Dispatcher.CreateTimer();
                _timer.Interval = TimeSpan.FromMinutes(1);
                _timer.Tick += (s, e) => _ = SyncAsync(null);
                _timer.Start();
            }
            _ = SyncAsync(null);
        }
        else SetStatus(Loc.T("Non associato a nessun PC"));
    }

    // ================= PC (base) =================

    public static string LocalAddress()
    {
        try
        {
            // Indirizzi IPv4 delle schede attive, preferendo Wi-Fi/Ethernet con un router e gli indirizzi di casa
            // (192.168.x, 10.x, 172.16-31.x) rispetto a VPN e simili (es. 100.x)
            var candidati = NetworkInterface.GetAllNetworkInterfaces()
                .Where(n => n.OperationalStatus == OperationalStatus.Up && n.NetworkInterfaceType != NetworkInterfaceType.Loopback)
                .SelectMany(n => n.GetIPProperties().UnicastAddresses
                    .Where(a => a.Address.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(a.Address))
                    .Select(a => new
                    {
                        a.Address,
                        Punteggio = (IsPrivate(a.Address) ? 4 : 0)
                                  + (n.NetworkInterfaceType is NetworkInterfaceType.Wireless80211 or NetworkInterfaceType.Ethernet ? 2 : 0)
                                  + (n.GetIPProperties().GatewayAddresses.Any(g => g.Address.AddressFamily == AddressFamily.InterNetwork) ? 1 : 0)
                    }));
            return candidati.OrderByDescending(c => c.Punteggio).FirstOrDefault()?.Address.ToString() ?? "";
        }
        catch { return ""; }
    }

    private static bool IsPrivate(IPAddress ip)
    {
        var b = ip.GetAddressBytes();
        return b[0] == 10 || (b[0] == 192 && b[1] == 168) || (b[0] == 172 && b[1] >= 16 && b[1] <= 31);
    }

    // Mostra un codice a 6 cifre da digitare sul dispositivo (valido 5 minuti)
    public string StartPairing()
    {
        _pairingCode = RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");
        _pairingExpires = DateTime.Now.AddMinutes(5);
        StartServer();
        SetStatus(Loc.T("Inserisci il codice sul dispositivo"));
        return _pairingCode;
    }

    public void StopPairing() => _pairingCode = null;
    public bool IsPairing => _pairingCode != null && DateTime.Now < _pairingExpires;

    private void StartServer()
    {
        if (_listener != null) return;
        try
        {
            _listener = new TcpListener(IPAddress.Any, SyncChannel.Port);
            _listener.Start();
            _ = AcceptLoopAsync(_listener);

            _discovery = new UdpClient(new IPEndPoint(IPAddress.Any, SyncChannel.DiscoveryPort));
            _ = DiscoveryLoopAsync(_discovery);
        }
        catch (Exception ex)
        {
            _listener = null;
            SetStatus(Loc.F("Impossibile mettersi in ascolto: {0}", ex.Message));
        }
    }

    private async Task DiscoveryLoopAsync(UdpClient udp)
    {
        while (true)
        {
            try
            {
                var r = await udp.ReceiveAsync();
                if (Encoding.UTF8.GetString(r.Buffer) != DiscoverRequest) continue;
                var reply = Encoding.UTF8.GetBytes($"{DiscoverReply}|{DeviceId}|{DeviceName}");
                await udp.SendAsync(reply, reply.Length, r.RemoteEndPoint);
            }
            catch (ObjectDisposedException) { return; }
            catch { /* pacchetto non valido: ignora */ }
        }
    }

    private async Task AcceptLoopAsync(TcpListener listener)
    {
        while (true)
        {
            TcpClient client;
            try { client = await listener.AcceptTcpClientAsync(); }
            catch { return; }
            _ = HandleClientAsync(client);
        }
    }

    private async Task HandleClientAsync(TcpClient client)
    {
        try
        {
            client.ReceiveTimeout = 60000;
            var stream = client.GetStream();
            var mode = new byte[1];
            if (await stream.ReadAsync(mode) != 1) { client.Dispose(); return; }

            // Abbinamento (chiave dal codice) oppure sessione di un dispositivo già associato (chiave del dispositivo)
            byte[] secret;
            string deviceId = null;
            if (mode[0] == SyncChannel.ModePairing)
            {
                if (_pairingCode == null || DateTime.Now > _pairingExpires) { client.Dispose(); return; }
                secret = SyncChannel.KeyFromCode(_pairingCode);
            }
            else if (mode[0] == SyncChannel.ModeDevice)
            {
                deviceId = await SyncChannel.ReadIdAsync(stream);
                var token = Preferences.Default.Get(DK(deviceId, "Token"), "");
                if (token == "" || !DeviceIds.Contains(deviceId)) { client.Dispose(); return; }
                secret = Convert.FromBase64String(token);
            }
            else if (mode[0] == SyncChannel.ModeSession)
            {
                // Dispositivo ancora con ARM 2.1 (non manda il suo id): è quello associato prima della 2.1.1
                deviceId = Preferences.Default.Get(K("LegacyDevice"), "");
                var token = deviceId == "" ? "" : Preferences.Default.Get(DK(deviceId, "Token"), "");
                if (token == "") { client.Dispose(); return; }
                secret = Convert.FromBase64String(token);
            }
            else { client.Dispose(); return; }

            using var ch = new SyncChannel(client, secret);
            long snapshot = 0;
            bool locked = false;
            string deviceName = deviceId != null ? Preferences.Default.Get(DK(deviceId, "Name"), "dispositivo") : "";
            try
            {
                while (true)
                {
                    using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(5));
                    var m = await ch.ReceiveAsync(cts.Token);   // chiave sbagliata → eccezione → chiude
                    switch (m.Type)
                    {
                        case "pair":
                            var newToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
                            AddDevice(m.DeviceId, m.DeviceName, newToken);
                            _pairingCode = null;
                            await ch.SendAsync(new SyncMessage { Type = "paired", DeviceId = DeviceId, DeviceName = DeviceName, Token = newToken }, cts.Token);
                            SetStatus(Loc.F("Associato a {0}", m.DeviceName));
                            RaiseDataSynced();
                            return;

                        case "hello":
                            await ch.SendAsync(new SyncMessage { Type = "hello", DeviceId = DeviceId, DeviceName = DeviceName }, cts.Token);
                            break;

                        case "changes":
                            // Un dispositivo alla volta: blocca finché non ha finito di applicare
                            if (!locked) { await _lock.WaitAsync(cts.Token); locked = true; }
                            SetStatus(Loc.F("Sincronizzazione con {0}...", deviceName));
                            snapshot = DatabaseService.Now;
                            long? mark = Preferences.Default.ContainsKey(DK(deviceId, "Mark")) ? Preferences.Default.Get(DK(deviceId, "Mark"), 0L) : null;
                            var mine = await _db.GetChangesAsync(mark);
                            await ch.SendAsync(new SyncMessage { Type = "changes", Payload = mine }, cts.Token);
                            break;

                        case "apply":
                            // propagate: le modifiche arrivate da questo dispositivo andranno anche agli altri
                            await _db.ApplyAsync(m.Payload, propagate: true);
                            Preferences.Default.Set(DK(deviceId, "Mark"), snapshot);
                            Preferences.Default.Set(DK(deviceId, "LastOk"), DateTime.UtcNow.Ticks);
                            await ch.SendAsync(new SyncMessage { Type = "applied" }, cts.Token);
                            RaiseDataSynced();
                            break;

                        case "files":
                            await ch.SendAsync(new SyncMessage { Type = "files", SongIds = await AvailableFilesAsync(m.Kind) }, cts.Token);
                            break;

                        case "getfile":
                            var path = await LocalFileAsync(m.Kind, m.SongId);
                            if (path == null)
                            {
                                await ch.SendAsync(new SyncMessage { Type = "error", Error = "File non trovato" }, cts.Token);
                                break;
                            }
                            await ch.SendAsync(new SyncMessage { Type = "file", SongId = m.SongId, Size = new FileInfo(path).Length }, cts.Token);
                            await ch.SendFileAsync(path, cts.Token);
                            break;

                        case "putfile":
                            var temp = Path.Combine(FileSystem.CacheDirectory, $"in-{Guid.NewGuid():N}.tmp");
                            await ch.ReceiveFileAsync(temp, m.Size, cts.Token);
                            await StoreReceivedFileAsync(m.Kind, m.SongId, temp);
                            await ch.SendAsync(new SyncMessage { Type = "stored" }, cts.Token);
                            break;

                        case "bye":
                            SetStatus(Loc.F("Sincronizzato con {0}", deviceName));
                            RaiseDataSynced();
                            return;
                    }
                }
            }
            finally
            {
                if (locked) _lock.Release();
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Sync (PC): {ex.Message}");
            client.Dispose();
        }
    }

    // ================= File: brani e copertine delle playlist =================
    private const string KindSong = "song";
    private const string KindCover = "cover";

    // Id dei file presenti su questo dispositivo
    private async Task<List<string>> AvailableFilesAsync(string kind)
    {
        if (kind == KindCover)
            return (await _db.GetAllPlaylistsAsync())
                .Where(p => !string.IsNullOrEmpty(p.ImageId) && File.Exists(p.ImagePath))
                .Select(p => p.ImageId).Distinct().ToList();

        return (await _db.GetAllSongsAsync()).Where(s => s.IsDownloaded && File.Exists(s.LocalFilePath)).Select(s => s.Id).ToList();
    }

    private async Task<string> LocalFileAsync(string kind, string id)
    {
        string path = kind == KindCover
            ? (await _db.GetAllPlaylistsAsync()).FirstOrDefault(p => p.ImageId == id)?.ImagePath
            : (await _db.GetAllSongsAsync()).FirstOrDefault(s => s.Id == id)?.LocalFilePath;
        return path != null && File.Exists(path) ? path : null;
    }

    private async Task StoreReceivedFileAsync(string kind, string id, string temp)
    {
        if (kind == KindCover)
        {
            var dest = DatabaseService.CoverPath(id);
            Directory.CreateDirectory(Path.GetDirectoryName(dest));
            File.Move(temp, dest, overwrite: true);
            await _db.SetCoverFileAsync(id, dest);
            return;
        }

        var song = (await _db.GetAllSongsAsync()).FirstOrDefault(s => s.Id == id);
        if (song == null) { File.Delete(temp); return; }
        var path = await MusicStorage.PublishAsync(temp, song);
        await _db.SetSongFileAsync(song.Id, path);
    }

    // ================= DISPOSITIVO ANDROID =================

    // Cerca il PC sulla rete Wi-Fi (risposta entro 1,5 secondi)
    public async Task<(string Host, string Id, string Name)?> DiscoverAsync()
    {
        try
        {
            using var udp = new UdpClient(0) { EnableBroadcast = true };
            var msg = Encoding.UTF8.GetBytes(DiscoverRequest);
            await udp.SendAsync(msg, msg.Length, new IPEndPoint(IPAddress.Broadcast, SyncChannel.DiscoveryPort));
#if DEBUG
            // Prove con due istanze sullo stesso PC
            await udp.SendAsync(msg, msg.Length, new IPEndPoint(IPAddress.Loopback, SyncChannel.DiscoveryPort));
#endif
            using var cts = new CancellationTokenSource(1500);
            while (true)
            {
                var r = await udp.ReceiveAsync(cts.Token);
                var parts = Encoding.UTF8.GetString(r.Buffer).Split('|');
                if (parts.Length < 3 || parts[0] != DiscoverReply) continue;
                var peerId = Preferences.Default.Get(K("PeerId"), "");
                if (IsPaired && parts[1] != peerId) continue;   // un altro PC con ARM: non è il nostro
                return (r.RemoteEndPoint.Address.ToString(), parts[1], parts[2]);
            }
        }
        catch { return null; }
    }

    private static async Task<TcpClient> ConnectAsync(string host)
    {
        var client = new TcpClient();
        try
        {
            using var cts = new CancellationTokenSource(2000);
            await client.ConnectAsync(host, SyncChannel.Port, cts.Token);
            return client;
        }
        catch { client.Dispose(); return null; }
    }

    // Abbina il dispositivo al PC con il codice mostrato sul PC. Restituisce null se va bene, altrimenti l'errore.
    public async Task<string> PairAsync(string host, string code)
    {
        var client = await ConnectAsync(host);
        if (client == null) return Loc.T("PC non raggiungibile. È aperto ARM sul PC, sulla stessa rete Wi-Fi?");
        try
        {
            await client.GetStream().WriteAsync(new[] { SyncChannel.ModePairing });
            using var ch = new SyncChannel(client, SyncChannel.KeyFromCode(code));
            var reply = await ch.RequestAsync(new SyncMessage { Type = "pair", DeviceId = DeviceId, DeviceName = DeviceName }, 10);
            if (reply.Type != "paired") return Loc.T("Risposta inattesa dal PC.");
            SavePc(reply.DeviceId, reply.DeviceName, reply.Token, host);
            Start();
            return null;
        }
        catch { return Loc.T("Codice errato o scaduto. Controlla il codice mostrato sul PC."); }
    }

    // Sincronizza. resolver: mostra i conflitti all'utente e restituisce true per procedere.
    // Senza resolver (sincronizzazione automatica) i conflitti fermano tutto e vengono segnalati.
    public async Task SyncAsync(Func<List<SyncConflict>, Task<bool>> resolver)
    {
        if (IsHost || !IsPaired) return;
        if (!await _lock.WaitAsync(0)) return;
        try
        {
            SetStatus(Loc.T("Cerco il PC..."));
            var client = await ConnectToPeerAsync();
            if (client == null) { SetStatus(Loc.T("PC non trovato: sincronizzo quando sarete sulla stessa rete Wi-Fi")); return; }

            var stream = client.GetStream();
            await stream.WriteAsync(new[] { SyncChannel.ModeDevice });
            await SyncChannel.WriteIdAsync(stream, DeviceId);
            using var ch = new SyncChannel(client, ClientToken);
            await ch.RequestAsync(new SyncMessage { Type = "hello", DeviceId = DeviceId, DeviceName = DeviceName });

            // 1. Confronto
            SetStatus(Loc.T("Confronto le modifiche..."));
            long snapshot = DatabaseService.Now;
            var local = await _db.GetChangesAsync(ClientMark);
            var remote = (await ch.RequestAsync(new SyncMessage { Type = "changes" })).Payload ?? new();
            var merge = SyncMerge.Compute(local, remote);

            // 2. Conflitti
            if (merge.Conflicts.Count > 0)
            {
                bool procedi = resolver != null && await MainThread.InvokeOnMainThreadAsync(() => resolver(merge.Conflicts));
                if (!procedi)
                {
                    PendingConflicts = merge.Conflicts.Count;
                    await SayByeAsync(ch);
                    SetStatus(PendingConflicts == 1 ? Loc.T("1 elemento modificato su entrambi: scegli quale versione tenere")
                                                    : Loc.F("{0} elementi modificati su entrambi: scegli quali versioni tenere", PendingConflicts));
                    RaiseDataSynced();
                    return;
                }
                merge.ApplyDecisions();
            }

            // 3. Applica su entrambi
            await ch.RequestAsync(new SyncMessage { Type = "apply", Payload = merge.ToRemote }, 120);
            await _db.ApplyAsync(merge.ToLocal);
            ClientMark = snapshot;
            PendingConflicts = 0;

            // 4. File: brani e copertine delle playlist
            var (riceviBrani, inviaBrani) = await SyncFilesAsync(ch, KindSong, "brani");
            var (riceviCopertine, inviaCopertine) = await SyncFilesAsync(ch, KindCover, "copertine");

            await SayByeAsync(ch);
            Preferences.Default.Set(K("LastOk"), DateTime.UtcNow.Ticks);

            var parti = new List<string>();
            if (merge.ToLocal.Count > 0) parti.Add(Loc.F("{0} ricevute", merge.ToLocal.Count));
            if (merge.ToRemote.Count > 0) parti.Add(Loc.F("{0} inviate", merge.ToRemote.Count));
            if (riceviBrani + inviaBrani > 0) parti.Add(Loc.F("{0} brani trasferiti", riceviBrani + inviaBrani));
            if (riceviCopertine + inviaCopertine > 0) parti.Add(Loc.F("{0} copertine", riceviCopertine + inviaCopertine));
            SetStatus(parti.Count == 0 ? Loc.T("Tutto aggiornato") : Loc.T("Sincronizzato: ") + string.Join(", ", parti));
            RaiseDataSynced();
        }
        catch (Exception ex)
        {
            SetStatus(Loc.F("Sincronizzazione interrotta: {0}", ex.Message));
        }
        finally
        {
            _lock.Release();
        }
    }

    private async Task<TcpClient> ConnectToPeerAsync()
    {
        // Prima prova l'ultimo indirizzo noto, poi cerca in rete (l'indirizzo del PC può cambiare)
        var last = Preferences.Default.Get(K("LastHost"), "");
        if (last != "")
        {
            var c = await ConnectAsync(last);
            if (c != null) return c;
        }
        var found = await DiscoverAsync();
        if (found == null) return null;
        Preferences.Default.Set(K("LastHost"), found.Value.Host);
        return await ConnectAsync(found.Value.Host);
    }

    private static async Task SayByeAsync(SyncChannel ch)
    {
        try { await ch.SendAsync(new SyncMessage { Type = "bye" }, CancellationToken.None); } catch { }
    }

    // Trasferisce i file (brani o copertine) che mancano da una parte o dall'altra
    private async Task<(int Received, int Sent)> SyncFilesAsync(SyncChannel ch, string kind, string nome)
    {
        var remoteHave = (await ch.RequestAsync(new SyncMessage { Type = "files", Kind = kind })).SongIds?.ToHashSet() ?? new();
        var mineIds = (await AvailableFilesAsync(kind)).ToHashSet();

        // Tutti gli id che questo dispositivo conosce (dai dati appena sincronizzati)
        var known = kind == KindCover
            ? (await _db.GetAllPlaylistsAsync()).Where(p => !string.IsNullOrEmpty(p.ImageId)).Select(p => p.ImageId).ToHashSet()
            : (await _db.GetAllSongsAsync()).Select(s => s.Id).ToHashSet();

        var daRicevere = known.Where(id => !mineIds.Contains(id) && remoteHave.Contains(id)).ToList();
        var daInviare = mineIds.Where(id => !remoteHave.Contains(id)).ToList();
        int totale = daRicevere.Count + daInviare.Count, n = 0;

        foreach (var id in daRicevere)
        {
            SetStatus(Loc.F("Ricevo {0} ({1}/{2})...", nome, ++n, totale));
            using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(10));
            await ch.SendAsync(new SyncMessage { Type = "getfile", Kind = kind, SongId = id }, cts.Token);
            var header = await ch.ReceiveAsync(cts.Token);
            var temp = Path.Combine(FileSystem.CacheDirectory, $"in-{Guid.NewGuid():N}.tmp");
            await ch.ReceiveFileAsync(temp, header.Size, cts.Token);
            await StoreReceivedFileAsync(kind, id, temp);
        }

        foreach (var id in daInviare)
        {
            SetStatus(Loc.F("Invio {0} ({1}/{2})...", nome, ++n, totale));
            var path = await LocalFileAsync(kind, id);
            if (path == null) continue;
            using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(10));
            await ch.SendAsync(new SyncMessage { Type = "putfile", Kind = kind, SongId = id, Size = new FileInfo(path).Length }, cts.Token);
            await ch.SendFileAsync(path, cts.Token);
            await ch.ReceiveAsync(cts.Token);
        }

        return (daRicevere.Count, daInviare.Count);
    }
}
