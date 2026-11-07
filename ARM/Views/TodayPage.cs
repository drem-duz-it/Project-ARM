using ARM.Data;
using ARM.Models;
using ARM.Services;
using ARM.Services.Sync;
using ARM.Views.Dashboard;
using Microsoft.Maui.Controls.Shapes;
using Path = Microsoft.Maui.Controls.Shapes.Path;

namespace ARM.Views;

// Pagina "Oggi" (ARM 3.0): uno spazio di lavoro sobrio.
// Ordine dei blocchi: intestazione · scrivi al volo + azioni rapide · conflitti · 4 indicatori · ordini in arrivo ·
// agenda di oggi · task · pagamenti · musica · note fissate · stato della sincronizzazione.
// Larghezza del contenuto: < 640 telefono (1 colonna) · 640-999 tablet (2 colonne) · ≥ 1000 desktop (8+4, poi 3 colonne).
public class TodayPage : ContentView
{
    private readonly DatabaseService _db;
    private readonly SyncService _sync;
    private readonly AudioService _audio;
    private readonly VerticalStackLayout _body = new() { Padding = new Thickness(16, 24, 16, 24), Spacing = 16 };

    // Struttura fissa: [intestazione] [scrivi al volo] [resto]. Il campo "scrivi al volo" non viene MAI tolto e
    // rimesso (spostare un campo col cursore dentro fa crollare WinUI): si ricrea solo col tema; si rifà solo il resto.
    private QuickCaptureBar _quick;
    private string _quickTheme;
    private readonly ContentView _header = new();
    private readonly VerticalStackLayout _dynamic = new() { Spacing = 16 };
    private Blocks.Modo _modo = Blocks.Modo.Telefono;

    // Musica: aggiornati ogni secondo senza ricostruire la pagina
    private Label _musicTime;
    private BoxView _musicFill;
    private Grid _musicTrack;
    private Path _musicPlayIcon;
    private IDispatcherTimer _timer;

    public Action<string> NavigateTo;        // Cambia tab ("Note", "Task", "Calendario"...)
    public Action OnChangelogRequested;

    public TodayPage(DatabaseService db, SyncService sync, AudioService audio)
    {
        _db = db;
        _sync = sync;
        _audio = audio;
        Content = new ScrollView { Content = _body };
        SizeChanged += (s, e) =>
        {
            var modo = Width >= 1000 ? Blocks.Modo.Desktop : Width >= 640 ? Blocks.Modo.Tablet : Blocks.Modo.Telefono;
            if (modo != _modo) { _modo = modo; Refresh(); }
        };

        if (GmailOrdiniService.Current != null)
            GmailOrdiniService.Current.Changed += () => MainThread.BeginInvokeOnMainThread(() => { if (Parent != null) Refresh(); });
        _audio.OnSongChanged += _ => MainThread.BeginInvokeOnMainThread(() => { if (Parent != null) Refresh(); });
        _audio.PlaybackChanged += playing => MainThread.BeginInvokeOnMainThread(() =>
        {
            if (_musicPlayIcon != null) Ui.SetIconData(_musicPlayIcon, playing ? Ui.Icons.Pause : Ui.Icons.Play);
        });

        Loaded += (s, e) => StartTimer();
        Unloaded += (s, e) => _timer?.Stop();
    }

    private void StartTimer()
    {
        _timer ??= Dispatcher.CreateTimer();
        _timer.Interval = TimeSpan.FromSeconds(1);
        _timer.Tick -= OnTick;
        _timer.Tick += OnTick;
        _timer.Start();
    }

    private void OnTick(object sender, EventArgs e)
    {
        var p = _audio.Player;
        if (_musicTime == null || p == null || _audio.CurrentSong == null) return;
        var pos = p.Position;
        var dur = p.Duration;
        _musicTime.Text = dur.TotalSeconds > 0 ? $"{Tempo(pos)} / {Tempo(dur)}" : Tempo(pos);
        if (_musicTrack != null && _musicFill != null && dur.TotalSeconds > 0)
            _musicFill.WidthRequest = Math.Max(0, _musicTrack.Width * Math.Clamp(pos.TotalSeconds / dur.TotalSeconds, 0, 1));
    }

    private static string Tempo(TimeSpan t) => t.TotalHours >= 1 ? t.ToString(@"h\:mm\:ss") : $"{(int)t.TotalMinutes}:{t.Seconds:00}";

    // Due Refresh che si accavallano svuotano e riempiono la stessa pagina insieme (e a volte WinUI crolla):
    // se ne sta già girando uno, il nuovo aspetta e rifà tutto una volta sola alla fine.
    private bool _refreshing, _refreshAgain;
    public async void Refresh()
    {
        if (_refreshing) { _refreshAgain = true; return; }
        _refreshing = true;
        try
        {
            do { _refreshAgain = false; await RefreshOnce(); } while (_refreshAgain);
        }
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Oggi: {ex}"); }
        finally { _refreshing = false; }
    }

