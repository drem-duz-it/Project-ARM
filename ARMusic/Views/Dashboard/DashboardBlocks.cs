using ARMusic.Models;
using ARMusic.Services;
using Microsoft.Maui.Controls.Shapes;

namespace ARMusic.Views.Dashboard;

// Blocchi della dashboard "Oggi" (ARM 3.0, da Claude Design): card piatte raggio 12 con bordo 1,
// titoli di sezione in maiuscoletto, numeri tabellari, solo forme geometriche e icone lineari.
// Ogni blocco si ricostruisce intero a ogni aggiornamento: nessuno contiene campi di testo.
public static class Blocks
{
    public enum Modo { Telefono, Tablet, Desktop }

    // ---------- Contenitore di sezione ----------
    // Card con intestazione (titolo + conteggio + azione a destra) e contenuto
    public static Border Section(string title, int? count, View right, View content, Thickness? padding = null, string subtitle = null)
    {
        var head = new Grid { ColumnDefinitions = new ColumnDefinitionCollection(new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto)), MinimumHeightRequest = 44, ColumnSpacing = 12 };
        var left = new FlexLayout { Wrap = Microsoft.Maui.Layouts.FlexWrap.Wrap, AlignItems = Microsoft.Maui.Layouts.FlexAlignItems.Center, VerticalOptions = LayoutOptions.Center };
        var t = Ui.Caps(title, "TextColor");
        t.FontSize = 13;
        t.Margin = new Thickness(0, 0, 8, 0);
        left.Add(t);
        if (count.HasValue) left.Add(new Label { Text = count.Value.ToString(), FontSize = 13, FontFamily = Ui.FontSemi, TextColor = Ui.C("SubTextColor"), Margin = new Thickness(0, 0, 8, 0) });
        if (subtitle != null) left.Add(Ui.Small(subtitle));
        head.Add(left);
        if (right != null) head.Add(right, 1);

