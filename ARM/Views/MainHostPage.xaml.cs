using ARM.Models;
using ARM.Services;
using ARM.Data;
using CommunityToolkit.Maui.Core;
using Microsoft.Maui.Controls.Shapes;
using System.Net.Http;

namespace ARM.Views;

public partial class MainHostPage : ContentPage
{
    // Le 5 sezioni dello spazio di lavoro
    private static readonly (string Nome, string Icona)[] Tabs =
    {
        ("Oggi", Ui.Icons.Today),
        ("Note", Ui.Icons.Note),
        ("Task", Ui.Icons.Task),
        ("Calendario", Ui.Icons.Calendar),
        ("Musica", Ui.Icons.Music)
    };

    private readonly TodayPage _todayPage;
    private readonly NotesPage _notesPage;
    private readonly TasksPage _tasksPage;
    private readonly CalendarPage _calendarPage;
    private readonly MusicHubPage _musicPage;
    private string _tabAttivo = "Oggi";
    private readonly DatabaseService _db;

    private AudioService _audioService;
    private YouTubeService _youTubeService;

    public MainHostPage(AudioService audioService, YouTubeService ytService, DatabaseService dbService, HttpClient httpClient, Services.Sync.SyncService sync)
    {
        InitializeComponent();
        Loc.Register(this);
        NavigationPage.SetHasNavigationBar(this, false);
        _audioService = audioService;
        _youTubeService = ytService;

        _audioService.Player = GlobalAudioPlayer;
        _audioService.OnSongChanged += AggiornaMiniPlayer;
        _audioService.PlaybackChanged += AggiornaIconaMini;
        _audioService.PlaybackError += msg => MainThread.BeginInvokeOnMainThread(async () => await DisplayAlert(Loc.T("Riproduzione non riuscita"), msg, Loc.T("OK")));
        _audioService.YtResolver = _youTubeService;

        // Spazio di lavoro
        _todayPage = new TodayPage(dbService, sync, audioService)
        {
            NavigateTo = ApriTab,
            OnChangelogRequested = () => OnChangelogTapped(this, null)
        };
        _notesPage = new NotesPage(dbService);
        _tasksPage = new TasksPage(dbService);
        _calendarPage = new CalendarPage(dbService);

        // Musica (pagine esistenti raccolte in un'unica sezione)
        var homePage = new HomePage(dbService, audioService, ytService);
        var searchPage = new SearchPage(_youTubeService, _audioService, dbService, httpClient);
        var libraryPage = new LibraryPage(dbService, audioService, ytService);
        _musicPage = new MusicHubPage(homePage, searchPage, libraryPage);

        ThemeService.ThemeChanged += () => { IconeMiniPlayer(); ApriTab(_tabAttivo); };
        Loc.LanguageChanged += () => { IconeMiniPlayer(); ApriTab(_tabAttivo); ChangelogScroll.Content = Novita.Build(); };
        IconeMiniPlayer();

        // Avanzamento del brano nel mini player (linea da 2 in alto)
        var timer = Dispatcher.CreateTimer();
        timer.Interval = TimeSpan.FromSeconds(1);
        timer.Tick += (s, e) =>
        {
            var p = _audioService.Player;
            if (!MiniPlayer.IsVisible || p == null || p.Duration.TotalSeconds <= 0) return;
            MiniFill.WidthRequest = MiniTrack.Width * Math.Clamp(p.Position.TotalSeconds / p.Duration.TotalSeconds, 0, 1);
        };
        timer.Start();

        // Dati arrivati dall'altro dispositivo: ridisegna la sezione aperta (la musica si aggiorna da sola)
        sync.DataSynced += () => { if (_tabAttivo != "Musica") ApriTab(_tabAttivo); };

        // Modifiche fatte da una notifica o dal widget (Fatto, Pagato, Domani...)
        ReminderService.ExternalChange += () => MainThread.BeginInvokeOnMainThread(() => { if (_tabAttivo != "Musica") ApriTab(_tabAttivo); });

        // Richieste dal widget: "scrivi" apre la scrittura libera
        _db = dbService;
        RichiesteEsterne.SetHandler(azione =>
        {
            if (azione == "scrivi") Navigation.PushModalAsync(new QuickCapturePage(_db, () => { if (_tabAttivo != "Musica") ApriTab(_tabAttivo); }));
        });

        _sync = sync;
        ChangelogScroll.Content = Novita.Build();

        // Finestra larga (Windows) → barra laterale; stretta (telefono) → tab in basso
        SizeChanged += (s, e) => AggiornaLayout();

        ApriTab("Oggi");
    }

