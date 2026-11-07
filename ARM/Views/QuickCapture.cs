using ARM.Data;
using ARM.Models;
using ARM.Services;
using Microsoft.Maui.Controls.Shapes;
using Path = Microsoft.Maui.Controls.Shapes.Path;

namespace ARM.Views;

// Campo "Scrivi al volo" (3.0): scrivi una frase normale e ARM capisce se è un task, un evento, un pagamento...
// Card con: campo 52 (superficie rialzata, bordo forte) + pulsante crea → "Verrà creato" (tipo modificabile) e riepilogo
// → conferma "Aggiunto". Nessuna emoji.
public class QuickCaptureBar : ContentView
{
    private readonly DatabaseService _db;
    private readonly Action _onSaved;
    private readonly Entry _entry;
    private readonly Border _field;
    private readonly Border _go;
    private readonly Path _goIcon;
    private readonly Ui.Segmented _kinds;
    private readonly HorizontalStackLayout _summary = new() { Spacing = 8 };
    private readonly VerticalStackLayout _previewBox;
    private readonly Grid _doneBox;
    private readonly Label _done;
    private readonly Label _hint;
    private QuickEntry _parsed;
    private bool _updatingKinds;

    public QuickCaptureBar(DatabaseService db, Action onSaved, bool large = false, bool showHint = true)
    {
        _db = db;
        _onSaved = onSaved;

        _entry = Ui.Entry(Loc.T("Scrivi al volo…"), null, large ? 17 : 15);
        _entry.ReturnType = ReturnType.Send;
        _entry.TextChanged += (s, e) => UpdatePreview();
        _entry.Completed += async (s, e) => await SaveAsync();
        _entry.Focused += (s, e) => SetFocus(true);
        _entry.Unfocused += (s, e) => SetFocus(false);

        // Pulsante "crea": 44, raggio 8, pieno nell'accento quando c'è del testo
        _goIcon = Ui.Icon(Ui.Icons.Arrow, Ui.C("SubTextColor"), 20);
        _go = new Border
        {
            WidthRequest = 44, HeightRequest = 44,
            StrokeThickness = 0,
            StrokeShape = new RoundRectangle { CornerRadius = 8 },
            BackgroundColor = Colors.Transparent,
            VerticalOptions = LayoutOptions.Center,
            Content = _goIcon
        };
        Ui.Press(_go, async () => await SaveAsync());

        var row = new Grid
        {
            ColumnDefinitions = new ColumnDefinitionCollection(new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Auto)),
            ColumnSpacing = 12,
            Padding = new Thickness(16, 0, 4, 0),
            MinimumHeightRequest = 52
        };
        row.Add(Ui.Icon(Ui.Icons.Edit, Ui.C("SubTextColor"), 18));
        row.Add(_entry, 1);
        if (DeviceInfo.Platform == DevicePlatform.WinUI && !large)
        {
            // Suggerimento della scorciatoia (solo Windows)
            row.Add(new Border
            {
                Padding = new Thickness(6, 2), StrokeThickness = 1, Stroke = new SolidColorBrush(Ui.C("BorderColor")),
                StrokeShape = new RoundRectangle { CornerRadius = 6 }, VerticalOptions = LayoutOptions.Center,
                Content = new Label { Text = "Ctrl K", FontSize = 12, FontFamily = Ui.FontSemi, TextColor = Ui.C("SubTextColor") }
            }, 2);
        }
        row.Add(_go, 3);

        _field = new Border
        {
            Content = row,
            BackgroundColor = Ui.C("FieldBgColor"),
            Stroke = new SolidColorBrush(Ui.C("BorderStrongColor")),
            StrokeThickness = 1,
            StrokeShape = new RoundRectangle { CornerRadius = 10 }
        };

        // Anteprima: "VERRÀ CREATO" + tipo (si può correggere) + riepilogo
        _kinds = new Ui.Segmented(new[] { ("Task", "Task"), ("Evento", "Evento"), ("Pagamento", "Pagamento"), ("Nota", "Nota") }, "Task", ChangeKind);
        _kinds.HorizontalOptions = LayoutOptions.Start;
        var kindsRow = new FlexLayout { Wrap = Microsoft.Maui.Layouts.FlexWrap.Wrap, AlignItems = Microsoft.Maui.Layouts.FlexAlignItems.Center };
        var caps = Ui.Caps(Loc.T("Verrà creato"));
        caps.Margin = new Thickness(0, 0, 12, 0);
        caps.VerticalOptions = LayoutOptions.Center;
        kindsRow.Add(caps);
        kindsRow.Add(_kinds);

