using ARMusic.Models;
using ARMusic.Services;
using ARMusic.Data;

namespace ARMusic.Views;

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

    // Metodo dedicato al download (con Task.Run per evitare il crash su Android)
    private async Task ScaricaBranoAsync(Song song)
    {
        try
        {
            _ = Application.Current.MainPage.DisplayAlert(Loc.T("Download in corso"), Loc.F("Sto scaricando {0} in background...", song.Title), Loc.T("Capito"));

            // Spostiamo il lavoro pesante sul thread secondario
            await Task.Run(async () =>
            {
                // Scarica in una cartella temporanea, poi sposta il brano al suo posto
                // (sul telefono: cartella Musica/ARM)
                string tempPath = MusicStorage.TempPath(song.Id);

                await _youTubeService.DownloadAudioAsync(song.Id, tempPath);

                song.LocalFilePath = await MusicStorage.PublishAsync(tempPath, song);
                await _databaseService.SaveSongAsync(song);
            });

            await Application.Current.MainPage.DisplayAlert(Loc.T("Completato"), Loc.F("{0} aggiunto alla Libreria!", song.Title), Loc.T("OK"));
        }
        catch (Exception ex)
        {
            await Application.Current.MainPage.DisplayAlert(Loc.T("Errore"), Loc.T("Download fallito: ") + YouTubeService.SpiegaErrore(ex), Loc.T("OK"));
        }
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
            {
                // Richiama il metodo pulito che gestisce tutto il processo in background
                await ScaricaBranoAsync(selectedSong);
            }
            else if (action == Loc.T("Aggiungi a una playlist"))
            {
                // Mostra il messaggio con il punto e virgola corretto e blocca l'operazione
                await Application.Current.MainPage.DisplayAlert(
                    Loc.T("Download Richiesto"),
                    Loc.T("Per garantire una riproduzione continua e senza interruzioni, puoi aggiungere alle playlist solo i brani scaricati offline. Clicca su 'Scarica Offline' prima di aggiungerlo!"),
                    Loc.T("Capito"));

                return;
            }
        }
    }
}