    private readonly Services.Sync.SyncService _sync;
    private bool _wide;
#if WINDOWS
    private bool _scorciatoia;
#endif

    private void AggiornaLayout()
    {
        bool wide = Width >= 720;
        if (wide == _wide && SideBar.IsVisible == wide) return;
        _wide = wide;
        SideBar.IsVisible = wide;
        TabBarHost.IsVisible = !wide;
        CostruisciTabBar();
    }

    // Cambia sezione. Accetta anche "Note:Pagamento" per aprire le note già filtrate.
    private void ApriTab(string tab)
    {
        var parti = tab.Split(':');
        _tabAttivo = parti[0];

        switch (_tabAttivo)
        {
            case "Note":
                PageHost.Content = _notesPage;
                if (parti.Length > 1) _notesPage.SetFilter(parti[1]); else _notesPage.Refresh();
                break;
            case "Task": PageHost.Content = _tasksPage; _tasksPage.Refresh(); break;
            case "Calendario": PageHost.Content = _calendarPage; _calendarPage.Refresh(); break;
            case "Musica": PageHost.Content = _musicPage; _musicPage.Refresh(); break;
            default: PageHost.Content = _todayPage; _todayPage.Refresh(); break;
        }

        CostruisciTabBar();
    }

    // Barra dei tab (3.0): 5 voci da 60, icona lineare 22 + etichetta 12; attiva = testo pieno e grassetto.
    // A finestra larga: barra laterale con "ARM", "Scrivi al volo" (Ctrl K), 5 voci da 44 e stato della sincronizzazione.
    private void CostruisciTabBar()
    {
        if (_wide) { CostruisciBarraLaterale(); return; }

        TabBar.Children.Clear();
        for (int i = 0; i < Tabs.Length; i++)
        {
            var (nome, icona) = Tabs[i];
            bool attivo = nome == _tabAttivo;
            var colore = Ui.C(attivo ? "TextColor" : "SubTextColor");
            var label = new Label
            {
                Text = Loc.T(nome),
                FontSize = 12,
                FontFamily = attivo ? Ui.FontBold : Ui.FontRegular,
                TextColor = colore,
                HorizontalOptions = LayoutOptions.Center
            };
            var cella = new VerticalStackLayout { Spacing = 4, VerticalOptions = LayoutOptions.Center, BackgroundColor = Colors.Transparent, Children = { Ui.Icon(icona, colore, 22), label } };
            Ui.OnTap(cella, () => { if (_tabAttivo != nome || nome == "Oggi") ApriTab(nome); });
            SemanticProperties.SetDescription(cella, Loc.T(nome));
            TabBar.Add(cella, i);
        }
    }

    private void CostruisciBarraLaterale()
    {
        SideContent.Children.Clear();

        // Logo testuale
        SideContent.Add(new Label { Text = "ARM", FontFamily = Ui.FontHeavy, FontSize = 20, CharacterSpacing = 2, TextColor = Ui.C("TextColor"), Padding = new Thickness(12, 10), VerticalOptions = LayoutOptions.Center }, 0, 0);

        // "Scrivi al volo" con la scorciatoia
        var quickRow = new Grid { ColumnDefinitions = new ColumnDefinitionCollection(new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto)), ColumnSpacing = 10 };
        quickRow.Add(Ui.Icon(Ui.Icons.Edit, Ui.C("SubTextColor"), 18));
        quickRow.Add(new Label { Text = Loc.T("Scrivi al volo"), FontFamily = Ui.FontRegular, FontSize = 14, TextColor = Ui.C("SubTextColor"), VerticalOptions = LayoutOptions.Center }, 1);
        quickRow.Add(new Border
        {
            Padding = new Thickness(6, 2), Stroke = new SolidColorBrush(Ui.C("BorderColor")), StrokeThickness = 1, StrokeShape = new RoundRectangle { CornerRadius = 6 }, VerticalOptions = LayoutOptions.Center,
            Content = new Label { Text = "Ctrl K", FontSize = 12, FontFamily = Ui.FontSemi, TextColor = Ui.C("SubTextColor") }
        }, 2);
        var quick = new Border { HeightRequest = 44, Padding = new Thickness(12, 0), BackgroundColor = Colors.Transparent, Stroke = new SolidColorBrush(Ui.C("BorderStrongColor")), StrokeThickness = 1, StrokeShape = new RoundRectangle { CornerRadius = 10 }, Content = quickRow };
        Ui.Press(quick, ApriScritturaLibera, 0.98);
        SideContent.Add(quick, 0, 1);

