using ARM.Models;
using ARM.Services;
using ARM.Data;

namespace ARM.Views;

public partial class SearchPage : ContentView
{
    private readonly YouTubeService _youTubeService;
    private readonly AudioService _audioService;
    private readonly DatabaseService _databaseService;
    private readonly HttpClient _httpClient;

    public SearchPage(YouTubeService ytService, AudioService audioService, DatabaseService dbService, HttpClient httpClient)
    {
        InitializeComponent();
        Loc.Register(this);
        _youTubeService = ytService;
        _audioService = audioService;
        _databaseService = dbService;
        _httpClient = httpClient;
    }

    // Ricerche recenti (le più nuove per prime), mostrate nella pagina Scopri
    public static List<string> RicercheRecenti() =>
        Preferences.Default.Get("RicercheRecenti", "").Split('\n', StringSplitOptions.RemoveEmptyEntries).ToList();

    private static void SalvaRicerca(string query)
    {
        var lista = RicercheRecenti();
        lista.RemoveAll(q => q.Equals(query, StringComparison.OrdinalIgnoreCase));
        lista.Insert(0, query);
        Preferences.Default.Set("RicercheRecenti", string.Join("\n", lista.Take(8)));
    }

    // Avvia una ricerca da fuori (es. toccando una ricerca recente)
    public void Cerca(string query)
    {
        MusicSearchBar.Text = query;
        OnSearchButtonPressed(this, EventArgs.Empty);
    }

    private async void OnSearchButtonPressed(object sender, EventArgs e)
    {
        string query = MusicSearchBar.Text?.Trim();
        if (string.IsNullOrWhiteSpace(query))
            return;

        SalvaRicerca(query);
        SearchHint.IsVisible = false;
        LoadingSpinner.IsRunning = true;
        LoadingSpinner.IsVisible = true;
        SearchResultsList.IsVisible = false;

        try
        {
            var results = await _youTubeService.SearchSongsAsync(query);
            SearchResultsList.ItemsSource = results;
        }
        catch (Exception ex)
        {
            await Application.Current.MainPage.DisplayAlert(Loc.T("Errore"), Loc.T("Impossibile trovare brani: ") + ex.Message, Loc.T("OK"));
        }
        finally
        {
            LoadingSpinner.IsRunning = false;
            LoadingSpinner.IsVisible = false;
            SearchResultsList.IsVisible = true;
        }
    }

    private MusicDownloads Downloads => IPlatformApplication.Current.Services.GetService<MusicDownloads>();

    // Scarica il brano nella libreria (in background, tramite la coda dei download)
    private async Task ScaricaBranoAsync(Song risultato)
    {
        var song = await _databaseService.AddOrGetSongAsync(risultato);
        if (song.IsDownloaded && File.Exists(song.LocalFilePath))
        {
            await Application.Current.MainPage.DisplayAlert(Loc.T("Completato"), Loc.F("{0} aggiunto alla Libreria!", song.Title), Loc.T("OK"));
            return;
        }

        _ = Application.Current.MainPage.DisplayAlert(Loc.T("Download in corso"), Loc.F("Sto scaricando {0} in background...", song.Title), Loc.T("Capito"));
        if (await Downloads.DownloadAsync(song))
            await Application.Current.MainPage.DisplayAlert(Loc.T("Completato"), Loc.F("{0} aggiunto alla Libreria!", song.Title), Loc.T("OK"));
        else
            await Application.Current.MainPage.DisplayAlert(Loc.T("Errore"), Loc.T("Download fallito: ") + Downloads.LastError, Loc.T("OK"));
    }

    // Aggiunge il brano a una playlist: entra subito, il file si scarica in background
    private async Task AggiungiAPlaylistAsync(Song risultato)
    {
        var playlists = await _databaseService.GetAllPlaylistsAsync();
        if (playlists.Count == 0)
        {
            await Application.Current.MainPage.DisplayAlert(Loc.T("Ops"), Loc.T("Non hai ancora creato nessuna playlist!"), Loc.T("OK"));
            return;
        }

        string scelta = await Application.Current.MainPage.DisplayActionSheet(Loc.T("Scegli la Playlist"), Loc.T("Annulla"), null, playlists.Select(p => p.Name).ToArray());
        var playlist = playlists.FirstOrDefault(p => p.Name == scelta);
        if (playlist == null) return;

        bool aggiunto = await Downloads.AddToPlaylistAsync(risultato, playlist.Id);
        await Application.Current.MainPage.DisplayAlert(Loc.T("Fatto"),
            aggiunto ? Loc.F("Brano aggiunto a {0}!", playlist.Name) : Loc.F("Il brano è già in {0}.", playlist.Name),
            Loc.T("OK"));
    }

    private async void OnSongSelected(object sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is Song selectedSong)
        {
            SearchResultsList.SelectedItem = null;

            string action = await Application.Current.MainPage.DisplayActionSheet(
                Loc.F("Opzioni: {0}", selectedSong.Title),
                Loc.T("Annulla"),
                null,
                Loc.T("Scarica offline"),
                Loc.T("Aggiungi a una playlist"));

            if (action == Loc.T("Scarica offline"))
                await ScaricaBranoAsync(selectedSong);
            else if (action == Loc.T("Aggiungi a una playlist"))
                await AggiungiAPlaylistAsync(selectedSong);
        }
    }
}