    private async Task RefreshOnce()
    {
        var notes = await _db.GetAllNotesAsync();
        var tasks = await _db.GetAllTasksAsync();
        var events = await _db.GetAllEventsAsync();
        var ordini = await _db.GetAllOrdiniAsync();
        var playlists = await _db.GetAllPlaylistsAsync();
        var recenti = new List<(Playlist P, int N)>();
        foreach (var pl in playlists.OrderByDescending(p => p.ChangedAt).Take(3))
            recenti.Add((pl, (await _db.GetSongsForPlaylistAsync(pl.Id)).Count));

        BackgroundColor = Ui.C("BgColor");
        bool tel = _modo == Blocks.Modo.Telefono;
        _body.Padding = _modo switch
        {
            Blocks.Modo.Desktop => new Thickness(32),
            Blocks.Modo.Tablet => new Thickness(24, 28, 24, 24),
            _ => new Thickness(16, 20, 16, 24)
        };
        _body.Spacing = tel ? 16 : 24;
        _dynamic.Spacing = tel ? 16 : 24;

        if (_body.Children.Count == 0)
        {
            _body.Add(_header);
            _body.Add(_dynamic);
        }
        _header.Content = Header();
        if (_quick == null || _quickTheme != ThemeService.TemaAttuale)
        {
            if (_quick != null) _body.Remove(_quick);
            _quick = new QuickCaptureBar(_db, Refresh);
            _quickTheme = ThemeService.TemaAttuale;
            _body.Insert(1, _quick);
        }

        _musicTime = null; _musicFill = null; _musicTrack = null; _musicPlayIcon = null;
        _dynamic.Children.Clear();
        _dynamic.Add(QuickActions());
        if (_sync.PendingConflicts > 0) _dynamic.Add(ConflictBanner());
        _dynamic.Add(Indicatori(notes, tasks, events, ordini));

        var ordiniCard = Ordini(ordini);
        var agenda = Agenda(notes, events);
        var taskCard = Tasks(tasks);
        var pagamenti = Pagamenti(notes);
        var musica = Musica(recenti);
        var fissate = NoteFissate(notes);

        switch (_modo)
        {
            case Blocks.Modo.Desktop:
                var rowA = Colonne(2, 1);
                rowA.Add(ordiniCard);
                rowA.Add(agenda, 1);
                _dynamic.Add(rowA);
                var rowB = Colonne(1, 1, 1);
                rowB.Add(taskCard);
                rowB.Add(pagamenti, 1);
                rowB.Add(new VerticalStackLayout { Spacing = 24, Children = { musica, fissate } }, 2);
                _dynamic.Add(rowB);
                break;
            case Blocks.Modo.Tablet:
                _dynamic.Add(ordiniCard);
                var r1 = Colonne(1, 1); r1.Add(agenda); r1.Add(taskCard, 1); _dynamic.Add(r1);
                var r2 = Colonne(1, 1); r2.Add(pagamenti); r2.Add(fissate, 1); _dynamic.Add(r2);
                _dynamic.Add(musica);
                break;
            default:
                _dynamic.Add(ordiniCard);
                _dynamic.Add(agenda);
                _dynamic.Add(taskCard);
                _dynamic.Add(pagamenti);
                _dynamic.Add(fissate);
                _dynamic.Add(musica);
                break;
        }
        _dynamic.Add(SyncFooter());
        OnTick(null, null);
    }

