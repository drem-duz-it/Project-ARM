using CommunityToolkit.Maui.Views;
using ARM.Models;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ARM.Services;

public class AudioService
{
    // Aggiungiamo un riferimento a YouTubeService per i brani non scaricati
    public YouTubeService YtResolver { get; set; }

    private MediaElement _player;
    public MediaElement Player
    {
        get => _player;
        set
        {
            // Quando assegniamo il player, sganciamo vecchi eventi e agganciamo fine brano e cambio di stato
            if (_player != null) { _player.MediaEnded -= OnMediaEnded; _player.StateChanged -= OnStateChanged; }
            _player = value;
            if (_player != null) { _player.MediaEnded += OnMediaEnded; _player.StateChanged += OnStateChanged; }
        }
    }

    public Song CurrentSong { get; private set; }
    public event Action<Song> OnSongChanged;

    // Stato reale del lettore: i pulsanti (mini player, player, notifica) si basano su questo
    public bool IsPlaying => Player?.CurrentState == CommunityToolkit.Maui.Core.MediaElementState.Playing;
    public event Action<bool> PlaybackChanged;
    public event Action<string> PlaybackError;      // es. brano non scaricato

    private void OnStateChanged(object sender, CommunityToolkit.Maui.Core.MediaStateChangedEventArgs e) =>
        MainThread.BeginInvokeOnMainThread(() => PlaybackChanged?.Invoke(IsPlaying));

    // Chiamato quando l'app torna in primo piano: ridisegna i pulsanti con lo stato attuale
    public void RefreshPlaybackState() => PlaybackChanged?.Invoke(IsPlaying);

    

    public AudioService() { }

