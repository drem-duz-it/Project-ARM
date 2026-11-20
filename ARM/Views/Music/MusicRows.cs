using ARM.Models;
using ARM.Services;
using Microsoft.Maui.Controls.Shapes;

namespace ARM.Views.Music;

// Righe degli elenchi musicali: alte 52, copertina 40, una sola griglia.
// Si riempiono a mano quando cambia l'elemento (niente binding a riflessione): sono le viste che si riciclano
// durante lo scorrimento, quindi più sono leggere più lo scorrimento è fluido.
public static class MusicRows
{
    public const double Height = 52;
    public const double CoverSize = 40;

    public static Border Cover(out Image image)
    {
        image = new Image { Aspect = Aspect.AspectFill };
        return new Border
        {
            WidthRequest = CoverSize, HeightRequest = CoverSize,
            StrokeThickness = 0,
            StrokeShape = new RoundRectangle { CornerRadius = 8 },
            BackgroundColor = Ui.C("FieldBgColor"),
            Content = image,
            VerticalOptions = LayoutOptions.Center
        };
    }

    // Area toccabile 44×44 con un'icona (azione di riga)
    public static Grid Action(string icon, Action onTap, string description)
    {
        var g = new Grid { WidthRequest = 44, HeightRequest = 44, BackgroundColor = Colors.Transparent, VerticalOptions = LayoutOptions.Center };
        g.Add(Ui.Icon(icon, Ui.C("SubTextColor"), 22));
        Ui.OnTap(g, onTap);
        SemanticProperties.SetDescription(g, description);
        return g;
    }

    public static CollectionView List(Func<View> row) => new()
    {
        ItemsLayout = new LinearItemsLayout(ItemsLayoutOrientation.Vertical),
        ItemSizingStrategy = ItemSizingStrategy.MeasureFirstItem,   // righe tutte uguali: niente misura per ogni elemento
        SelectionMode = SelectionMode.None,
        ItemTemplate = new DataTemplate(row)
    };

    public static string CountText(int n) => n == 1 ? Loc.T("1 brano") : Loc.F("{0} brani", n);
}

// Riga brano: copertina · titolo e artista · stato (in download / non scaricato) · azioni
public class SongRow : Grid
{
    private readonly Image _cover;
    private readonly Label _title;
    private readonly Label _artist;
    private readonly Label _status;
    private readonly MusicDownloads _downloads;
    private readonly bool _showLibraryState;

    public Song Song { get; private set; }

    // onTap: tocco sulla riga. actions: icone a destra (es. ＋, ⋮). showLibraryState: mostra "In libreria" (ricerca)
    public SongRow(MusicDownloads downloads, Action<Song> onTap, IEnumerable<(string Icon, string Description, Action<Song> OnTap)> actions, bool showLibraryState = false)
    {
        _downloads = downloads;
        _showLibraryState = showLibraryState;
        HeightRequest = MusicRows.Height;
        Padding = new Thickness(16, 0, 4, 0);
        ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));

        var main = new Grid
        {
            ColumnDefinitions = new ColumnDefinitionCollection(new ColumnDefinition(MusicRows.CoverSize), new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto)),
            ColumnSpacing = 12,
            BackgroundColor = Colors.Transparent
        };
        main.Add(MusicRows.Cover(out _cover));

        var text = new VerticalStackLayout { Spacing = 0, VerticalOptions = LayoutOptions.Center };
        _title = Ui.RowTitle("");
        _artist = Ui.Meta("");
        text.Add(_title);
        text.Add(_artist);
        main.Add(text, 1);

        _status = new Label { FontSize = 12, FontFamily = Ui.FontSemi, TextColor = Ui.C("SubTextColor"), VerticalOptions = LayoutOptions.Center, IsVisible = false };
        main.Add(_status, 2);

        Ui.OnTap(main, () => { if (Song != null) onTap?.Invoke(Song); });
        Add(main);

        int col = 1;
        foreach (var (icon, description, act) in actions ?? Enumerable.Empty<(string, string, Action<Song>)>())
        {
            ColumnDefinitions.Add(new ColumnDefinition(44));
            var a = act;
            Add(MusicRows.Action(icon, () => { if (Song != null) a(Song); }, description), col++);
        }

        BindingContextChanged += (s, e) => Bind(BindingContext as Song);
    }

    private void Bind(Song song)
    {
        Song = song;
        if (song == null) return;
        _title.Text = song.Title;
        _artist.Text = song.Artist;
        _cover.Source = string.IsNullOrEmpty(song.CoverSmall) ? null : song.CoverSmall;
        Refresh();
    }

    // Ridisegna stato e colore (dopo un download o un cambio di brano)
    public void Refresh(string playingSongId = null)
    {
        if (Song == null) return;
        string status = _downloads != null && _downloads.IsDownloading(Song.Id) ? Loc.T("In download…")
            : !Song.IsDownloaded ? (_showLibraryState ? "" : Loc.T("Non scaricato"))
            : _showLibraryState ? Loc.T("In libreria") : "";
        _status.Text = status;
        _status.IsVisible = status.Length > 0;
        _title.TextColor = Ui.C(playingSongId != null && playingSongId == Song.Id ? "AccentColor" : "TextColor");
    }
}