        var footer = new Grid { ColumnDefinitions = new ColumnDefinitionCollection(new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto)) };
        footer.Add(Ui.LinkLabel(Loc.T("Modifica prima di salvare"), OpenEditor, Ui.Icons.Edit));
        var enter = Ui.Small(Loc.T("Invio per salvare"));
        enter.VerticalOptions = LayoutOptions.Center;
        footer.Add(enter, 1);
        _previewBox = new VerticalStackLayout { Spacing = 10, Padding = new Thickness(16, 12, 16, 6), IsVisible = false, Children = { kindsRow, _summary, footer } };

        // Conferma
        _done = Ui.Small("", "TextColor");
        _done.VerticalOptions = LayoutOptions.Center;
        _doneBox = new Grid
        {
            Padding = new Thickness(16, 12), ColumnSpacing = 8, IsVisible = false,
            ColumnDefinitions = new ColumnDefinitionCollection(new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Star))
        };
        _doneBox.Add(Ui.Icon(Ui.Icons.Check, Ui.C("TextColor"), 18));
        _doneBox.Add(_done, 1);

        _hint = Ui.Small(Loc.T("Prova: «dentista domani alle 15:30» o «bollo auto 85€ venerdì»"));
        _hint.Padding = new Thickness(16, 0, 16, 14);
        _hint.IsVisible = showHint;

        var stack = new VerticalStackLayout
        {
            Children = { new ContentView { Padding = new Thickness(12), Content = _field }, Line(_previewBox), _previewBox, Line(_doneBox), _doneBox, _hint }
        };
        Content = Ui.Card(stack, new Thickness(0));

        // Linea sottile sopra anteprima e conferma (visibile insieme a loro)
        static BoxView Line(View owner)
        {
            var l = new BoxView { HeightRequest = 1, Color = Ui.C("BorderColor") };
            l.SetBinding(IsVisibleProperty, new Binding(nameof(IsVisible), source: owner));
            return l;
        }
    }

    public void FocusEntry() => _entry.Focus();

    public void SetText(string text)
    {
        _entry.Text = text;
        _entry.Focus();
    }

    private void SetFocus(bool on) =>
        _field.Stroke = new SolidColorBrush(on ? Ui.C("AccentColor") : Ui.C("BorderStrongColor"));

    private static string KindKey(QuickEntry q) => q.Kind switch
    {
        QuickKind.Task => "Task",
        QuickKind.Evento => "Evento",
        QuickKind.Pagamento => "Pagamento",
        _ => "Nota"
    };

    // Il tipo indovinato si può correggere toccando il segmento
    private void ChangeKind(string key)
    {
        if (_parsed == null || _updatingKinds) return;
        _parsed.Kind = key switch
        {
            "Task" => QuickKind.Task,
            "Evento" => QuickKind.Evento,
            "Pagamento" => QuickKind.Pagamento,
            _ => _parsed.Kind is QuickKind.Promemoria or QuickKind.Idea ? _parsed.Kind : QuickKind.Nota
        };
        FillSummary();
    }

    private void UpdatePreview()
    {
        var text = (_entry.Text ?? "").Trim();
        bool has = text.Length > 0;
        _go.BackgroundColor = has ? Ui.C("AccentColor") : Colors.Transparent;
        Ui.SetIconColor(_goIcon, has ? Ui.C("OnAccentColor") : Ui.C("SubTextColor"));
        _hint.IsVisible = false;
        if (!has) { _parsed = null; _previewBox.IsVisible = false; return; }

        _parsed = QuickParser.Parse(text);
        _previewBox.IsVisible = true;
        _doneBox.IsVisible = false;
        _updatingKinds = true;
        _kinds.Select(KindKey(_parsed));
        _updatingKinds = false;
        FillSummary();
    }

    // Riepilogo su una riga: titolo · dettagli · (tipo, se è promemoria/idea/entrata)
    private void FillSummary()
    {
        _summary.Children.Clear();
        var parts = new List<string>();
        if (_parsed.KindName is "Promemoria" or "Idea" or "Entrata") parts.Add(Loc.T(_parsed.KindName));
        parts.AddRange(_parsed.Details());
        var title = new Label { Text = _parsed.Title, FontSize = 13, FontFamily = Ui.FontSemi, TextColor = Ui.C("TextColor"), MaxLines = 1, LineBreakMode = LineBreakMode.TailTruncation, VerticalOptions = LayoutOptions.Center };
        _summary.Add(title);
        foreach (var p in parts)
        {
            _summary.Add(new Label { Text = "·", FontSize = 13, TextColor = Ui.C("SubTextColor"), VerticalOptions = LayoutOptions.Center });
            _summary.Add(new Label { Text = p, FontSize = 13, FontFamily = Ui.FontRegular, TextColor = Ui.C("SubTextColor"), VerticalOptions = LayoutOptions.Center });
        }
    }

    // Etichetta dell'anteprima (usata anche dalla pagina "Scrivi al volo"): 30 alta, raggio 8, bordo
    public static View PChip(string text, string colorKey, string icon = null)
    {
        var row = new HorizontalStackLayout { Spacing = 6, VerticalOptions = LayoutOptions.Center };
        if (icon != null) row.Add(Ui.Icon(icon, Ui.C("TextColor"), 16));
        row.Add(new Label { Text = text, FontSize = 13, FontFamily = Ui.FontSemi, TextColor = Ui.C("TextColor"), VerticalOptions = LayoutOptions.Center });
        return new Border
        {
            HeightRequest = 30,
            Padding = new Thickness(10, 0),
            Margin = new Thickness(0, 0, 6, 6),
            Stroke = new SolidColorBrush(Ui.C("BorderStrongColor")),
            StrokeThickness = 1,
            StrokeShape = new RoundRectangle { CornerRadius = 8 },
            BackgroundColor = Colors.Transparent,
            Content = row
        };
    }

    private async Task SaveAsync()
    {
        if (_parsed == null) return;
        var saved = _parsed;
        await saved.SaveAsync(_db);
        _entry.Text = "";
        _done.Text = Loc.F("Aggiunto: {0} «{1}»", saved.KindName.ToLower(), saved.Title);
        _doneBox.IsVisible = true;
        _onSaved?.Invoke();
    }

    // Apre il modulo completo già compilato, per ritoccare prima di salvare
    private void OpenEditor()
    {
        if (_parsed == null) return;
        Action after = () => { _entry.Text = ""; _onSaved?.Invoke(); };
        Page page = _parsed.ToItem() switch
        {
            TaskItem t => new TaskEditorPage(_db, after, t, draft: true),
            CalendarEvent e => new EventEditorPage(_db, after, e, draft: true),
            Note n => new NoteEditorPage(_db, after, n, draft: true),
            _ => null
        };
        if (page != null) Application.Current.MainPage.Navigation.PushModalAsync(page);
    }
}

