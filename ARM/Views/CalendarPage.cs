using ARM.Data;
using ARM.Models;
using Microsoft.Maui.Controls.Shapes;

namespace ARM.Views;

// Calendario (ARM 2.5): CALENDARIO / Mese ‹ ›, vista Mese o Settimana, agenda del giorno scelto e scorciatoie.
// Eventi, task con scadenza, pagamenti e promemoria nello stesso posto. Finestra larga (≥ 900): mese 60% · agenda 40%.
public class CalendarPage : ContentView
{
    private readonly DatabaseService _db;
    private readonly VerticalStackLayout _body = new() { Padding = new Thickness(20, 28, 20, 100), Spacing = 16 };
    private DateTime _month = new(DateTime.Today.Year, DateTime.Today.Month, 1);
    private DateTime _selected = DateTime.Today;
    private bool _week;
    private bool _wide;

    private List<Note> _notes = new();
    private List<TaskItem> _tasks = new();
    private List<CalendarEvent> _events = new();
    private List<Ordine> _ordini = new();

    private const int FirstHour = 7, LastHour = 22;
    private const double HourHeight = 30.4;

    public CalendarPage(DatabaseService db)
    {
        _db = db;
        var root = new Grid();
        root.Add(new ScrollView { Content = _body });
        root.Add(Ui.Fab(() => Open(new EventEditorPage(_db, Refresh, presetDate: _selected))));
        Content = root;
        SizeChanged += (s, e) =>
        {
            bool wide = Width >= 900;
            if (wide != _wide) { _wide = wide; Render(); }
        };
    }

    public async void Refresh()
    {
        _notes = await _db.GetAllNotesAsync();
        _tasks = await _db.GetAllTasksAsync();
        _events = await _db.GetAllEventsAsync();
        _ordini = await _db.GetAllOrdiniAsync();
        Render();
    }

    private static void Open(Page page) => Application.Current.MainPage.Navigation.PushModalAsync(page);

    private void Move(int delta)
    {
        if (_week)
        {
            _selected = _selected.AddDays(7 * delta);
            _month = new DateTime(_selected.Year, _selected.Month, 1);
        }
        else
        {
            _month = _month.AddMonths(delta);
            _selected = _month.Month == DateTime.Today.Month && _month.Year == DateTime.Today.Year ? DateTime.Today : _month;
        }
        Render();
    }

    private void Render()
    {
        BackgroundColor = Ui.C("BgColor");
        _body.Padding = Width >= 720 ? new Thickness(32, 32, 32, 100) : new Thickness(20, 28, 20, 100);
        _body.Children.Clear();

        // Elementi del periodo visibile (con margine per le settimane a cavallo)
        var from = (_week ? WeekStart(_selected) : _month).AddDays(-7);
        var agenda = Ui.BuildAgenda(_notes, _tasks, _events, from, _month.AddMonths(1).AddDays(14), _ordini);
        var byDay = agenda.GroupBy(i => i.Date).ToDictionary(g => g.Key, g => g.ToList());

        _body.Add(Header());
        var seg = new Ui.Segmented(new[] { ("mese", "Mese"), ("settimana", "Settimana") }, _week ? "settimana" : "mese", k => { _week = k == "settimana"; Render(); });
        var view = _week ? WeekView() : MonthGrid(byDay);
        var day = DayAgenda(byDay.TryGetValue(_selected.Date, out var items) ? items : new List<Ui.AgendaItem>());

        if (_wide)
        {
            var g = new Grid { ColumnSpacing = 24, ColumnDefinitions = new ColumnDefinitionCollection(new ColumnDefinition(new GridLength(3, GridUnitType.Star)), new ColumnDefinition(new GridLength(2, GridUnitType.Star))) };
            g.Add(new VerticalStackLayout { Spacing = 16, Children = { seg, view } });
            g.Add(day, 1);
            _body.Add(g);
        }
        else
        {
            _body.Add(seg);
            _body.Add(view);
            _body.Add(day);
        }
    }

    private static DateTime WeekStart(DateTime d) => d.Date.AddDays(-(((int)d.DayOfWeek + 6) % 7));

