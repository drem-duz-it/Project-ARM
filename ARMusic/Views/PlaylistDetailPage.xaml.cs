using ARMusic.Models;
using ARMusic.Data;
using ARMusic.Services;

namespace ARMusic.Views;

public partial class PlaylistDetailPage : ContentView
{
    private readonly Playlist _playlist;
    private readonly DatabaseService _databaseService;
    private readonly AudioService _audioService;
    private readonly YouTubeService _youTubeService;

    public Action OnBackRequested;

    public PlaylistDetailPage(Playlist playlist, DatabaseService dbService, AudioService audioService, YouTubeService ytService)
    {
        InitializeComponent();
        Loc.Register(this);
        _playlist = playlist;
        _databaseService = dbService;
        _audioService = audioService;
        _youTubeService = ytService;

        // Imposta la copertina e i testi
        HeaderImage.Source = playlist.CoverSource;
        HeaderTitle.Text = playlist.Name;
        HeaderDescription.Text = playlist.Description;

        CaricaBrani();
    }

    private async void CaricaBrani()
    {
        var songs = await _databaseService.GetSongsForPlaylistAsync(_playlist.Id);
        PlaylistSongsList.ItemsSource = songs;

        int mancanti = songs.Count(s => !s.IsDownloaded || !File.Exists(s.LocalFilePath));
        MissingButton.IsVisible = mancanti > 0;
        MissingLabel.Text = mancanti == 1 ? Loc.T("Scarica 1 brano mancante") : Loc.F("Scarica i {0} brani mancanti", mancanti);
    }

    // Avvia la coda dalla canzone cliccata
    private void OnSongPlayClicked(object sender, TappedEventArgs e)
    {
        if (e.Parameter is Song branoCliccato)
        {
                PlaylistSongsList.SelectedItem = null;

                var songsList = (List<Song>)PlaylistSongsList.ItemsSource;
                int startIndex = songsList.IndexOf(branoCliccato);

                _audioService.PlayQueue(songsList, startIndex);
            
        }
    }

    // Riprova a scaricare i brani che non hanno ancora il file
    private bool _downloading;
    private async void OnDownloadMissingClicked(object sender, TappedEventArgs e)
    {
        if (_downloading) return;
        _downloading = true;
        var spotify = IPlatformApplication.Current.Services.GetService<SpotifyService>();
        var (ok, failed) = await spotify.ScaricaMancantiAsync(_playlist.Id, new Progress<string>(t => MissingLabel.Text = t));
        _downloading = false;
        CaricaBrani();
        if (failed > 0)
            await Application.Current.MainPage.DisplayAlert(Loc.T("Download"), Loc.F("Scaricati {0} brani, {1} non riusciti.\n\n{2}", ok, failed, spotify.LastDownloadError), Loc.T("OK"));
    }

    // Riproduce tutta la playlist dal primo brano
    private void OnPlayAllClicked(object sender, TappedEventArgs e)
    {
        if (PlaylistSongsList.ItemsSource is List<Song> songs && songs.Count > 0)
            _audioService.PlayQueue(songs, 0);
    }

    // Riproduce la playlist in casuale, partendo da un brano a caso
    private void OnShuffleAllClicked(object sender, TappedEventArgs e)
    {
        if (PlaylistSongsList.ItemsSource is List<Song> songs && songs.Count > 0)
            _audioService.PlayShuffled(songs);
    }

    // Apre la pagina di modifica
    // Quando clicchi la freccia indietro
    private void OnBackClicked(object sender, TappedEventArgs e)
    {
        OnBackRequested?.Invoke();
    }

    // Quando clicchi la matita
    private async void OnEditClicked(object sender, TappedEventArgs e)
    {
        await Application.Current.MainPage.Navigation.PushModalAsync(new CreatePlaylistPage(_databaseService, _playlist));
    }

    private void OnCollectionViewScrolled(object sender, ItemsViewScrolledEventArgs e)
    {
        double offset = e.VerticalOffset;
        if (offset < 0) offset = 0;

        // Riduciamo la DIMENSIONE REALE dell'immagine (da 180 fino a 0)
        double newSize = Math.Max(0, 200 - offset);
        ImageContainer.HeightRequest = newSize;
        ImageContainer.WidthRequest = newSize;
        ImageContainer.Opacity = Math.Max(0, 1 - (offset / 150)); // La fa anche sfumare

        // Sfumiamo la descrizione
        HeaderDescription.Opacity = Math.Max(0, 1 - (offset / 50));

        // Quando la descrizione scompare, impostiamo la sua altezza a 0 per recuperare altro spazio!
        // (Il -1 in MAUI significa altezza "Automatica")
        HeaderDescription.HeightRequest = HeaderDescription.Opacity == 0 ? 0 : -1;
    }

    private async void OnSongOptionsClicked(object sender, TappedEventArgs e)
    {
        if (e.Parameter is Song branoSelezionato)
        {
            // Mostra il menu dal basso
            string azione = await Application.Current.MainPage.DisplayActionSheet(
                branoSelezionato.Title,
                Loc.T("Annulla"),
                null,
                Loc.T("Rimuovi dalla playlist"),
                Loc.T("Modifica brano"));

            if (azione == Loc.T("Rimuovi dalla playlist"))
            {
                // Chiama il database per eliminare il collegamento
                await _databaseService.RemoveSongFromPlaylistAsync(branoSelezionato.Id, _playlist.Id);
                CaricaBrani(); // Ricarica la grafica per far sparire la canzone
            }
            else if (azione == Loc.T("Modifica brano"))
            {
                // 1. Popup per cambiare il TITOLO
                string nuovoTitolo = await Application.Current.MainPage.DisplayPromptAsync(
                    Loc.T("Modifica Titolo"), Loc.T("Inserisci il nuovo titolo:"),
                    initialValue: branoSelezionato.Title);

                if (string.IsNullOrWhiteSpace(nuovoTitolo)) return; // Ferma tutto se l'utente annulla

                // 2. Popup per cambiare l'ARTISTA
                string nuovoArtista = await Application.Current.MainPage.DisplayPromptAsync(
                    Loc.T("Modifica Artista"), Loc.T("Inserisci il nuovo artista:"),
                    initialValue: branoSelezionato.Artist);

                if (string.IsNullOrWhiteSpace(nuovoArtista)) return;

                // 3. Applica le modifiche e salva globalmente!
                branoSelezionato.Title = nuovoTitolo;
                branoSelezionato.Artist = nuovoArtista;

                await _databaseService.UpdateSongAsync(branoSelezionato);

                CaricaBrani(); // Aggiorna la lista per vedere il nuovo nome
            }
        }
    }
}