// Pagina a sé con solo il campo "scrivi al volo" (aperta dal widget e dalla barra laterale su Windows)
public class QuickCapturePage : SheetPage
{
    private readonly QuickCaptureBar _bar;

    public QuickCapturePage(DatabaseService db, Action onSaved) : base(Loc.T("Scrivi al volo"))
    {
        Body.Spacing = 22;
        Body.Add(Ui.Body(Loc.T("Scrivi come parli: ARM capisce da solo se è un task, un evento, un pagamento o un promemoria."), "SubTextColor"));

        _bar = new QuickCaptureBar(db, onSaved, large: true, showHint: false);
        Body.Add(_bar);

        // Esempi da toccare
        var examples = new FlexLayout { Wrap = Microsoft.Maui.Layouts.FlexWrap.Wrap };
        foreach (var ex in new[]
        {
            "bollo auto 85€ venerdì", "dentista domani alle 15:30", "Marco mi deve 20 euro entro il 15",
            "ricordami di chiamare la nonna domenica alle 11", "idea: corso di ceramica per la mamma",
            "netflix 13,99€ ogni mese il 18", "comprare le lampadine urgente"
        })
        {
            var chip = new Border
            {
                MinimumHeightRequest = 40,
                Padding = new Thickness(14, 8),
                Margin = new Thickness(0, 0, 8, 8),
                BackgroundColor = Ui.C("TabBgColor"),
                Stroke = new SolidColorBrush(Ui.C("BorderColor")),
                StrokeThickness = 1,
                StrokeShape = new RoundRectangle { CornerRadius = 10 },
                Content = new Label { Text = ex, FontSize = 14, FontFamily = Ui.FontSemi, TextColor = Ui.C("TextColor"), VerticalOptions = LayoutOptions.Center }
            };
            var text = ex;
            Ui.Press(chip, () => _bar.SetText(text));
            examples.Add(chip);
        }
        var exBlock = new VerticalStackLayout { Spacing = 8, Children = { Ui.Caps(Loc.T("Prova con")), examples } };
        Body.Add(exBlock);

        // Cosa riconosce
        var types = new FlexLayout { Wrap = Microsoft.Maui.Layouts.FlexWrap.Wrap };
        foreach (var (label, key, icon) in new[]
        {
            ("Task", "AccentColor", Ui.Icons.Task), ("Evento", "Accent5Color", Ui.Icons.Calendar), ("Pagamento", "Accent2Color", Ui.Icons.Euro),
            ("Entrata", "Accent3Color", Ui.Icons.Euro), ("Promemoria", "Accent4Color", Ui.Icons.Bell), ("Idea", "Accent4Color", Ui.Icons.Bulb), ("Nota", "AccentColor", Ui.Icons.Note)
        })
            types.Add(QuickCaptureBar.PChip(Loc.T(label), key, icon));
        var how = Ui.Small(Loc.T("Importi (85€, 20 euro), giorni (oggi, domani, venerdì, entro il 15), orari (alle 15:30), «ogni mese», «urgente»."));
        how.FontFamily = Ui.FontRegular;
        Body.Add(Ui.Card(new VerticalStackLayout { Spacing = 10, Children = { Ui.Caps(Loc.T("Cosa riconosce")), types, how } }));
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await Task.Delay(250);
        _bar.FocusEntry();
    }
}