// Riga playlist: copertina · nome e numero di brani · ▶ (suona subito)
public class PlaylistRow : Grid
{
    private readonly Image _cover;
    private readonly Label _name;
    private readonly Label _count;
    private readonly Func<string, int> _countOf;

    public Playlist Playlist { get; private set; }

    public PlaylistRow(Func<string, int> countOf, Action<Playlist> onOpen, Action<Playlist> onPlay)
    {
        _countOf = countOf;
        HeightRequest = MusicRows.Height;
        Padding = new Thickness(16, 0, 4, 0);
        ColumnDefinitions = new ColumnDefinitionCollection(new ColumnDefinition(GridLength.Star), new ColumnDefinition(44));

        var main = new Grid
        {
            ColumnDefinitions = new ColumnDefinitionCollection(new ColumnDefinition(MusicRows.CoverSize), new ColumnDefinition(GridLength.Star)),
            ColumnSpacing = 12,
            BackgroundColor = Colors.Transparent
        };
        main.Add(MusicRows.Cover(out _cover));
        var text = new VerticalStackLayout { Spacing = 0, VerticalOptions = LayoutOptions.Center };
        _name = Ui.RowTitle("");
        _count = Ui.Meta("");
        text.Add(_name);
        text.Add(_count);
        main.Add(text, 1);
        Ui.OnTap(main, () => { if (Playlist != null) onOpen(Playlist); });
        Add(main);

        var play = new Grid { WidthRequest = 44, HeightRequest = 44, BackgroundColor = Colors.Transparent, VerticalOptions = LayoutOptions.Center };
        play.Add(Ui.Icon(Ui.Icons.Play, Ui.C("AccentColor"), 22));
        Ui.Press(play, () => { if (Playlist != null) onPlay(Playlist); }, 0.9);
        SemanticProperties.SetDescription(play, Loc.T("Riproduci"));
        Add(play, 1);

        BindingContextChanged += (s, e) => Bind(BindingContext as Playlist);
    }

    private void Bind(Playlist playlist)
    {
        Playlist = playlist;
        if (playlist == null) return;
        _name.Text = playlist.Name;
        _count.Text = MusicRows.CountText(_countOf(playlist.Id));
        _cover.Source = playlist.CoverSource;
    }
}

// Tiene traccia delle righe brano a schermo per aggiornarle senza ricaricare l'elenco
// (fine di un download, brano in riproduzione cambiato)
public class SongRowTracker
{
    private readonly List<WeakReference<SongRow>> _rows = new();
    private readonly Data.DatabaseService _db;

    public SongRowTracker(Data.DatabaseService db) => _db = db;

    public SongRow Track(SongRow row)
    {
        _rows.Add(new WeakReference<SongRow>(row));
        return row;
    }

    private IEnumerable<SongRow> Rows()
    {
        _rows.RemoveAll(w => !w.TryGetTarget(out _));
        foreach (var w in _rows) if (w.TryGetTarget(out var r)) yield return r;
    }

    public void RefreshAll(string playingSongId = null)
    {
        foreach (var r in Rows().ToList()) r.Refresh(playingSongId);
    }

    // Un download è iniziato o finito: aggiorna il percorso del file nelle righe di quel brano
    public async Task SongChangedAsync(string songId, string playingSongId = null)
    {
        var saved = await _db.GetSongAsync(songId);
        foreach (var r in Rows().Where(r => r.Song?.Id == songId).ToList())
        {
            if (saved != null) r.Song.LocalFilePath = saved.LocalFilePath;
            r.Refresh(playingSongId);
        }
    }
}