        var v = new VerticalStackLayout { Spacing = 4 };
        v.Add(head);
        if (content != null) v.Add(content);
        return Ui.Card(v, padding ?? new Thickness(20, 12, 20, 16));
    }

    // Pulsante secondario 44 col bordo forte (Aggiorna, Risolvi…)
    public static Border Secondary(string text, Action onTap, string icon = null) => Ui.Button(text, onTap, icon, ghost: true, small: true);

    public static BoxView Hr(double marginH = 0) => new() { HeightRequest = 1, Color = Ui.C("BorderColor"), Margin = new Thickness(marginH, 0) };

    // ---------- Indicatori ----------
    public static View Kpi(string value, string label, string context, string contextKey, Action onTap)
    {
        var v = new VerticalStackLayout { Spacing = 4 };
        v.Add(new Label
        {
            Text = value, FontSize = value.Length > 9 ? 24 : value.Length > 6 ? 26 : 32, FontFamily = Ui.FontBold, CharacterSpacing = -0.6,
            TextColor = Ui.C("TextColor"), MaxLines = 1, LineBreakMode = LineBreakMode.NoWrap, MinimumHeightRequest = 38, VerticalTextAlignment = TextAlignment.End
        });
        var l = new Label { Text = label, FontSize = 13, FontFamily = Ui.FontSemi, TextColor = Ui.C("TextColor"), Margin = new Thickness(0, 4, 0, 0) };
        v.Add(l);
        v.Add(new Label { Text = context, FontSize = 13, FontFamily = Ui.FontRegular, TextColor = Ui.C(contextKey), MaxLines = 2, LineBreakMode = LineBreakMode.TailTruncation });
        var card = Ui.Card(v, new Thickness(20));
        Ui.Press(card, onTap, 0.98);
        return card;
    }

    // ---------- Tracciato a 4 tappe ----------
    // Cerchi 10, linee 2. Pieni = tappe fatte, contorno = future. Rosso solo se in ritardo (linea e cerchio successivi).
    public static Grid StatoIndicator(Ordine o, double width = 112)
    {
        var g = new Grid { WidthRequest = width, HeightRequest = 10, ColumnSpacing = 0, HorizontalOptions = LayoutOptions.Start, VerticalOptions = LayoutOptions.Center };
        for (int i = 0; i < 7; i++) g.ColumnDefinitions.Add(new ColumnDefinition(i % 2 == 0 ? new GridLength(10) : GridLength.Star));
        int tappa = o.Tappa;
        bool late = o.InRitardo;
        var ink = Ui.C("AccentColor");
        var off = Ui.C("BorderStrongColor");
        var red = Ui.C("WarnColor");
        for (int c = 1; c <= 4; c++)
        {
            bool fatto = c <= tappa;
            bool rossa = late && c == tappa + 1;
            g.Add(new Border
            {
                WidthRequest = 10, HeightRequest = 10,
                StrokeShape = new RoundRectangle { CornerRadius = 5 },
                StrokeThickness = fatto ? 0 : 1.5,
                Stroke = new SolidColorBrush(rossa ? red : off),
                BackgroundColor = fatto ? ink : Colors.Transparent
            }, (c - 1) * 2);
            if (c < 4)
            {
                var col = c < tappa ? ink : late && c == tappa ? red : off;
                g.Add(new BoxView { HeightRequest = 2, Color = col, VerticalOptions = LayoutOptions.Center }, (c - 1) * 2 + 1);
            }
        }
        SemanticProperties.SetDescription(g, Loc.F("Stato: {0}, tappa {1} di 4", o.StatoTesto, tappa));
        return g;
    }

    // Testi della colonna "Consegna": principale (Oggi / Domani / ven 6 nov) e secondario
    public static (string Main, string Sub, bool Red) ConsegnaTesti(Ordine o)
    {
        if (o.Consegnato)
            return (Breve(o.ConsegnatoIl ?? o.Aggiornato), Loc.T("Consegnato"), false);
        if (!o.ConsegnaPrevista.HasValue) return (Loc.T("Da definire"), Loc.T("Attesa conferma"), false);
        var d = o.ConsegnaPrevista.Value.Date;
        if (o.InRitardo) return (Breve(d), o.GiorniRitardo == 1 ? Loc.T("1 giorno di ritardo") : Loc.F("{0} giorni di ritardo", o.GiorniRitardo), true);
        if (d == DateTime.Today) return (Loc.T("Oggi"), o.FasciaOraria ?? Loc.T("In giornata"), false);
        if (d == DateTime.Today.AddDays(1)) return (Loc.T("Domani"), Breve(d), false);
        return (Breve(d), Loc.T("Prevista"), false);
    }

    public static string Breve(DateTime d) => d.ToString(d.Year == DateTime.Today.Year ? "ddd d MMM" : "d MMM yyyy", Ui.It).Replace(".", "");

    // Riga ordine larga: prodotto e numero · consegna · stato (griglia * / colConsegna / colStato)
    public static View OrdineRowLarga(Ordine o, double colConsegna, double colStato, Action onTap)
    {
        var g = new Grid
        {
            ColumnDefinitions = new ColumnDefinitionCollection(new ColumnDefinition(GridLength.Star), new ColumnDefinition(colConsegna), new ColumnDefinition(colStato)),
            ColumnSpacing = 16, Padding = new Thickness(20, 12), MinimumHeightRequest = 68, BackgroundColor = Colors.Transparent
        };
        var a = new VerticalStackLayout { Spacing = 2, VerticalOptions = LayoutOptions.Center };
        a.Add(Ui.RowTitle(o.Prodotto));
        a.Add(Numero(o.NumeroOrdine));
        g.Add(a);

        var (main, sub, red) = ConsegnaTesti(o);
        var b = new VerticalStackLayout { Spacing = 2, VerticalOptions = LayoutOptions.Center };
        b.Add(new Label { Text = main, FontSize = 15, FontFamily = Ui.FontSemi, TextColor = Ui.C(red ? "WarnColor" : "TextColor"), MaxLines = 1, LineBreakMode = LineBreakMode.TailTruncation });
        b.Add(Ui.Meta(sub));
        g.Add(b, 1);

        var c = new VerticalStackLayout { Spacing = 6, VerticalOptions = LayoutOptions.Center };
        c.Add(StatoIndicator(o, Math.Min(112, colStato - 8)));
        c.Add(new Label { Text = o.StatoTesto, FontSize = 13, FontFamily = Ui.FontSemi, TextColor = Ui.C(o.InRitardo ? "WarnColor" : "TextColor") });
        g.Add(c, 2);
        Ui.Press(g, onTap, 0.99);
        return g;
    }

    // Riga ordine su telefono: prodotto, poi numero ↔ consegna, poi tracciato + etichetta
    public static View OrdineRowStretta(Ordine o, Action onTap)
    {
        var v = new VerticalStackLayout { Spacing = 8, Padding = new Thickness(16, 14), BackgroundColor = Colors.Transparent };
        v.Add(Ui.RowTitle(o.Prodotto));
        // Riga 2: numero d'ordine · giorno di consegna (rosso se in ritardo)
        var (main, sub, red) = ConsegnaTesti(o);
        var r1 = new Grid { ColumnDefinitions = new ColumnDefinitionCollection(new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto)), ColumnSpacing = 12, Margin = new Thickness(0, -4, 0, 0) };
        r1.Add(Numero(o.NumeroOrdine));
        r1.Add(new Label { Text = o.Consegnato ? Loc.F("Consegnato {0}", main) : main, FontSize = 13, FontFamily = Ui.FontSemi, TextColor = Ui.C(red ? "WarnColor" : "TextColor"), MaxLines = 1, HorizontalTextAlignment = TextAlignment.End }, 1);
        v.Add(r1);
        // Riga 3: tracciato · stato (+ fascia oraria o giorni di ritardo)
        string dettaglio = red ? (o.GiorniRitardo == 1 ? Loc.T("1 giorno") : Loc.F("{0} giorni", o.GiorniRitardo)) : main == Loc.T("Oggi") && sub != Loc.T("In giornata") ? sub.ToLower(Ui.It) : null;
        var r = new Grid { ColumnDefinitions = new ColumnDefinitionCollection(new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Star)), ColumnSpacing = 12 };
        r.Add(StatoIndicator(o, 72));
        r.Add(new Label { Text = dettaglio == null ? o.StatoTesto : $"{o.StatoTesto} · {dettaglio}", FontSize = 13, FontFamily = Ui.FontSemi, TextColor = Ui.C(o.InRitardo ? "WarnColor" : "TextColor"), MaxLines = 1, LineBreakMode = LineBreakMode.TailTruncation, VerticalOptions = LayoutOptions.Center }, 1);
        v.Add(r);
        Ui.Press(v, onTap, 0.99);
        return v;
    }

    private static Label Numero(string n) => new()
    {
        Text = string.IsNullOrEmpty(n) ? Loc.T("Numero non disponibile") : n,
        FontSize = 13, FontFamily = Ui.FontRegular, CharacterSpacing = 0.2, TextColor = Ui.C("SubTextColor"), MaxLines = 1
    };

    // Stato del blocco ordini senza righe: vuoto / Gmail non collegato / errore (icona in un quadrato 44 col bordo)
    public static View StatoBlocco(string icon, string iconKey, string titolo, string testo, params View[] azioni)
    {
        var v = new VerticalStackLayout { Spacing = 6, Padding = new Thickness(20, 28, 20, 28), HorizontalOptions = LayoutOptions.Fill };
        v.Add(new Border
        {
            WidthRequest = 44, HeightRequest = 44, BackgroundColor = Colors.Transparent,
            Stroke = new SolidColorBrush(Ui.C("BorderStrongColor")), StrokeThickness = 1,
            StrokeShape = new RoundRectangle { CornerRadius = 10 },
            Content = Ui.Icon(icon, Ui.C(iconKey), 20), HorizontalOptions = LayoutOptions.Center, Margin = new Thickness(0, 0, 0, 8)
        });
        var t = new Label { Text = titolo, FontSize = 15, FontFamily = Ui.FontSemi, TextColor = Ui.C("TextColor"), HorizontalTextAlignment = TextAlignment.Center };
        v.Add(t);
        var s = Ui.Small(testo);
        s.HorizontalTextAlignment = TextAlignment.Center;
        s.MaximumWidthRequest = 420;
        s.HorizontalOptions = LayoutOptions.Center;
        v.Add(s);
        if (azioni.Length > 0)
        {
            var row = new HorizontalStackLayout { Spacing = 8, HorizontalOptions = LayoutOptions.Center, Margin = new Thickness(0, 12, 0, 0) };
            foreach (var a in azioni) row.Add(a);
            v.Add(row);
        }
        return v;
    }

    // ---------- Agenda di oggi (linea temporale) ----------
    public class VoceAgenda
    {
        public TimeSpan? Inizio, Fine;
        public bool TuttoIlGiorno;
        public string Titolo, Sotto, ColorKey;
        public object Sorgente;
    }

    public static View AgendaRiga(VoceAgenda v, bool passato, Action onTap)
    {
        var g = new Grid
        {
            ColumnDefinitions = new ColumnDefinitionCollection(new ColumnDefinition(44), new ColumnDefinition(3), new ColumnDefinition(GridLength.Star)),
            ColumnSpacing = 12, Padding = new Thickness(0, 8), MinimumHeightRequest = 52, Opacity = passato ? 0.6 : 1, BackgroundColor = Colors.Transparent
        };
        var t = new VerticalStackLayout { Spacing = 2, VerticalOptions = LayoutOptions.Start };
        if (v.TuttoIlGiorno) t.Add(new Label { Text = Loc.T("Tutto il giorno"), FontSize = 11, FontFamily = Ui.FontSemi, TextColor = Ui.C("SubTextColor"), LineBreakMode = LineBreakMode.WordWrap });
        else
        {
            t.Add(new Label { Text = v.Inizio?.ToString(@"hh\:mm"), FontSize = 14, FontFamily = Ui.FontSemi, TextColor = Ui.C(passato ? "SubTextColor" : "TextColor") });
            if (v.Fine.HasValue) t.Add(new Label { Text = v.Fine.Value.ToString(@"hh\:mm"), FontSize = 12, FontFamily = Ui.FontRegular, TextColor = Ui.C("SubTextColor") });
        }
        g.Add(t);
        g.Add(new BoxView { Color = Ui.C(v.ColorKey), CornerRadius = 1.5, WidthRequest = 3, VerticalOptions = LayoutOptions.Fill }, 1);
        var s = new VerticalStackLayout { Spacing = 2, VerticalOptions = LayoutOptions.Center };
        s.Add(Ui.RowTitle(v.Titolo, passato ? "SubTextColor" : "TextColor"));
        if (!string.IsNullOrWhiteSpace(v.Sotto)) s.Add(Ui.Meta(v.Sotto));
        g.Add(s, 2);
        Ui.Press(g, onTap, 0.99);
        return g;
    }

    // Linea "adesso" nell'accento del tema
    public static View Adesso()
    {
        var g = new Grid { ColumnDefinitions = new ColumnDefinitionCollection(new ColumnDefinition(44), new ColumnDefinition(GridLength.Star)), ColumnSpacing = 12, Padding = new Thickness(0, 2) };
        g.Add(new Label { Text = DateTime.Now.ToString("HH:mm"), FontSize = 12, FontFamily = Ui.FontBold, TextColor = Ui.C("AccentColor"), VerticalOptions = LayoutOptions.Center });
        var line = new Grid { VerticalOptions = LayoutOptions.Center, Margin = new Thickness(-3, 0, 0, 0) };
        line.Add(new BoxView { HeightRequest = 1, Color = Ui.C("AccentColor"), VerticalOptions = LayoutOptions.Center });
        line.Add(new BoxView { WidthRequest = 8, HeightRequest = 8, CornerRadius = 4, Color = Ui.C("AccentColor"), HorizontalOptions = LayoutOptions.Start });
        g.Add(line, 1);
        SemanticProperties.SetDescription(g, Loc.T("Ora attuale ") + DateTime.Now.ToString("HH:mm"));
        return g;
    }

    // ---------- Task ----------
    public static View TaskRiga(TaskItem t, Action onToggle, Action onOpen)
    {
        var g = new Grid
        {
            ColumnDefinitions = new ColumnDefinitionCollection(new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto), new ColumnDefinition(72)),
            ColumnSpacing = 8, MinimumHeightRequest = 52, Padding = new Thickness(0, 4)
        };
        var check = Ui.CheckCircle(t.IsDone, "TextColor", onToggle);
        check.Margin = new Thickness(-12, 0, 0, 0);
        g.Add(check);
        var s = new VerticalStackLayout { Spacing = 2, VerticalOptions = LayoutOptions.Center, BackgroundColor = Colors.Transparent };
        s.Add(Ui.RowTitle(t.Title));
        string meta = !string.IsNullOrWhiteSpace(t.Notes) ? t.Notes.Split('\n')[0].Trim() : Loc.F("Priorità {0}", Ui.PriorityText(t.Priority).ToLower(Ui.It));
        s.Add(Ui.Meta(meta));
        g.Add(s, 1);
        Ui.OnTap(s, onOpen);
        g.Add(Priorita(t.Priority), 2);
        string due; bool red = t.IsOverdue;
        if (!t.DueDate.HasValue) due = "";
        else if (t.IsOverdue) due = (DateTime.Today - t.DueDate.Value.Date).Days == 1 ? Loc.T("Ieri") : Breve(t.DueDate.Value);
        else due = t.DueDate.Value.TimeOfDay != TimeSpan.Zero ? t.DueDate.Value.ToString("HH:mm") : Loc.T("Oggi");
        g.Add(new Label { Text = due, FontSize = 13, FontFamily = Ui.FontSemi, TextColor = Ui.C(red ? "WarnColor" : "SubTextColor"), HorizontalTextAlignment = TextAlignment.End, VerticalOptions = LayoutOptions.Center, MaxLines = 1 }, 3);
        return g;
    }

    // Priorità come quadratino 8: alta = pieno accento, normale = pieno grigio, bassa = contorno
    public static View Priorita(int p)
    {
        var b = new Border
        {
            WidthRequest = 8, HeightRequest = 8, StrokeShape = new RoundRectangle { CornerRadius = 2 }, VerticalOptions = LayoutOptions.Center,
            BackgroundColor = p == 2 ? Ui.C("AccentColor") : p == 1 ? Ui.C("SubTextColor") : Colors.Transparent,
            Stroke = new SolidColorBrush(Ui.C("BorderStrongColor")), StrokeThickness = p == 0 ? 1.5 : 0
        };
        SemanticProperties.SetDescription(b, Loc.T("Priorità ") + Ui.PriorityText(p).ToLower(Ui.It));
        return b;
    }

    public static Label SottoTitolo(string text, string colorKey, int count)
    {
        var l = new Label { FontSize = 12, Padding = new Thickness(0, 12, 0, 4) };
        l.FormattedText = new FormattedString
        {
            Spans =
            {
                new Span { Text = text.ToUpper(Ui.It), FontFamily = Ui.FontBold, CharacterSpacing = 1, TextColor = Ui.C(colorKey) },
                new Span { Text = "  " + count, FontFamily = Ui.FontSemi, TextColor = Ui.C("SubTextColor") }
            }
        };
        return l;
    }

    // ---------- Pagamenti ----------
    public static View PagamentoRiga(Note n, Action onOpen)
    {
        var g = new Grid
        {
            ColumnDefinitions = new ColumnDefinitionCollection(new ColumnDefinition(8), new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto)),
            ColumnSpacing = 12, MinimumHeightRequest = 52, Padding = new Thickness(0, 4), BackgroundColor = Colors.Transparent
        };
        var cat = Category.Of(n);
        g.Add(Ui.Dot(cat.Color));
        var s = new VerticalStackLayout { Spacing = 2, VerticalOptions = LayoutOptions.Center };
        s.Add(Ui.RowTitle(n.Title));
        bool scaduto = n.IsOverdue;
        string meta = scaduto ? Loc.F("Scaduto {0}", Breve(n.DueDate.Value)) : $"{Breve(n.DueDate.Value)} · {cat.Label}";
        s.Add(Ui.Meta(meta, scaduto ? "WarnColor" : "SubTextColor"));
        g.Add(s, 1);
        g.Add(new Label
        {
            Text = n.IsIncome ? Ui.Euro(n.Amount, true) : Ui.Euro(n.Amount),
            FontSize = 15, FontFamily = Ui.FontBold, TextColor = Ui.C(n.IsIncome ? "Accent3Color" : "TextColor"),
            VerticalOptions = LayoutOptions.Center, LineBreakMode = LineBreakMode.NoWrap
        }, 2);
        Ui.Press(g, onOpen, 0.99);
        return g;
    }

    public static View Totale(string label, string value, string colorKey)
    {
        var v = new VerticalStackLayout { Spacing = 2 };
        v.Add(Ui.Meta(label));
        v.Add(new Label { Text = value, FontSize = 20, FontFamily = Ui.FontBold, CharacterSpacing = -0.3, TextColor = Ui.C(colorKey), MaxLines = 1, LineBreakMode = LineBreakMode.NoWrap });
        return v;
    }

    // ---------- Note fissate ----------
    public static View NotaRiga(Note n, Action onOpen)
    {
        var v = new VerticalStackLayout { Spacing = 4, Padding = new Thickness(0, 12), BackgroundColor = Colors.Transparent };
        var top = new Grid { ColumnDefinitions = new ColumnDefinitionCollection(new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto)), ColumnSpacing = 12 };
        top.Add(Ui.RowTitle(n.Title));
        var tag = new HorizontalStackLayout { Spacing = 6, VerticalOptions = LayoutOptions.Center };
        var dot = Ui.NoteDot(n.ColorHex);
        if (dot != null) tag.Add(Ui.Dot(dot));
        tag.Add(new Label { Text = Loc.T(n.Type), FontSize = 12, FontFamily = Ui.FontSemi, TextColor = Ui.C("SubTextColor"), VerticalOptions = LayoutOptions.Center });
        top.Add(tag, 1);
        v.Add(top);
        var items = n.ChecklistItems;
        string anteprima = items.Count > 0
            ? Loc.F("{0} di {1} completati · ", items.Count(i => i.Done), items.Count) + string.Join(", ", items.Where(i => !i.Done).Take(3).Select(i => i.Text))
            : (n.Content ?? "").Replace("\n", " ").Trim();
        if (anteprima.Length > 0) v.Add(Ui.Meta(anteprima));
        Ui.Press(v, onOpen, 0.99);
        return v;
    }
}
