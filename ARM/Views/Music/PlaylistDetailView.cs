using ARM.Data;
using ARM.Models;
using ARM.Services;
using Microsoft.Maui.Controls.Shapes;

namespace ARM.Views.Music;

// Dettaglio playlist: intestazione su una riga (← · copertina · nome e brani · ▶ · casuale · ⋮), poi i brani.
// Il brano in riproduzione ha il titolo nel colore d'accento.
public class PlaylistDetailView : ContentView
{
    private readonly DatabaseService _db;
    private readonly AudioService _audio;
    private readonly MusicDownloads _downloads;
    private readonly Action _onBack;
    private readonly SongRowTracker _rows;
    private Playlist _playlist;
    private List<Song> _songs = new();
    private int _missing;
    private bool _downloadingMissing;

    private readonly Image _cover;
    private readonly Label _name;
    private readonly Label _count;
    private readonly View _missingRow;
    private readonly Label _missingLabel;
    private readonly CollectionView _list;
    private CancellationTokenSource _reload;

    public string PlaylistId => _playlist?.Id;

    public PlaylistDetailView(Playlist playlist, DatabaseService db, AudioService audio, MusicDownloads downloads, Action onBack)
    {
        _playlist = playlist;
        _db = db;
        _audio = audio;
        _downloads = downloads;
        _onBack = onBack;
        _rows = new SongRowTracker(db);
        BackgroundColor = Ui.C("BgColor");

        // 1. Intestazione compatta
        var header = new Grid
        {
            ColumnDefinitions = new ColumnDefinitionCollection(
                new ColumnDefinition(44), new ColumnDefinition(MusicRows.CoverSize), new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(44), new ColumnDefinition(44), new ColumnDefinition(44)),
            ColumnSpacing = 8,
            Padding = new Thickness(4, 12, 4, 8)
        };
        header.Add(MusicRows.Action(Ui.Icons.Left, Back, Loc.T("Indietro")));
        var coverBox = MusicRows.Cover(out _cover);
        header.Add(coverBox, 1);
        var titles = new VerticalStackLayout { Spacing = 0, VerticalOptions = LayoutOptions.Center };
        _name = Ui.RowTitle("");
        _name.FontFamily = Ui.FontBold;
        _count = Ui.Meta("");
        titles.Add(_name);
        titles.Add(_count);
        header.Add(titles, 2);

        var play = new Border
        {
            WidthRequest = 40, HeightRequest = 40, StrokeThickness = 0,
            StrokeShape = new RoundRectangle { CornerRadius = 20 },
            BackgroundColor = Ui.C("AccentColor"),
            Content = Ui.Icon(Ui.Icons.Play, Ui.C("OnAccentColor"), 20),
            VerticalOptions = LayoutOptions.Center, HorizontalOptions = LayoutOptions.Center
        };
        Ui.Press(play, PlayAll, 0.9);
        SemanticProperties.SetDescription(play, Loc.T("Riproduci"));
        header.Add(play, 3);
        header.Add(MusicRows.Action(Ui.Icons.Shuffle, ShuffleAll, Loc.T("Casuale")), 4);
        header.Add(MusicRows.Action(Ui.Icons.More, ShowMenu, Loc.T("Altre azioni")), 5);

        // 2. Brani non ancora scaricati: riga sottile, solo se ce ne sono
        var missing = new HorizontalStackLayout { Spacing = 6, Padding = new Thickness(16, 0), MinimumHeightRequest = 36, BackgroundColor = Colors.Transparent };
        missing.Add(Ui.Icon(Ui.Icons.Download, Ui.C("AccentColor"), 16));
        _missingLabel = new Label { FontSize = 13, FontFamily = Ui.FontSemi, TextColor = Ui.C("AccentColor"), VerticalOptions = LayoutOptions.Center };
        missing.Add(_missingLabel);
        Ui.OnTap(missing, DownloadMissing);
        missing.IsVisible = false;
        _missingRow = missing;

        // 3. Brani
        _list = MusicRows.List(() => _rows.Track(new SongRow(_downloads, PlayFrom,
            new (string, string, Action<Song>)[] { (Ui.Icons.More, Loc.T("Altre azioni"), SongMenu) })));
        _list.EmptyView = new Label
        {
            Text = Loc.T("Ancora niente qui… aggiungi brani dalla Libreria o da Cerca."),
            FontSize = 13, FontFamily = Ui.FontRegular, TextColor = Ui.C("SubTextColor"),
            HorizontalTextAlignment = TextAlignment.Center, Margin = new Thickness(20, 24)
        };
        _list.Footer = new BoxView { HeightRequest = 12, Color = Colors.Transparent };

        var root = new Grid { RowDefinitions = new RowDefinitionCollection(new RowDefinition(GridLength.Auto), new RowDefinition(GridLength.Auto), new RowDefinition(GridLength.Star)) };
        root.Add(header);
        root.Add(_missingRow, 0, 1);
        root.Add(_list, 0, 2);
        Content = root;

        _db.DataChanged += OnDataChanged;
        _downloads.Changed += OnDownloadChanged;
        _audio.OnSongChanged += OnSongChanged;

        ShowHeader();
        _ = LoadAsync();
    }

    // Va chiamato quando la vista esce di scena: sgancia gli eventi dei servizi
    private bool _closed;

    public void Close()
    {
        _closed = true;
        _db.DataChanged -= OnDataChanged;
        _downloads.Changed -= OnDownloadChanged;
        _audio.OnSongChanged -= OnSongChanged;
    }

