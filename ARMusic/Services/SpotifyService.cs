using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using ARMusic.Data;
using ARMusic.Models;

namespace ARMusic.Services;

// Un brano letto da una playlist di Spotify
public record SpotifyTrack(string Id, string Title, string Artist);

// Una playlist di Spotify (solo i dati che servono per importarla)
public class SpotifyPlaylist
{
    public string Name;
    public string Description;
    public string ImageUrl;
    public List<SpotifyTrack> Tracks = new();
}

// Importa playlist da Spotify tramite l'API ufficiale (Client Credentials).
// Servono un Client ID e un Client Secret, che si ottengono creando gratis un'app su developer.spotify.com.
// I brani mancanti vengono cercati su YouTube e scaricati.
public class SpotifyService
{
    private readonly HttpClient _http = new();
    private readonly DatabaseService _db;
    private readonly YouTubeService _youtube;
    private string _token;
    private DateTime _tokenExpires;

    public SpotifyService(DatabaseService db, YouTubeService youtube)
    {
        _db = db;
        _youtube = youtube;
    }

    // ---------- Credenziali ----------
    // Nessuna chiave inclusa nel codice: ognuno inserisce le proprie dalla schermata di importazione (vedi README)

    public string ClientId => Preferences.Default.Get("Spotify.ClientId", "");
    private string ClientSecret => Preferences.Default.Get("Spotify.ClientSecret", "");
    public bool HasCredentials => ClientId != "" && ClientSecret != "";

    public void SaveCredentials(string clientId, string clientSecret)
    {
        Preferences.Default.Set("Spotify.ClientId", clientId.Trim());
        Preferences.Default.Set("Spotify.ClientSecret", clientSecret.Trim());
        _token = null;
    }

    public void ClearCredentials()
    {
        // Vuoti (non rimossi): così si torna alla schermata per inserire codici diversi
        Preferences.Default.Set("Spotify.ClientId", "");
        Preferences.Default.Set("Spotify.ClientSecret", "");
        _token = null;
    }

    // Estrae l'id da un link (open.spotify.com/playlist/ID?si=...) o da "spotify:playlist:ID"
    public static string ParsePlaylistId(string link)
    {
        if (string.IsNullOrWhiteSpace(link)) return null;
        var m = Regex.Match(link.Trim(), @"playlist[/:]([A-Za-z0-9]{22})");
        if (m.Success) return m.Groups[1].Value;
        return Regex.IsMatch(link.Trim(), "^[A-Za-z0-9]{22}$") ? link.Trim() : null;
    }

