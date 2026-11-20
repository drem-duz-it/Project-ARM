using ARM.Data;
using ARM.Models;
using ARM.Services;
using ARM.Views.Music;

namespace ARM.Views;

// Sezione Musica: ricerca sempre in alto, sotto due viste (Playlist / Brani).
// - toccando il campo compaiono le ricerche recenti; i risultati prendono il posto delle viste
// - una playlist si apre al posto di tutto, con la sua intestazione compatta
public class MusicHubPage : ContentView
{
    private const string TabPlaylist = "Playlist";
    private const string TabBrani = "Brani";

    private readonly DatabaseService _db;
    private readonly AudioService _audio;
    private readonly YouTubeService _youtube;
    private MusicDownloads _downloads;   // preso alla prima apertura: all'avvio i servizi dell'app non sono ancora pronti

    private string _tab = TabPlaylist;
    private bool _searching;
    private PlaylistDetailView _detail;

    // Dati
    private List<Playlist> _playlists = new();
    private Dictionary<string, int> _counts = new();
    private List<Song> _songs = new();
    private bool _loaded;
    private bool _dirty = true;
    private bool _rebuild = true;
    private CancellationTokenSource _reload;

    // Viste (ricreate al cambio di tema o lingua)
    private Grid _root;
    private View _top;
    private Entry _search;
    private View _clearSearch;
    private Ui.Segmented _segment;
    private ContentView _host;
    private CollectionView _playlistsList;
    private CollectionView _songsList;
    private Label _songsCount;
    private Grid _searchView;
    private CollectionView _results;
    private ContentView _recentView;
    private ActivityIndicator _spinner;
    private SongRowTracker _rows;

    public MusicHubPage(DatabaseService db, AudioService audio, YouTubeService youtube)
    {
        _db = db;
        _audio = audio;
        _youtube = youtube;

        // Dati cambiati (qui, da un'altra pagina, dalla sincronizzazione): ricarica se la sezione è aperta, altrimenti alla prossima apertura
        _db.DataChanged += () => MainThread.BeginInvokeOnMainThread(() => { _dirty = true; if (Parent != null) ScheduleReload(); });
        ThemeService.ThemeChanged += () => _rebuild = true;
        Loc.LanguageChanged += () => _rebuild = true;
    }

    // Chiamato quando si apre la sezione (e dopo un cambio di tema o lingua)
    public void Refresh()
    {
        if (_rebuild) Build();
        if (_dirty || !_loaded) _ = LoadAsync();
    }

    // Tasto indietro: chiude la playlist aperta o la ricerca. true = gestito
    public bool HandleBack()
    {
        if (_detail != null) { _detail.HandleBack(); return true; }
        if (_searching) { CloseSearch(); return true; }
        return false;
    }

    // ---------------- Costruzione ----------------

    private void Build()
    {
        _rebuild = false;
        if (_downloads == null)
        {
            _downloads = IPlatformApplication.Current.Services.GetService<MusicDownloads>();
            _downloads.Changed += OnDownloadChanged;
        }
        _detail?.Close();
        _detail = null;
        _searching = false;
        BackgroundColor = Ui.C("BgColor");
        _rows = new SongRowTracker(_db);

        // Campo di ricerca: lente · testo · ✕
        _search = Ui.Entry(Loc.T("Cerca brano o artista"));
        _search.ReturnType = ReturnType.Search;
        _search.Completed += (s, e) => _ = SearchAsync(_search.Text);
        _search.Focused += (s, e) => OpenSearch();
        _search.TextChanged += (s, e) => _clearSearch.IsVisible = _searching || !string.IsNullOrEmpty(e.NewTextValue);
        var clear = MusicRows.Action(Ui.Icons.Close, CloseSearch, Loc.T("Chiudi la ricerca"));
        clear.IsVisible = false;
        _clearSearch = clear;
        var fieldRow = new Grid
        {
            ColumnDefinitions = new ColumnDefinitionCollection(new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto)),
            ColumnSpacing = 8
        };
        fieldRow.Add(Ui.Icon(Ui.Icons.Search, Ui.C("SubTextColor"), 20));
        fieldRow.Add(_search, 1);
        fieldRow.Add(clear, 2);
        var field = Ui.Field(fieldRow);
        field.Padding = new Thickness(14, 0, 2, 0);