        // Voci: attiva = superficie rialzata, grassetto, punto nell'accento
        var voci = new VerticalStackLayout { Spacing = 4 };
        foreach (var (nome, icona) in Tabs)
        {
            bool attivo = nome == _tabAttivo;
            var n = nome;
            var row = new Grid { ColumnDefinitions = new ColumnDefinitionCollection(new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto)), ColumnSpacing = 12 };
            row.Add(Ui.Icon(icona, Ui.C(attivo ? "TextColor" : "SubTextColor"), 20));
            row.Add(new Label { Text = Loc.T(nome), FontFamily = attivo ? Ui.FontBold : Ui.FontRegular, FontSize = 15, TextColor = Ui.C(attivo ? "TextColor" : "SubTextColor"), VerticalOptions = LayoutOptions.Center }, 1);
            if (attivo) row.Add(Ui.Dot(Ui.C("AccentColor"), 6), 2);
            var item = new Border { HeightRequest = 44, Padding = new Thickness(12, 0), StrokeThickness = 0, StrokeShape = new RoundRectangle { CornerRadius = 8 }, BackgroundColor = attivo ? Ui.C("SurfaceRaisedColor") : Colors.Transparent, Content = row };
            Ui.OnTap(item, () => { if (_tabAttivo != n || n == "Oggi") ApriTab(n); });
            voci.Add(item);
        }
        SideContent.Add(voci, 0, 2);

        // Stato della sincronizzazione (riquadro col bordo)
        var box = new VerticalStackLayout { Spacing = 4 };
        string stato = !_sync.IsPaired ? Loc.T("Sincronizzazione non attiva") : _sync.PendingConflicts > 0 ? Loc.T("Da allineare") : Loc.T("Sincronizzato");
        box.Add(new HorizontalStackLayout { Spacing = 8, Children =
        {
            Ui.Dot(Ui.C(_sync.IsPaired ? "AccentColor" : "BorderStrongColor")),
            new Label { Text = stato, FontSize = 13, FontFamily = Ui.FontSemi, TextColor = Ui.C("TextColor"), VerticalOptions = LayoutOptions.Center }
        } });
        if (_sync.IsPaired)
        {
            var nomi = _sync.IsHost ? new[] { _sync.DeviceName }.Concat(_sync.Devices.Select(d => d.Name)) : new[] { _sync.DeviceName, _sync.PeerName };
            box.Add(Ui.Small(string.Join(" · ", nomi.Where(x => !string.IsNullOrWhiteSpace(x)))));
            if (_sync.LastSync is DateTime t) box.Add(Ui.Small(Loc.T("Ultimo allineamento ") + t.ToString(t.Date == DateTime.Today ? "HH:mm" : "d MMM HH:mm", Ui.It)));
            if (_sync.PendingConflicts > 0) box.Add(Ui.Small(_sync.PendingConflicts == 1 ? Loc.T("1 conflitto da risolvere") : Loc.F("{0} conflitti da risolvere", _sync.PendingConflicts), "TextColor"));
        }
        else box.Add(Ui.Small(Loc.T("Tocca per abbinare telefono e tablet")));
        var syncCard = new Border { Padding = new Thickness(12), Stroke = new SolidColorBrush(Ui.C("BorderColor")), StrokeThickness = 1, StrokeShape = new RoundRectangle { CornerRadius = 10 }, BackgroundColor = Colors.Transparent, Content = box };
        Ui.OnTap(syncCard, () => Navigation.PushModalAsync(new SyncPage(_sync)));
        SideContent.Add(syncCard, 0, 4);
    }

    private void ApriScritturaLibera() =>
        Navigation.PushModalAsync(new QuickCapturePage(_db, () => { if (_tabAttivo != "Musica") ApriTab(_tabAttivo); }));

    private void AggiornaMiniPlayer(Song song)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            MiniPlayer.IsVisible = true;
            MiniTitle.Text = song.Title;
            MiniArtist.Text = song.Artist;
            MiniCover.Source = string.IsNullOrEmpty(song.ThumbnailUrl) ? null : song.ThumbnailUrl;
            MiniFill.WidthRequest = 0;
            AggiornaIconaMini(_audioService.IsPlaying);
        });
    }

    // Icona play/pausa del mini player, sempre allineata allo stato reale del lettore
    private void AggiornaIconaMini(bool isPlaying)
    {
        if (_audioService.CurrentSong != null) MiniPlayer.IsVisible = true;
        if (_miniPlayIcon != null) Ui.SetIconData(_miniPlayIcon, isPlaying ? Ui.Icons.Pause : Ui.Icons.Play);
    }

    // Icone del mini player (lineari): si rifanno col tema
    private Microsoft.Maui.Controls.Shapes.Path _miniPlayIcon;
    private void IconeMiniPlayer()
    {
        _miniPlayIcon = Ui.Icon(_audioService.IsPlaying ? Ui.Icons.Pause : Ui.Icons.Play, Ui.C("OnAccentColor"), 20);
        MiniPlayButton.Content = _miniPlayIcon;
        MiniNextButton.Children.Clear();
        MiniNextButton.Add(Ui.Icon(Ui.Icons.Next, Ui.C("MiniTextColor"), 20));
    }

    private void OnMiniPlayClicked(object sender, EventArgs e)
    {
        if (_audioService.CurrentSong == null || _audioService.Player == null) return;
        _audioService.TogglePlayPause();   // l'icona si aggiorna con PlaybackChanged
    }

    private async void OnMiniPlayerTapped(object sender, TappedEventArgs e)
    {
        // Se c'è una canzone in corso, apre il lettore grande (PlayerPage) sopra a tutto!
        if (_audioService.CurrentSong != null)
        {
            await Navigation.PushAsync(new PlayerPage(_audioService.CurrentSong, _youTubeService, _audioService));
        }
    }

    private void OnMiniPlayerSkipClicked(object sender, TappedEventArgs e)
    {
        // Passa istantaneamente alla prossima canzone in coda (rispettando lo Shuffle!)
        _audioService.PlayNext();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        // Nome della finestra su Windows
        if (Window != null) Window.Title = "ARM";
#if WINDOWS
        // Ctrl+K: "Scrivi al volo" da qualsiasi sezione
        if (!_scorciatoia && Handler?.PlatformView is Microsoft.UI.Xaml.UIElement el)
        {
            _scorciatoia = true;
            var acc = new Microsoft.UI.Xaml.Input.KeyboardAccelerator { Key = Windows.System.VirtualKey.K, Modifiers = Windows.System.VirtualKeyModifiers.Control };
            acc.Invoked += (s, a) => { a.Handled = true; if (Navigation.ModalStack.Count == 0) ApriScritturaLibera(); };
            el.KeyboardAccelerators.Add(acc);
        }
#endif

        // 1. Fa apparire la pagina dolcemente
        await this.FadeTo(1, 800, Easing.CubicInOut);

        // 2. Chiede il permesso PER LE NOTIFICHE in modo sicuro (Solo Android 13+)
#if ANDROID
        if (OperatingSystem.IsAndroidVersionAtLeast(33))
        {
            var status = await Permissions.CheckStatusAsync<Permissions.PostNotifications>();
            if (status != PermissionStatus.Granted)
            {
                await Permissions.RequestAsync<Permissions.PostNotifications>();
            }
        }
#endif
    }

    // Questo intercetta il tasto/gesto fisico "Indietro" del telefono
    protected override bool OnBackButtonPressed()
    {
        // 1. Se la modale degli aggiornamenti è aperta, la chiudiamo
        if (ChangelogOverlay.IsVisible)
        {
            OnCloseChangelogTapped(this, null);
            return true;
        }

        // 2. Se siamo dentro una playlist, andiamo indietro
        if (PageHost.Content == _musicPage && _musicPage.HandleBack())
            return true;

        // 3. Da qualsiasi sezione, "Indietro" riporta prima a Oggi
        if (_tabAttivo != "Oggi")
        {
            ApriTab("Oggi");
            return true;
        }

        return base.OnBackButtonPressed();
    }

    // --- GESTIONE MODALE AGGIORNAMENTI ---

    private async void OnChangelogTapped(object sender, TappedEventArgs e)
    {
        // Rende visibile il contenitore fantasma e intercetta i tocchi
        ChangelogOverlay.IsVisible = true;
        ChangelogOverlay.InputTransparent = false;

        // Imposta la posizione iniziale in basso fuori dallo schermo
        ChangelogScroll.Content = Novita.Build();   // con i colori del tema attuale
        ChangelogSheet.MaximumHeightRequest = Height * 0.82;
        ChangelogSheet.TranslationY = 500;

        // Sfondo che si scurisce e foglio che sale dal basso (220 ms, come le schermate a comparsa)
        await Task.WhenAll(
            ChangelogOverlay.FadeTo(1, 220, Easing.CubicOut),
            ChangelogSheet.TranslateTo(0, 0, 220, Easing.CubicOut)
        );
    }

    private async void OnCloseChangelogTapped(object sender, TappedEventArgs e)
    {
        // Torna giù e scompare
        await Task.WhenAll(
            ChangelogOverlay.FadeTo(0, 200, Easing.CubicIn),
            ChangelogSheet.TranslateTo(0, 500, 200, Easing.CubicIn)
        );

        ChangelogOverlay.IsVisible = false;
        ChangelogOverlay.InputTransparent = true;
    }
}
