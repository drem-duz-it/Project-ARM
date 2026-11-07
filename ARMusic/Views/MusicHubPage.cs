using Microsoft.Maui.Controls.Shapes;

namespace ARMusic.Views;

// Contenitore della sezione Musica: selettore Scopri / Cerca / Libreria sopra le pagine musicali esistenti
public class MusicHubPage : ContentView
{
    private readonly HomePage _homePage;
    private readonly SearchPage _searchPage;
    private readonly LibraryPage _libraryPage;
    private readonly ContentView _host = new();
    private readonly Grid _segment = new() { ColumnSpacing = 4 };
    private string _current = "Scopri";

    public MusicHubPage(HomePage home, SearchPage search, LibraryPage library)
    {
        _homePage = home;
        _searchPage = search;
        _libraryPage = library;

        // Le playlist si aprono dentro la sezione musica, sotto il selettore
        _homePage.OnChangePageRequested = view => _host.Content = view;
        _libraryPage.OnChangePageRequested = view => _host.Content = view;
        _homePage.OnSearchRequested = query => { Show("Cerca"); _searchPage.Cerca(query); };

        var root = new Grid { RowDefinitions = new RowDefinitionCollection(new RowDefinition(GridLength.Auto), new RowDefinition(GridLength.Star)) };
        root.Add(new ContentView { Content = _segment, Padding = new Thickness(20, 28, 20, 14) });
        root.Add(_host, 0, 1);
        Content = root;

        Show("Scopri");
    }

    // Se è aperta una playlist, torna indietro e restituisce true
    public bool HandleBack()
    {
        if (_host.Content is PlaylistDetailPage detail)
        {
            detail.OnBackRequested?.Invoke();
            return true;
        }
        return false;
    }

    public void Refresh() => Show(_current);

    private void Show(string section)
    {
        _current = section;
        BackgroundColor = Ui.C("BgColor");
        switch (section)
        {
            case "Cerca": _host.Content = _searchPage; break;
            case "Libreria": _host.Content = _libraryPage; _libraryPage.RicaricaLibreria(); break;
            default: _host.Content = _homePage; _homePage.RicaricaDati(); break;
        }
        BuildSegment();
    }

    // Intestazione: "ARMusic" (Display 34, "usic" in Accent) con l'icona tonda a gradiente, poi Scopri / Cerca / Libreria
    private void BuildSegment()
    {
        _segment.Children.Clear();
        _segment.RowDefinitions.Clear();
        _segment.RowSpacing = 18;
        _segment.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        _segment.RowDefinitions.Add(new RowDefinition(GridLength.Auto));

        var title = Ui.Display("");
        title.FormattedText = new FormattedString { Spans =
        {
            new Span { Text = "ARM", TextColor = Ui.C("TextColor") },
            new Span { Text = "usic", TextColor = Ui.C("AccentColor") }
        } };
        title.VerticalOptions = LayoutOptions.Center;
        var icon = new Border
        {
            WidthRequest = 44, HeightRequest = 44, StrokeThickness = 0,
            StrokeShape = new RoundRectangle { CornerRadius = 22 },
            Background = Ui.HeroBrush(),
            Content = Ui.Icon(Ui.Icons.Music, Ui.C("OnHeroColor"), 22),
            HorizontalOptions = LayoutOptions.End, VerticalOptions = LayoutOptions.Center
        };
        var head = new Grid { Children = { title, icon } };
        _segment.Add(head, 0, 0);

        var seg = new Ui.Segmented(new[] { ("Scopri", "Scopri"), ("Cerca", "Cerca"), ("Libreria", "Libreria") }, _current, Show);
        _segment.Add(seg, 0, 1);
    }
}
