using ARM.Services;
using Microsoft.Maui.Controls.Shapes;
using ARM.Data;
using ARM.Models;

namespace ARM.Views;

public partial class HomePage : ContentView
{
    private readonly DatabaseService _databaseService;
    private readonly AudioService _audioService;
    private readonly YouTubeService _youTubeService;

    public Action<ContentView> OnChangePageRequested;

    public HomePage(DatabaseService dbService, AudioService audioService, YouTubeService ytService)
    {
        InitializeComponent();
        Loc.Register(this);

        _databaseService = dbService;
        _audioService = audioService;
        _youTubeService = ytService;

        ImpostaSaluto();
        AggiornaTestoTema();
    }

    private void ImpostaSaluto()
    {
        // ARMusic: la sezione musica di ARM, con un nome tutto suo
        GreetingLabel.Text = Loc.T("ARMusic");
    }

    // Toccando una ricerca recente si apre la scheda Cerca con quella ricerca
    public Action<string> OnSearchRequested;

    public async void RicaricaDati()
    {
        // 1. Carica le Playlist
        var playlists = await _databaseService.GetAllPlaylistsAsync();
        playlists.Reverse(); // Le inverte per mostrare prima le ultime create/modificate
        HomePlaylistsCollection.ItemsSource = playlists;
        HomePlaylistsCollection.IsVisible = playlists.Count > 0;
        NoPlaylistsLabel.IsVisible = playlists.Count == 0;

        // 2. Carica le Ricerche Recenti (salvate dalla pagina Cerca)
        CaricaRicercheRecenti();

        // 3. Carica i Brani Scaricati di Recente
        await CaricaBraniRecenti();
    }

    private void CaricaRicercheRecenti()
    {
        RecentSearchesContainer.Children.Clear();

        var searches = SearchPage.RicercheRecenti();
        if (searches.Count == 0)
        {
            RecentSearchesContainer.Children.Add(Ui.Small(Loc.T("Le tue ricerche appariranno qui.")));
            return;
        }

        foreach (var s in searches)
        {
            var testo = s;
            RecentSearchesContainer.Children.Add(Ui.Chip(testo, false, "TextColor", () => OnSearchRequested?.Invoke(testo), Ui.Icons.Search));
        }
    }

    private async Task CaricaBraniRecenti()
    {
        RecentSongsContainer.Children.Clear();
        var allSongs = await _databaseService.GetAllSongsAsync();

        // Filtra solo quelli offline, inverte la lista e prende solo gli ultimi 5
        var downloadedSongs = allSongs.Where(s => !string.IsNullOrEmpty(s.LocalFilePath))
                                      .Reverse()
                                      .Take(5)
                                      .ToList();

        if (downloadedSongs.Count == 0)
        {
            RecentSongsContainer.Children.Add(Ui.EmptyCard(Loc.T("Nessun brano scaricato. Cercane uno e scaricalo per ascoltarlo offline."), Ui.Icons.Music));
            return;
        }

        for (int i = 0; i < downloadedSongs.Count; i++)
        {
            var song = downloadedSongs[i];

            // Toccando un brano, puliamo la coda e lo riproduciamo!
            RecentSongsContainer.Children.Add(SongRow(song, () =>
            {
                _audioService.ClearQueue();
                _audioService.PlaySong(song, song.LocalFilePath);
            }));
        }
    }

    // Riga brano (64): copertina 48 raggio 10, titolo 15 Bold, artista, "Non scaricato", tasto play
    public static View SongRow(Song song, Action onPlay)
    {
        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitionCollection { new ColumnDefinition(48), new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto), new ColumnDefinition(44) },
            ColumnSpacing = 12,
            MinimumHeightRequest = 64,
            Padding = new Thickness(0, 8),
            BackgroundColor = Colors.Transparent
        };
        grid.Add(Cover(song.ThumbnailUrl, 48, 10));

        var stack = new VerticalStackLayout { VerticalOptions = LayoutOptions.Center, Spacing = 1 };
        var t = Ui.Body(song.Title);
        t.FontFamily = Ui.FontBold;
        t.MaxLines = 1;
        t.LineBreakMode = LineBreakMode.TailTruncation;
        stack.Add(t);
        var a = Ui.Small(song.Artist);
        a.FontFamily = Ui.FontRegular;
        a.MaxLines = 1;
        a.LineBreakMode = LineBreakMode.TailTruncation;
        stack.Add(a);
        grid.Add(stack, 1);

        if (string.IsNullOrEmpty(song.LocalFilePath)) grid.Add(Ui.Badge(Loc.T("Non scaricato"), "TextColor"), 2);
        grid.Add(Ui.IconButton(Ui.Icons.Play, onPlay, flat: true), 3);

        Ui.Press(grid, onPlay, 0.98);
        return grid;
    }

    // Copertina con gradiente e due cerchi (si vede se manca l'immagine del brano)
    public static Border Cover(string image, double size, double radius)
    {
        var g = new Grid();
        g.Add(new Ellipse { Fill = Colors.White, Opacity = 0.28, WidthRequest = size * 0.62, HeightRequest = size * 0.62, HorizontalOptions = LayoutOptions.End, VerticalOptions = LayoutOptions.Start, Margin = new Thickness(0, -size * 0.17, -size * 0.17, 0) });
        g.Add(new Image { Source = image, Aspect = Aspect.AspectFill });
        return new Border
        {
            WidthRequest = size, HeightRequest = size,
            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = radius },
            StrokeThickness = 0,
            Background = Ui.HeroBrush(),
            Content = g,
            VerticalOptions = LayoutOptions.Center
        };
    }

    // --- GESTIONE TEMA ---
    private void OnThemeToggleClicked(object sender, EventArgs e)
    {
        // Passa al tema successivo (Chiaro ↔ Classico) e ridisegna tutto
        ThemeService.ProssimoTema();
        AggiornaTestoTema();
    }

    private void AggiornaTestoTema()
    {
        // Mostra il nome reale del tema applicato!
        ThemeLabel.Text = Loc.F("Tema {0}", ThemeService.TemaAttuale);
    }

    private void OnPlaylistSelected(object sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is Playlist selectedPlaylist)
        {
            HomePlaylistsCollection.SelectedItem = null;
            var detailPage = new PlaylistDetailPage(selectedPlaylist, _databaseService, _audioService, _youTubeService);
            detailPage.OnBackRequested = () =>
            {
                OnChangePageRequested?.Invoke(this);
                RicaricaDati();
            };
            OnChangePageRequested?.Invoke(detailPage);
        }
    }
}