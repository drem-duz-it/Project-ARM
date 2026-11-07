#if WINDOWS
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ARMusic.Data;
using ARMusic.Models;
using ARMusic.Services.Sync;

namespace ARMusic.Services;

// API locale per assistenti esterni: eventi, task, note, pagamenti e musica.
// - Solo su Windows, solo su 127.0.0.1, ogni richiesta deve portare la chiave nell'header X-Arm-Key.
// - Passa dallo stesso DatabaseService dell'app: notifiche, sincronizzazione col telefono e cancellazioni
//   funzionano come se la modifica l'avessi fatta tu dall'interfaccia.
// - Porta e chiave stanno in %LOCALAPPDATA%\ARM\arm-api.json (la chiave si crea al primo avvio): l'assistente le legge da lì.
public class AgentApi
{
    private const int DefaultPort = 47810;
    private static readonly JsonSerializerOptions Out = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = false };

    private readonly DatabaseService _db;
    private readonly SyncService _sync;
    private readonly AudioService _audio;
    private HttpListener _listener;
    private byte[] _key;

    public AgentApi(DatabaseService db, SyncService sync, AudioService audio) { _db = db; _sync = sync; _audio = audio; }

    public void Start()
    {
        try
        {
            int port = int.TryParse(Environment.GetEnvironmentVariable("ARM_AGENT_PORT"), out var p) ? p : DefaultPort;
            string file = Environment.GetEnvironmentVariable("ARM_AGENT_FILE")
                ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ARM", "arm-api.json");

            string key = null;
            try { key = JsonDocument.Parse(File.ReadAllText(file)).RootElement.GetProperty("key").GetString(); } catch { }
            if (string.IsNullOrEmpty(key)) key = Convert.ToHexString(RandomNumberGenerator.GetBytes(24)).ToLowerInvariant();
            _key = Encoding.UTF8.GetBytes(key);

            _listener = new HttpListener();
            _listener.Prefixes.Add($"http://127.0.0.1:{port}/");
            _listener.Start();

            Directory.CreateDirectory(Path.GetDirectoryName(file)!);
            File.WriteAllText(file, JsonSerializer.Serialize(new { port, key }));
            _ = Task.Run(LoopAsync);
        }
        catch (Exception ex)
        {
            // porta occupata o altro: ARM funziona comunque, l'assistente non lo raggiunge
            System.Diagnostics.Debug.WriteLine($"API locale non disponibile: {ex.Message}");
        }
    }

    private async Task LoopAsync()
    {
        while (_listener.IsListening)
        {
            HttpListenerContext ctx;
            try { ctx = await _listener.GetContextAsync(); }
            catch { break; }
            _ = Task.Run(() => HandleAsync(ctx));
        }
    }

    private async Task HandleAsync(HttpListenerContext ctx)
    {
        int status = 200;
        object body;
        try
        {
            var given = Encoding.UTF8.GetBytes(ctx.Request.Headers["X-Arm-Key"] ?? "");
            if (!CryptographicOperations.FixedTimeEquals(given, _key)) { status = 401; body = new { errore = "Chiave non valida" }; }
            else body = await RouteAsync(ctx.Request);
        }
        catch (ApiError e) { status = e.Status; body = new { errore = e.Message }; }
        catch (Exception e) { status = 500; body = new { errore = e.Message }; }

        var bytes = JsonSerializer.SerializeToUtf8Bytes(body, Out);
        ctx.Response.StatusCode = status;
        ctx.Response.ContentType = "application/json; charset=utf-8";
        ctx.Response.ContentLength64 = bytes.Length;
        await ctx.Response.OutputStream.WriteAsync(bytes);
        ctx.Response.Close();
    }

    private class ApiError : Exception
    {
        public int Status { get; }
        public ApiError(int status, string message) : base(message) { Status = status; }
    }

    // ---------- instradamento ----------
    private async Task<object> RouteAsync(HttpListenerRequest req)
    {
        var parts = req.Url.AbsolutePath.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        string method = req.HttpMethod.ToUpperInvariant();
        if (parts.Length < 2 || parts[0] != "api") throw new ApiError(404, "Percorso sconosciuto");
        string area = parts[1];
        string id = parts.Length > 2 ? parts[2] : null;
        string action = parts.Length > 3 ? parts[3] : null;
        JsonElement json = default;
        if (method is "POST" or "PATCH")
        {
            using var reader = new StreamReader(req.InputStream, Encoding.UTF8);
            string text = await reader.ReadToEndAsync();
            json = string.IsNullOrWhiteSpace(text) ? default : JsonDocument.Parse(text).RootElement.Clone();
        }

        object result = (area, method) switch
        {
            ("stato", "GET") => new { ok = true, adesso = DateTime.Now },
            ("eventi", "GET") => await ListEventsAsync(req),
            ("eventi", "POST") => await CreateEventAsync(json),
            ("eventi", "DELETE") => await DeleteEventAsync(id),
            ("task", "GET") => await ListTasksAsync(req),
            ("task", "POST") when action == "completa" => await CompleteTaskAsync(id),
            ("task", "POST") => await CreateTaskAsync(json),
            ("task", "PATCH") => await EditTaskAsync(id, json),
            ("task", "DELETE") => await DeleteTaskAsync(id),
            ("note", "GET") => await ListNotesAsync(req),
            ("note", "POST") when action == "pagato" => await TogglePaidAsync(id),
            ("note", "POST") => await CreateNoteAsync(json),
            ("note", "DELETE") => await DeleteNoteAsync(id),
            ("ordini", "GET") => await ListOrdiniAsync(req),
            ("ordini", "POST") when action == "ricevuto" => await OrdineRicevutoAsync(id),
            ("musica", "GET") when id == null => await MusicStateAsync(),
            ("musica", "GET") when id == "playlist" => await ListPlaylistsAsync(),
            ("musica", "GET") when id == "brani" => await ListSongsAsync(Q(req, "q")),
            ("musica", "POST") => await MusicCommandAsync(id, json),
            _ => throw new ApiError(404, "Operazione sconosciuta"),
        };
        // le pagine aperte si ridisegnano (come dopo una sincronizzazione)
        if (method != "GET") _sync.RefreshPages();
        return result;
    }

    // ---------- helper per leggere il JSON ----------
    private static string Str(JsonElement j, string name) =>
        j.ValueKind == JsonValueKind.Object && j.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    private static int? Int(JsonElement j, string name) =>
        j.ValueKind == JsonValueKind.Object && j.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetInt32() : null;

    private static double? Num(JsonElement j, string name) =>
        j.ValueKind == JsonValueKind.Object && j.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetDouble() : null;

    private static bool? Bool(JsonElement j, string name) =>
        j.ValueKind == JsonValueKind.Object && j.TryGetProperty(name, out var v) && v.ValueKind is JsonValueKind.True or JsonValueKind.False ? v.GetBoolean() : null;

    private static DateTime Date(string text, string what)
    {
        if (DateTime.TryParse(text, System.Globalization.CultureInfo.GetCultureInfo("it-IT"), System.Globalization.DateTimeStyles.None, out var d)) return d;
        throw new ApiError(400, $"{what} non valida: '{text}'");
    }

    private static string Q(HttpListenerRequest r, string name) => r.QueryString[name];

    // ---------- ordini (3.0, letti da Gmail) ----------
    // GET /api/ordini            -> ordini in arrivo (più i consegnati degli ultimi 3 giorni)
    // GET /api/ordini?tutti=1    -> anche quelli consegnati da più tempo
    // POST /api/ordini/{id}/ricevuto
    private static object Describe(Ordine o) => new
    {
        id = o.Id, negozio = o.Negozio, prodotto = o.Prodotto, numeroOrdine = o.NumeroOrdine,
        stato = o.StatoTesto, tappa = o.Tappa, inRitardo = o.InRitardo, giorniRitardo = o.GiorniRitardo,
        consegnaPrevista = o.ConsegnaPrevista?.ToString("yyyy-MM-dd"), fasciaOraria = o.FasciaOraria,
        consegnatoIl = o.ConsegnatoIl?.ToString("yyyy-MM-dd"), tracking = o.LinkTracking,
    };

    private async Task<object> ListOrdiniAsync(HttpListenerRequest r)
    {
        bool tutti = Q(r, "tutti") is "1" or "true" or "si";
        return (await _db.GetAllOrdiniAsync())
            .Where(o => tutti || o.Visibile)
            .OrderBy(o => o.Consegnato).ThenBy(o => o.ConsegnaPrevista ?? DateTime.MaxValue)
            .Select(Describe).ToList();
    }

    private async Task<object> OrdineRicevutoAsync(string id)
    {
        var o = (await _db.GetAllOrdiniAsync()).FirstOrDefault(x => x.Id == id) ?? throw new ApiError(404, "Ordine non trovato");
        if (!o.Consegnato) await _db.SegnaConsegnatoAsync(o);
        return Describe(o);
    }

    // ---------- eventi ----------
    private static object Describe(CalendarEvent e) => new
    {
        id = e.Id, titolo = e.Title, data = e.Date.ToString("yyyy-MM-dd"), tuttoIlGiorno = e.AllDay,
        inizio = e.AllDay ? null : e.StartTime.ToString(@"hh\:mm"), fine = e.AllDay ? null : e.EndTime.ToString(@"hh\:mm"),
        luogo = e.Location, note = e.Notes,
    };

    private async Task<object> ListEventsAsync(HttpListenerRequest r)
    {
        var from = Q(r, "da") is { } a ? Date(a, "Data iniziale").Date : DateTime.Today;
        var to = Q(r, "a") is { } b ? Date(b, "Data finale").Date : from.AddDays(30);
        return (await _db.GetAllEventsAsync())
            .Where(e => e.Date.Date >= from && e.Date.Date <= to)
            .OrderBy(e => e.Date).ThenBy(e => e.AllDay ? TimeSpan.Zero : e.StartTime)
            .Select(Describe).ToList();
    }

    private async Task<object> CreateEventAsync(JsonElement j)
    {
        string title = Str(j, "titolo");
        if (string.IsNullOrWhiteSpace(title)) throw new ApiError(400, "Manca il titolo");
        var start = Date(Str(j, "inizio"), "Data di inizio");
        bool hasTime = Bool(j, "tuttoIlGiorno") != true && Str(j, "inizio").Contains(':');
        var ev = new CalendarEvent { Title = title.Trim(), Date = start.Date, AllDay = !hasTime, Location = Str(j, "luogo"), Notes = Str(j, "note") };
        if (hasTime)
        {
            ev.StartTime = start.TimeOfDay;
            var end = start.AddMinutes(Math.Max(5, Int(j, "durataMinuti") ?? 60));
            ev.EndTime = end.Date > start.Date ? new TimeSpan(23, 59, 0) : end.TimeOfDay; // un evento non attraversa la mezzanotte
        }
        await _db.SaveEventAsync(ev);
        return Describe(ev);
    }

    private async Task<object> DeleteEventAsync(string id)
    {
        var ev = (await _db.GetAllEventsAsync()).FirstOrDefault(e => e.Id == id) ?? throw new ApiError(404, "Evento non trovato");
        await _db.DeleteEventAsync(ev);
        return new { eliminato = ev.Title };
    }

    // ---------- task ----------
    private static object Describe(TaskItem t) => new
    {
        id = t.Id, titolo = t.Title, note = t.Notes, scadenza = t.DueDate?.ToString("yyyy-MM-dd"),
        priorita = t.Priority switch { 0 => "bassa", 2 => "alta", _ => "normale" }, completato = t.IsDone, inRitardo = t.IsOverdue,
    };

    private async Task<object> ListTasksAsync(HttpListenerRequest r)
    {
        bool all = Q(r, "tutti") == "1";
        return (await _db.GetAllTasksAsync()).Where(t => all || !t.IsDone)
            .OrderBy(t => t.IsDone).ThenBy(t => t.DueDate ?? DateTime.MaxValue).ThenByDescending(t => t.Priority)
            .Select(Describe).ToList();
    }

    private static int Priority(string text) => text?.ToLowerInvariant() switch { "bassa" => 0, "alta" => 2, _ => 1 };

    private async Task<object> CreateTaskAsync(JsonElement j)
    {
        string title = Str(j, "titolo");
        if (string.IsNullOrWhiteSpace(title)) throw new ApiError(400, "Manca il titolo");
        var t = new TaskItem { Title = title.Trim(), Notes = Str(j, "note"), Priority = Priority(Str(j, "priorita")) };
        if (Str(j, "scadenza") is { Length: > 0 } due) t.DueDate = Date(due, "Scadenza").Date;
        await _db.SaveTaskAsync(t);
        return Describe(t);
    }

    private async Task<TaskItem> FindTaskAsync(string id) =>
        (await _db.GetAllTasksAsync()).FirstOrDefault(t => t.Id == id) ?? throw new ApiError(404, "Task non trovato");

    private async Task<object> CompleteTaskAsync(string id)
    {
        var t = await FindTaskAsync(id);
        if (!t.IsDone) await _db.ToggleTaskAsync(t);
        return Describe(t);
    }

    private async Task<object> EditTaskAsync(string id, JsonElement j)
    {
        var t = await FindTaskAsync(id);
        if (Str(j, "titolo") is { Length: > 0 } title) t.Title = title.Trim();
        if (Str(j, "note") is { } notes) t.Notes = notes;
        if (Str(j, "scadenza") is { } due) t.DueDate = due.Length == 0 ? null : Date(due, "Scadenza").Date;
        if (Str(j, "priorita") is { Length: > 0 } pr) t.Priority = Priority(pr);
        await _db.SaveTaskAsync(t);
        return Describe(t);
    }

    private async Task<object> DeleteTaskAsync(string id)
    {
        var t = await FindTaskAsync(id);
        await _db.DeleteTaskAsync(t);
        return new { eliminato = t.Title };
    }

    // ---------- musica ----------
    // Il lettore è un controllo grafico: va toccato solo dal thread dell'interfaccia.
    // minuscolo, senza accenti e senza punteggiatura (così «Stayin' Alive» e «stayin alive» coincidono)
    private static string Norm(string t)
    {
        var sb = new StringBuilder();
        foreach (var c in (t ?? "").Normalize(NormalizationForm.FormD))
        {
            if (System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c) == System.Globalization.UnicodeCategory.NonSpacingMark) continue;
            sb.Append(char.IsLetterOrDigit(c) ? char.ToLowerInvariant(c) : ' ');
        }
        return string.Join(' ', sb.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }

    private static bool Contains(string text, string q) => Norm(text).Contains(Norm(q));

    // il più simile: prima uguale, poi contiene tutto, poi contiene tutte le parole
    private static T Best<T>(IEnumerable<T> items, string query, Func<T, string> name) where T : class
    {
        string q = Norm(query).Trim();
        var list = items.ToList();
        // tra i candidati vince il nome più corto (il più vicino a ciò che è stato detto)
        return list.FirstOrDefault(i => Norm(name(i)) == q)
            ?? list.Where(i => Norm(name(i)).Contains(q)).OrderBy(i => name(i).Length).FirstOrDefault()
            ?? list.Where(i => q.Split(' ', StringSplitOptions.RemoveEmptyEntries).All(w => Norm(name(i)).Contains(w))).OrderBy(i => name(i).Length).FirstOrDefault();
    }

    private static bool Local(Song s) => !string.IsNullOrEmpty(s.LocalFilePath) && File.Exists(s.LocalFilePath);

    private static object Describe(Song s) => new { id = s.Id, titolo = s.Title, artista = s.Artist, scaricato = Local(s) };

    private async Task<object> MusicStateAsync() => await MainThread.InvokeOnMainThreadAsync(() => new
    {
        inRiproduzione = _audio.IsPlaying,
        brano = _audio.CurrentSong == null ? null : new { titolo = _audio.CurrentSong.Title, artista = _audio.CurrentSong.Artist },
        casuale = _audio.IsShuffleEnabled,
        codaAttiva = _audio.IsQueueActive,
    });

    private async Task<object> ListPlaylistsAsync()
    {
        var result = new List<object>();
        foreach (var p in await _db.GetAllPlaylistsAsync())
        {
            var songs = await _db.GetSongsForPlaylistAsync(p.Id);
            result.Add(new { id = p.Id, nome = p.Name.Trim(), brani = songs.Count, scaricati = songs.Count(Local) });
        }
        return result;
    }

    private async Task<object> ListSongsAsync(string q)
    {
        var songs = await _db.GetAllSongsAsync();
        return songs.Where(s => string.IsNullOrWhiteSpace(q) || Contains(s.Title, q) || Contains(s.Artist, q))
                    .OrderBy(s => s.Title).Take(30).Select(Describe).ToList();
    }

    private double? _volumeOriginale;
    private IDispatcherTimer _duckTimer;

    // Da chiamare sul thread dell'interfaccia.
    private void Duck(bool attiva)
    {
        if (_audio.Player == null) return;
        if (attiva)
        {
            if (_volumeOriginale == null) { _volumeOriginale = _audio.Player.Volume; }
            _audio.Player.Volume = _volumeOriginale.Value * 0.2;
            _duckTimer ??= Application.Current.Dispatcher.CreateTimer();
            _duckTimer.Interval = TimeSpan.FromSeconds(90);
            _duckTimer.IsRepeating = false;
            _duckTimer.Tick -= OnDuckTimeout;
            _duckTimer.Tick += OnDuckTimeout;
            _duckTimer.Stop(); _duckTimer.Start();
        }
        else if (_volumeOriginale != null)
        {
            _audio.Player.Volume = _volumeOriginale.Value;
            _volumeOriginale = null;
            _duckTimer?.Stop();
        }
    }

    private void OnDuckTimeout(object sender, EventArgs e) => Duck(false);

    private async Task<object> MusicCommandAsync(string command, JsonElement j)
    {
        switch (command)
        {
            case "play":
            {
                string name = Str(j, "playlist");
                var playlist = Best(await _db.GetAllPlaylistsAsync(), name ?? "", p => p.Name) ?? throw new ApiError(404, $"Non trovo la playlist «{name}»");
                var songs = (await _db.GetSongsForPlaylistAsync(playlist.Id)).Where(Local).ToList(); // si ascoltano solo brani scaricati
                if (songs.Count == 0) throw new ApiError(409, $"La playlist «{playlist.Name}» non ha brani scaricati su questo PC");
                bool shuffle = Bool(j, "casuale") ?? false;
                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    if (shuffle) _audio.PlayShuffled(songs);
                    else { _audio.IsShuffleEnabled = false; _audio.PlayQueue(songs, 0); }
                });
                await Task.Delay(800); // il lettore impiega un attimo a partire: così lo stato che segue è quello vero
                return new { playlist = playlist.Name.Trim(), brani = songs.Count, casuale = shuffle };
            }
            case "brano":
            {
                string title = Str(j, "titolo");
                var song = Best((await _db.GetAllSongsAsync()).Where(Local), title ?? "", s => $"{s.Title} {s.Artist}") ?? throw new ApiError(404, $"Non trovo un brano scaricato «{title}»");
                await MainThread.InvokeOnMainThreadAsync(() => { _audio.ClearQueue(); _audio.PlaySong(song, song.LocalFilePath); });
                await Task.Delay(800);
                return new { brano = song.Title, artista = song.Artist };
            }
            case "pausa": await MainThread.InvokeOnMainThreadAsync(_audio.Pause); return new { ok = true };
            case "riprendi": await MainThread.InvokeOnMainThreadAsync(_audio.Resume); return new { ok = true };
            case "prossimo": await MainThread.InvokeOnMainThreadAsync(_audio.PlayNext); await Task.Delay(500); return new { ok = _audio.IsQueueActive };
            case "precedente": await MainThread.InvokeOnMainThreadAsync(_audio.PlayPrevious); await Task.Delay(500); return new { ok = _audio.IsQueueActive };
            case "volume":
            {
                int level = Math.Clamp(Int(j, "percentuale") ?? throw new ApiError(400, "Manca la percentuale"), 0, 100);
                await MainThread.InvokeOnMainThreadAsync(() => { if (_audio.Player != null) _audio.Player.Volume = level / 100.0; });
                return new { volume = level };
            }
            case "attenua":
            {
                // l'assistente sta parlando: la musica scende e poi torna com'era. Se «torna su» non arriva, ci pensa un timer.
                bool attiva = Bool(j, "attiva") ?? true;
                await MainThread.InvokeOnMainThreadAsync(() => Duck(attiva));
                return new { attenuata = _volumeOriginale != null };
            }
            default: throw new ApiError(404, "Comando musicale sconosciuto");
        }
    }

    // ---------- note e pagamenti ----------
    private static object Describe(Note n) => new
    {
        id = n.Id, tipo = n.Type, titolo = n.Title, contenuto = n.Content, scadenza = n.DueDate?.ToString("yyyy-MM-dd"),
        importo = n.IsPayment ? n.Amount : (double?)null, entrata = n.IsPayment ? n.IsIncome : (bool?)null,
        pagato = n.IsPayment ? n.IsPaid : (bool?)null, mensile = n.IsPayment ? n.IsMonthly : (bool?)null, inRitardo = n.IsOverdue,
    };

    private async Task<object> ListNotesAsync(HttpListenerRequest r)
    {
        var all = await _db.GetAllNotesAsync();
        string type = Q(r, "tipo");
        bool onlyOpen = Q(r, "aperti") == "1"; // pagamenti non ancora pagati
        string q = Q(r, "q")?.Trim();
        return all.Where(n => string.IsNullOrEmpty(type) || string.Equals(n.Type, type, StringComparison.OrdinalIgnoreCase))
            .Where(n => !onlyOpen || !(n.IsPayment && n.IsPaid))
            .Where(n => string.IsNullOrEmpty(q) || Contains(n.Title, q) || Contains(n.Content, q))
            .OrderBy(n => n.DueDate ?? DateTime.MaxValue).ThenByDescending(n => n.UpdatedAt)
            .Select(Describe).ToList();
    }

    private async Task<object> CreateNoteAsync(JsonElement j)
    {
        string title = Str(j, "titolo");
        if (string.IsNullOrWhiteSpace(title)) throw new ApiError(400, "Manca il titolo");
        string type = Note.Tipi.FirstOrDefault(t => string.Equals(t, Str(j, "tipo"), StringComparison.OrdinalIgnoreCase)) ?? "Nota";
        var n = new Note { Title = title.Trim(), Content = Str(j, "contenuto"), Type = type };
        if (Str(j, "scadenza") is { Length: > 0 } due) n.DueDate = Date(due, "Scadenza").Date;
        if (type == "Pagamento")
        {
            n.Amount = Num(j, "importo") ?? 0;
            n.IsIncome = Bool(j, "entrata") ?? false;
            n.IsMonthly = Bool(j, "mensile") ?? false;
        }
        await _db.SaveNoteAsync(n);
        return Describe(n);
    }

    private async Task<Note> FindNoteAsync(string id) =>
        (await _db.GetAllNotesAsync()).FirstOrDefault(n => n.Id == id) ?? throw new ApiError(404, "Nota non trovata");

    private async Task<object> TogglePaidAsync(string id)
    {
        var n = await FindNoteAsync(id);
        if (!n.IsPayment) throw new ApiError(400, "Non è un pagamento");
        await _db.TogglePaymentAsync(n); // se è mensile passa al mese dopo
        return Describe(n);
    }

    private async Task<object> DeleteNoteAsync(string id)
    {
        var n = await FindNoteAsync(id);
        await _db.DeleteNoteAsync(n);
        return new { eliminato = n.Title };
    }
}
#endif
