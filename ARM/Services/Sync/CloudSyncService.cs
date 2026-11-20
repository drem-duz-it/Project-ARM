using System.Net;
using System.Net.Http.Headers;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using ARM.Data;
using ARM.Models;

namespace ARM.Services.Sync;

// Sincronizzazione tramite Supabase, ovunque ci sia Internet.
// - ogni elemento (nota, task, evento, ordine, playlist, brano, collegamento, eliminazione) è cifrato sul dispositivo
// - il server assegna una versione a ogni scrittura e rifiuta quelle partite da una versione vecchia: così si vedono i conflitti,
//   che si risolvono con la stessa schermata della sincronizzazione Wi-Fi (SyncMerge)
// - ad app aperta un canale in tempo reale avvisa quando un altro dispositivo cambia qualcosa
// - i brani non passano dal cloud: ogni dispositivo li scarica da YouTube (sul telefono solo in Wi-Fi). Le copertine sì, cifrate.
// Nessuna chiave nel codice: indirizzo del progetto e chiave pubblica si inseriscono dall'app.
public class CloudSyncService
{
    private readonly DatabaseService _db;
    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(40) };
    private readonly SemaphoreSlim _lock = new(1, 1);

    private string _refreshToken;
    private string _accessToken;
    private DateTime _accessExpires;
    private byte[] _master;
    private CloudCrypto _crypto;
    private bool _secretsLoaded;
    private bool _applying;
    private bool _started;
    private bool _again;
    private readonly SemaphoreSlim _tokenLock = new(1, 1);
    private CancellationTokenSource _debounce;
    private CancellationTokenSource _realtime;
    private IDispatcherTimer _timer;
    private readonly HashSet<string> _songsTried = new();

    public CloudSyncService(DatabaseService db)
    {
        _db = db;
        // Modifica locale: invia dopo un attimo (le modifiche ravvicinate partono insieme)
        _db.DataChanged += () => { if (IsEnabled && !_applying) ScheduleSync(TimeSpan.FromSeconds(1)); };
    }

    // ---------------- Stato ----------------
    public event Action StatusChanged;
    public event Action DataSynced;

    public string Status { get; private set; } = "";
    public int PendingConflicts { get; private set; }
    public bool IsBusy => _lock.CurrentCount == 0;

    public bool IsEnabled => Preferences.Default.Get(P("Enabled"), false);
    public bool IsSignedIn => !string.IsNullOrEmpty(Preferences.Default.Get(P("UserId"), ""));
    public bool HasKey => _master != null;
    public string Url => Preferences.Default.Get(P("Url"), "");
    public string ApiKey => Preferences.Default.Get(P("ApiKey"), "");
    public string Email => Preferences.Default.Get(P("Email"), "");
    private string UserId => Preferences.Default.Get(P("UserId"), "");

    public DateTime? LastSync
    {
        get
        {
            var t = Preferences.Default.Get(P("LastOk"), 0L);
            return t == 0 ? null : new DateTime(t, DateTimeKind.Utc).ToLocalTime();
        }
    }

    private string DeviceId
    {
        get
        {
            var id = Preferences.Default.Get(P("DeviceId"), "");
            if (id == "") { id = Guid.NewGuid().ToString("N"); Preferences.Default.Set(P("DeviceId"), id); }
            return id;
        }
    }

    private static string P(string name) => $"Cloud.{name}";

    private void SetStatus(string text)
    {
        Status = text;
        MainThread.BeginInvokeOnMainThread(() => StatusChanged?.Invoke());
    }

    private void RaiseDataSynced() => MainThread.BeginInvokeOnMainThread(() => DataSynced?.Invoke());

    // ---------------- Avvio e ciclo di vita ----------------

    public async Task StartAsync()
    {
        await LoadSecretsAsync();
        if (!IsEnabled || !HasKey || _refreshToken == null)
        {
            if (IsEnabled) SetStatus(Loc.T("Accedi di nuovo al cloud dalle impostazioni della sincronizzazione"));
            return;
        }
        _started = true;
        if (_timer == null)
        {
            // Riserva: se il canale in tempo reale si interrompe, controlla comunque ogni 2 minuti
            _timer = Application.Current.Dispatcher.CreateTimer();
            _timer.Interval = TimeSpan.FromMinutes(2);
            _timer.Tick += (s, e) => _ = SyncAsync(null);
            _timer.Start();
        }
        StartRealtime();
        _ = SyncAsync(null);
    }

    // App di nuovo in primo piano: allinea subito e riapre il canale
    public void Resume()
    {
        if (!_started) return;
        StartRealtime();
        _ = SyncAsync(null);
    }

    // App in background: Android chiuderebbe comunque la connessione
    public void Sleep() => StopRealtime();

    public void ScheduleSync(TimeSpan delay)
    {
        if (!_started) return;
        _debounce?.Cancel();
        var cts = _debounce = new CancellationTokenSource();
        _ = Task.Delay(delay, cts.Token).ContinueWith(t => { if (!t.IsCanceled) _ = SyncAsync(null); }, TaskScheduler.Default);
    }

    // ---------------- Segreti ----------------
    // Token di accesso e chiave di cifratura: nel portachiavi del sistema (SecureStorage). Se non è disponibile, nelle preferenze.

    private async Task LoadSecretsAsync()
    {
        if (_secretsLoaded) return;
        _secretsLoaded = true;
        _refreshToken = await SecretGetAsync(P("Refresh"));
        var key = await SecretGetAsync(P("Key"));
        if (!string.IsNullOrEmpty(key)) SetMaster(Convert.FromBase64String(key));
    }

    private void SetMaster(byte[] master)
    {
        _master = master;
        _crypto = master == null ? null : new CloudCrypto(master);
    }

    private static async Task<string> SecretGetAsync(string key)
    {
        try
        {
            var v = await SecureStorage.Default.GetAsync(key);
            if (v != null) return v;
        }
        catch { /* portachiavi non disponibile */ }
        var fallback = Preferences.Default.Get("Secret." + key, "");
        return fallback == "" ? null : fallback;
    }

    private static async Task SecretSetAsync(string key, string value)
    {
        if (value == null)
        {
            try { SecureStorage.Default.Remove(key); } catch { }
            Preferences.Default.Remove("Secret." + key);
            return;
        }
        try
        {
            await SecureStorage.Default.SetAsync(key, value);
            Preferences.Default.Remove("Secret." + key);
        }
        catch { Preferences.Default.Set("Secret." + key, value); }
    }

    // ---------------- Accesso ----------------

    // null = fatto, altrimenti il messaggio d'errore
    public async Task<string> SignInAsync(string url, string apiKey, string email, string password)
    {
        url = (url ?? "").Trim().TrimEnd('/');
        apiKey = (apiKey ?? "").Trim();
        if (!url.StartsWith("https://", StringComparison.OrdinalIgnoreCase)) return Loc.T("L'indirizzo del progetto deve iniziare con https://");
        if (apiKey.Length == 0 || string.IsNullOrWhiteSpace(email) || string.IsNullOrEmpty(password)) return Loc.T("Compila tutti i campi.");

        try
        {
            var body = new JsonObject { ["email"] = email.Trim(), ["password"] = password };
            using var req = new HttpRequestMessage(HttpMethod.Post, $"{url}/auth/v1/token?grant_type=password") { Content = Json(body) };
            req.Headers.Add("apikey", apiKey);
            using var res = await _http.SendAsync(req);
            var text = await res.Content.ReadAsStringAsync();
            if (!res.IsSuccessStatusCode)
                return res.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Unauthorized
                    ? Loc.T("Email o password non corretti.")
                    : Loc.F("Il server ha risposto {0}.", (int)res.StatusCode);

            Preferences.Default.Set(P("Url"), url);
            Preferences.Default.Set(P("ApiKey"), apiKey);
            Preferences.Default.Set(P("Email"), email.Trim());
            await SaveSessionAsync(JsonNode.Parse(text));
            return null;
        }
        catch (HttpRequestException) { return Loc.T("Server non raggiungibile. Controlla l'indirizzo e la connessione."); }
        catch (TaskCanceledException) { return Loc.T("Il server non risponde. Riprova."); }
    }

    private async Task SaveSessionAsync(JsonNode session)
    {
        _accessToken = session["access_token"]?.GetValue<string>();
        _refreshToken = session["refresh_token"]?.GetValue<string>();
        var expiresIn = session["expires_in"]?.GetValue<int>() ?? 3600;
        _accessExpires = DateTime.UtcNow.AddSeconds(expiresIn - 60);
        var uid = session["user"]?["id"]?.GetValue<string>();
        if (!string.IsNullOrEmpty(uid)) Preferences.Default.Set(P("UserId"), uid);
        await SecretSetAsync(P("Refresh"), _refreshToken);
    }

    private async Task<string> TokenAsync()
    {
        if (_accessToken != null && DateTime.UtcNow < _accessExpires) return _accessToken;
        await _tokenLock.WaitAsync();   // un solo rinnovo alla volta (il token di rinnovo vale una volta)
        try { return await RefreshTokenAsync(); }
        finally { _tokenLock.Release(); }
    }

    private async Task<string> RefreshTokenAsync()
    {
        if (_accessToken != null && DateTime.UtcNow < _accessExpires) return _accessToken;
        await LoadSecretsAsync();
        if (_refreshToken == null) throw new CloudException(Loc.T("Accedi di nuovo al cloud dalle impostazioni della sincronizzazione"));

        var body = new JsonObject { ["refresh_token"] = _refreshToken };
        using var req = new HttpRequestMessage(HttpMethod.Post, $"{Url}/auth/v1/token?grant_type=refresh_token") { Content = Json(body) };
        req.Headers.Add("apikey", ApiKey);
        using var res = await _http.SendAsync(req);
        var text = await res.Content.ReadAsStringAsync();
        if (res.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Unauthorized)
            throw new CloudException(Loc.T("Accedi di nuovo al cloud dalle impostazioni della sincronizzazione"));
        res.EnsureSuccessStatusCode();
        await SaveSessionAsync(JsonNode.Parse(text));
        return _accessToken;
    }

    // Esci: questo dispositivo smette di sincronizzarsi (i dati locali restano)
    public async Task SignOutAsync()
    {
        StopRealtime();
        _started = false;
        _timer?.Stop();
        _timer = null;
        Preferences.Default.Set(P("Enabled"), false);
        foreach (var k in new[] { "UserId", "Mark", "Version", "LastOk" }) Preferences.Default.Remove(P(k));
        await SecretSetAsync(P("Refresh"), null);
        await SecretSetAsync(P("Key"), null);
        await _db.ClearCloudVersionsAsync();
        _refreshToken = _accessToken = null;
        SetMaster(null);
        PendingConflicts = 0;
        SetStatus(Loc.T("Sincronizzazione cloud non attiva"));
        RaiseDataSynced();
    }

    // ---------------- Chiave di cifratura e abbinamento ----------------

    // Il server ha già dati? (allora questo non è il primo dispositivo: serve il codice)
    public async Task<bool> ServerHasDataAsync()
    {
        var rows = await GetJsonAsync("/rest/v1/records?select=key&limit=1");
        return rows is JsonArray a && a.Count > 0;
    }

    // Primo dispositivo: crea la chiave e attiva
    public async Task CreateKeyAsync()
    {
        var master = CloudCrypto.NewMasterKey();
        await SecretSetAsync(P("Key"), Convert.ToBase64String(master));
        SetMaster(master);
        await ActivateAsync();
    }

    // Dispositivo già associato: crea un codice valido 10 minuti per un nuovo dispositivo
    public async Task<string> CreatePairingCodeAsync()
    {
        var code = CloudCrypto.NewPairingCode();
        var (wrapped, salt) = await Task.Run(() => CloudCrypto.WrapMaster(_master, code));
        var body = new JsonObject
        {
            ["wrapped_key"] = wrapped,
            ["salt"] = salt,
            ["expires_at"] = DateTime.UtcNow.AddMinutes(10).ToString("O")
        };
        using var req = await RequestAsync(HttpMethod.Post, "/rest/v1/pairing", body);
        req.Headers.Add("Prefer", "resolution=merge-duplicates,return=minimal");
        using var res = await _http.SendAsync(req);
        await EnsureOkAsync(res);
        return $"{code[..5]}-{code[5..]}";
    }

    // Nuovo dispositivo: riceve la chiave con il codice. null = fatto, altrimenti l'errore
    public async Task<string> JoinWithCodeAsync(string code)
    {
        if (CloudCrypto.NormalizeCode(code).Length != 10) return Loc.T("Il codice è di 10 caratteri.");
        var rows = await GetJsonAsync("/rest/v1/pairing?select=wrapped_key,salt,expires_at");
        var row = (rows as JsonArray)?.FirstOrDefault();
        if (row == null) return Loc.T("Nessun codice attivo. Generane uno sull'altro dispositivo.");
        if (DateTime.TryParse(row["expires_at"]?.GetValue<string>(), null, System.Globalization.DateTimeStyles.AdjustToUniversal | System.Globalization.DateTimeStyles.AssumeUniversal, out var exp) && exp < DateTime.UtcNow)
            return Loc.T("Il codice è scaduto. Generane uno nuovo sull'altro dispositivo.");

        var master = await Task.Run(() => CloudCrypto.UnwrapMaster(row["wrapped_key"]?.GetValue<string>(), row["salt"]?.GetValue<string>(), code));
        if (master == null) return Loc.T("Codice errato. Controlla il codice mostrato sull'altro dispositivo.");

        await SecretSetAsync(P("Key"), Convert.ToBase64String(master));
        SetMaster(master);
        try
        {
            using var del = await RequestAsync(HttpMethod.Delete, $"/rest/v1/pairing?user_id=eq.{UserId}", null);
            using var _ = await _http.SendAsync(del);
        }
        catch { /* scade da solo */ }
        await ActivateAsync();
        return null;
    }

    private async Task ActivateAsync()
    {
        // Copia di sicurezza del database prima della prima unione con il cloud
        try { await _db.BackupAsync("prima-del-cloud"); } catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Backup: {ex.Message}"); }
        Preferences.Default.Remove(P("Mark"));      // la prima volta si confronta tutto
        Preferences.Default.Remove(P("Version"));
        await _db.ClearCloudVersionsAsync();
        Preferences.Default.Set(P("Enabled"), true);
        await StartAsync();
    }

    // ---------------- Sincronizzazione ----------------

    // resolver: mostra i conflitti e restituisce true per procedere. Senza resolver i conflitti fermano tutto e vengono segnalati.
    public async Task SyncAsync(Func<List<SyncConflict>, Task<bool>> resolver)
    {
        if (!IsEnabled || _crypto == null) return;
        if (!await _lock.WaitAsync(0)) { _again = true; return; }   // ne è già in corso una: si ripete appena finisce
        try
        {
            SetStatus(Loc.T("Sincronizzo con il cloud..."));

            // 1. Modifiche locali dall'ultima volta e modifiche sul server dall'ultima versione vista
            long snapshot = DatabaseService.Now;
            long? mark = Preferences.Default.ContainsKey(P("Mark")) ? Preferences.Default.Get(P("Mark"), 0L) : null;
            long since = Preferences.Default.Get(P("Version"), 0L);
            var local = await _db.GetChangesAsync(mark);
            var (remote, versions, maxVersion) = await PullAsync(since);

            // 2. Unione (stessa logica della sincronizzazione Wi-Fi)
            var merge = SyncMerge.Compute(local, remote);
            if (merge.Conflicts.Count > 0)
            {
                bool procedi = resolver != null && await MainThread.InvokeOnMainThreadAsync(() => resolver(merge.Conflicts));
                if (!procedi)
                {
                    PendingConflicts = merge.Conflicts.Count;
                    SetStatus(PendingConflicts == 1 ? Loc.T("1 elemento modificato su entrambi: scegli quale versione tenere")
                                                    : Loc.F("{0} elementi modificati su entrambi: scegli quali versioni tenere", PendingConflicts));
                    RaiseDataSynced();
                    return;
                }
                merge.ApplyDecisions();
            }

            // 3. Applica qui le modifiche arrivate
            if (merge.ToLocal.Count > 0)
            {
                _applying = true;
                try { await _db.ApplyAsync(merge.ToLocal); }
                finally { _applying = false; }
            }
            await _db.SetCloudVersionsAsync(versions);

            // 4. Invia le mie (con la versione da cui sono partito: il server rifiuta se nel frattempo è cambiata)
            int rifiutate = await PushAsync(merge.ToRemote);

            // Se qualcosa è stato rifiutato, al prossimo giro si riconfronta (l'altra versione arriva con il pull)
            Preferences.Default.Set(P("Version"), Math.Max(since, maxVersion));
            if (rifiutate == 0) Preferences.Default.Set(P("Mark"), snapshot);
            else ScheduleSync(TimeSpan.FromSeconds(2));
            PendingConflicts = 0;

            // 5. File: copertine (cifrate) e brani mancanti (da YouTube)
            int copertine = await SyncCoversAsync();
            QueueMissingSongs();

            Preferences.Default.Set(P("LastOk"), DateTime.UtcNow.Ticks);
            var parti = new List<string>();
            if (merge.ToLocal.Count > 0) parti.Add(Loc.F("{0} ricevute", merge.ToLocal.Count));
            if (merge.ToRemote.Count > 0) parti.Add(Loc.F("{0} inviate", merge.ToRemote.Count));
            if (copertine > 0) parti.Add(Loc.F("{0} copertine", copertine));
            SetStatus(parti.Count == 0 ? Loc.T("Tutto aggiornato") : Loc.T("Sincronizzato: ") + string.Join(", ", parti));
            if (merge.ToLocal.Count > 0 || copertine > 0) RaiseDataSynced();
        }
        catch (CloudException ex) { SetStatus(ex.Message); }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or WebException)
        {
            SetStatus(Loc.T("Cloud non raggiungibile: sincronizzo appena torna la connessione"));
        }
        catch (Exception ex)
        {
            SetStatus(Loc.F("Sincronizzazione interrotta: {0}", ex.Message));
        }
        finally
        {
            _lock.Release();
            if (_again) { _again = false; ScheduleSync(TimeSpan.FromMilliseconds(500)); }
        }
    }

    // Righe del server più nuove di "since", decifrate. Le mie (stesso dispositivo) si saltano: le ho già.
    private async Task<(SyncPayload Payload, List<KeyValuePair<string, long>> Versions, long MaxVersion)> PullAsync(long since)
    {
        var payload = new SyncPayload();
        var versions = new List<KeyValuePair<string, long>>();
        long max = since;
        string me = DeviceId;
        const int page = 500;

        while (true)
        {
            var rows = await GetJsonAsync($"/rest/v1/records?select=key,version,deleted,payload,device_id&version=gt.{max}&order=version.asc&limit={page}") as JsonArray;
            if (rows == null || rows.Count == 0) break;

            foreach (var row in rows)
            {
                var serverKey = row["key"]?.GetValue<string>();
                long version = row["version"]?.GetValue<long>() ?? 0;
                max = Math.Max(max, version);
                if (serverKey == null) continue;
                versions.Add(new(serverKey, version));
                if (row["device_id"]?.GetValue<string>() == me) continue;

                var data = row["payload"]?.GetValue<string>();
                if (string.IsNullOrEmpty(data)) continue;
                CloudEnvelope env;
                try { env = JsonSerializer.Deserialize(_crypto.Decrypt(data, serverKey), SyncJson.Default.CloudEnvelope); }
                catch (System.Security.Cryptography.CryptographicException)
                {
                    throw new CloudException(Loc.T("I dati nel cloud sono cifrati con un'altra chiave. Esci e associa questo dispositivo con un codice."));
                }
                AddToPayload(payload, env);
            }
            if (rows.Count < page) break;
        }
        return (payload, versions, max);
    }

    private static void AddToPayload(SyncPayload p, CloudEnvelope env)
    {
        if (env?.D == null) return;
        switch (env.T)
        {
            case "del": p.Deletions.Add(JsonSerializer.Deserialize(env.D, SyncJson.Default.SyncTombstone)); break;
            case nameof(Note): p.Notes.Add(JsonSerializer.Deserialize(env.D, SyncJson.Default.Note)); break;
            case nameof(TaskItem): p.Tasks.Add(JsonSerializer.Deserialize(env.D, SyncJson.Default.TaskItem)); break;
            case nameof(CalendarEvent): p.Events.Add(JsonSerializer.Deserialize(env.D, SyncJson.Default.CalendarEvent)); break;
            case nameof(Ordine): p.Ordini.Add(JsonSerializer.Deserialize(env.D, SyncJson.Default.Ordine)); break;
            case nameof(Playlist): p.Playlists.Add(JsonSerializer.Deserialize(env.D, SyncJson.Default.Playlist)); break;
            case nameof(Song): p.Songs.Add(JsonSerializer.Deserialize(env.D, SyncJson.Default.Song)); break;
            case nameof(PlaylistSong): p.Links.Add(JsonSerializer.Deserialize(env.D, SyncJson.Default.PlaylistSong)); break;
        }
    }

    // Elementi da inviare: chiave dell'elemento, tipo e JSON
    private static IEnumerable<CloudEnvelope> Envelopes(SyncPayload p)
    {
        foreach (var x in p.Notes) yield return new() { K = $"{nameof(Note)}:{x.Id}", T = nameof(Note), D = JsonSerializer.Serialize(x, SyncJson.Default.Note) };
        foreach (var x in p.Tasks) yield return new() { K = $"{nameof(TaskItem)}:{x.Id}", T = nameof(TaskItem), D = JsonSerializer.Serialize(x, SyncJson.Default.TaskItem) };
        foreach (var x in p.Events) yield return new() { K = $"{nameof(CalendarEvent)}:{x.Id}", T = nameof(CalendarEvent), D = JsonSerializer.Serialize(x, SyncJson.Default.CalendarEvent) };
        foreach (var x in p.Ordini ?? new()) yield return new() { K = $"{nameof(Ordine)}:{x.Id}", T = nameof(Ordine), D = JsonSerializer.Serialize(x, SyncJson.Default.Ordine) };
        foreach (var x in p.Playlists)
        {
            var path = x.ImagePath;
            x.ImagePath = null;   // il percorso vale solo su questo dispositivo
            yield return new() { K = $"{nameof(Playlist)}:{x.Id}", T = nameof(Playlist), D = JsonSerializer.Serialize(x, SyncJson.Default.Playlist) };
            x.ImagePath = path;
        }
        foreach (var x in p.Songs)
        {
            var path = x.LocalFilePath;
            x.LocalFilePath = null;
            yield return new() { K = $"{nameof(Song)}:{x.Id}", T = nameof(Song), D = JsonSerializer.Serialize(x, SyncJson.Default.Song) };
            x.LocalFilePath = path;
        }
        foreach (var x in p.Links) yield return new() { K = $"{nameof(PlaylistSong)}:{DatabaseService.LinkKey(x)}", T = nameof(PlaylistSong), D = JsonSerializer.Serialize(x, SyncJson.Default.PlaylistSong) };
        foreach (var x in p.Deletions) yield return new() { K = x.Key, T = "del", D = JsonSerializer.Serialize(x, SyncJson.Default.SyncTombstone) };
    }

    // Restituisce quante scritture il server ha rifiutato (versione cambiata nel frattempo)
    private async Task<int> PushAsync(SyncPayload p)
    {
        if (p.Count == 0) return 0;
        var known = await _db.GetCloudVersionsAsync();
        var items = Envelopes(p).Select(env =>
        {
            var serverKey = _crypto.ServerKey(env.K);
            var plain = JsonSerializer.SerializeToUtf8Bytes(env, SyncJson.Default.CloudEnvelope);
            return new JsonObject
            {
                ["key"] = serverKey,
                ["base"] = known.TryGetValue(serverKey, out var v) ? JsonValue.Create(v) : null,
                ["payload"] = _crypto.Encrypt(plain, serverKey),
                ["deleted"] = env.T == "del",
                ["device"] = DeviceId
            };
        }).ToList();

        int rifiutate = 0;
        foreach (var chunk in items.Chunk(200))
        {
            var body = new JsonObject { ["items"] = new JsonArray(chunk.Select(i => (JsonNode)i).ToArray()) };
            using var req = await RequestAsync(HttpMethod.Post, "/rest/v1/rpc/push_records", body);
            using var res = await _http.SendAsync(req);
            await EnsureOkAsync(res);
            var results = JsonNode.Parse(await res.Content.ReadAsStringAsync()) as JsonArray;
            var accepted = new List<KeyValuePair<string, long>>();
            foreach (var r in results ?? new JsonArray())
            {
                var key = r["key"]?.GetValue<string>();
                bool ok = r["ok"] is JsonValue okValue && okValue.TryGetValue<bool>(out var b) && b;
                if (ok && key != null && r["version"] is JsonValue vv && vv.TryGetValue<long>(out var version)) accepted.Add(new(key, version));
                else rifiutate++;
            }
            await _db.SetCloudVersionsAsync(accepted);
        }
        return rifiutate;
    }

    // ---------------- Copertine e brani ----------------

    // Carica le copertine che il cloud non ha ancora e scarica quelle che mancano qui. Restituisce quante ne ha ricevute.
    private async Task<int> SyncCoversAsync()
    {
        int ricevute = 0;
        var playlists = await _db.GetAllPlaylistsAsync();
        foreach (var imageId in playlists.Where(p => !string.IsNullOrEmpty(p.ImageId)).Select(p => p.ImageId).Distinct())
        {
            var pl = playlists.First(p => p.ImageId == imageId);
            var path = $"/storage/v1/object/covers/{UserId}/{imageId}";
            try
            {
                if (!string.IsNullOrEmpty(pl.ImagePath) && File.Exists(pl.ImagePath))
                {
                    if (Preferences.Default.Get(P($"CoverUp.{UserId}.{imageId}"), false)) continue;
                    var data = _crypto.EncryptBytes(await File.ReadAllBytesAsync(pl.ImagePath), "cover:" + imageId);
                    using var req = await RequestAsync(HttpMethod.Post, path, null);
                    req.Content = new ByteArrayContent(data);
                    req.Content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
                    req.Headers.Add("x-upsert", "true");
                    using var res = await _http.SendAsync(req);
                    if (res.IsSuccessStatusCode) Preferences.Default.Set(P($"CoverUp.{UserId}.{imageId}"), true);
                }
                else
                {
                    if (Preferences.Default.Get(P($"CoverMissing.{UserId}.{imageId}"), 0L) > DateTime.UtcNow.AddMinutes(-10).Ticks) continue;
                    using var req = await RequestAsync(HttpMethod.Get, $"/storage/v1/object/authenticated/covers/{UserId}/{imageId}", null);
                    using var res = await _http.SendAsync(req);
                    if (!res.IsSuccessStatusCode)
                    {
                        // Non ancora caricata dall'altro dispositivo: riprova più tardi
                        Preferences.Default.Set(P($"CoverMissing.{UserId}.{imageId}"), DateTime.UtcNow.Ticks);
                        continue;
                    }
                    var plain = _crypto.DecryptBytes(await res.Content.ReadAsByteArrayAsync(), "cover:" + imageId);
                    var dest = DatabaseService.CoverPath(imageId);
                    Directory.CreateDirectory(Path.GetDirectoryName(dest));
                    await File.WriteAllBytesAsync(dest, plain);
                    await _db.SetCoverFileAsync(imageId, dest);
                    Preferences.Default.Set(P($"CoverUp.{UserId}.{imageId}"), true);
                    ricevute++;
                }
            }
            catch (Exception ex) when (ex is not CloudException) { System.Diagnostics.Debug.WriteLine($"Copertina {imageId}: {ex.Message}"); }
        }
        return ricevute;
    }

    // Brani arrivati da un altro dispositivo senza file: si scaricano da YouTube (sul telefono solo in Wi-Fi)
    private void QueueMissingSongs()
    {
#if ANDROID
        if (!Connectivity.Current.ConnectionProfiles.Contains(ConnectionProfile.WiFi)) return;
#endif
        _ = Task.Run(async () =>
        {
            var downloads = IPlatformApplication.Current?.Services.GetService<MusicDownloads>();
            if (downloads == null) return;
            foreach (var song in await _db.GetAllSongsAsync())
            {
                if (song.IsDownloaded && File.Exists(song.LocalFilePath)) continue;
                lock (_songsTried) { if (!_songsTried.Add(song.Id)) continue; }   // una prova per brano a ogni avvio
                _ = downloads.DownloadAsync(song);
            }
        });
    }

    // ---------------- Tempo reale ----------------
    // Canale Supabase Realtime (protocollo Phoenix): un avviso per ogni riga cambiata → si sincronizza subito.

    private void StartRealtime()
    {
        if (!_started || _realtime != null) return;
        var cts = _realtime = new CancellationTokenSource();
        _ = RealtimeLoopAsync(cts.Token);
    }

    private void StopRealtime()
    {
        _realtime?.Cancel();
        _realtime = null;
    }

    private async Task RealtimeLoopAsync(CancellationToken ct)
    {
        int attesa = 5;
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await RealtimeSessionAsync(ct);
                attesa = 5;
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { return; }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Tempo reale: {ex.Message}"); }

            try { await Task.Delay(TimeSpan.FromSeconds(attesa), ct); } catch { return; }
            attesa = Math.Min(attesa * 2, 120);
        }
    }

    // Una connessione: dura al massimo 45 minuti (poi si riapre con un token nuovo)
    private async Task RealtimeSessionAsync(CancellationToken ct)
    {
        var token = await TokenAsync();
        var wsUrl = Url.Replace("https://", "wss://", StringComparison.OrdinalIgnoreCase) + $"/realtime/v1/websocket?apikey={Uri.EscapeDataString(ApiKey)}&vsn=1.0.0";
        using var ws = new ClientWebSocket();
        using var session = CancellationTokenSource.CreateLinkedTokenSource(ct);
        session.CancelAfter(TimeSpan.FromMinutes(45));
        await ws.ConnectAsync(new Uri(wsUrl), session.Token);

        const string topic = "realtime:arm-records";
        int refs = 1;
        var join = new JsonObject
        {
            ["topic"] = topic,
            ["event"] = "phx_join",
            ["ref"] = (refs++).ToString(),
            ["join_ref"] = "1",
            ["payload"] = new JsonObject
            {
                ["access_token"] = token,
                ["config"] = new JsonObject
                {
                    ["broadcast"] = new JsonObject { ["self"] = false },
                    ["presence"] = new JsonObject { ["key"] = "" },
                    ["postgres_changes"] = new JsonArray(new JsonObject
                    {
                        ["event"] = "*",
                        ["schema"] = "public",
                        ["table"] = "records",
                        ["filter"] = $"user_id=eq.{UserId}"
                    })
                }
            }
        };
        await SendAsync(ws, join, session.Token);

        // Battito ogni 25 secondi, altrimenti il server chiude
        _ = Task.Run(async () =>
        {
            try
            {
                while (ws.State == WebSocketState.Open && !session.IsCancellationRequested)
                {
                    await Task.Delay(TimeSpan.FromSeconds(25), session.Token);
                    await SendAsync(ws, new JsonObject { ["topic"] = "phoenix", ["event"] = "heartbeat", ["payload"] = new JsonObject(), ["ref"] = (refs++).ToString() }, session.Token);
                }
            }
            catch { }
        });

        var buffer = new byte[16 * 1024];
        var message = new MemoryStream();
        while (ws.State == WebSocketState.Open && !session.IsCancellationRequested)
        {
            var r = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), session.Token);
            if (r.MessageType == WebSocketMessageType.Close) break;
            message.Write(buffer, 0, r.Count);
            if (!r.EndOfMessage) continue;

            var text = Encoding.UTF8.GetString(message.GetBuffer(), 0, (int)message.Length);
            message.SetLength(0);
            if (text.Contains("\"postgres_changes\"")) ScheduleSync(TimeSpan.FromMilliseconds(500));
        }

        try { if (ws.State == WebSocketState.Open) await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "", CancellationToken.None); } catch { }
    }

    private static Task SendAsync(ClientWebSocket ws, JsonNode msg, CancellationToken ct) =>
        ws.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes(msg.ToJsonString())), WebSocketMessageType.Text, true, ct);

    // ---------------- HTTP ----------------

    private static StringContent Json(JsonNode body) => new(body.ToJsonString(), Encoding.UTF8, "application/json");

    private async Task<HttpRequestMessage> RequestAsync(HttpMethod method, string path, JsonNode body)
    {
        var token = await TokenAsync();
        var req = new HttpRequestMessage(method, Url + path);
        req.Headers.Add("apikey", ApiKey);
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (body != null) req.Content = Json(body);
        return req;
    }

    private async Task<JsonNode> GetJsonAsync(string path)
    {
        using var req = await RequestAsync(HttpMethod.Get, path, null);
        using var res = await _http.SendAsync(req);
        await EnsureOkAsync(res);
        return JsonNode.Parse(await res.Content.ReadAsStringAsync());
    }

    private async Task EnsureOkAsync(HttpResponseMessage res)
    {
        if (res.IsSuccessStatusCode) return;
        if (res.StatusCode == HttpStatusCode.Unauthorized) _accessToken = null;   // al prossimo giro si rinnova
        var text = await res.Content.ReadAsStringAsync();
        if (res.StatusCode == HttpStatusCode.NotFound && text.Contains("push_records"))
            throw new CloudException(Loc.T("Sul progetto Supabase manca lo schema di ARM: esegui lo script supabase/migrations nell'editor SQL."));
        throw new CloudException(Loc.F("Il cloud ha risposto {0}: {1}", (int)res.StatusCode, text.Length > 200 ? text[..200] : text));
    }
}

public class CloudException : Exception
{
    public CloudException(string message) : base(message) { }
}
