using ARMusic.Models;
using Microsoft.Maui.Controls.Shapes;

namespace ARMusic.Views;

// Card "Soldi del mese" (sezione Pagamenti):
// - intestazione ‹ SOLDI DEL MESE / Novembre 2026 ›, riga "Uscite X · Entrate Y"
// - ciambella 168 delle uscite per categoria (spessore 24, traccia FieldBg) con il totale al centro
// - legenda: categoria · barra · importo · percentuale (righe da 36)
// - ULTIMI 6 MESI: colonne toccabili con barre uscite (corallo) e entrate (verde), il mese scelto evidenziato
// Un pagamento conta nel mese della sua scadenza; quelli mensili contano in ogni mese da quando sono stati creati.
public class MoneyChart : ContentView
{
    private readonly Label _month = Ui.CardTitle("", 1);
    private readonly Label _summary = new() { FontSize = 13, FontFamily = Ui.FontSemi, HorizontalTextAlignment = TextAlignment.Center };
    private readonly VerticalStackLayout _legend = new() { Spacing = 2 };
    private readonly DonutDrawable _donut = new();
    private readonly GraphicsView _donutView;
    private readonly Grid _bars = new() { ColumnSpacing = 4 };
    private List<Note> _payments = new();
    private DateTime _selected = new(DateTime.Today.Year, DateTime.Today.Month, 1);

    public DateTime Selected { get => _selected; set => _selected = value; }
    public Action<DateTime> MonthChanged;