    private void Back()
    {
        if (_closed) return;
        Close();
        _onBack?.Invoke();
    }

    public void HandleBack() => Back();

    private void ShowHeader()
    {
        _name.Text = _playlist.Name;
        _cover.Source = _playlist.CoverSource;
    }

    private async Task LoadAsync()
    {
        var current = await _db.GetPlaylistAsync(_playlist.Id);
        if (current == null) { Back(); return; }   // eliminata (qui o da un altro dispositivo)
        _playlist = current;
        await CoverThumbnails.PrepareAsync(new[] { _playlist.ImagePath });
        ShowHeader();

        var songs = await _db.GetSongsForPlaylistAsync(_playlist.Id);
        // Il controllo dei file su disco si fa fuori dal thread grafico
        _missing = await Task.Run(() => songs.Count(s => !s.IsDownloaded || !File.Exists(s.LocalFilePath)));
        _songs = songs;
        _list.ItemsSource = songs;
        _count.Text = MusicRows.CountText(songs.Count);
        ShowMissing();
        _rows.RefreshAll(_audio.CurrentSong?.Id);
    }

    private void ShowMissing()
    {
        if (_downloadingMissing) return;
        _missingRow.IsVisible = _missing > 0;
        _missingLabel.Text = _missing == 1 ? Loc.T("Scarica 1 brano mancante") : Loc.F("Scarica i {0} brani mancanti", _missing);
    }

    // Modifiche ai dati (qui, in un'altra pagina o dalla sincronizzazione): ricarica, raggruppando quelle ravvicinate
    private void OnDataChanged() => MainThread.BeginInvokeOnMainThread(() =>
    {
        _reload?.Cancel();
        var cts = _reload = new CancellationTokenSource();
        Dispatcher.DispatchDelayed(TimeSpan.FromMilliseconds(300), () => { if (!cts.IsCancellationRequested && !_closed) _ = LoadAsync(); });
    });

    private async void OnDownloadChanged(string songId)
    {
        if (!_songs.Any(s => s.Id == songId)) return;
        await _rows.SongChangedAsync(songId, _audio.CurrentSong?.Id);
        if (!_downloadingMissing)
        {
            _missing = await Task.Run(() => _songs.Count(s => !s.IsDownloaded || !File.Exists(s.LocalFilePath)));
            ShowMissing();
        }
    }

    private void OnSongChanged(Song song) => MainThread.BeginInvokeOnMainThread(() => _rows.RefreshAll(song?.Id));

    private void PlayFrom(Song song)
    {
        int index = _songs.IndexOf(song);
        if (index >= 0) _audio.PlayQueue(_songs, index);
    }

    private void PlayAll()
    {
        if (_songs.Count > 0) _audio.PlayQueue(_songs, 0);
    }

    private void ShuffleAll()
    {
        if (_songs.Count > 0) _audio.PlayShuffled(_songs);
    }

    private async void ShowMenu()
    {
        var edit = Loc.T("Modifica playlist");
        var download = _missing > 0 ? (_missing == 1 ? Loc.T("Scarica 1 brano mancante") : Loc.F("Scarica i {0} brani mancanti", _missing)) : null;
        var options = download == null ? new[] { edit } : new[] { edit, download };
        var choice = await Application.Current.MainPage.DisplayActionSheet(_playlist.Name, Loc.T("Annulla"), null, options);
        if (choice == edit) await Application.Current.MainPage.Navigation.PushModalAsync(new CreatePlaylistPage(_db, _playlist));
        else if (choice != null && choice == download) DownloadMissing();
    }

    // Scarica i brani che non hanno ancora il file
    private async void DownloadMissing()
    {
        if (_downloadingMissing) return;
        _downloadingMissing = true;
        var spotify = IPlatformApplication.Current.Services.GetService<SpotifyService>();
        var (ok, failed) = await spotify.ScaricaMancantiAsync(_playlist.Id, new Progress<string>(t => _missingLabel.Text = t));
        _downloadingMissing = false;
        await LoadAsync();
        if (failed > 0)
            await Application.Current.MainPage.DisplayAlert(Loc.T("Download"), Loc.F("Scaricati {0} brani, {1} non riusciti.\n\n{2}", ok, failed, spotify.LastDownloadError), Loc.T("OK"));
    }

    private async void SongMenu(Song song)
    {
        string azione = await Application.Current.MainPage.DisplayActionSheet(
            song.Title, Loc.T("Annulla"), null,
            Loc.T("Rimuovi dalla playlist"),
            Loc.T("Modifica brano"));

        if (azione == Loc.T("Rimuovi dalla playlist"))
        {
            await _db.RemoveSongFromPlaylistAsync(song.Id, _playlist.Id);   // la lista si ricarica con DataChanged
        }
        else if (azione == Loc.T("Modifica brano"))
        {
            string nuovoTitolo = await Application.Current.MainPage.DisplayPromptAsync(
                Loc.T("Modifica Titolo"), Loc.T("Inserisci il nuovo titolo:"), initialValue: song.Title);
            if (string.IsNullOrWhiteSpace(nuovoTitolo)) return;

            string nuovoArtista = await Application.Current.MainPage.DisplayPromptAsync(
                Loc.T("Modifica Artista"), Loc.T("Inserisci il nuovo artista:"), initialValue: song.Artist);
            if (string.IsNullOrWhiteSpace(nuovoArtista)) return;

            song.Title = nuovoTitolo;
            song.Artist = nuovoArtista;
            await _db.UpdateSongAsync(song);
        }
    }
}
