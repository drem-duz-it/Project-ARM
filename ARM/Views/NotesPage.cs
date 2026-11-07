using ARM.Data;
using ARM.Models;
using ARM.Services;
using Microsoft.Maui.Controls.Shapes;

namespace ARM.Views;

// Note (ARM 2.5): titolo, ricerca, filtri; griglia "masonry" di card (2 colonne sul telefono, di più su schermi larghi).
// Filtro Pagamenti: riepilogo, "Soldi del mese" e righe Da pagare / Da ricevere / Pagati e ricevuti.
public class NotesPage : ContentView
{
    private readonly DatabaseService _db;
    private readonly VerticalStackLayout _body = new() { Padding = new Thickness(20, 28, 20, 100), Spacing = 16 };
    private readonly ContentView _head = new();
    private readonly ContentView _filtersHost = new();
    private readonly VerticalStackLayout _results = new() { Spacing = 16 };
    private Border _searchField;
    private Entry _search;
    private string _searchTheme;
    // Mese mostrato nel grafico "Soldi del mese" (il grafico si ricrea a ogni aggiornamento e riparte da qui)
    private DateTime _chartMonth = new(DateTime.Today.Year, DateTime.Today.Month, 1);
    private string _filter = "Tutte";
    private List<Note> _notes = new();
    private int _columns = 2;

    public NotesPage(DatabaseService db)
    {
        _db = db;
        var root = new Grid();
        root.Add(new ScrollView { Content = _body });
        root.Add(Ui.Fab(() => Open(new NoteEditorPage(_db, Refresh, presetType: _filter is "Tutte" ? null : _filter))));
        Content = root;

        // Colonne in base alla larghezza: card larghe almeno 160 (telefono 2, finestra larga fino a 5)
        SizeChanged += (s, e) =>
        {
            int cols = Math.Clamp((int)((Width - 40 + 12) / 212), 2, 5);
            if (cols != _columns) { _columns = cols; Render(); }
        };
    }

    public void SetFilter(string filter) { _filter = filter; Refresh(); }

    public async void Refresh()
    {
        _notes = await _db.GetAllNotesAsync();
        BuildStatic();
        Render();
    }

    private static void Open(Page page) => Application.Current.MainPage.Navigation.PushModalAsync(page);