    private View Header()
    {
        var title = new VerticalStackLayout { Spacing = 2 };
        title.Add(Ui.Caps(_month.Year == DateTime.Today.Year ? Loc.T("Calendario") : Loc.F("Calendario · {0}", _month.Year)));
        title.Add(Ui.PageTitle(Ui.Cap(_month.ToString("MMMM", Ui.It))));

        var g = new Grid { ColumnDefinitions = new ColumnDefinitionCollection(new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto)), ColumnSpacing = 8 };
        g.Add(title);
        var nav = new HorizontalStackLayout { Spacing = 8, VerticalOptions = LayoutOptions.Center };
        bool atToday = _selected.Date == DateTime.Today && _month.Month == DateTime.Today.Month;
        if (!atToday)
            nav.Add(Ui.Chip(Loc.T("Oggi"), false, "TextColor", () =>
            {
                _month = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
                _selected = DateTime.Today;
                Render();
            }));
        nav.Add(Ui.IconButton(Ui.Icons.Left, () => Move(-1)));
        nav.Add(Ui.IconButton(Ui.Icons.Right, () => Move(1)));
        g.Add(nav, 1);
        return g;
    }

    // ---------- Mese ----------
    private View MonthGrid(Dictionary<DateTime, List<Ui.AgendaItem>> byDay)
    {
        // Lunedì come primo giorno della settimana
        int offset = ((int)_month.DayOfWeek + 6) % 7;
        var start = _month.AddDays(-offset);
        int weeks = (int)Math.Ceiling((offset + DateTime.DaysInMonth(_month.Year, _month.Month)) / 7.0);
        double cellH = _wide ? 64 : 50;

        var grid = new Grid { ColumnSpacing = 0, RowSpacing = 2 };
        for (int c = 0; c < 7; c++) grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
        grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        for (int r = 0; r < weeks; r++) grid.RowDefinitions.Add(new RowDefinition(cellH));

        string[] nomi = { Loc.T("Lun"), Loc.T("Mar"), Loc.T("Mer"), Loc.T("Gio"), Loc.T("Ven"), Loc.T("Sab"), Loc.T("Dom") };
        for (int c = 0; c < 7; c++)
        {
            var l = Ui.Caps(nomi[c]);
            l.HorizontalOptions = LayoutOptions.Center;
            l.Margin = new Thickness(0, 4, 0, 4);
            grid.Add(l, c, 0);
        }

        for (int i = 0; i < weeks * 7; i++)
        {
            var day = start.AddDays(i);
            grid.Add(DayCell(day, byDay.TryGetValue(day.Date, out var items) ? items : null), i % 7, i / 7 + 1);
        }

        // Scorrimento per cambiare mese
        var left = new SwipeGestureRecognizer { Direction = SwipeDirection.Left };
        left.Swiped += (s, e) => Move(1);
        var right = new SwipeGestureRecognizer { Direction = SwipeDirection.Right };
        right.Swiped += (s, e) => Move(-1);
        grid.GestureRecognizers.Add(left);
        grid.GestureRecognizers.Add(right);

        return Ui.Card(grid, new Thickness(8, 12));
    }

    // Cella: numero in un cerchio da 36 (scelto = Accent pieno, oggi = SoftAccent), sotto fino a 3 pallini da 5
    private View DayCell(DateTime day, List<Ui.AgendaItem> items)
    {
        bool inMonth = day.Month == _month.Month;
        bool isToday = day.Date == DateTime.Today;
        bool isSelected = day.Date == _selected.Date;

        var number = new Label
        {
            Text = day.Day.ToString(),
            FontSize = 15,
            FontFamily = isToday || isSelected ? Ui.FontHeavy : Ui.FontSemi,
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.Center,
            TextColor = isSelected ? Ui.C("OnAccentColor") : isToday ? Ui.C("AccentColor") : Ui.C(inMonth ? "TextColor" : "SubTextColor")
        };
        var circle = new Border
        {
            WidthRequest = 36, HeightRequest = 36,
            HorizontalOptions = LayoutOptions.Center,
            StrokeThickness = 0,
            StrokeShape = new RoundRectangle { CornerRadius = 18 },
            BackgroundColor = isSelected ? Ui.C("AccentColor") : isToday ? Ui.C("SoftAccentColor") : Colors.Transparent,
            Content = number
        };

        var dots = new HorizontalStackLayout { Spacing = 3, HorizontalOptions = LayoutOptions.Center, HeightRequest = 5 };
        if (items != null)
            foreach (var key in items.Where(i => !i.Done).Select(i => i.ColorKey).Distinct().Take(3))
                dots.Add(new BoxView { Color = Ui.C(key), WidthRequest = 5, HeightRequest = 5, CornerRadius = 2.5 });

        var cell = new VerticalStackLayout { Spacing = 3, Padding = new Thickness(0, 2, 0, 0), Opacity = inMonth ? 1 : 0.35, BackgroundColor = Colors.Transparent, Children = { circle, dots } };
        Ui.OnTap(cell, () =>
        {
            _selected = day.Date;
            if (!inMonth) _month = new DateTime(day.Year, day.Month, 1);
            Render();
        });
        return cell;
    }

    // ---------- Settimana ----------
    // Colonna delle ore (34) + 7 giorni; riga "tutto il giorno"; eventi con orario come blocchi nel loro colore
    private View WeekView()
    {
        var start = WeekStart(_selected);
        var days = Enumerable.Range(0, 7).Select(i => start.AddDays(i)).ToList();

        var head = new Grid { ColumnSpacing = 2, RowSpacing = 4 };
        head.ColumnDefinitions.Add(new ColumnDefinition(34));
        for (int i = 0; i < 7; i++) head.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
        head.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        head.RowDefinitions.Add(new RowDefinition(GridLength.Auto));

        var allDayLabel = new Label { Text = Loc.T("TUTTO IL GIORNO"), FontSize = 9, FontFamily = Ui.FontBold, TextColor = Ui.C("SubTextColor"), LineBreakMode = LineBreakMode.WordWrap };
        head.Add(allDayLabel, 0, 1);

        var body = new Grid { ColumnSpacing = 2, HeightRequest = (LastHour - FirstHour) * HourHeight };
        body.ColumnDefinitions.Add(new ColumnDefinition(34));
        for (int i = 0; i < 7; i++) body.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));

        // Ore
        var hours = new AbsoluteLayout();
        for (int h = FirstHour; h < LastHour; h += 2)
        {
            var l = new Label { Text = $"{h}", FontSize = 10, FontFamily = Ui.FontBold, TextColor = Ui.C("SubTextColor") };
            AbsoluteLayout.SetLayoutBounds(l, new Rect(0, (h - FirstHour) * HourHeight - 2, 34, 14));
            hours.Add(l);
        }
        body.Add(hours, 0);

        for (int i = 0; i < 7; i++)
        {
            var d = days[i];
            bool sel = d == _selected.Date;
            bool today = d == DateTime.Today;
            var dayBtn = new Border
            {
                MinimumHeightRequest = 52, StrokeThickness = 0, StrokeShape = new RoundRectangle { CornerRadius = 14 },
                BackgroundColor = sel ? Ui.C("AccentColor") : today ? Ui.C("SoftAccentColor") : Colors.Transparent,
                Content = new VerticalStackLayout { Spacing = 2, VerticalOptions = LayoutOptions.Center, Children =
                {
                    new Label { Text = d.ToString("ddd", Ui.It), FontSize = 11, FontFamily = Ui.FontBold, Opacity = 0.8, HorizontalOptions = LayoutOptions.Center, TextColor = Ui.C(sel ? "OnAccentColor" : "TextColor") },
                    new Label { Text = d.Day.ToString(), FontSize = 16, FontFamily = Ui.FontHeavy, HorizontalOptions = LayoutOptions.Center, TextColor = Ui.C(sel ? "OnAccentColor" : today ? "AccentColor" : "TextColor") }
                } }
            };
            var day = d;
            Ui.Press(dayBtn, () => { _selected = day; Render(); });
            head.Add(dayBtn, i + 1, 0);

            var dayEvents = _events.Where(e => e.Date.Date == d).ToList();
            var allDay = new VerticalStackLayout { Spacing = 2, MinimumHeightRequest = 24 };
            foreach (var e in dayEvents.Where(e => e.AllDay))
                allDay.Add(Block(e.Title, e.ColorKey ?? "Accent5Color", e));
            head.Add(allDay, i + 1, 1);

            var col = new AbsoluteLayout { BackgroundColor = Colors.Transparent };
            col.Add(new BoxView { Color = Ui.C("BorderColor"), WidthRequest = 1 });
            AbsoluteLayout.SetLayoutBounds(col.Children[0] as BoxView, new Rect(0, 0, 1, (LastHour - FirstHour) * HourHeight));
            foreach (var e in dayEvents.Where(e => !e.AllDay))
            {
                double top = Math.Max(0, (e.StartTime.TotalHours - FirstHour) * HourHeight);
                double h = Math.Max(22, (Math.Max(e.EndTime.TotalHours, e.StartTime.TotalHours + 0.5) - Math.Max(FirstHour, e.StartTime.TotalHours)) * HourHeight);
                var block = Block($"{e.StartTime:hh\\:mm} {e.Title}", e.ColorKey ?? "Accent5Color", e);
                AbsoluteLayout.SetLayoutBounds(block, new Rect(1, top, 1, h));
                AbsoluteLayout.SetLayoutFlags(block, Microsoft.Maui.Layouts.AbsoluteLayoutFlags.WidthProportional);
                col.Add(block);
            }
            body.Add(col, i + 1);
        }

        return Ui.Card(new VerticalStackLayout { Spacing = 10, Children = { head, body } }, new Thickness(10, 12));
    }

    private View Block(string text, string colorKey, CalendarEvent e)
    {
        var b = new Border
        {
            Padding = new Thickness(3), StrokeThickness = 0, StrokeShape = new RoundRectangle { CornerRadius = 6 },
            BackgroundColor = Ui.Soft(colorKey),
            Content = new Label { Text = text, FontSize = 9, FontFamily = Ui.FontBold, TextColor = Ui.C("TextColor"), LineBreakMode = LineBreakMode.CharacterWrap }
        };
        Ui.OnTap(b, () => Open(new EventEditorPage(_db, Refresh, e)));
        return b;
    }

    // ---------- Agenda del giorno ----------
    private View DayAgenda(List<Ui.AgendaItem> items)
    {
        var v = new VerticalStackLayout { Spacing = 12 };

        int diff = (_selected.Date - DateTime.Today).Days;
        string label = diff == 0 ? Loc.T("Oggi") : diff == 1 ? Loc.T("Domani") : diff == -1 ? Loc.T("Ieri") : Ui.Cap(_selected.ToString("dddd d MMMM", Ui.It));
        string count = items.Count == 0 ? "" : items.Count == 1 ? Loc.T("1 cosa") : Loc.F("{0} cose", items.Count);
        v.Add(Ui.SectionTitle(label, count));

        if (items.Count == 0)
        {
            var empty = new VerticalStackLayout { Spacing = 6, Padding = new Thickness(22) };
            var t = Ui.CardTitle(Loc.T("Giornata libera"), 0);
            t.HorizontalTextAlignment = TextAlignment.Center;
            var s = Ui.Small(Loc.T("Ancora niente qui… aggiungi qualcosa per questo giorno."));
            s.FontFamily = Ui.FontRegular;
            s.HorizontalTextAlignment = TextAlignment.Center;
            empty.Add(t);
            empty.Add(s);
            v.Add(Ui.Card(empty, new Thickness(0)));
        }
        else
        {
            var rows = new VerticalStackLayout();
            foreach (var item in items) rows.Add(Ui.AgendaRow(item, () => OpenItem(item.Source)));
            v.Add(Ui.Card(rows, new Thickness(0, 2)));
        }

        var actions = new FlexLayout { Wrap = Microsoft.Maui.Layouts.FlexWrap.Wrap };
        foreach (var chip in new View[]
        {
            Ui.Chip(Loc.T("Evento"), false, "Accent5Color", () => Open(new EventEditorPage(_db, Refresh, presetDate: _selected)), Ui.Icons.Plus),
            Ui.Chip(Loc.T("Task"), false, "AccentColor", () => Open(new TaskEditorPage(_db, Refresh, presetDate: _selected)), Ui.Icons.Plus),
            Ui.Chip(Loc.T("Pagamento"), false, "Accent2Color", () => Open(new NoteEditorPage(_db, Refresh, presetType: "Pagamento", presetDate: _selected)), Ui.Icons.Plus)
        })
        {
            chip.Margin = new Thickness(0, 0, 8, 8);
            actions.Add(chip);
        }
        v.Add(actions);
        return v;
    }

    private void OpenItem(object source)
    {
        switch (source)
        {
            case Note n: Open(new NoteEditorPage(_db, Refresh, n)); break;
            case TaskItem t: Open(new TaskEditorPage(_db, Refresh, t)); break;
            case CalendarEvent e: Open(new EventEditorPage(_db, Refresh, e)); break;
            case Ordine o: _ = ARM.Views.Dashboard.OrdineAzioni.ApriAsync(_db, o, Refresh); break;
        }
    }
}