        _segment = new Ui.Segmented(new[] { (TabPlaylist, "Playlist"), (TabBrani, "Brani") }, _tab, ShowTab);

        var top = new VerticalStackLayout { Spacing = 10, Padding = new Thickness(16, 16, 16, 8) };
        top.Add(field);
        top.Add(_segment);
        _top = top;

        BuildPlaylists();
        BuildSongs();
        BuildSearch();

        _host = new ContentView();   // nuovo a ogni ricostruzione: spostare viste già a schermo fa crashare Android/WinUI
        _root = new Grid { RowDefinitions = new RowDefinitionCollection(new RowDefinition(GridLength.Auto), new RowDefinition(GridLength.Star)) };
        _root.Add(_top);
        _root.Add(_host, 0, 1);
        Content = _root;

        ShowData();
        ShowTab(_tab);
    }

    private void BuildPlaylists()
    {
        _playlistsList = MusicRows.List(() => new PlaylistRow(id => _counts.TryGetValue(id, out var n) ? n : 0, OpenPlaylist, PlayPlaylist));
        _playlistsList.EmptyView = Ui.Empty(Loc.T("Ancora nessuna playlist: tocca «Nuova playlist» o importane una da Spotify."), Ui.Icons.Music);

        var buttons = new Grid
        {
            ColumnDefinitions = new ColumnDefinitionCollection(new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Star)),
            ColumnSpacing = 10,
            Padding = new Thickness(16, 12, 16, 16)
        };
        buttons.Add(Ui.Button(Loc.T("Nuova playlist"), NewPlaylist, Ui.Icons.Plus, small: true));
        buttons.Add(Ui.Button(Loc.T("Importa"), ImportSpotify, Ui.Icons.Download, ghost: true, small: true), 1);
        _playlistsList.Footer = buttons;
    }

    private void BuildSongs()
    {
        _songsList = MusicRows.List(() => _rows.Track(new SongRow(_downloads, PlaySong,
            new (string, string, Action<Song>)[] { (Ui.Icons.PlaylistAdd, Loc.T("Aggiungi a una playlist"), song => _ = AddToPlaylistAsync(song)) })));
        _songsList.EmptyView = Ui.Empty(Loc.T("Ancora niente qui… I brani scaricati si ascoltano anche senza rete."), Ui.Icons.Music);
        _songsCount = new Label { FontSize = 13, FontFamily = Ui.FontSemi, TextColor = Ui.C("SubTextColor"), Padding = new Thickness(16, 4, 16, 6) };
        _songsList.Header = _songsCount;
        _songsList.Footer = new BoxView { HeightRequest = 12, Color = Colors.Transparent };
    }

    private void BuildSearch()
    {
        _results = MusicRows.List(() => _rows.Track(new SongRow(_downloads, song => _ = ResultMenuAsync(song),
            new (string, string, Action<Song>)[]
            {
                (Ui.Icons.Download, Loc.T("Scarica offline"), song => _ = DownloadAsync(song)),
                (Ui.Icons.PlaylistAdd, Loc.T("Aggiungi a una playlist"), song => _ = AddToPlaylistAsync(song))
            },
            showLibraryState: true)));
        _results.IsVisible = false;

        _spinner = new ActivityIndicator { Color = Ui.C("AccentColor"), IsRunning = false, IsVisible = false, HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Start, Margin = new Thickness(0, 40) };
        _recentView = new ContentView();

        _searchView = new Grid();
        _searchView.Add(_recentView);
        _searchView.Add(_results);
        _searchView.Add(_spinner);
    }

    // ---------------- Navigazione ----------------

    private void ShowTab(string tab)
    {
        _tab = tab;
        _segment?.Select(tab);
        if (_searching || _detail != null || _host == null) return;
        _host.Content = tab == TabBrani ? _songsList : _playlistsList;
    }

    private void OpenSearch()
    {
        if (_searching) return;
        _searching = true;
        _segment.IsVisible = false;
        _clearSearch.IsVisible = true;
        _results.IsVisible = _results.ItemsSource != null;
        _recentView.IsVisible = !_results.IsVisible;
        ShowRecentSearches();
        _host.Content = _searchView;
    }

    private void CloseSearch()
    {
        _searching = false;
        _search.Text = "";
        _search.Unfocus();
        _results.ItemsSource = null;
        _results.IsVisible = false;
        _clearSearch.IsVisible = false;
        _segment.IsVisible = true;
        ShowTab(_tab);
    }

    private void OpenPlaylist(Playlist playlist)
    {
        _detail?.Close();
        _detail = new PlaylistDetailView(playlist, _db, _audio, _downloads, () =>
        {
            _detail = null;
            _top.IsVisible = true;
            ShowTab(_tab);
            if (_dirty) _ = LoadAsync();
        });
        _top.IsVisible = false;
        _host.Content = _detail;
    }

    // ---------------- Dati ----------------

    private void ScheduleReload()
    {
        _reload?.Cancel();
        var cts = _reload = new CancellationTokenSource();
        Dispatcher.DispatchDelayed(TimeSpan.FromMilliseconds(300), () => { if (!cts.IsCancellationRequested) _ = LoadAsync(); });
    }

    private async Task LoadAsync()
    {
        _dirty = false;
        _loaded = true;
        var playlists = await _db.GetAllPlaylistsAsync();
        var counts = await _db.GetPlaylistCountsAsync();
        var songs = await _db.GetDownloadedSongsAsync();
        await CoverThumbnails.PrepareAsync(playlists.Select(p => p.ImagePath));

        _playlists = playlists.OrderBy(p => p.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
        _counts = counts;
        _songs = songs;
        ShowData();
    }

    private void ShowData()
    {
        if (_playlistsList == null) return;
        _playlistsList.ItemsSource = null;   // le righe rileggono anche i conteggi
        _playlistsList.ItemsSource = _playlists;
        _songsList.ItemsSource = _songs;
        _songsCount.Text = MusicRows.CountText(_songs.Count);
        _songsCount.IsVisible = _songs.Count > 0;
    }

    private async void OnDownloadChanged(string songId)
    {
        if (_rows == null) return;
        await _rows.SongChangedAsync(songId);
        // Un brano appena scaricato entra in "Brani"
        if (!_downloads.IsDownloading(songId)) { _dirty = true; if (Parent != null && _detail == null) ScheduleReload(); }
    }

    // ---------------- Azioni ----------------

    private async void PlayPlaylist(Playlist playlist)
    {
        var songs = await _db.GetSongsForPlaylistAsync(playlist.Id);
        if (songs.Count > 0) _audio.PlayQueue(songs, 0);
        else OpenPlaylist(playlist);
    }

    private void PlaySong(Song song)
    {
        _audio.ClearQueue();
        _audio.PlaySong(song, song.LocalFilePath);
    }

    private async void NewPlaylist() =>
        await Application.Current.MainPage.Navigation.PushModalAsync(new CreatePlaylistPage(_db));

    private async void ImportSpotify()
    {
        var spotify = IPlatformApplication.Current.Services.GetService<SpotifyService>();
        await Application.Current.MainPage.Navigation.PushModalAsync(new SpotifyImportPage(spotify, () => _ = LoadAsync()));
    }

    private async Task AddToPlaylistAsync(Song song)
    {
        var playlists = await _db.GetAllPlaylistsAsync();
        if (playlists.Count == 0)
        {
            await Application.Current.MainPage.DisplayAlert(Loc.T("Ops"), Loc.T("Non hai ancora creato nessuna playlist!"), Loc.T("OK"));
            return;
        }
        playlists = playlists.OrderBy(p => p.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
        string scelta = await Application.Current.MainPage.DisplayActionSheet(Loc.T("Scegli la Playlist"), Loc.T("Annulla"), null, playlists.Select(p => p.Name).ToArray());
        var playlist = playlists.FirstOrDefault(p => p.Name == scelta);
        if (playlist == null) return;

        // Entra subito nella playlist; se manca il file si scarica in background (la riga mostra "In download…")
        bool aggiunto = await _downloads.AddToPlaylistAsync(song, playlist.Id);
        if (!aggiunto)
            await Application.Current.MainPage.DisplayAlert(Loc.T("Fatto"), Loc.F("Il brano è già in {0}.", playlist.Name), Loc.T("OK"));
        _rows.RefreshAll();
    }

    private async Task DownloadAsync(Song risultato)
    {
        var song = await _db.AddOrGetSongAsync(risultato);
        if (song.IsDownloaded && File.Exists(song.LocalFilePath)) return;
        _rows.RefreshAll();
        if (!await _downloads.DownloadAsync(song))
            await Application.Current.MainPage.DisplayAlert(Loc.T("Errore"), Loc.T("Download fallito: ") + _downloads.LastError, Loc.T("OK"));
    }

    // Tocco su un risultato: riproduci (se è già scaricato), scarica o aggiungi a una playlist
    private async Task ResultMenuAsync(Song song)
    {
        bool scaricato = song.IsDownloaded && File.Exists(song.LocalFilePath);
        var options = new List<string>();
        if (scaricato) options.Add(Loc.T("Riproduci"));
        else options.Add(Loc.T("Scarica offline"));
        options.Add(Loc.T("Aggiungi a una playlist"));

        string action = await Application.Current.MainPage.DisplayActionSheet(Loc.F("Opzioni: {0}", song.Title), Loc.T("Annulla"), null, options.ToArray());
        if (action == Loc.T("Riproduci")) PlaySong(song);
        else if (action == Loc.T("Scarica offline")) await DownloadAsync(song);
        else if (action == Loc.T("Aggiungi a una playlist")) await AddToPlaylistAsync(song);
    }

    // ---------------- Ricerca ----------------

    // Ricerche recenti (le più nuove per prime)
    public static List<string> RicercheRecenti() =>
        Preferences.Default.Get("RicercheRecenti", "").Split('\n', StringSplitOptions.RemoveEmptyEntries).ToList();

    private static void SalvaRicerca(string query)
    {
        var lista = RicercheRecenti();
        lista.RemoveAll(q => q.Equals(query, StringComparison.OrdinalIgnoreCase));
        lista.Insert(0, query);
        Preferences.Default.Set("RicercheRecenti", string.Join("\n", lista.Take(8)));
    }

    private void ShowRecentSearches()
    {
        var v = new VerticalStackLayout { Spacing = 10, Padding = new Thickness(16, 12) };
        var recenti = RicercheRecenti();
        v.Add(Ui.Caps(Loc.T("Ricerche recenti")));
        if (recenti.Count == 0)
        {
            v.Add(Ui.Small(Loc.T("Le tue ricerche appariranno qui.")));
        }
        else
        {
            var flex = new FlexLayout { Wrap = Microsoft.Maui.Layouts.FlexWrap.Wrap, AlignItems = Microsoft.Maui.Layouts.FlexAlignItems.Start };
            foreach (var q in recenti)
            {
                var testo = q;
                var chip = Ui.Chip(testo, false, "TextColor", () => { _search.Text = testo; _ = SearchAsync(testo); }, Ui.Icons.Clock);
                chip.Margin = new Thickness(0, 0, 8, 8);
                flex.Add(chip);
            }
            v.Add(flex);
        }
        _recentView.Content = v;
    }

    private async Task SearchAsync(string text)
    {
        string query = text?.Trim();
        if (string.IsNullOrWhiteSpace(query)) return;
        OpenSearch();
        SalvaRicerca(query);
        _recentView.IsVisible = false;
        _results.IsVisible = false;
        _spinner.IsVisible = _spinner.IsRunning = true;

        try
        {
            var results = await _youtube.SearchSongsAsync(query);
            // I brani già in libreria si mostrano con i dati salvati (file scaricato, titolo modificato)
            var library = (await _db.GetAllSongsAsync()).ToDictionary(s => s.Id);
            _results.ItemsSource = results.Select(r => library.TryGetValue(r.Id, out var saved) ? saved : r).ToList();
        }
        catch (Exception ex)
        {
            await Application.Current.MainPage.DisplayAlert(Loc.T("Errore"), Loc.T("Impossibile trovare brani: ") + YouTubeService.SpiegaErrore(ex), Loc.T("OK"));
        }
        finally
        {
            _spinner.IsVisible = _spinner.IsRunning = false;
            _results.IsVisible = _results.ItemsSource != null;
            _recentView.IsVisible = !_results.IsVisible;
        }
    }
}