    private async Task<string> GetTokenAsync()
    {
        if (_token != null && DateTime.Now < _tokenExpires) return _token;

        var req = new HttpRequestMessage(HttpMethod.Post, "https://accounts.spotify.com/api/token")
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string> { ["grant_type"] = "client_credentials" })
        };
        req.Headers.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes($"{ClientId}:{ClientSecret}")));
        using var res = await _http.SendAsync(req);
        if (!res.IsSuccessStatusCode)
            throw new InvalidOperationException(Loc.T("Spotify non ha accettato Client ID e Client Secret. Controlla di averli copiati bene."));

        using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync());
        _token = doc.RootElement.GetProperty("access_token").GetString();
        _tokenExpires = DateTime.Now.AddSeconds(doc.RootElement.GetProperty("expires_in").GetInt32() - 60);
        return _token;
    }

    private async Task<JsonDocument> GetJsonAsync(string url)
    {
        var req = new HttpRequestMessage(HttpMethod.Get, url);
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", await GetTokenAsync());
        using var res = await _http.SendAsync(req);
        if ((int)res.StatusCode == 404)
            throw new InvalidOperationException(Loc.T("Playlist non trovata. Deve essere pubblica; le playlist create da Spotify stesso (es. \"Top 50\") non sono accessibili alle app."));
        if ((int)res.StatusCode == 403 && (await res.Content.ReadAsStringAsync()).Contains("premium", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException(Loc.T("Spotify permette di leggere le playlist solo se l'account che ha creato l'app su developer.spotify.com ha un abbonamento Premium attivo. ") +
                                                Loc.T("Se l'hai appena attivato, possono servire alcune ore prima che funzioni."));
        if (!res.IsSuccessStatusCode)
            throw new InvalidOperationException(Loc.F("Spotify ha risposto con un errore ({0}).", (int)res.StatusCode));
        return JsonDocument.Parse(await res.Content.ReadAsStringAsync());
    }

    // Legge nome, copertina e brani (tutte le pagine, 100 alla volta)
    public async Task<SpotifyPlaylist> GetPlaylistAsync(string playlistId)
    {
        var result = new SpotifyPlaylist();
        using (var info = await GetJsonAsync($"https://api.spotify.com/v1/playlists/{playlistId}?fields=name,description,images"))
        {
            var root = info.RootElement;
            result.Name = root.GetProperty("name").GetString();
            result.Description = root.TryGetProperty("description", out var d) ? Regex.Replace(d.GetString() ?? "", "<.*?>", "") : "";
            if (root.TryGetProperty("images", out var imgs) && imgs.ValueKind == JsonValueKind.Array && imgs.GetArrayLength() > 0)
                result.ImageUrl = imgs[0].GetProperty("url").GetString();
        }

        string next = $"https://api.spotify.com/v1/playlists/{playlistId}/tracks?limit=100&fields=next,items(track(id,name,type,artists(name)))";
        while (next != null)
        {
            using var page = await GetJsonAsync(next);
            foreach (var item in page.RootElement.GetProperty("items").EnumerateArray())
            {
                if (!item.TryGetProperty("track", out var t) || t.ValueKind != JsonValueKind.Object) continue;
                if (t.TryGetProperty("type", out var type) && type.GetString() != "track") continue;   // salta i podcast
                var artists = t.GetProperty("artists").EnumerateArray().Select(a => a.GetProperty("name").GetString());
                var id = t.TryGetProperty("id", out var tid) && tid.ValueKind == JsonValueKind.String ? tid.GetString() : Guid.NewGuid().ToString("N");
                result.Tracks.Add(new SpotifyTrack(id, t.GetProperty("name").GetString(), string.Join(", ", artists)));
            }
            next = page.RootElement.TryGetProperty("next", out var n) && n.ValueKind == JsonValueKind.String ? n.GetString() : null;
        }
        return result;
    }

    // ---------- Importazione ----------
    public class ImportProgress
    {
        public int Done, Total, Reused, Downloaded, NotDownloaded, NotFound;
        public string Current;
    }

    // Crea la playlist in ARMusic e la riempie: brani già presenti riusati, gli altri cercati e scaricati da YouTube
    public async Task<Playlist> ImportAsync(SpotifyPlaylist sp, IProgress<ImportProgress> progress, CancellationToken ct)
    {
        var playlist = new Playlist { Name = sp.Name, Description = string.IsNullOrWhiteSpace(sp.Description) ? Loc.T("Importata da Spotify") : sp.Description };
        await ScaricaCopertinaAsync(playlist, sp.ImageUrl);
        await _db.SavePlaylistAsync(playlist);

        var p = new ImportProgress { Total = sp.Tracks.Count };
        var library = await _db.GetAllSongsAsync();

        foreach (var track in sp.Tracks)
        {
            ct.ThrowIfCancellationRequested();
            p.Current = $"{track.Artist} - {track.Title}";
            progress?.Report(p);

            var song = TrovaInLibreria(library, track);
            if (song != null)
            {
                p.Reused++;
            }
            else
            {
                song = await CercaSuYouTubeAsync(track);
                if (song == null) { p.NotFound++; p.Done++; continue; }

                var existing = library.FirstOrDefault(s => s.Id == song.Id);
                if (existing != null) { song = existing; p.Reused++; }
                else
                {
                    await _db.SaveSongAsync(song);
                    library.Add(song);
                    if (await ScaricaAsync(song)) p.Downloaded++; else p.NotDownloaded++;
                }
            }

            Preferences.Default.Set($"Spotify.Track.{track.Id}", song.Id);   // ricorda l'abbinamento per le prossime volte
            await _db.AddSongToPlaylistAsync(playlist.Id, song.Id);
            p.Done++;
            progress?.Report(p);
        }
        p.Current = null;
        progress?.Report(p);
        return playlist;
    }

    // Riprova a scaricare i brani di una playlist che non hanno ancora il file
    public async Task<(int Ok, int Failed)> ScaricaMancantiAsync(string playlistId, IProgress<string> progress)
    {
        int ok = 0, failed = 0;
        var missing = (await _db.GetSongsForPlaylistAsync(playlistId)).Where(s => !s.IsDownloaded || !File.Exists(s.LocalFilePath)).ToList();
        foreach (var song in missing)
        {
            progress?.Report(Loc.F("Scarico {0}/{1}: {2}", ok + failed + 1, missing.Count, song.Title));
            if (await ScaricaAsync(song)) ok++; else failed++;
        }
        return (ok, failed);
    }

    public string LastDownloadError { get; private set; }

    private async Task<bool> ScaricaAsync(Song song)
    {
        try
        {
            var temp = MusicStorage.TempPath(song.Id);
            await _youtube.DownloadAudioAsync(song.Id, temp);
            if (!File.Exists(temp)) return false;
            var path = await MusicStorage.PublishAsync(temp, song);
            await _db.SetSongFileAsync(song.Id, path);
            song.LocalFilePath = path;
            return true;
        }
        catch (Exception ex)
        {
            LastDownloadError = YouTubeService.SpiegaErrore(ex);
            return false;
        }
    }

    private static Song TrovaInLibreria(List<Song> library, SpotifyTrack track)
    {
        // 1. Abbinamento già fatto in un'importazione precedente
        var saved = Preferences.Default.Get($"Spotify.Track.{track.Id}", "");
        if (saved != "" && library.FirstOrDefault(s => s.Id == saved) is Song known) return known;

        // 2. Stesso titolo e (primo) artista già in libreria
        var title = Normalizza(track.Title);
        var artist = Normalizza(track.Artist.Split(',')[0]);
        return library.FirstOrDefault(s =>
        {
            var t = Normalizza(s.Title);
            return t.Contains(title) && (t.Contains(artist) || Normalizza(s.Artist).Contains(artist));
        });
    }

    // Sceglie tra i risultati di YouTube quello che assomiglia di più al brano
    private async Task<Song> CercaSuYouTubeAsync(SpotifyTrack track)
    {
        List<Song> results;
        try { results = await _youtube.SearchSongsAsync($"{track.Artist} - {track.Title} audio"); }
        catch { return null; }
        if (results.Count == 0) return null;

        var title = Normalizza(track.Title);
        var artist = Normalizza(track.Artist.Split(',')[0]);
        int Punteggio(Song s)
        {
            var t = Normalizza(s.Title);
            var a = Normalizza(s.Artist);
            int score = 0;
            if (t.Contains(title)) score += 3;
            if (t.Contains(artist) || a.Contains(artist)) score += 2;
            if (a.Contains("topic") || t.Contains("audio")) score += 1;      // canali "Artista - Topic": audio ufficiale
            if (t.Contains("live") && !title.Contains("live")) score -= 2;
            if (t.Contains("cover") && !title.Contains("cover")) score -= 2;
            return score;
        }
        return results.Take(8).OrderByDescending(Punteggio).First();
    }

    private static string Normalizza(string s) =>
        Regex.Replace((s ?? "").ToLowerInvariant().Normalize(NormalizationForm.FormD), @"[^\p{L}\p{N} ]", "").Trim();

    private async Task ScaricaCopertinaAsync(Playlist playlist, string url)
    {
        if (string.IsNullOrEmpty(url)) return;
        try
        {
            var bytes = await _http.GetByteArrayAsync(url);
            playlist.ImageId = Guid.NewGuid().ToString("N");
            var dest = DatabaseService.CoverPath(playlist.ImageId);
            Directory.CreateDirectory(Path.GetDirectoryName(dest));
            await File.WriteAllBytesAsync(dest, bytes);
            playlist.ImagePath = dest;
        }
        catch { /* senza copertina va bene lo stesso */ }
    }
}