    public MoneyChart()
    {
        _donutView = new GraphicsView { Drawable = _donut, HeightRequest = 168, WidthRequest = 168, HorizontalOptions = LayoutOptions.Center };
        for (int i = 0; i < 6; i++) _bars.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));

        var head = new Grid { ColumnDefinitions = new ColumnDefinitionCollection(new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto)) };
        head.Add(Ui.IconButton(Ui.Icons.Left, () => { _selected = _selected.AddMonths(-1); Update(); }));
        var title = new VerticalStackLayout { Spacing = 2, HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center };
        var cap = Ui.Caps(Loc.T("Soldi del mese"));
        cap.HorizontalOptions = LayoutOptions.Center;
        _month.HorizontalOptions = LayoutOptions.Center;
        title.Add(cap);
        title.Add(_month);
        head.Add(title, 1);
        head.Add(Ui.IconButton(Ui.Icons.Right, () => { _selected = _selected.AddMonths(1); Update(); }), 2);

        var legendKey = new HorizontalStackLayout { Spacing = 10, HorizontalOptions = LayoutOptions.End, VerticalOptions = LayoutOptions.Center, Children = { Key(Loc.T("Uscite"), "Accent2Color"), Key(Loc.T("Entrate"), "Accent3Color") } };
        var barsHead = new Grid { ColumnDefinitions = new ColumnDefinitionCollection(new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto)) };
        var bc = Ui.Caps(Loc.T("Ultimi 6 mesi"));
        bc.VerticalOptions = LayoutOptions.Center;
        barsHead.Add(bc);
        barsHead.Add(legendKey, 1);

        var v = new VerticalStackLayout { Spacing = 14 };
        v.Add(head);
        v.Add(_summary);
        v.Add(_donutView);
        v.Add(_legend);
        v.Add(new BoxView { HeightRequest = 1, Color = Ui.C("BorderColor") });
        v.Add(barsHead);
        v.Add(_bars);
        Content = Ui.Card(v);

        static View Key(string text, string key) => new HorizontalStackLayout { Spacing = 4, Children =
        {
            new BoxView { Color = Ui.C(key), WidthRequest = 8, HeightRequest = 8, CornerRadius = 2, VerticalOptions = LayoutOptions.Center },
            new Label { Text = text, FontSize = 12, FontFamily = Ui.FontSemi, TextColor = Ui.C("SubTextColor"), VerticalOptions = LayoutOptions.Center }
        } };
    }

    public void SetData(List<Note> notes)
    {
        _payments = notes.Where(n => n.IsPayment).ToList();
        Update();
    }

    // Pagamenti che "pesano" su un certo mese
    public static IEnumerable<Note> InMonth(IEnumerable<Note> payments, DateTime month)
    {
        var first = new DateTime(month.Year, month.Month, 1);
        foreach (var n in payments.Where(n => n.IsPayment && n.Amount > 0))
        {
            if (n.IsMonthly)
            {
                var start = new DateTime(n.CreatedAt.Year, n.CreatedAt.Month, 1);
                if (first >= start) yield return n;
            }
            else if (n.DueDate is DateTime d && d.Year == month.Year && d.Month == month.Month) yield return n;
        }
    }

    private void Update()
    {
        MonthChanged?.Invoke(_selected);
        var mese = InMonth(_payments, _selected).ToList();
        var uscite = mese.Where(n => !n.IsIncome).GroupBy(Category.Of)
                         .Select(g => (Cat: g.Key, Total: g.Sum(n => n.Amount)))
                         .OrderByDescending(x => x.Total).ToList();
        double totUscite = uscite.Sum(x => x.Total);
        double totEntrate = mese.Where(n => n.IsIncome).Sum(n => n.Amount);

        _month.Text = Ui.Cap(_selected.ToString("MMMM yyyy", Ui.It));
        _summary.FormattedText = new FormattedString { Spans =
        {
            new Span { Text = Loc.T("Uscite "), TextColor = Ui.C("TextColor") },
            new Span { Text = Ui.Euro(totUscite), TextColor = Ui.C("Accent2Color"), FontFamily = Ui.FontHeavy },
            new Span { Text = Loc.T(" · Entrate "), TextColor = Ui.C("TextColor") },
            new Span { Text = Ui.Euro(totEntrate), TextColor = Ui.C("Accent3Color"), FontFamily = Ui.FontHeavy },
        } };

        _donut.Slices = uscite.Select(x => (x.Cat.Color, x.Total)).ToList();
        _donut.Center = Ui.Euro(totUscite);
        _donutView.Invalidate();

        _legend.Children.Clear();
        if (uscite.Count == 0)
        {
            var e = Ui.Small(Loc.T("Nessuna uscita in questo mese."));
            e.HorizontalTextAlignment = TextAlignment.Center;
            _legend.Add(e);
        }
        double max = uscite.Count > 0 ? uscite[0].Total : 1;
        foreach (var (cat, total) in uscite)
        {
            var g = new Grid
            {
                ColumnDefinitions = new ColumnDefinitionCollection(new ColumnDefinition(116), new ColumnDefinition(GridLength.Star), new ColumnDefinition(64), new ColumnDefinition(38)),
                ColumnSpacing = 10,
                MinimumHeightRequest = 36
            };
            var name = Ui.Small(cat.Label, "TextColor");
            name.MaxLines = 1;
            name.LineBreakMode = LineBreakMode.TailTruncation;
            name.VerticalOptions = LayoutOptions.Center;
            g.Add(name);

            double pct = Math.Max(0.03, total / max);
            var track = new Grid { HeightRequest = 6, VerticalOptions = LayoutOptions.Center };
            track.Add(new BoxView { Color = Ui.C("FieldBgColor"), CornerRadius = 3 });
            var fill = new BoxView { Color = cat.Color, CornerRadius = 3, HorizontalOptions = LayoutOptions.Start };
            track.Add(fill);
            track.SizeChanged += (s, e) => fill.WidthRequest = track.Width * pct;
            g.Add(track, 1);

            var amount = Ui.Small(Ui.Euro(total), "TextColor");
            amount.HorizontalTextAlignment = TextAlignment.End;
            amount.VerticalOptions = LayoutOptions.Center;
            g.Add(amount, 2);
            var p = Ui.Small($"{(totUscite > 0 ? total / totUscite : 0):P0}".Replace(" ", ""));
            p.HorizontalTextAlignment = TextAlignment.End;
            p.VerticalOptions = LayoutOptions.Center;
            g.Add(p, 3);
            _legend.Add(g);
        }

        // Ultimi 6 mesi fino a quello scelto (il mese scelto è l'ultima colonna)
        _bars.Children.Clear();
        var first = _selected.AddMonths(-5);
        var data = Enumerable.Range(0, 6).Select(i =>
        {
            var m = first.AddMonths(i);
            var list = InMonth(_payments, m).ToList();
            return (Month: m, Out: list.Where(n => !n.IsIncome).Sum(n => n.Amount), In: list.Where(n => n.IsIncome).Sum(n => n.Amount));
        }).ToList();
        double top = Math.Max(1, data.Max(d => Math.Max(d.Out, d.In)));
        for (int i = 0; i < 6; i++)
        {
            var (m, o, inn) = data[i];
            bool sel = i == 5;
            var pair = new HorizontalStackLayout { Spacing = 3, HeightRequest = 112, HorizontalOptions = LayoutOptions.Center };
            pair.Add(Bar(o / top, "Accent2Color"));
            pair.Add(Bar(inn / top, "Accent3Color"));
            var label = new Label
            {
                Text = Ui.Cap(m.ToString("MMM", Ui.It)), FontSize = 12, HorizontalOptions = LayoutOptions.Center,
                FontFamily = sel ? Ui.FontHeavy : Ui.FontSemi, TextColor = Ui.C(sel ? "TextColor" : "SubTextColor")
            };
            var col = new Border
            {
                StrokeThickness = 0, StrokeShape = new RoundRectangle { CornerRadius = 12 }, Padding = new Thickness(0, 8, 0, 6),
                BackgroundColor = sel ? Ui.C("SoftAccentColor") : Colors.Transparent,
                Content = new VerticalStackLayout { Spacing = 6, Children = { pair, label } }
            };
            var month = m;
            Ui.Press(col, () => { _selected = month; Update(); }, 0.97);
            _bars.Add(col, i);
        }

        static View Bar(double ratio, string key) => new BoxView
        {
            WidthRequest = 12,
            HeightRequest = Math.Max(2, ratio * 112),
            CornerRadius = 4,
            Color = Ui.C(key),
            VerticalOptions = LayoutOptions.End
        };
    }

    // Ciambella: raggio 52 su 140 (scalata a 168), spessore 20 → 24; si parte da mezzogiorno in senso orario
    private class DonutDrawable : IDrawable
    {
        public List<(Color Color, double Value)> Slices = new();
        public string Center = "";

        public void Draw(ICanvas canvas, RectF r)
        {
            float thick = 24;
            float size = Math.Min(r.Width, r.Height) * (104f / 140f);
            var box = new RectF(r.Center.X - size / 2, r.Center.Y - size / 2, size, size);
            canvas.StrokeSize = thick;
            canvas.StrokeLineCap = LineCap.Butt;

            canvas.StrokeColor = Ui.C("FieldBgColor");
            canvas.DrawEllipse(box);

            double total = Slices.Sum(s => s.Value);
            if (total > 0)
            {
                if (Slices.Count == 1)
                {
                    canvas.StrokeColor = Slices[0].Color;
                    canvas.DrawEllipse(box);
                }
                else
                {
                    float angle = 90;
                    foreach (var (color, value) in Slices)
                    {
                        float sweep = (float)(value / total * 360);
                        float gap = sweep > 4 ? 2f : 0;
                        canvas.StrokeColor = color;
                        canvas.DrawArc(box, angle, angle - sweep + gap, true, false);
                        angle -= sweep;
                    }
                }
            }

            canvas.FontColor = Ui.C("TextColor");
            canvas.FontSize = 22;
            canvas.Font = new Microsoft.Maui.Graphics.Font(Ui.FontHeavy);
            canvas.DrawString(Center, new RectF(r.X, r.Center.Y - 20, r.Width, 26), HorizontalAlignment.Center, VerticalAlignment.Center);
            canvas.FontColor = Ui.C("SubTextColor");
            canvas.FontSize = 12;
            canvas.Font = new Microsoft.Maui.Graphics.Font(Ui.FontSemi);
            canvas.DrawString("uscite", new RectF(r.X, r.Center.Y + 6, r.Width, 18), HorizontalAlignment.Center, VerticalAlignment.Center);
        }
    }
}