    // Struttura fissa: [titolo] [ricerca] [filtri] [risultati]. La ricerca non viene mai spostata
    // (spostare un campo col cursore dentro fa crollare WinUI): si ricrea solo se cambia il tema.
    private void BuildStatic()
    {
        BackgroundColor = Ui.C("BgColor");
        if (_body.Children.Count == 0)
        {
            _body.Add(_head);
            _body.Add(_filtersHost);
            _body.Add(_results);
        }
        if (_searchField == null || _searchTheme != ThemeService.TemaAttuale)
        {
            string text = _search?.Text;
            if (_searchField != null) _body.Remove(_searchField);
            _search = Ui.Entry(Loc.T("Cerca nelle note"), text);
            _search.TextChanged += (s, e) => Render();
            var row = new Grid { ColumnDefinitions = new ColumnDefinitionCollection(new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Star)), ColumnSpacing = 10 };
            row.Add(Ui.Icon(Ui.Icons.Search, Ui.C("SubTextColor"), 20));
            row.Add(_search, 1);
            _searchField = Ui.Field(row);
            _searchTheme = ThemeService.TemaAttuale;
            _body.Insert(1, _searchField);
        }
    }

    private void Render()
    {
        if (_search == null) return;
        _body.Padding = Width >= 720 ? new Thickness(32, 32, 32, 100) : new Thickness(20, 28, 20, 100);

        // Titolo e sottotitolo
        int pagamentiAperti = _notes.Count(n => n.IsPayment && !n.IsPaid);
        int noteCount = _notes.Count(n => !n.IsPayment);
        string sub = (noteCount == 1 ? Loc.T("1 nota") : Loc.F("{0} note", noteCount)) + (pagamentiAperti > 0 ? " · " + (pagamentiAperti == 1 ? Loc.T("1 pagamento da fare") : Loc.F("{0} pagamenti da fare", pagamentiAperti)) : "");
        _head.Content = new VerticalStackLayout { Spacing = 4, Children = { Ui.PageTitle(Loc.T("Note")), Ui.Small(sub) } };

        // Filtri (chip monocromatiche)
        var filters = new HorizontalStackLayout { Spacing = 8 };
        foreach (var (key, label) in new[] { ("Tutte", Loc.T("Tutte")), ("Nota", Loc.T("Note")), ("Pagamento", Loc.T("Pagamenti")), ("Promemoria", Loc.T("Promemoria")), ("Idea", Loc.T("Idee")) })
        {
            var k = key;
            filters.Add(Ui.Chip(label, _filter == k, "TextColor", () => { _filter = k; Render(); }));
        }
        _filtersHost.Content = new ScrollView { Orientation = ScrollOrientation.Horizontal, HorizontalScrollBarVisibility = ScrollBarVisibility.Never, Content = filters };

        // Elenco filtrato
        var q = (_search.Text ?? "").Trim();
        bool Match(Note n) => q.Length == 0
            || (n.Title ?? "").Contains(q, StringComparison.OrdinalIgnoreCase)
            || (n.Content ?? "").Contains(q, StringComparison.OrdinalIgnoreCase)
            || (n.Checklist ?? "").Contains(q, StringComparison.OrdinalIgnoreCase);

        _results.Children.Clear();

        if (_filter == "Pagamento") { RenderPayments(_notes.Where(n => n.IsPayment).Where(Match).ToList()); return; }

        // In "Tutte" i pagamenti già chiusi non servono più
        var list = _notes.Where(n => _filter == "Tutte" ? !(n.IsPayment && n.IsPaid) : n.Type == _filter).Where(Match)
                         .OrderByDescending(n => n.IsPinned).ThenByDescending(n => n.UpdatedAt).ToList();
        if (list.Count == 0)
        {
            _results.Add(Ui.EmptyCard(q.Length > 0 ? Loc.T("Nessuna nota corrisponde alla ricerca.") : Loc.T("Tocca + per scrivere la prima nota, o scrivila al volo da Oggi."), Ui.Icons.Note));
            return;
        }
        _results.Add(Masonry(list));
    }

    // Griglia "masonry": ogni card va nella colonna più corta (stima dell'altezza dal contenuto)
    private View Masonry(List<Note> notes)
    {
        var cols = Enumerable.Range(0, _columns).Select(_ => new VerticalStackLayout { Spacing = 12 }).ToArray();
        var heights = new double[_columns];
        foreach (var n in notes)
        {
            int i = Array.IndexOf(heights, heights.Min());
            cols[i].Add(NoteCard(n, _db, Refresh));
            int lines = string.IsNullOrWhiteSpace(n.Content) ? 0 : Math.Min(6, (int)Math.Ceiling(n.Content.Length / 24.0));
            heights[i] += 70 + lines * 20 + Math.Min(6, n.ChecklistItems.Count) * 32 + (n.IsPayment ? 120 : 0);
        }
        var g = new Grid { ColumnSpacing = 12 };
        for (int i = 0; i < _columns; i++)
        {
            g.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            g.Add(cols[i], i);
        }
        return g;
    }

    // ---------- Pagamenti ----------
    private void RenderPayments(List<Note> pays)
    {
        if (pays.Count == 0)
        {
            var empty = new VerticalStackLayout { Spacing = 0, Children = { Ui.Empty(Loc.T("Aggiungi un pagamento o un'entrata: qui vedrai quanto esce e quanto arriva ogni mese."), Ui.Icons.Euro) } };
            var btn = Ui.Button(Loc.T("Nuovo pagamento"), () => Open(new NoteEditorPage(_db, Refresh, presetType: "Pagamento")), ghost: true, small: true);
            btn.HorizontalOptions = LayoutOptions.Center;
            btn.Margin = new Thickness(0, 0, 0, 24);
            empty.Add(btn);
            _results.Add(Ui.Card(empty, new Thickness(0)));
            return;
        }

        _results.Add(Summary());
        foreach (var a in Alerts()) _results.Add(a);

        var chart = new MoneyChart { Selected = _chartMonth, MonthChanged = m => _chartMonth = m };
        chart.SetData(_notes);
        _results.Add(chart);

        AddPaySection(Loc.T("Da pagare"), pays.Where(n => !n.IsPaid && !n.IsIncome).OrderBy(n => n.DueDate ?? DateTime.MaxValue));
        AddPaySection(Loc.T("Da ricevere"), pays.Where(n => !n.IsPaid && n.IsIncome).OrderBy(n => n.DueDate ?? DateTime.MaxValue));
        AddPaySection(Loc.T("Pagati e ricevuti"), pays.Where(n => n.IsPaid).OrderByDescending(n => n.LastPaidAt));
    }

    // Riepilogo: Da pagare (30 gg) · In arrivo (30 gg) · Saldo previsto
    private View Summary()
    {
        var aperti = _notes.Where(n => n.IsPayment && !n.IsPaid).ToList();
        var limite = DateTime.Today.AddDays(30);
        bool Entro30(Note n) => !n.DueDate.HasValue || n.DueDate.Value.Date <= limite;
        double daPagare = aperti.Where(n => !n.IsIncome && Entro30(n)).Sum(n => n.Amount);
        double daRicevere = aperti.Where(n => n.IsIncome && Entro30(n)).Sum(n => n.Amount);
        double saldo = daRicevere - daPagare;

        var v = new VerticalStackLayout();
        v.Add(Line(Loc.T("Da pagare"), "Accent2Color", Ui.Euro(-daPagare, daPagare > 0), 18, 44));
        v.Add(new BoxView { HeightRequest = 1, Color = Ui.C("BorderColor") });
        v.Add(Line(Loc.T("In arrivo"), "Accent3Color", Ui.Euro(daRicevere, daRicevere > 0), 18, 44));
        v.Add(new BoxView { HeightRequest = 1, Color = Ui.C("BorderColor") });
        v.Add(Line(Loc.T("Saldo previsto"), saldo >= 0 ? "Accent3Color" : "Accent2Color", Ui.Euro(saldo, saldo != 0), 22, 48, total: true));
        return Ui.Card(v, new Thickness(16, 6));

        static View Line(string label, string colorKey, string value, double size, double height, bool total = false)
        {
            var g = new Grid { ColumnDefinitions = new ColumnDefinitionCollection(new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto)), MinimumHeightRequest = height };
            var left = new HorizontalStackLayout { Spacing = 8, VerticalOptions = LayoutOptions.Center };
            if (!total) left.Add(new BoxView { WidthRequest = 8, HeightRequest = 8, CornerRadius = 4, Color = Ui.C(colorKey), VerticalOptions = LayoutOptions.Center });
            var l = new Label { FontSize = 13, FontFamily = total ? Ui.FontHeavy : Ui.FontSemi, TextColor = Ui.C("TextColor"), VerticalOptions = LayoutOptions.Center };
            l.FormattedText = new FormattedString { Spans = { new Span { Text = label } } };
            if (!total) l.FormattedText.Spans.Add(new Span { Text = Loc.T(" · 30 gg"), TextColor = Ui.C("SubTextColor") });
            left.Add(l);
            g.Add(left);
            g.Add(new Label { Text = value, FontSize = size, FontFamily = Ui.FontHeavy, TextColor = Ui.C(colorKey), VerticalOptions = LayoutOptions.Center }, 1);
            return g;
        }
    }

    // Avvisi: pagamenti scaduti, entrate in ritardo
    private IEnumerable<View> Alerts()
    {
        var aperti = _notes.Where(n => n.IsPayment && !n.IsPaid).ToList();
        var scaduti = aperti.Where(n => !n.IsIncome && n.IsOverdue).ToList();
        var ritardo = aperti.Where(n => n.IsIncome && n.IsOverdue).ToList();
        if (scaduti.Count > 0) yield return Alert((scaduti.Count == 1 ? Loc.F("1 pagamento scaduto · {0}", Ui.Euro(scaduti.Sum(n => n.Amount))) : Loc.F("{0} pagamenti scaduti · {1}", scaduti.Count, Ui.Euro(scaduti.Sum(n => n.Amount)))), "Accent2Color");
        if (ritardo.Count > 0) yield return Alert((ritardo.Count == 1 ? Loc.F("1 entrata in ritardo · {0}", Ui.Euro(ritardo.Sum(n => n.Amount))) : Loc.F("{0} entrate in ritardo · {1}", ritardo.Count, Ui.Euro(ritardo.Sum(n => n.Amount)))), "Accent4Color");

        static View Alert(string text, string key)
        {
            var g = new Grid { ColumnDefinitions = new ColumnDefinitionCollection(new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Star)), ColumnSpacing = 10 };
            g.Add(Ui.Icon(Ui.Icons.Warn, Ui.C(key), 20));
            var l = Ui.Small(text, "TextColor");
            l.VerticalOptions = LayoutOptions.Center;
            g.Add(l, 1);
            return new Border { MinimumHeightRequest = 44, Padding = new Thickness(12, 8), StrokeThickness = 0, StrokeShape = new RoundRectangle { CornerRadius = 14 }, BackgroundColor = Ui.Soft(key), Content = g };
        }
    }

    private void AddPaySection(string title, IEnumerable<Note> items)
    {
        var list = items.ToList();
        if (list.Count == 0) return;
        var rows = new VerticalStackLayout();
        foreach (var n in list) rows.Add(PayRow(n));
        _results.Add(new VerticalStackLayout { Spacing = 8, Children = { Ui.Caps(title), Ui.Card(rows, new Thickness(0, 2)) } });
    }

    // Riga di un pagamento: spunta · titolo + categoria + scadenza · importo
    private View PayRow(Note n)
    {
        var g = new Grid
        {
            ColumnDefinitions = new ColumnDefinitionCollection(new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto)),
            ColumnSpacing = 8,
            Padding = new Thickness(8, 6, 16, 6),
            MinimumHeightRequest = 64
        };
        g.Add(Ui.CheckCircle(n.IsPaid, n.PaymentColorKey, async () => { await _db.TogglePaymentAsync(n); Refresh(); }));

        var text = new VerticalStackLayout { Spacing = 4, VerticalOptions = LayoutOptions.Center, Padding = new Thickness(0, 6) };
        var title = Ui.Body(n.Title);
        title.FontFamily = Ui.FontBold;
        title.MaxLines = 1;
        title.LineBreakMode = LineBreakMode.TailTruncation;
        if (n.IsPaid) { title.TextDecorations = TextDecorations.Strikethrough; title.Opacity = 0.55; }
        text.Add(title);
        var meta = new HorizontalStackLayout { Spacing = 6 };
        meta.Add(CategoryBadge(n));
        meta.Add(DueLabel(n));
        text.Add(meta);
        g.Add(text, 1);

        var amount = new Label { Text = Ui.Euro(n.IsIncome ? n.Amount : -n.Amount, true), FontSize = 16, FontFamily = Ui.FontHeavy, TextColor = Ui.C(n.PaymentColorKey), VerticalOptions = LayoutOptions.Center };
        if (n.IsPaid) { amount.TextDecorations = TextDecorations.Strikethrough; amount.Opacity = 0.55; }
        g.Add(amount, 2);
        Ui.OnTap(text, () => Open(new NoteEditorPage(_db, Refresh, n)));
        return g;
    }

    // Badge della categoria: fondo tenue del colore della categoria
    public static Border CategoryBadge(Note n)
    {
        var cat = Category.Of(n);
        var b = Ui.Badge(cat.Label, "TextColor");
        b.BackgroundColor = cat.Color.WithAlpha((float)Ui.D("SoftAlpha", 0.13));
        return b;
    }

    // "Entro 27 nov", "Scaduto · 3 nov" (corallo), "Pagato · 2 nov"...
    public static Label DueLabel(Note n)
    {
        string text;
        bool late = false;
        if (!n.DueDate.HasValue) text = "";
        else
        {
            var d = n.DueDate.Value.Date;
            int diff = (d - DateTime.Today).Days;
            string shortD = d.ToString("d MMM", Ui.It);
            if (n.IsPaid) text = (n.IsIncome ? Loc.T("Ricevuto · ") : Loc.T("Pagato · ")) + (n.LastPaidAt ?? d).ToString("d MMM", Ui.It);
            else if (diff < 0) { text = (n.IsIncome ? Loc.T("In ritardo · ") : Loc.T("Scaduto · ")) + shortD; late = true; }
            else if (diff == 0) text = n.IsIncome ? Loc.T("Arriva oggi") : Loc.T("Scade oggi");
            else if (diff == 1) text = n.IsIncome ? Loc.T("Arriva domani") : Loc.T("Entro domani");
            else text = (n.IsIncome ? Loc.T("Arriva entro il ") : Loc.T("Entro ")) + shortD;
        }
        return new Label
        {
            Text = text, FontSize = 12, VerticalOptions = LayoutOptions.Center,
            FontFamily = late ? Ui.FontHeavy : Ui.FontSemi,
            TextColor = Ui.C(late ? "Accent2Color" : "SubTextColor")
        };
    }

    // Badge del tipo (o Uscita/Entrata per i pagamenti)
    public static Border TypeBadge(Note n) => n.IsPayment
        ? Ui.Badge(n.IsIncome ? Loc.T("Entrata") : Loc.T("Uscita"), n.PaymentColorKey, n.IsIncome ? Ui.Icons.ArrowDown : Ui.Icons.ArrowUp)
        : Ui.Badge(Loc.T(n.Type), Ui.TypeColorKey(n.Type), Ui.TypeIcon(n.Type));

    // ---------- Card di una nota ----------
    public static Border NoteCard(Note n, DatabaseService db, Action refresh)
    {
        var v = new VerticalStackLayout { Spacing = 8 };
        var items = n.ChecklistItems;

        // Riga superiore: tipo · avanzamento lista · puntina
        var top = new Grid { ColumnDefinitions = new ColumnDefinitionCollection(new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto)), ColumnSpacing = 6 };
        top.Add(n.IsPayment ? Ui.Badge(n.IsIncome ? Loc.T("Entrata") : Loc.T("Uscita"), n.PaymentColorKey, n.IsIncome ? Ui.Icons.ArrowDown : Ui.Icons.ArrowUp) : TypeBadge(n));
        Label progress = null;
        if (items.Count > 0)
        {
            var pb = Ui.Badge($"{items.Count(x => x.Done)}/{items.Count}", "TextColor");
            progress = (Label)((HorizontalStackLayout)pb.Content).Children.Last();
            top.Add(pb, 1);
        }
        if (n.IsPinned) top.Add(Ui.Icon(Ui.Icons.Pin, Ui.C("SubTextColor"), 18), 3);
        v.Add(top);

        if (!string.IsNullOrWhiteSpace(n.Title)) v.Add(Ui.CardTitle(n.Title));

        if (!string.IsNullOrWhiteSpace(n.Content))
        {
            var t = new Label { Text = n.Content.Trim(), FontSize = 14, FontFamily = Ui.FontRegular, TextColor = Ui.C("SubTextColor"), MaxLines = 6, LineBreakMode = LineBreakMode.TailTruncation, LineHeight = 1.2 };
            v.Add(t);
        }

        // Promemoria: quando suona
        if (n.Type == "Promemoria" && n.DueDate.HasValue)
        {
            var d = n.DueDate.Value;
            string when = d.ToString("ddd d MMM", Ui.It) + (d.TimeOfDay != TimeSpan.Zero ? " · " + d.ToString("HH:mm") : "");
            v.Add(n.IsOverdue ? Ui.Badge(Loc.T("Scaduto · ") + d.ToString("d MMM", Ui.It), "Accent2Color") : Ui.Badge(when, "Accent4Color"));
        }

        // Lista con le spunte: le prime 6 voci si spuntano dalla card
        if (items.Count > 0)
        {
            var list = new VerticalStackLayout { Spacing = 0 };
            for (int i = 0; i < Math.Min(items.Count, 6); i++)
            {
                int index = i;
                var label = new Label { Text = items[i].Text, FontSize = 14, FontFamily = Ui.FontSemi, TextColor = Ui.C("TextColor"), MaxLines = 2, LineBreakMode = LineBreakMode.TailTruncation, VerticalOptions = LayoutOptions.Center };
                if (items[i].Done) { label.TextDecorations = TextDecorations.Strikethrough; label.Opacity = 0.55; }
                var box = new Ui.CheckBox(items[i].Done, "AccentColor", async done =>
                {
                    label.TextDecorations = done ? TextDecorations.Strikethrough : TextDecorations.None;
                    label.Opacity = done ? 0.55 : 1;
                    await db.ToggleChecklistItemAsync(n, index);
                    var now = n.ChecklistItems;
                    if (progress != null) progress.Text = $"{now.Count(x => x.Done)}/{now.Count}";
                }, 20);
                var row = new Grid { ColumnDefinitions = new ColumnDefinitionCollection(new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Star)), ColumnSpacing = 8, MinimumHeightRequest = 32 };
                row.Add(box);
                row.Add(label, 1);
                list.Add(row);
            }
            if (items.Count > 6)
            {
                var more = Ui.Small(Loc.F("+ altre {0}", items.Count - 6));
                more.Padding = new Thickness(28, 4, 0, 0);
                list.Add(more);
            }
            v.Add(list);
        }

        // Pagamento: importo grande + spunta, categoria, scadenza, mensile
        if (n.IsPayment)
        {
            var row = new Grid { ColumnDefinitions = new ColumnDefinitionCollection(new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto)) };
            // Importo senza ",00" inutili e mai spezzato: i più lunghi usano un corpo più piccolo
            string amountText = Ui.Euro(n.IsIncome ? n.Amount : -n.Amount, true);
            var amount = new Label { Text = amountText, FontSize = amountText.Length > 9 ? 18 : amountText.Length > 7 ? 21 : 24, FontFamily = Ui.FontHeavy, CharacterSpacing = -0.6, TextColor = Ui.C(n.PaymentColorKey), VerticalOptions = LayoutOptions.Center, LineBreakMode = LineBreakMode.NoWrap };
            if (n.IsPaid) { amount.TextDecorations = TextDecorations.Strikethrough; amount.Opacity = 0.55; }
            row.Add(amount);
            var check = Ui.CheckCircle(n.IsPaid, n.PaymentColorKey, async () => { await db.TogglePaymentAsync(n); refresh(); });
            check.Margin = new Thickness(0, 0, -8, 0);
            row.Add(check, 1);
            v.Add(row);
            v.Add(CategoryBadge(n));
            var meta = new HorizontalStackLayout { Spacing = 6, Children = { DueLabel(n) } };
            if (n.IsMonthly) meta.Add(new HorizontalStackLayout { Spacing = 3, Children = { Ui.Icon(Ui.Icons.Repeat, Ui.C("SubTextColor"), 14), new Label { Text = Loc.T("Mensile"), FontSize = 12, FontFamily = Ui.FontSemi, TextColor = Ui.C("SubTextColor"), VerticalOptions = LayoutOptions.Center } } });
            v.Add(meta);
        }

        bool pastel = Array.IndexOf(Ui.NoteColors, n.ColorHex ?? "") > 0;
        var card = Ui.Card(v, new Thickness(14, 14, 14, 12), Ui.NoteBackground(n.ColorHex));
        if (pastel) card.Stroke = new SolidColorBrush(Colors.Transparent);
        Ui.OnTap(card, () => Application.Current.MainPage.Navigation.PushModalAsync(new NoteEditorPage(db, refresh, n)));
        return card;
    }
}