    private Grid Colonne(params double[] pesi)
    {
        var g = new Grid { ColumnSpacing = 24 };
        foreach (var p in pesi) g.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(p, GridUnitType.Star)));
        return g;
    }

    private void Open(Page page) => Application.Current.MainPage.Navigation.PushModalAsync(page);

    // ---------- Intestazione ----------
    private View Header()
    {
        var ora = DateTime.Now.Hour;
        string saluto = ora >= 5 && ora < 13 ? Loc.T("Buongiorno") : ora >= 13 && ora < 18 ? Loc.T("Buon pomeriggio") : Loc.T("Buonasera");
        string nome = Preferences.Default.Get("Utente.Nome", "").Trim();
        if (nome.Length > 0) saluto += ", " + nome;

        var g = new Grid { ColumnDefinitions = new ColumnDefinitionCollection(new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto)), ColumnSpacing = 16 };
        var date = Ui.Caps(DateTime.Today.ToString("dddd d MMMM", Ui.It));
        date.CharacterSpacing = 1.4;
        var title = Ui.PageTitle(saluto);
        title.MaxLines = 2;
        bool tel = _modo == Blocks.Modo.Telefono;
        if (tel)
        {
            title.FontSize = 26;
            title.LineHeight = 1.15;
            g.RowDefinitions = new RowDefinitionCollection(new RowDefinition(GridLength.Auto), new RowDefinition(GridLength.Auto));
            g.RowSpacing = 12;
            date.VerticalOptions = LayoutOptions.Center;
            g.Add(date);
            g.Add(title, 0, 1);
            Grid.SetColumnSpan(title, 2);
        }
        else g.Add(new VerticalStackLayout { Spacing = 6, VerticalOptions = LayoutOptions.Center, Children = { date, title } });

        string dot = _sync.IsPaired && _sync.PendingConflicts > 0 ? "AccentColor" : null;
        var buttons = new HorizontalStackLayout { Spacing = 8, VerticalOptions = LayoutOptions.Center, Children =
        {
            Ui.IconButton(Ui.Icons.Theme, () => ThemeService.ProssimoTema()),
            Ui.IconButton(Ui.Icons.Sync, OpenSync, dot),
            Ui.IconButton(Ui.Icons.Spark, () => OnChangelogRequested?.Invoke()),
            Ui.IconButton(Ui.Icons.Settings, () => Open(new SettingsPage(_sync)))
        } };
        SemanticProperties.SetDescription(buttons.Children[0] as View, Loc.T("Cambia tema"));
        SemanticProperties.SetDescription(buttons.Children[1] as View, _sync.PendingConflicts > 0 ? Loc.F("Sincronizzazione: {0} conflitti", _sync.PendingConflicts) : Loc.T("Sincronizzazione"));
        SemanticProperties.SetDescription(buttons.Children[2] as View, Loc.T("Novità"));
        SemanticProperties.SetDescription(buttons.Children[3] as View, Loc.T("Impostazioni"));
        g.Add(buttons, 1);
        return g;
    }

    private void OpenSync() => Open(new SyncPage(_sync));

    // Azioni rapide: pulsanti 44 col bordo forte
    private View QuickActions()
    {
        var row = new HorizontalStackLayout { Spacing = 8 };
        row.Add(Blocks.Secondary(Loc.T("Nota"), () => Open(new NoteEditorPage(_db, Refresh)), Ui.Icons.Plus));
        row.Add(Blocks.Secondary(Loc.T("Task"), () => Open(new TaskEditorPage(_db, Refresh, presetDate: DateTime.Today)), Ui.Icons.Plus));
        row.Add(Blocks.Secondary(Loc.T("Evento"), () => Open(new EventEditorPage(_db, Refresh)), Ui.Icons.Plus));
        row.Add(Blocks.Secondary(Loc.T("Pagamento"), () => Open(new NoteEditorPage(_db, Refresh, presetType: "Pagamento")), Ui.Icons.Plus));
        row.Add(Blocks.Secondary(Loc.T("Idea"), () => Open(new NoteEditorPage(_db, Refresh, presetType: "Idea")), Ui.Icons.Plus));
        bool tel = _modo == Blocks.Modo.Telefono;
        return new ScrollView
        {
            Orientation = ScrollOrientation.Horizontal, HorizontalScrollBarVisibility = ScrollBarVisibility.Never, Content = row,
            Margin = tel ? new Thickness(-16, 0) : 0, Padding = tel ? new Thickness(16, 0) : 0
        };
    }

    // Banner conflitti di sincronizzazione
    private View ConflictBanner()
    {
        var g = new Grid
        {
            ColumnDefinitions = new ColumnDefinitionCollection(new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto)),
            ColumnSpacing = 12
        };
        g.Add(new Border
        {
            WidthRequest = 36, HeightRequest = 36, Stroke = new SolidColorBrush(Ui.C("BorderStrongColor")), StrokeThickness = 1,
            StrokeShape = new RoundRectangle { CornerRadius = 8 }, Content = Ui.Icon(Ui.Icons.Conflict, Ui.C("TextColor"), 18), VerticalOptions = LayoutOptions.Center
        });
        var n = _sync.PendingConflicts;
        var text = new VerticalStackLayout { Spacing = 2, VerticalOptions = LayoutOptions.Center };
        text.Add(new Label { Text = n == 1 ? Loc.T("1 conflitto di sincronizzazione") : Loc.F("{0} conflitti di sincronizzazione", n), FontSize = 15, FontFamily = Ui.FontSemi, TextColor = Ui.C("TextColor") });
        var peer = string.IsNullOrWhiteSpace(_sync.PeerName) ? Loc.T("l'altro dispositivo") : _sync.PeerName;
        text.Add(Ui.Small(Loc.F("Elementi modificati sia su {0} sia su {1}. Scegli quale versione tenere.", _sync.DeviceName, peer)));
        g.Add(text, 1);
        if (_modo != Blocks.Modo.Telefono) g.Add(Blocks.Secondary(Loc.T("Risolvi conflitti"), OpenSync), 2);
        var card = Ui.Card(g, new Thickness(16, 12, 12, 12));
        Ui.Press(card, OpenSync, 0.99);
        return card;
    }

    // ---------- Indicatori ----------
    private View Indicatori(List<Note> notes, List<TaskItem> tasks, List<CalendarEvent> events, List<Ordine> ordini)
    {
        var oggi = DateTime.Today;
        var taskOggi = tasks.Where(t => !t.IsDone && t.DueDate.HasValue && t.DueDate.Value.Date <= oggi).ToList();
        int ritardo = taskOggi.Count(t => t.IsOverdue);
        int fattiOggi = tasks.Count(t => t.IsDone && t.CompletedAt?.Date == oggi);

        var eventiOggi = events.Where(e => e.Date.Date == oggi).ToList();
        var prossimo = eventiOggi.Where(e => !e.AllDay && oggi + e.StartTime > DateTime.Now).OrderBy(e => e.StartTime).FirstOrDefault();

        var limite7 = oggi.AddDays(7);
        double uscite = notes.Where(n => n.IsPayment && !n.IsIncome && !n.IsPaid && n.DueDate.HasValue && n.DueDate.Value.Date <= limite7).Sum(n => n.Amount);
        double entrate = notes.Where(n => n.IsPayment && n.IsIncome && !n.IsPaid && n.DueDate.HasValue && n.DueDate.Value.Date <= oggi.AddDays(30)).Sum(n => n.Amount);

        var inArrivo = ordini.Where(o => !o.Consegnato).ToList();
        int ordOggi = inArrivo.Count(o => o.ConsegnaPrevista?.Date == oggi);
        int ordRit = inArrivo.Count(o => o.InRitardo);
        var ctxOrd = new List<string>();
        if (ordOggi > 0) ctxOrd.Add(Loc.F("{0} oggi", ordOggi));
        if (ordRit > 0) ctxOrd.Add(Loc.F("{0} in ritardo", ordRit));

        var tiles = new View[]
        {
            Blocks.Kpi(taskOggi.Count.ToString(), Loc.T("Task di oggi"),
                ritardo > 0 ? Loc.F("{0} in ritardo", ritardo) : fattiOggi > 0 ? Loc.F("{0} completati oggi", fattiOggi) : Loc.T("Nessuno in ritardo"),
                ritardo > 0 ? "WarnColor" : "SubTextColor", () => NavigateTo?.Invoke("Task")),
            Blocks.Kpi(eventiOggi.Count.ToString(), Loc.T("Eventi di oggi"),
                prossimo != null ? Loc.F("Prossimo alle {0:hh\\:mm}", prossimo.StartTime) : eventiOggi.Count > 0 ? Loc.T("Nessun altro oggi") : Loc.T("Giornata libera"),
                "SubTextColor", () => NavigateTo?.Invoke("Calendario")),
            Blocks.Kpi(Ui.Euro(Math.Round(uscite)).Replace(",00", ""), Loc.T("Uscite in 7 giorni"),
                entrate > 0 ? Loc.F("Entrate in arrivo {0}", Ui.Euro(entrate)) : Loc.T("Nessuna entrata prevista"),
                entrate > 0 ? "Accent3Color" : "SubTextColor", () => NavigateTo?.Invoke("Note:Pagamento")),
            Blocks.Kpi(inArrivo.Count.ToString(), Loc.T("Ordini in arrivo"),
                ctxOrd.Count > 0 ? string.Join(" · ", ctxOrd) : inArrivo.Count > 0 ? Loc.T("Nessuno oggi") : Loc.T("Nessun pacco in viaggio"),
                ordRit > 0 ? "WarnColor" : "SubTextColor", null)
        };

        bool tel = _modo == Blocks.Modo.Telefono;
        var g = new Grid { ColumnSpacing = tel ? 12 : 24, RowSpacing = 12 };
        int cols = tel ? 2 : 4;
        for (int c = 0; c < cols; c++) g.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
        for (int i = 0; i < tiles.Length; i++)
        {
            if (i / cols >= g.RowDefinitions.Count) g.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            g.Add(tiles[i], i % cols, i / cols);
        }
        return g;
    }

    // ---------- Ordini in arrivo ----------
    private View Ordini(List<Ordine> tutti)
    {
        var gmail = GmailOrdiniService.Current;
        bool pc = GmailOrdiniService.Supportato;
        bool collegato = gmail?.Collegato == true;
        string errore = collegato ? gmail.UltimoErrore : "";

        var lista = tutti.Where(o => o.Visibile)
                         .OrderBy(o => o.Consegnato)
                         .ThenBy(o => o.ConsegnaPrevista ?? DateTime.MaxValue)
                         .ThenBy(o => o.DataOrdine)
                         .ToList();
        int inArrivo = lista.Count(o => !o.Consegnato);

        // Sottotitolo e azione dell'intestazione
        string sub;
        View azione = null;
        if (pc && collegato)
        {
            if (gmail.InLettura) sub = Loc.T("Da Gmail · Lettura in corso…");
            else if (!string.IsNullOrEmpty(errore)) sub = gmail.UltimoRiuscito is DateTime ok ? Loc.F("Ultimo aggiornamento riuscito {0}", GmailPage.Quando(ok)) : Loc.T("Nessuna lettura riuscita");
            else sub = gmail.UltimoAggiornamento is DateTime t ? Loc.F("Da Gmail · Aggiornato {0}", GmailPage.Quando(t)) : Loc.T("Da Gmail");
            var bottoni = new HorizontalStackLayout { Spacing = 8 };
            bottoni.Add(Blocks.Secondary(string.IsNullOrEmpty(errore) ? Loc.T("Aggiorna") : Loc.T("Riprova"), async () => await gmail.AggiornaAsync(), Ui.Icons.Refresh));
            var impostazioni = Ui.IconButton(Ui.Icons.Mail, () => Open(new GmailPage(Refresh)), flat: true);
            SemanticProperties.SetDescription(impostazioni, Loc.T("Impostazioni Gmail"));
            bottoni.Add(impostazioni);
            azione = bottoni;
        }
        else if (pc) sub = null;
        else
        {
            sub = _sync.IsPaired ? Loc.T("Dal PC · con la sincronizzazione") : Loc.T("Dal PC");
            if (_sync.IsPaired) azione = Blocks.Secondary(Loc.T("Aggiorna"), async () => { await _sync.SyncAsync(null); Refresh(); }, Ui.Icons.Refresh);
        }

        var corpo = new VerticalStackLayout();
        bool tel = _modo == Blocks.Modo.Telefono;

        if (pc && !collegato && lista.Count == 0)
        {
            corpo.Add(Blocks.StatoBlocco(Ui.Icons.Mail, "TextColor", Loc.T("Collega Gmail per seguire gli ordini"),
                Loc.T("ARM legge solo le email di conferma e spedizione di Amazon. Nessun altro messaggio viene aperto."),
                Ui.Button(Loc.T("Collega Gmail"), () => Open(new GmailPage(Refresh)), Ui.Icons.Mail, small: true)));
            return Sezione(null);
        }

        if (!string.IsNullOrEmpty(errore))
        {
            corpo.Add(Blocks.Hr());
            corpo.Add(Blocks.StatoBlocco(Ui.Icons.Warn, "WarnColor", Loc.T("Non riesco a leggere le email"), errore,
                Blocks.Secondary(Loc.T("Riprova"), async () => await gmail.AggiornaAsync(), Ui.Icons.Refresh),
                Blocks.Secondary(Loc.T("Ricollega Gmail"), () => { gmail.Scollega(); Open(new GmailPage(Refresh)); }, Ui.Icons.Mail)));
        }

        if (lista.Count == 0 && string.IsNullOrEmpty(errore))
        {
            corpo.Add(Blocks.Hr());
            corpo.Add(Blocks.StatoBlocco(Ui.Icons.Box, "SubTextColor", Loc.T("Nessun ordine in arrivo"),
                pc ? Loc.T("Gli ordini Amazon che ricevi su Gmail compariranno qui fino alla consegna.")
                   : Loc.T("Gli ordini Amazon arrivano dal PC: collega Gmail in ARM su Windows.")));
        }
        else if (lista.Count > 0)
        {
            if (tel)
            {
                foreach (var o in lista.Take(4))
                {
                    corpo.Add(Blocks.Hr());
                    var oo = o;
                    corpo.Add(Blocks.OrdineRowStretta(o, () => _ = OrdineAzioni.ApriAsync(_db, oo, Refresh)));
                }
                if (lista.Count > 4)
                {
                    corpo.Add(Blocks.Hr());
                    var all = new HorizontalStackLayout { Spacing = 4, HorizontalOptions = LayoutOptions.Center, MinimumHeightRequest = 44 };
                    all.Add(new Label { Text = Loc.F("Mostra tutti ({0})", lista.Count), FontSize = 14, FontFamily = Ui.FontSemi, TextColor = Ui.C("TextColor"), VerticalOptions = LayoutOptions.Center });
                    all.Add(Ui.Icon(Ui.Icons.Down, Ui.C("TextColor"), 16));
                    var holder = new VerticalStackLayout();
                    var allBox = new ContentView { Content = all, BackgroundColor = Colors.Transparent };
                    Ui.OnTap(allBox, () =>
                    {
                        // Aggiunge le righe restanti sotto (nessuna vista viene spostata)
                        allBox.IsVisible = false;
                        foreach (var o in lista.Skip(4))
                        {
                            holder.Add(Blocks.Hr());
                            var oo = o;
                            holder.Add(Blocks.OrdineRowStretta(o, () => _ = OrdineAzioni.ApriAsync(_db, oo, Refresh)));
                        }
                    });
                    corpo.Add(holder);
                    corpo.Add(allBox);
                }
            }
            else
            {
                double cCons = _modo == Blocks.Modo.Desktop ? 128 : 104, cStato = _modo == Blocks.Modo.Desktop ? 168 : 128;
                var head = new Grid
                {
                    ColumnDefinitions = new ColumnDefinitionCollection(new ColumnDefinition(GridLength.Star), new ColumnDefinition(cCons), new ColumnDefinition(cStato)),
                    ColumnSpacing = 16, Padding = new Thickness(20, 8)
                };
                head.Add(ColLabel(Loc.T("Prodotto e numero d'ordine")));
                head.Add(ColLabel(Loc.T("Consegna")), 1);
                head.Add(ColLabel(Loc.T("Stato")), 2);
                corpo.Add(Blocks.Hr());
                corpo.Add(head);
                corpo.Add(Blocks.Hr());
                bool first = true;
                foreach (var o in lista)
                {
                    if (!first) corpo.Add(Blocks.Hr());
                    first = false;
                    var oo = o;
                    corpo.Add(Blocks.OrdineRowLarga(o, cCons, cStato, () => _ = OrdineAzioni.ApriAsync(_db, oo, Refresh)));
                }
            }
        }

        return Sezione(inArrivo);

        // L'intestazione ha i margini interni; il corpo va a filo (le righe hanno i loro margini)
        View Sezione(int? count)
        {
            var section = Blocks.Section(Loc.T("Ordini in arrivo"), count, azione, corpo, new Thickness(0, 8, 0, 0), sub);
            if (section.Content is VerticalStackLayout v && v.Children.FirstOrDefault() is View h) h.Margin = new Thickness(tel ? 16 : 20, 0, tel ? 12 : 20, 4);
            return section;
        }
        static Label ColLabel(string t) => new() { Text = t, FontSize = 12, FontFamily = Ui.FontSemi, TextColor = Ui.C("SubTextColor") };
    }

    // ---------- Agenda di oggi ----------
    private View Agenda(List<Note> notes, List<CalendarEvent> events)
    {
        var oggi = DateTime.Today;
        var voci = new List<Blocks.VoceAgenda>();
        foreach (var e in events.Where(e => e.Date.Date == oggi))
        {
            string durata = e.AllDay ? null : Durata(e.EndTime - e.StartTime);
            string sotto = string.Join(" · ", new[] { e.Location, durata }.Where(x => !string.IsNullOrWhiteSpace(x)));
            voci.Add(new Blocks.VoceAgenda
            {
                Inizio = e.AllDay ? null : e.StartTime, Fine = e.AllDay ? null : e.EndTime, TuttoIlGiorno = e.AllDay,
                Titolo = e.Title, Sotto = sotto, ColorKey = e.ColorKey ?? "Accent5Color", Sorgente = e
            });
        }
        foreach (var n in notes.Where(n => !n.IsPayment && n.DueDate.HasValue && n.DueDate.Value.Date == oggi && n.DueDate.Value.TimeOfDay != TimeSpan.Zero))
            voci.Add(new Blocks.VoceAgenda { Inizio = n.DueDate.Value.TimeOfDay, Titolo = n.Title, Sotto = Loc.T(n.Type), ColorKey = "Accent4Color", Sorgente = n });

        var lista = new VerticalStackLayout();
        var ordinate = voci.OrderBy(v => v.TuttoIlGiorno ? 0 : 1).ThenBy(v => v.Inizio).ToList();
        var adesso = DateTime.Now.TimeOfDay;
        bool lineaMessa = false, ciSonoOrari = ordinate.Any(v => !v.TuttoIlGiorno);
        foreach (var v in ordinate)
        {
            bool passato = !v.TuttoIlGiorno && (v.Fine ?? v.Inizio) <= adesso;
            if (ciSonoOrari && !lineaMessa && !v.TuttoIlGiorno && !passato) { lista.Add(Blocks.Adesso()); lineaMessa = true; }
            var vv = v;
            lista.Add(Blocks.AgendaRiga(v, passato, () => OpenItem(vv.Sorgente)));
        }
        if (ciSonoOrari && !lineaMessa) lista.Add(Blocks.Adesso());
        if (voci.Count == 0)
        {
            var e = Ui.Small(Loc.T("Nessun evento oggi. Gli eventi del calendario compariranno qui."));
            e.Padding = new Thickness(0, 8, 0, 4);
            lista.Add(e);
        }
        return Blocks.Section(Loc.T("Agenda di oggi"), voci.Count, Ui.LinkLabel(Loc.T("Apri calendario"), () => NavigateTo?.Invoke("Calendario")), lista);
    }

    private static string Durata(TimeSpan d)
    {
        if (d.TotalMinutes <= 0) return null;
        if (d.TotalMinutes < 60) return $"{(int)d.TotalMinutes} min";
        return d.Minutes == 0 ? $"{(int)d.TotalHours} h" : $"{(int)d.TotalHours} h {d.Minutes} min";
    }

    // ---------- Task ----------
    private View Tasks(List<TaskItem> tasks)
    {
        var oggi = DateTime.Today;
        var ritardo = tasks.Where(t => t.IsOverdue).OrderBy(t => t.DueDate).ToList();
        var diOggi = tasks.Where(t => !t.IsDone && t.IsToday).OrderBy(t => t.DueDate.Value.TimeOfDay == TimeSpan.Zero).ThenBy(t => t.DueDate).ThenByDescending(t => t.Priority).ToList();
        var lista = new VerticalStackLayout();
        int spazio = 8;
        if (ritardo.Count > 0)
        {
            lista.Add(Blocks.SottoTitolo(Loc.T("In ritardo"), "WarnColor", ritardo.Count));
            foreach (var t in ritardo.Take(spazio)) lista.Add(TaskRow(t));
            spazio -= Math.Min(spazio, ritardo.Count);
        }
        if (diOggi.Count > 0 && spazio > 0)
        {
            lista.Add(Blocks.SottoTitolo(Loc.T("Oggi"), "SubTextColor", diOggi.Count));
            foreach (var t in diOggi.Take(spazio)) lista.Add(TaskRow(t));
        }
        if (ritardo.Count + diOggi.Count == 0)
        {
            var e = Ui.Small(Loc.T("Nessun task per oggi. Scrivine uno al volo qui sopra."));
            e.Padding = new Thickness(0, 8, 0, 4);
            lista.Add(e);
        }
        return Blocks.Section(Loc.T("Task"), ritardo.Count + diOggi.Count, Ui.LinkLabel(Loc.T("Tutti i task"), () => NavigateTo?.Invoke("Task")), lista);
    }

    private View TaskRow(TaskItem t) =>
        Blocks.TaskRiga(t, async () => { await _db.ToggleTaskAsync(t); Refresh(); }, () => Open(new TaskEditorPage(_db, Refresh, t)));

    // ---------- Pagamenti ----------
    private View Pagamenti(List<Note> notes)
    {
        var oggi = DateTime.Today;
        var aperti = notes.Where(n => n.IsPayment && !n.IsPaid && n.DueDate.HasValue).ToList();
        double uscite = aperti.Where(n => !n.IsIncome && n.DueDate.Value.Date <= oggi.AddDays(7)).Sum(n => n.Amount);
        double entrate = aperti.Where(n => n.IsIncome && n.DueDate.Value.Date <= oggi.AddDays(30)).Sum(n => n.Amount);

        var v = new VerticalStackLayout { Spacing = 0 };
        var tot = new Grid { ColumnDefinitions = new ColumnDefinitionCollection(new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Star)), ColumnSpacing = 16, Padding = new Thickness(0, 4, 0, 12) };
        tot.Add(Blocks.Totale(Loc.T("Uscite · 7 giorni"), Ui.Euro(uscite), "TextColor"));
        tot.Add(Blocks.Totale(Loc.T("Entrate in arrivo"), Ui.Euro(entrate), entrate > 0 ? "Accent3Color" : "TextColor"), 1);
        v.Add(tot);
        v.Add(Blocks.Hr());

        var lista = aperti.Where(n => n.DueDate.Value.Date <= oggi.AddDays(14)).OrderBy(n => n.DueDate).Take(_modo == Blocks.Modo.Telefono ? 5 : 8).ToList();
        if (lista.Count == 0)
        {
            var e = Ui.Small(Loc.T("Nessuna scadenza nelle prossime due settimane."));
            e.Padding = new Thickness(0, 12, 0, 4);
            v.Add(e);
        }
        foreach (var n in lista)
        {
            var nn = n;
            v.Add(Blocks.PagamentoRiga(n, () => Open(new NoteEditorPage(_db, Refresh, nn))));
        }
        return Blocks.Section(Loc.T("Pagamenti"), null, Ui.LinkLabel(Loc.T("Apri pagamenti"), () => NavigateTo?.Invoke("Note:Pagamento")), v);
    }

    // ---------- Musica ----------
    private View Musica(List<(Playlist P, int N)> recenti)
    {
        var song = _audio.CurrentSong;
        var v = new VerticalStackLayout { Spacing = 12 };

        // Copertina 72 + titoli
        var top = new Grid { ColumnDefinitions = new ColumnDefinitionCollection(new ColumnDefinition(72), new ColumnDefinition(GridLength.Star)), ColumnSpacing = 16 };
        top.Add(Copertina(song?.ThumbnailUrl, 72));
        var titoli = new VerticalStackLayout { Spacing = 2, VerticalOptions = LayoutOptions.Center };
        titoli.Add(new Label { Text = song == null ? Loc.T("Niente in riproduzione") : _audio.IsPlaying ? Loc.T("In riproduzione") : Loc.T("In pausa"), FontSize = 12, FontFamily = Ui.FontSemi, TextColor = Ui.C(song != null && _audio.IsPlaying ? "AccentColor" : "SubTextColor") });
        titoli.Add(new Label { Text = song?.Title ?? Loc.T("Scegli una playlist"), FontSize = 16, FontFamily = Ui.FontBold, TextColor = Ui.C("TextColor"), MaxLines = 1, LineBreakMode = LineBreakMode.TailTruncation });
        titoli.Add(Ui.Meta(song?.Artist ?? (recenti.Count > 0 ? Loc.T("Le ultime che hai usato sono qui sotto") : Loc.T("Apri ARMusic per cercare un brano"))));
        top.Add(titoli, 1);
        v.Add(top);

        if (song != null)
        {
            var ctr = new Grid { ColumnDefinitions = new ColumnDefinitionCollection(new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Star)) };
            var buttons = new HorizontalStackLayout { Spacing = 4 };
            buttons.Add(Controllo(Ui.Icons.Prev, () => _audio.PlayPrevious(), Loc.T("Brano precedente")));
            var play = new Border
            {
                WidthRequest = 44, HeightRequest = 44, StrokeThickness = 0, StrokeShape = new RoundRectangle { CornerRadius = 22 },
                BackgroundColor = Ui.C("AccentColor")
            };
            _musicPlayIcon = Ui.Icon(_audio.IsPlaying ? Ui.Icons.Pause : Ui.Icons.Play, Ui.C("OnAccentColor"), 20);
            play.Content = _musicPlayIcon;
            Ui.Press(play, () => _audio.TogglePlayPause(), 0.94);
            SemanticProperties.SetDescription(play, Loc.T("Riproduci o metti in pausa"));
            buttons.Add(play);
            buttons.Add(Controllo(Ui.Icons.Next, () => _audio.PlayNext(), Loc.T("Brano successivo")));
            ctr.Add(buttons);
            _musicTime = new Label { Text = "", FontSize = 12, FontFamily = Ui.FontSemi, TextColor = Ui.C("SubTextColor"), HorizontalOptions = LayoutOptions.End, VerticalOptions = LayoutOptions.Center };
            ctr.Add(_musicTime, 1);
            v.Add(ctr);

            _musicTrack = new Grid { HeightRequest = 4 };
            _musicTrack.Add(new BoxView { Color = Ui.C("BorderColor"), CornerRadius = 2 });
            _musicFill = new BoxView { Color = Ui.C("AccentColor"), CornerRadius = 2, HorizontalOptions = LayoutOptions.Start, WidthRequest = 0 };
            _musicTrack.Add(_musicFill);
            _musicTrack.SizeChanged += (s, e) => OnTick(null, null);
            v.Add(_musicTrack);
        }

        if (recenti.Count > 0)
        {
            v.Add(Blocks.Hr());
            var r = new VerticalStackLayout();
            var cap = Ui.Caps(Loc.T("Riprendi"));
            cap.Padding = new Thickness(0, 4);
            r.Add(cap);
            foreach (var (pl, n) in recenti)
            {
                var g = new Grid
                {
                    ColumnDefinitions = new ColumnDefinitionCollection(new ColumnDefinition(40), new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto)),
                    ColumnSpacing = 12, MinimumHeightRequest = 52, Padding = new Thickness(0, 4), BackgroundColor = Colors.Transparent
                };
                g.Add(Copertina(null, 40, pl.CoverSource));
                var s = new VerticalStackLayout { Spacing = 0, VerticalOptions = LayoutOptions.Center };
                s.Add(Ui.RowTitle(pl.Name));
                s.Add(Ui.Meta(n == 1 ? Loc.T("1 brano") : Loc.F("{0} brani", n)));
                g.Add(s, 1);
                g.Add(Ui.Icon(Ui.Icons.Play, Ui.C("SubTextColor"), 16), 2);
                var plId = pl.Id;
                Ui.Press(g, async () =>
                {
                    var songs = await _db.GetSongsForPlaylistAsync(plId);
                    if (songs.Count > 0) _audio.PlayQueue(songs); else NavigateTo?.Invoke("Musica");
                }, 0.99);
                r.Add(g);
            }
            v.Add(r);
        }
        return Blocks.Section(Loc.T("Musica"), null, Ui.LinkLabel(Loc.T("Apri ARMusic"), () => NavigateTo?.Invoke("Musica")), v, new Thickness(20, 12, 20, 12));
    }

    private static View Controllo(string icon, Action onTap, string descr)
    {
        var g = new Grid { WidthRequest = 44, HeightRequest = 44, BackgroundColor = Colors.Transparent };
        g.Add(Ui.Icon(icon, Ui.C("TextColor"), 20));
        Ui.Press(g, onTap, 0.94);
        SemanticProperties.SetDescription(g, descr);
        return g;
    }

    // Copertina quadrata: immagine se c'è, altrimenti un disco geometrico neutro
    private static View Copertina(string url, double size, ImageSource source = null)
    {
        var b = new Border
        {
            WidthRequest = size, HeightRequest = size, StrokeShape = new RoundRectangle { CornerRadius = size >= 60 ? 8 : 6 },
            BackgroundColor = Ui.C("SurfaceRaisedColor"), Stroke = new SolidColorBrush(Ui.C("BorderColor")), StrokeThickness = 1,
            VerticalOptions = LayoutOptions.Center
        };
        var img = source ?? (string.IsNullOrEmpty(url) ? null : ImageSource.FromUri(new Uri(url)));
        var grid = new Grid();
        double d = size / 2;
        grid.Add(new Border
        {
            WidthRequest = d, HeightRequest = d, StrokeShape = new RoundRectangle { CornerRadius = d / 2 },
            Stroke = new SolidColorBrush(Ui.C("SubTextColor")), StrokeThickness = 1.5, BackgroundColor = Colors.Transparent,
            HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center,
            Content = new BoxView { WidthRequest = d / 4, HeightRequest = d / 4, CornerRadius = d / 8, Color = Ui.C("SubTextColor"), HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center }
        });
        if (img != null) grid.Add(new Image { Source = img, Aspect = Aspect.AspectFill });
        b.Content = grid;
        return b;
    }

    // ---------- Note fissate ----------
    private View NoteFissate(List<Note> notes)
    {
        var pinned = notes.Where(n => n.IsPinned && !n.IsPayment).OrderByDescending(n => n.UpdatedAt).ToList();
        var v = new VerticalStackLayout();
        bool first = true;
        foreach (var n in pinned.Take(6))
        {
            if (!first) v.Add(Blocks.Hr());
            first = false;
            var nn = n;
            v.Add(Blocks.NotaRiga(n, () => Open(new NoteEditorPage(_db, Refresh, nn))));
        }
        if (pinned.Count == 0)
        {
            var e = Ui.Small(Loc.T("Fissa una nota per ritrovarla sempre qui."));
            e.Padding = new Thickness(0, 8, 0, 4);
            v.Add(e);
        }
        return Blocks.Section(Loc.T("Note fissate"), pinned.Count, Ui.LinkLabel(Loc.T("Tutte le note"), () => NavigateTo?.Invoke("Note")), v);
    }

    // ---------- Stato della sincronizzazione ----------
    private View SyncFooter()
    {
        var f = new FlexLayout
        {
            Wrap = Microsoft.Maui.Layouts.FlexWrap.Wrap, AlignItems = Microsoft.Maui.Layouts.FlexAlignItems.Center,
            AlignContent = Microsoft.Maui.Layouts.FlexAlignContent.Start
        };
        Thickness gap = new(0, 4, 24, 4);
        var stato = new HorizontalStackLayout { Spacing = 8, Margin = gap };
        stato.Add(Ui.Dot(Ui.C(_sync.IsPaired ? "AccentColor" : "BorderStrongColor")));
        stato.Add(new Label { Text = _sync.IsPaired ? Loc.T("Sincronizzazione Wi-Fi attiva") : Loc.T("Sincronizzazione non attiva"), FontSize = 13, FontFamily = Ui.FontSemi, TextColor = Ui.C("TextColor"), VerticalOptions = LayoutOptions.Center });
        f.Add(stato);

        if (_sync.IsPaired)
        {
            var dev = new HorizontalStackLayout { Spacing = 16, Margin = gap };
            dev.Add(Dispositivo(_sync.IsHost ? Ui.Icons.Pc : Ui.Icons.Phone, _sync.DeviceName));
            if (_sync.IsHost) foreach (var d in _sync.Devices) dev.Add(Dispositivo(Ui.Icons.Phone, d.Name));
            else dev.Add(Dispositivo(Ui.Icons.Pc, _sync.PeerName));
            f.Add(dev);
            if (_sync.LastSync is DateTime t)
                f.Add(new Label { Text = Loc.T("Ultimo allineamento ") + GmailPage.Quando(t), FontSize = 13, FontFamily = Ui.FontRegular, TextColor = Ui.C("SubTextColor"), Margin = gap, VerticalOptions = LayoutOptions.Center });
        }
        else f.Add(new Label { Text = Loc.T("Abbina telefono e tablet per avere tutto ovunque"), FontSize = 13, FontFamily = Ui.FontRegular, TextColor = Ui.C("SubTextColor"), Margin = gap });

        var g = new Grid { ColumnDefinitions = new ColumnDefinitionCollection(new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto)), ColumnSpacing = 12 };
        g.Add(f);
        if (_sync.PendingConflicts > 0)
        {
            var c = new HorizontalStackLayout { Spacing = 8, VerticalOptions = LayoutOptions.Center };
            c.Add(new Label { Text = _sync.PendingConflicts == 1 ? Loc.T("1 conflitto") : Loc.F("{0} conflitti", _sync.PendingConflicts), FontSize = 13, FontFamily = Ui.FontRegular, TextColor = Ui.C("SubTextColor"), VerticalOptions = LayoutOptions.Center });
            c.Add(Blocks.Secondary(Loc.T("Risolvi"), OpenSync));
            g.Add(c, 1);
        }
        else g.Add(Ui.LinkLabel(_sync.IsPaired ? Loc.T("Dispositivi") : Loc.T("Abbina"), OpenSync), 1);
        return Ui.Card(g, new Thickness(16, 8));
    }

    private static View Dispositivo(string icon, string name)
    {
        var h = new HorizontalStackLayout { Spacing = 8 };
        h.Add(Ui.Icon(icon, Ui.C("SubTextColor"), 16));
        h.Add(new Label { Text = name, FontSize = 13, FontFamily = Ui.FontRegular, TextColor = Ui.C("SubTextColor"), VerticalOptions = LayoutOptions.Center });
        return h;
    }

    private void OpenItem(object source)
    {
        switch (source)
        {
            case Note n: Open(new NoteEditorPage(_db, Refresh, n)); break;
            case TaskItem t: Open(new TaskEditorPage(_db, Refresh, t)); break;
            case CalendarEvent e: Open(new EventEditorPage(_db, Refresh, e)); break;
        }
    }
}
