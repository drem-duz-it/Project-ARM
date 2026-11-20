using ARM.Data;
using ARM.Models;

namespace ARM.Services;

// Tutti i download dei brani passano da qui (Cerca, Spotify, "Scarica mancanti", altri dispositivi).
// - un download alla volta, gli altri aspettano in coda
// - lo stesso brano richiesto due volte si scarica una volta sola
// - continua anche se esci dalla pagina che l'ha chiesto
public class MusicDownloads
{
    private readonly YouTubeService _youtube;
    private readonly DatabaseService _db;
    private readonly SemaphoreSlim _uno = new(1, 1);
    private readonly Dictionary<string, Task<bool>> _inCorso = new();

    public MusicDownloads(YouTubeService youtube, DatabaseService db)
    {
        _youtube = youtube;
        _db = db;
    }

    // Un brano è entrato in coda o ha finito (id del brano). Arriva sul thread grafico.
    public event Action<string> Changed;

    public string LastError { get; private set; }

    public bool IsDownloading(string songId)
    {
        lock (_inCorso) return songId != null && _inCorso.ContainsKey(songId);
    }

    // Scarica il file del brano (che deve essere già salvato nel database). true = il file c'è.
    public Task<bool> DownloadAsync(Song song)
    {
        lock (_inCorso)
        {
            if (_inCorso.TryGetValue(song.Id, out var esistente)) return esistente;
            var t = EseguiAsync(song);
            _inCorso[song.Id] = t;
            return t;
        }
    }

    // Aggiunge un risultato della ricerca a una playlist: entra subito, il file arriva in background.
    // false = era già nella playlist.
    public async Task<bool> AddToPlaylistAsync(Song risultato, string playlistId)
    {
        var song = await _db.AddOrGetSongAsync(risultato);
        bool aggiunto = await _db.AddSongToPlaylistAsync(playlistId, song.Id);
        if (!song.IsDownloaded || !File.Exists(song.LocalFilePath)) _ = DownloadAsync(song);
        return aggiunto;
    }

    private async Task<bool> EseguiAsync(Song song)
    {
        await Task.Yield();   // esce dal lock prima di iniziare
        Notifica(song.Id);
        await _uno.WaitAsync();
        try
        {
            // Già scaricato nel frattempo (es. da un'altra richiesta o dalla sincronizzazione)?
            var salvato = await _db.GetSongAsync(song.Id);
            if (!string.IsNullOrEmpty(salvato?.LocalFilePath) && File.Exists(salvato.LocalFilePath))
            {
                song.LocalFilePath = salvato.LocalFilePath;
                return true;
            }

            // Lavoro pesante fuori dal thread grafico (su Android altrimenti si blocca)
            var path = await Task.Run(async () =>
            {
                var temp = MusicStorage.TempPath(song.Id);
                await _youtube.DownloadAudioAsync(song.Id, temp);
                if (!File.Exists(temp)) return null;
                return await MusicStorage.PublishAsync(temp, song);
            });
            if (path == null)
            {
                LastError = Loc.T("YouTube non ha fornito l'audio di questo brano.");
                return false;
            }

            await _db.SetSongFileAsync(song.Id, path);
            song.LocalFilePath = path;
            return true;
        }
        catch (Exception ex)
        {
            LastError = YouTubeService.SpiegaErrore(ex);
            return false;
        }
        finally
        {
            _uno.Release();
            lock (_inCorso) _inCorso.Remove(song.Id);
            Notifica(song.Id);
        }
    }

    private void Notifica(string songId) => MainThread.BeginInvokeOnMainThread(() => Changed?.Invoke(songId));
}