    public void PlaySong(Song song, string audioUrl)
    {
        // Se l'URL è vuoto, fermiamo tutto prima di crashare
        if (string.IsNullOrWhiteSpace(audioUrl)) return;

        CurrentSong = song;
        OnSongChanged?.Invoke(song);

        if (Player != null)
        {
            // Informazioni mostrate nella notifica e nella schermata di blocco di Android
            Player.MetadataTitle = song.Title;
            Player.MetadataArtist = song.Artist;
            Player.MetadataArtworkUrl = song.ThumbnailUrl;

            // Controlla se è un link di streaming o un file locale
            if (audioUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase))
            {
                Player.Source = MediaSource.FromUri(audioUrl);
            }
            else
            {
                // È un file scaricato nel telefono/PC!
                Player.Source = MediaSource.FromFile(audioUrl);
            }
            Player.Speed = 1.0;
            Player.Play();
        }
    }

    public void Pause() => Player?.Pause();
    public void Resume() => Player?.Play();

    public void TogglePlayPause()
    {
        if (IsPlaying) Pause(); else Resume();
    }

    // --- NUOVI METODI DELLA CODA ---

    // --- VARIABILI DELLA CODA E SHUFFLE ---
    private List<Song> _currentQueue = new();
    private int _currentIndex = -1;

    public bool IsShuffleEnabled { get; set; } = false;
    public bool IsQueueActive { get; set; } = false; // Ci dice se stiamo suonando una playlist!

    // Ordine dei brani già suonati in casuale: serve al pulsante «precedente»
    private readonly List<int> _history = new();

    // Quando è stato suonato l'ultima volta ogni brano (contatore progressivo), salvato tra un avvio e l'altro
    private Dictionary<string, long> _recenti;
    private long _tick;
    private const int MaxRecenti = 3000;

    public async void PlayQueue(List<Song> songs, int startIndex = 0)
    {
        if (songs == null || songs.Count == 0) return;

        IsQueueActive = true; // Attiviamo la coda
        _currentQueue = songs;
        _currentIndex = startIndex;
        _history.Clear();
        await PlayCurrentIndex();
    }

    // Avvia la playlist direttamente in casuale, partendo da un brano scelto a caso (non dal primo)
    public void PlayShuffled(List<Song> songs)
    {
        if (songs == null || songs.Count == 0) return;
        IsShuffleEnabled = true;
        _currentQueue = songs;
        _currentIndex = -1;
        PlayQueue(songs, PickShuffleIndex());
    }

    private void CaricaRecenti()
    {
        if (_recenti != null) return;
        try
        {
            _recenti = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, long>>(Preferences.Default.Get("shuffle_recenti", "{}")) ?? new();
            _tick = Preferences.Default.Get("shuffle_tick", 0L);
        }
        catch { _recenti = new(); _tick = 0; }
    }

    private void RegistraAscolto(Song song)
    {
        if (song?.Id == null) return;
        CaricaRecenti();
        _recenti[song.Id] = ++_tick;
        if (_recenti.Count > MaxRecenti)
            foreach (var vecchio in _recenti.OrderBy(kv => kv.Value).Take(_recenti.Count - MaxRecenti).Select(kv => kv.Key).ToList())
                _recenti.Remove(vecchio);
        try
        {
            Preferences.Default.Set("shuffle_recenti", System.Text.Json.JsonSerializer.Serialize(_recenti));
            Preferences.Default.Set("shuffle_tick", _tick);
        }
        catch { /* la memoria dei brani sentiti è solo un di più */ }
    }

    // Casuale «a memoria»: i brani sentiti da poco sono esclusi, poi più è passato tempo più sono probabili.
    // Così non ricapita il brano di due canzoni fa, e andando avanti si gira sempre meno a caso e più «a rotazione».
    private int PickShuffleIndex()
    {
        int n = _currentQueue.Count;
        if (n <= 1) return 0;
        CaricaRecenti();

        int escluse = Math.Min(n - 2, Math.Max(3, n / 4));   // quanti ascolti recenti non si ripetono (ne resta sempre almeno uno disponibile)
        double scala = 2.0 * n;
        var pesi = new double[n];
        double totale = 0;
        for (int i = 0; i < n; i++)
        {
            if (i == _currentIndex) continue;
            long eta = _recenti.TryGetValue(_currentQueue[i].Id ?? "", out long t) ? _tick - t : long.MaxValue;
            if (eta <= escluse) continue;
            double x = Math.Min(eta, scala) / scala;
            pesi[i] = x * x;
            totale += pesi[i];
        }
        if (totale <= 0)   // non dovrebbe succedere: scegli il meno recente
        {
            int migliore = -1; long min = long.MaxValue;
            for (int i = 0; i < n; i++)
            {
                if (i == _currentIndex) continue;
                long t = _recenti.TryGetValue(_currentQueue[i].Id ?? "", out long v) ? v : 0;
                if (t < min) { min = t; migliore = i; }
            }
            return Math.Max(0, migliore);
        }

        double r = Random.Shared.NextDouble() * totale;
        for (int i = 0; i < n; i++)
        {
            r -= pesi[i];
            if (pesi[i] > 0 && r <= 0) return i;
        }
        return Array.FindLastIndex(pesi, p => p > 0);
    }

    // NUOVO: Chiama questo quando avvii una canzone singola dalla ricerca
    public void ClearQueue()
    {
        IsQueueActive = false;
        _currentQueue?.Clear();
    }

    public async void PlayNext()
    {
        if (!IsQueueActive || _currentQueue == null || _currentQueue.Count == 0) return;

        if (IsShuffleEnabled)
        {
            // SHUFFLE ACCESO: casuale ma a memoria (vedi PickShuffleIndex)
            if (_currentIndex >= 0)
            {
                _history.Add(_currentIndex);
                if (_history.Count > 100) _history.RemoveAt(0);
            }
            _currentIndex = PickShuffleIndex();
            await PlayCurrentIndex();
        }
        else if (_currentIndex < _currentQueue.Count - 1)
        {
            // SHUFFLE SPENTO: Va semplicemente alla successiva
            _currentIndex++;
            await PlayCurrentIndex();
        }
    }
    // Logica interna per riprodurre l'elemento corrente
    public async void PlayPrevious()
    {
        if (!IsQueueActive || _currentQueue == null || _currentQueue.Count == 0) return;

        // In casuale torna al brano suonato prima, non a quello che sta sopra nella lista
        if (IsShuffleEnabled && _history.Count > 0)
        {
            _currentIndex = _history[^1];
            _history.RemoveAt(_history.Count - 1);
        }
        // Torna indietro di uno. Se è all'inizio, ricomincia dalla prima canzone
        else if (_currentIndex > 0)
            _currentIndex--;
        else
            _currentIndex = 0;

        await PlayCurrentIndex();
    }
    private async Task PlayCurrentIndex()
    {
        if (_currentIndex >= 0 && _currentIndex < _currentQueue.Count)
        {
            var song = _currentQueue[_currentIndex];
            RegistraAscolto(song);
            string audioUrl = song.LocalFilePath;

            bool scaricato = !string.IsNullOrEmpty(audioUrl) && File.Exists(audioUrl);
            if (!scaricato)
            {
                // Si ascoltano solo i brani scaricati: avvisa e passa al primo brano scaricato che segue nella coda
                PlaybackError?.Invoke(Loc.F("\"{0}\" non è scaricato. Scaricalo per ascoltarlo.", song.Title));
                int prossimo = _currentQueue.FindIndex(_currentIndex + 1, s => !string.IsNullOrEmpty(s.LocalFilePath) && File.Exists(s.LocalFilePath));
                if (prossimo >= 0)
                {
                    _currentIndex = prossimo;
                    await PlayCurrentIndex();
                }
                return;
            }

            PlaySong(song, audioUrl);
        }
    }

    // L'evento che scatta da solo quando la canzone finisce!
    private void OnMediaEnded(object sender, EventArgs e)
    {
        // Forza l'esecuzione sul thread della grafica per evitare il COMException
        MainThread.BeginInvokeOnMainThread(() =>
        {
            // Se il player NON è in loop, passa alla successiva
            if (Player != null && !Player.ShouldLoopPlayback)
            {
                PlayNext();
            }
        });
    }
}