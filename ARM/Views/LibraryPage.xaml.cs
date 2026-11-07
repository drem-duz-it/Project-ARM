using ARM.Models;
using ARM.Services;
using ARM.Data;

namespace ARM.Views;

public partial class LibraryPage : ContentView
{
    private readonly DatabaseService _databaseService;
    private readonly AudioService _audioService;
    public Action<ContentView> OnChangePageRequested;
    private readonly YouTubeService _youTubeService;
    public LibraryPage(DatabaseService dbService, AudioService audioService, YouTubeService ytService)
    {
        InitializeComponent();
        Loc.Register(this);
        _databaseService = dbService;
        _audioService = audioService;
        _youTubeService = ytService;
    }

    public async void RicaricaLibreria()
    {
        // 1. Carica le Playlist create
        var playlists = await _databaseService.GetAllPlaylistsAsync();
        PlaylistsCollection.ItemsSource = playlists;
        PlaylistsCollection.IsVisible = playlists.Count > 0;
        NoPlaylistsLabel.IsVisible = playlists.Count == 0;

        // 2. Carica i brani scaricati
        var braniScaricati = (await _databaseService.GetAllSongsAsync()).Where(s => s.IsDownloaded).ToList();
        LibraryList.ItemsSource = braniScaricati;
        NoSongsLabel.IsVisible = braniScaricati.Count == 0;
        SongsCountLabel.Text = braniScaricati.Count == 1 ? Loc.T("1 brano") : Loc.F("{0} brani", braniScaricati.Count);
    }

    // Apre la finestra per creare la playlist (sopra al mini-player)
    private async void OnAddPlaylistClicked(object sender, EventArgs e)
    {
        await Application.Current.MainPage.Navigation.PushModalAsync(new CreatePlaylistPage(_databaseService));
    }

    // Importa una playlist da Spotify (i brani mancanti vengono scaricati da YouTube)
    private async void OnSpotifyClicked(object sender, EventArgs e)
    {
        var spotify = IPlatformApplication.Current.Services.GetService<SpotifyService>();
        await Application.Current.MainPage.Navigation.PushModalAsync(new SpotifyImportPage(spotify, RicaricaLibreria));
    }

    // Placeholder per quando clicchi una playlist
    private void OnPlaylistSelected(object sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is Playlist selectedPlaylist)
        {
            PlaylistsCollection.SelectedItem = null;

            // Crea la nuova vista della playlist
            var detailPage = new PlaylistDetailPage(selectedPlaylist, _databaseService, _audioService, _youTubeService);

            // Quando la playlist chiama "Indietro", rimettiamo a schermo QUESTA libreria
            detailPage.OnBackRequested = () =>
            {
                OnChangePageRequested?.Invoke(this);
                RicaricaLibreria(); // Aggiorna in caso tu abbia fatto modifiche!
            };

            // Diciamo alla grafica principale di visualizzare la pagina della playlist!
            OnChangePageRequested?.Invoke(detailPage);
        }
    }

    private async void OnSongSelected(object sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is Song selectedSong)
        {
            LibraryList.SelectedItem = null;

            string action = await Application.Current.MainPage.DisplayActionSheet(
                Loc.F("Opzioni: {0}", selectedSong.Title),
                Loc.T("Annulla"),
                null,
                Loc.T("▶ Riproduci Offline"),
                Loc.T("Aggiungi a una playlist")); // Aggiunta la voce qui!

            if (action == Loc.T("▶ Riproduci Offline"))
            {
                _audioService.ClearQueue();
                _audioService.PlaySong(selectedSong, selectedSong.LocalFilePath);
            }
            else if (action == Loc.T("Aggiungi a una playlist"))
            {
                var playlists = await _databaseService.GetAllPlaylistsAsync();

                if (playlists.Count == 0)
                {
                    await Application.Current.MainPage.DisplayAlert(Loc.T("Ops"), Loc.T("Non hai ancora creato nessuna playlist!"), Loc.T("OK"));
                    return;
                }

                string[] playlistNames = playlists.Select(p => p.Name).ToArray();
                string chosen = await Application.Current.MainPage.DisplayActionSheet(Loc.T("Scegli la Playlist"), Loc.T("Annulla"), null, playlistNames);

                if (chosen != Loc.T("Annulla") && !string.IsNullOrEmpty(chosen))
                {
                    var targetPlaylist = playlists.First(p => p.Name == chosen);

                    // Salviamo il collegamento nel database
                    await _databaseService.AddSongToPlaylistAsync(targetPlaylist.Id, selectedSong.Id);

                    await Application.Current.MainPage.DisplayAlert(Loc.T("Fatto"), Loc.F("Brano aggiunto a {0}!", targetPlaylist.Name), Loc.T("OK"));
                }
            }
        }
    }
}