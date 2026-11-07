using System.Globalization;
using ARMusic.Data;
using ARMusic.Models;
using Microsoft.Maui.Controls.Shapes;

namespace ARMusic.Views;

// Base comune delle schermate di modifica (ARM 2.5):
// intestazione [chiudi 44] titolo 17 ExtraBold [Salva: pillola 40], contenuto scorrevole con margine 20 e sezioni a 20.
public abstract class EditorPageBase : ContentPage
{
    protected readonly DatabaseService Db;
    protected readonly Action OnSaved;
    protected readonly VerticalStackLayout Body = new() { Spacing = 20, Padding = new Thickness(20, 8, 20, 32) };

    protected EditorPageBase(DatabaseService db, Action onSaved, string title)
    {
        Db = db;
        OnSaved = onSaved;
        BackgroundColor = Ui.C("BgColor");
        NavigationPage.SetHasNavigationBar(this, false);

        var header = SheetHeader(title, async () => await Navigation.PopModalAsync(), SaveButton(async () =>
        {
            if (!await SaveAsync()) return;
            OnSaved?.Invoke();
            await Navigation.PopModalAsync();
        }));

        var root = new Grid { RowDefinitions = new RowDefinitionCollection(new RowDefinition(GridLength.Auto), new RowDefinition(GridLength.Star)) };
        root.Add(header);
        root.Add(new ScrollView { Content = new ContentView { Content = Body, MaximumWidthRequest = 640 } }, 0, 1);
        Content = root;
    }

    // Intestazione condivisa con i fogli (SheetPage)
    public static Grid SheetHeader(string title, Action onClose, View right = null)
    {
        var t = new Label { Text = title, FontSize = 17, FontFamily = Ui.FontHeavy, TextColor = Ui.C("TextColor"), HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center, MaxLines = 1, LineBreakMode = LineBreakMode.TailTruncation };
        var header = new Grid { Padding = new Thickness(12, 20, 12, 8), MinimumHeightRequest = 72, ColumnSpacing = 8, ColumnDefinitions = new ColumnDefinitionCollection(new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto)) };
        header.Add(Ui.IconButton(Ui.Icons.Close, onClose));
        header.Add(t, 1);
        header.Add(right ?? new BoxView { WidthRequest = 44, Color = Colors.Transparent }, 2);
        return header;
    }

    // "Salva": pillola 40 alta, raggio 20, 15 ExtraBold
    protected static Border SaveButton(Action onTap)
    {
        var b = new Border
        {
            HeightRequest = 40,
            Padding = new Thickness(18, 0),
            BackgroundColor = Ui.C("AccentColor"),
            StrokeThickness = 0,
            StrokeShape = new RoundRectangle { CornerRadius = 20 },
            VerticalOptions = LayoutOptions.Center,
            Content = new Label { Text = Loc.T("Salva"), TextColor = Ui.C("OnAccentColor"), FontFamily = Ui.FontHeavy, FontSize = 15, VerticalOptions = LayoutOptions.Center }
        };
        Ui.Press(b, onTap);
        return b;
    }

    protected abstract Task<bool> SaveAsync();

    // ---- helper per costruire i form ----
    protected static Label FieldLabel(string text) => Ui.Caps(text);

    protected static VerticalStackLayout Group(string label, View content)
    {
        var v = new VerticalStackLayout { Spacing = 8 };
        v.Add(FieldLabel(label));
        v.Add(content);
        return v;
    }

    protected static Entry MakeEntry(string placeholder, string text = null, Keyboard keyboard = null) => Ui.Entry(placeholder, text, 16, keyboard);

    // Titolo grande: 24 ExtraBold, senza cornice
    protected static Entry TitleEntry(string placeholder, string text)
    {
        var e = Ui.Entry(placeholder, text, 24);
        e.FontFamily = Ui.FontHeavy;
        e.CharacterSpacing = -0.4;
        e.MinimumHeightRequest = 44;
        return e;
    }

    protected static Editor MakeEditor(string placeholder, string text = null, double minHeight = 110) => new()
    {
        Placeholder = placeholder,
        Text = text,
        TextColor = Ui.C("TextColor"),
        PlaceholderColor = Ui.C("SubTextColor"),
        BackgroundColor = Colors.Transparent,
        FontSize = 15,
        FontFamily = Ui.FontRegular,
        AutoSize = EditorAutoSizeOption.TextChanges,
        MinimumHeightRequest = minHeight
    };

    protected static DatePicker MakeDatePicker(DateTime date) => new()
    {
        Date = date,
        Format = "ddd d MMMM yyyy",
        FontFamily = Ui.FontSemi,
        FontSize = 16,
        TextColor = Ui.C("TextColor"),
        BackgroundColor = Colors.Transparent,
        VerticalOptions = LayoutOptions.Center
    };

    protected static TimePicker MakeTimePicker(TimeSpan time) => new()
    {
        Time = time,
        Format = "HH:mm",
        FontFamily = Ui.FontBold,
        FontSize = 16,
        TextColor = Ui.C("TextColor"),
        BackgroundColor = Colors.Transparent,
        VerticalOptions = LayoutOptions.Center
    };

    // Campo con icona a sinistra (data, ora, luogo)
    protected static Border IconField(string icon, View input)
    {
        var g = new Grid { ColumnDefinitions = new ColumnDefinitionCollection(new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Star)), ColumnSpacing = 10 };
        g.Add(Ui.Icon(icon, Ui.C("SubTextColor"), 20));
        g.Add(input, 1);
        return Ui.Field(g);
    }

    protected static DateTime ReadDate(DatePicker p) { DateTime? d = p.Date; return (d ?? DateTime.Today).Date; }
    protected static TimeSpan ReadTime(TimePicker p) { TimeSpan? t = p.Time; return t ?? TimeSpan.Zero; }

    // Riga impostazione (56): icona · testo 15/600 · interruttore. Più righe vanno nella stessa card con SettingsCard.
    protected static Grid SettingRow(string icon, string text, bool value, Action<bool> onChanged, out Switch sw, out Label label)
    {
        sw = new Switch { IsToggled = value, OnColor = Ui.C("AccentColor"), ThumbColor = Colors.White, VerticalOptions = LayoutOptions.Center };
        sw.Toggled += (s, e) => onChanged(e.Value);
        var g = new Grid { ColumnDefinitions = new ColumnDefinitionCollection(new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto)), ColumnSpacing = 12, MinimumHeightRequest = 56, Padding = new Thickness(16, 4) };
        g.Add(Ui.Icon(icon, Ui.C("SubTextColor"), 22));
        label = Ui.Body(text);
        label.FontFamily = Ui.FontSemi;
        label.VerticalOptions = LayoutOptions.Center;
        g.Add(label, 1);
        g.Add(sw, 2);
        var s2 = sw;
        Ui.OnTap(g, () => s2.IsToggled = !s2.IsToggled);
        return g;
    }

    protected static Grid SettingRow(string icon, string text, bool value, Action<bool> onChanged) => SettingRow(icon, text, value, onChanged, out _, out _);

    protected static Border SettingsCard(params View[] rows)
    {
        var v = new VerticalStackLayout();
        for (int i = 0; i < rows.Length; i++)
        {
            if (i > 0) v.Add(Ui.Divider());
            v.Add(rows[i]);
        }
        return Ui.Card(v, new Thickness(0, 2));
    }

    // Pallini colore: 34 dentro un'area 44; quello scelto ha un anello Accent
    protected static Grid ColorDot(Color fill, bool outline, Action onTap, out Border ring)
    {
        var dot = new Border
        {
            WidthRequest = 34, HeightRequest = 34,
            BackgroundColor = fill,
            Stroke = new SolidColorBrush(outline ? Ui.C("BorderColor") : Colors.Transparent),
            StrokeThickness = 1,
            StrokeShape = new RoundRectangle { CornerRadius = 17 },
            HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center
        };
        ring = new Border
        {
            WidthRequest = 42, HeightRequest = 42,
            BackgroundColor = Colors.Transparent,
            Stroke = new SolidColorBrush(Ui.C("AccentColor")),
            StrokeThickness = 2,
            StrokeShape = new RoundRectangle { CornerRadius = 21 },
            HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center,
            IsVisible = false
        };
        var cell = new Grid { WidthRequest = 44, HeightRequest = 44, Children = { ring, dot } };
        Ui.Press(cell, onTap, 0.92);
        return cell;
    }

    protected async Task<bool> ConfirmDelete(string what) =>
        await DisplayAlert(Loc.T("Elimina"), Loc.F("Vuoi eliminare {0}?", what), Loc.T("Elimina"), Loc.T("Annulla"));

    // "Elimina": riga centrata 48, icona cestino, Accent2 ExtraBold
    protected View DeleteButton(string what, Func<Task> delete, string label = "Elimina")
    {
        var row = new HorizontalStackLayout
        {
            Spacing = 8, HorizontalOptions = LayoutOptions.Center, MinimumHeightRequest = 48,
            Children =
            {
                Ui.Icon(Ui.Icons.Trash, Ui.C("Accent2Color"), 20),
                new Label { Text = Loc.T(label), TextColor = Ui.C("Accent2Color"), FontFamily = Ui.FontHeavy, FontSize = 15, VerticalOptions = LayoutOptions.Center }
            }
        };
        Ui.Press(row, async () =>
        {
            if (!await ConfirmDelete(what)) return;
            await delete();
            OnSaved?.Invoke();
            await Navigation.PopModalAsync();
        });
        return row;
    }
}

// Riquadri del tipo di nota (griglia 2×2): 56 alti, raggio 16; scelto = bordo 2 e fondo tenue del suo colore
public class TypeTiles : ContentView
{
    private readonly Dictionary<string, Border> _tiles = new();

    public TypeTiles(string selected, Action<string> onSelect)
    {
        var g = new Grid { ColumnSpacing = 10, RowSpacing = 10, ColumnDefinitions = new ColumnDefinitionCollection(new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Star)) };
        int i = 0;
        foreach (var t in Note.Tipi)
        {
            var type = t;
            var row = new HorizontalStackLayout { Spacing = 10, VerticalOptions = LayoutOptions.Center, Children =
            {
                Ui.Icon(Ui.TypeIcon(t), Ui.C(Ui.TypeColorKey(t)), 22),
                new Label { Text = t, FontSize = 15, FontFamily = Ui.FontBold, TextColor = Ui.C("TextColor"), VerticalOptions = LayoutOptions.Center }
            } };
            var b = new Border { HeightRequest = 56, Padding = new Thickness(14, 0), StrokeThickness = 2, StrokeShape = new RoundRectangle { CornerRadius = 16 }, Content = row, ClassId = t };
            Ui.Press(b, () => { Select(type); onSelect(type); });
            _tiles[t] = b;
            if (i / 2 >= g.RowDefinitions.Count) g.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            g.Add(b, i % 2, i / 2);
            i++;
        }
        Content = g;
        Select(selected);
    }

    public void Select(string type)
    {
        foreach (var kv in _tiles)
        {
            bool on = kv.Key == type;
            string key = Ui.TypeColorKey(kv.Key);
            kv.Value.Stroke = new SolidColorBrush(on ? Ui.C(key) : Colors.Transparent);
            kv.Value.BackgroundColor = on ? Ui.Soft(key) : Ui.C("TabBgColor");
            kv.Value.Shadow = on ? null : Ui.CardShadow();
        }
    }
}

// ================= NOTE / PAGAMENTI / PROMEMORIA / IDEE =================
public class NoteEditorPage : EditorPageBase
{
    private readonly Note _note;
    private readonly bool _isNew;

    private readonly Entry _title;
    private readonly Editor _content;
    private readonly Entry _amount;
    private readonly Label _sign;
    private readonly DatePicker _dueDate;
    private readonly TimePicker _dueTime;
    private readonly HorizontalStackLayout _colorRow = new() { Spacing = 4 };
    private readonly List<(string Hex, Border Ring)> _dots = new();
    private readonly VerticalStackLayout _paymentSection = new() { Spacing = 18 };
    private readonly Label _paidLabel;
    private readonly Label _dueLabel;
    private readonly Ui.ChipGroup _catOut, _catIn;

    // Lista con le spunte (righe aggiunte/tolte sul posto)
    private readonly VerticalStackLayout _checklistSection = new() { Spacing = 8 };
    private readonly VerticalStackLayout _checkRows = new();
    private readonly List<ChecklistItem> _items;
    private Entry _newItem;

    // Promemoria / data: costruita una volta sola, poi solo mostrata/nascosta
    private readonly Border _dateCard;
    private readonly Switch _dateSwitch;
    private readonly Label _dateToggleLabel;
    private readonly Grid _dateRow;
    private bool _hasDate;

    // draft = true: nota già compilata (es. dalla scrittura libera) ma non ancora salvata
    public NoteEditorPage(DatabaseService db, Action onSaved, Note note = null, string presetType = null, DateTime? presetDate = null, bool draft = false)
        : base(db, onSaved, note == null || draft ? Loc.T("Nuova nota") : Loc.T("Modifica nota"))
    {
        _isNew = note == null || draft;
        _note = note ?? new Note { Type = presetType ?? "Nota" };
        if (note == null) _note.DueDate = presetDate?.Date.AddHours(9) ?? (_note.Type == "Pagamento" ? DateTime.Today.AddDays(7) : null);
        _hasDate = _note.DueDate.HasValue;
        _items = _note.ChecklistItems;

        // Tipo (griglia 2×2)
        var typeTiles = new TypeTiles(_note.Type, t =>
        {
            _note.Type = t;
            if (t == "Pagamento") _hasDate = true;
            UpdateSections();
        });

        _title = TitleEntry(Loc.T("Titolo"), _note.Title);

        // ---- Pagamento ----
        var direction = new Ui.Segmented(new[] { ("out", "Da pagare"), ("in", "Da ricevere") }, _note.IsIncome ? "in" : "out", k =>
        {
            bool income = k == "in";
            if (income != _note.IsIncome) { _note.Category = ""; _catOut.Select(null); _catIn.Select(null); }
            _note.IsIncome = income;
            UpdateSections();
        });

        // Importo: card con segno 32 e cifra 40 ExtraBold
        _sign = new Label { FontSize = 32, FontFamily = Ui.FontHeavy, VerticalOptions = LayoutOptions.Center };
        _amount = Ui.Entry("0,00", _note.Amount > 0 ? _note.Amount.ToString("0.00", Ui.It) : null, 40, Keyboard.Numeric);
        _amount.FontFamily = Ui.FontHeavy;
        _amount.CharacterSpacing = -1;
        var amountRow = new Grid { ColumnDefinitions = new ColumnDefinitionCollection(new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto)), ColumnSpacing = 6 };
        amountRow.Add(_sign);
        amountRow.Add(_amount, 1);
        amountRow.Add(new Label { Text = "€", FontSize = 28, FontFamily = Ui.FontHeavy, TextColor = Ui.C("SubTextColor"), VerticalOptions = LayoutOptions.Center }, 2);

        _catOut = new Ui.ChipGroup(Category.Uscite.Select(c => (c.Name, c.Label, "TextColor", (string)null)), _note.IsIncome ? null : _note.Category, k => _note.Category = k, columns: -1);
        _catIn = new Ui.ChipGroup(Category.Entrate.Select(c => (c.Name, c.Label, "TextColor", (string)null)), _note.IsIncome ? _note.Category : null, k => _note.Category = k, columns: -1);

        var due = _note.DueDate ?? DateTime.Today.AddDays(1).AddHours(9);
        _dueDate = MakeDatePicker(due.Date);
        _dueTime = MakeTimePicker(due.TimeOfDay);

        _dueLabel = FieldLabel("");
        _paymentSection.Add(direction);
        _paymentSection.Add(Ui.Card(amountRow, new Thickness(18, 12)));
        _paymentSection.Add(Group(Loc.T("Categoria"), new VerticalStackLayout { Children = { _catOut, _catIn } }));
        var paymentDate = new VerticalStackLayout { Spacing = 8, Children = { _dueLabel } };
        _paymentSection.Add(paymentDate);
        _paymentSection.Add(SettingsCard(
            SettingRow(Ui.Icons.Repeat, Loc.T("Ricorrente ogni mese"), _note.IsMonthly, v => _note.IsMonthly = v),
            SettingRow(Ui.Icons.Check, "", _note.IsPaid, v => _note.IsPaid = v, out _, out _paidLabel)));

        // ---- Data (promemoria / calendario) ----
        var dateToggle = SettingRow(Ui.Icons.Bell, "", _hasDate, v => { _hasDate = v; UpdateSections(); }, out _dateSwitch, out _dateToggleLabel);
        _dateRow = new Grid { ColumnDefinitions = new ColumnDefinitionCollection(new ColumnDefinition(GridLength.Star), new ColumnDefinition(110)), ColumnSpacing = 10, Padding = new Thickness(16, 4, 16, 14) };
        _dateCard = Ui.Card(new VerticalStackLayout { Children = { dateToggle, _dateRow } }, new Thickness(0, 2));

        // Due selettori di data distinti (pagamento / promemoria): nessuna vista viene mai spostata
        _payDue = MakeDatePicker(due.Date);
        paymentDate.Add(IconField(Ui.Icons.Calendar, _payDue));
        _dateRow.Add(IconField(Ui.Icons.Calendar, _dueDate));
        _dateRow.Add(Ui.Field(_dueTime), 1);

        // ---- Testo e lista ----
        _content = MakeEditor(Loc.T("Scrivi qualcosa…"), _note.Content, 110);

        // ---- Colore ----
        foreach (var hex in Ui.NoteColors)
        {
            var h = hex;
            var cell = ColorDot(Ui.NoteBackground(h), string.IsNullOrEmpty(h), () => { _note.ColorHex = h; RefreshColors(); }, out var ring);
            _dots.Add((h, ring));
            _colorRow.Add(cell);
        }

        Body.Add(Group(Loc.T("Tipo"), typeTiles));
        Body.Add(_title);
        Body.Add(_paymentSection);
        Body.Add(Ui.Field(_content));
        Body.Add(_checklistSection);
        Body.Add(_dateCard);
        Body.Add(Group(Loc.T("Colore"), new ScrollView { Orientation = ScrollOrientation.Horizontal, HorizontalScrollBarVisibility = ScrollBarVisibility.Never, Content = _colorRow }));
        Body.Add(SettingsCard(SettingRow(Ui.Icons.Pin, Loc.T("Fissa in alto"), _note.IsPinned, v => _note.IsPinned = v)));
        if (!_isNew) Body.Add(DeleteButton(Loc.T("la nota"), () => Db.DeleteNoteAsync(_note)));

        BuildChecklist();
        UpdateSections();
        RefreshColors();
    }

    private readonly DatePicker _payDue;

    // ---- Lista con le spunte: card con righe 44 (casella · voce · ×) e "Aggiungi una voce" ----
    private void BuildChecklist()
    {
        _checklistSection.Add(FieldLabel(Loc.T("Lista")));
        foreach (var item in _items) _checkRows.Add(CheckRow(item));

        _newItem = Ui.Entry(Loc.T("Aggiungi una voce"), null, 15);
        _newItem.FontFamily = Ui.FontBold;
        _newItem.PlaceholderColor = Ui.C("AccentColor");
        _newItem.ReturnType = ReturnType.Done;
        _newItem.Completed += (s, e) => AddItemFromEntry();
        var add = Ui.Icon(Ui.Icons.Plus, Ui.C("AccentColor"), 22);
        Ui.OnTap(add, AddItemFromEntry);
        var addRow = new Grid { ColumnDefinitions = new ColumnDefinitionCollection(new ColumnDefinition(44), new ColumnDefinition(GridLength.Star)), ColumnSpacing = 2, MinimumHeightRequest = 48, Padding = new Thickness(6, 0, 16, 0) };
        addRow.Add(add);
        addRow.Add(_newItem, 1);
        _checkRows.Add(addRow);
        _checklistSection.Add(Ui.Card(_checkRows, new Thickness(0, 4)));
    }

    private void AddItemFromEntry()
    {
        var text = (_newItem.Text ?? "").Trim();
        if (text.Length == 0) return;
        var item = new ChecklistItem { Text = text };
        _items.Add(item);
        _checkRows.Insert(_checkRows.Children.Count - 1, CheckRow(item));   // prima della riga "aggiungi"
        _newItem.Text = "";
        // Il cursore torna sul campo per la voce successiva (dopo l'Invio, che altrimenti lo sposta altrove)
        Dispatcher.DispatchDelayed(TimeSpan.FromMilliseconds(80), () => _newItem.Focus());
    }

    private View CheckRow(ChecklistItem item)
    {
        var entry = Ui.Entry(Loc.T("Voce"), item.Text, 15);
        entry.FontFamily = Ui.FontSemi;
        void Style(bool done) => entry.Opacity = done ? 0.55 : 1;   // voce fatta: più tenue
        Style(item.Done);
        var box = new Ui.CheckBox(item.Done, "AccentColor", v => { item.Done = v; Style(v); });
        var boxArea = new Grid { WidthRequest = 44, HeightRequest = 44, Children = { box } };
        entry.TextChanged += (s, e) => item.Text = e.NewTextValue ?? "";
        var row = new Grid { ColumnDefinitions = new ColumnDefinitionCollection(new ColumnDefinition(44), new ColumnDefinition(GridLength.Star), new ColumnDefinition(44)), ColumnSpacing = 2, MinimumHeightRequest = 44, Padding = new Thickness(6, 0, 4, 0) };
        row.Add(boxArea);
        row.Add(entry, 1);
        row.Add(Ui.IconButton(Ui.Icons.Close, () => { _items.Remove(item); _checkRows.Remove(row); }, flat: true), 2);
        return row;
    }

    // Mostra/nasconde le parti del form in base al tipo scelto
    private void UpdateSections()
    {
        bool isPayment = _note.Type == "Pagamento";
        _paymentSection.IsVisible = isPayment;
        _checklistSection.IsVisible = !isPayment;
        _catOut.IsVisible = !_note.IsIncome;
        _catIn.IsVisible = _note.IsIncome;
        _sign.Text = _note.IsIncome ? "+" : "−";
        _sign.TextColor = Ui.C(_note.IsIncome ? "Accent3Color" : "Accent2Color");
        _paidLabel.Text = _note.IsIncome ? Loc.T("Già ricevuto") : Loc.T("Già pagato");
        _dueLabel.Text = _note.IsIncome ? Loc.T("ARRIVERÀ ENTRO IL") : Loc.T("SCADENZA");

        _dateCard.IsVisible = !isPayment;
        _dateToggleLabel.Text = _note.Type == Loc.T("Promemoria") ? Loc.T("Ricordamelo in una data") : Loc.T("Aggiungi al calendario");
        if (!isPayment && _dateSwitch.IsToggled != _hasDate) _dateSwitch.IsToggled = _hasDate;
        _dateRow.IsVisible = _hasDate;
    }

    private void RefreshColors()
    {
        foreach (var (hex, ring) in _dots) ring.IsVisible = (_note.ColorHex ?? "") == hex;
    }

    protected override async Task<bool> SaveAsync()
    {
        AddItemFromEntry();   // una voce scritta ma non ancora confermata con Invio
        bool hasList = _note.Type != "Pagamento" && _items.Any(i => !string.IsNullOrWhiteSpace(i.Text));
        if (string.IsNullOrWhiteSpace(_title.Text) && string.IsNullOrWhiteSpace(_content.Text) && !hasList)
        {
            await DisplayAlert(Loc.T("Ops"), Loc.T("Scrivi almeno un titolo, un contenuto o una voce della lista."), Loc.T("OK"));
            return false;
        }

        _note.Title = string.IsNullOrWhiteSpace(_title.Text) ? (hasList ? Loc.T("Lista") : _note.Type) : _title.Text.Trim();
        _note.Content = _content.Text;
        _note.Checklist = _note.Type == "Pagamento" ? "" : ChecklistItem.Serialize(_items);
        if (_note.Type == "Pagamento" && string.IsNullOrEmpty(_note.Category))
            _note.Category = Category.Guess(_note.Title + " " + _note.Content, _note.IsIncome);

        if (_note.Type == "Pagamento")
        {
            // "1.450,50" (italiano) oppure "72.40" (punto decimale)
            var raw = (_amount.Text ?? "").Replace("€", "").Replace(" ", "");
            if (raw.Contains(',')) raw = raw.Replace(".", "").Replace(",", ".");
            if (!double.TryParse(raw, NumberStyles.Any, CultureInfo.InvariantCulture, out var amount) && raw.Length > 0)
            {
                await DisplayAlert(Loc.T("Importo non valido"), Loc.T("Scrivi l'importo in euro, per esempio 85 o 72,40."), Loc.T("OK"));
                return false;
            }
            _note.Amount = amount;
            _note.DueDate = ReadDate(_payDue);
        }
        else
        {
            _note.DueDate = _hasDate ? ReadDate(_dueDate) + ReadTime(_dueTime) : null;
        }

        await Db.SaveNoteAsync(_note);
        return true;
    }
}

// ================= TASK =================
public class TaskEditorPage : EditorPageBase
{
    private readonly TaskItem _task;
    private readonly Entry _title;
    private readonly Editor _notes;
    private readonly DatePicker _date;
    private readonly Border _dateField;
    private readonly Ui.ChipGroup _quickDates;
    private bool _hasDate;

    public TaskEditorPage(DatabaseService db, Action onSaved, TaskItem task = null, DateTime? presetDate = null, bool draft = false)
        : base(db, onSaved, task == null || draft ? Loc.T("Nuovo task") : Loc.T("Modifica task"))
    {
        _task = task ?? new TaskItem { DueDate = presetDate };
        _hasDate = _task.DueDate.HasValue;
        Body.Spacing = 22;

        _title = TitleEntry(Loc.T("Cosa devi fare?"), _task.Title);

        _date = MakeDatePicker(_task.DueDate ?? DateTime.Today);
        _dateField = IconField(Ui.Icons.Calendar, _date);
        _dateField.Margin = new Thickness(0, 2, 0, 0);
        _notes = MakeEditor(Loc.T("Dettagli, link, numeri utili…"), _task.Notes);

        // Priorità: chip con pallino del colore (Alta corallo · Normale giallo · Bassa verde)
        var priorita = new[] { 2, 1, 0 }.Select(p => (p.ToString(), Ui.PriorityText(p), Ui.PriorityColorKey(p), Dot));
        var priorityGroup = new Ui.ChipGroup(priorita, _task.Priority.ToString(), k => _task.Priority = int.Parse(k), columns: -1);

        var scelte = new (string, string, string, string)[]
        {
            ("none", Loc.T("Nessuna"), "TextColor", null),
            ("today", Loc.T("Oggi"), "TextColor", null),
            ("tomorrow", Loc.T("Domani"), "TextColor", null),
            ("week", Loc.T("Tra una settimana"), "TextColor", null),
            ("custom", Loc.T("Altra data"), "TextColor", null)
        };
        _quickDates = new Ui.ChipGroup(scelte, KeyForDate(), k =>
        {
            _hasDate = k != "none";
            if (k == "today") _date.Date = DateTime.Today;
            else if (k == "tomorrow") _date.Date = DateTime.Today.AddDays(1);
            else if (k == "week") _date.Date = DateTime.Today.AddDays(7);
            _dateField.IsVisible = k == "custom";
        }, columns: -1);
        _dateField.IsVisible = KeyForDate() == "custom";
        _date.DateSelected += (s, e) => { if (_hasDate) _quickDates.Select(KeyForDate() == "custom" ? "custom" : KeyForDate()); };

        Body.Add(_title);
        Body.Add(Group(Loc.T("Priorità"), priorityGroup));
        Body.Add(new VerticalStackLayout { Spacing = 8, Children = { FieldLabel(Loc.T("Scadenza")), _quickDates, _dateField } });
        Body.Add(Group(Loc.T("Note"), Ui.Field(_notes)));
        if (task != null && !draft) Body.Add(DeleteButton(Loc.T("il task"), () => Db.DeleteTaskAsync(_task), Loc.T("Elimina task")));
    }

    // Pallino 10 (cerchio pieno) per le chip della priorità
    private const string Dot = "M12 7a5 5 0 1 1 0 10a5 5 0 1 1 0-10z";

    private string KeyForDate()
    {
        if (!_hasDate) return "none";
        var d = ReadDate(_date);
        if (d == DateTime.Today) return "today";
        if (d == DateTime.Today.AddDays(1)) return "tomorrow";
        if (d == DateTime.Today.AddDays(7)) return "week";
        return "custom";
    }

    protected override async Task<bool> SaveAsync()
    {
        if (string.IsNullOrWhiteSpace(_title.Text))
        {
            await DisplayAlert(Loc.T("Ops"), Loc.T("Dai un nome al task."), Loc.T("OK"));
            return false;
        }
        _task.Title = _title.Text.Trim();
        _task.Notes = _notes.Text;
        _task.DueDate = _hasDate ? ReadDate(_date) : null;
        await Db.SaveTaskAsync(_task);
        return true;
    }
}

// ================= EVENTI =================
public class EventEditorPage : EditorPageBase
{
    private static readonly string[] EventColors = { "Accent5Color", "AccentColor", "Accent3Color", "Accent4Color", "Accent2Color" };

    private readonly CalendarEvent _event;
    private readonly Entry _title;
    private readonly Entry _location;
    private readonly Editor _notes;
    private readonly DatePicker _date;
    private readonly TimePicker _start;
    private readonly TimePicker _end;
    private readonly Grid _timeRow;
    private readonly List<(string Key, Border Ring)> _dots = new();

    public EventEditorPage(DatabaseService db, Action onSaved, CalendarEvent ev = null, DateTime? presetDate = null, bool draft = false)
        : base(db, onSaved, ev == null || draft ? Loc.T("Nuovo evento") : Loc.T("Modifica evento"))
    {
        _event = ev ?? new CalendarEvent { Date = presetDate ?? DateTime.Today };

        _title = TitleEntry(Loc.T("Nome dell'evento"), _event.Title);
        _date = MakeDatePicker(_event.Date);
        _start = MakeTimePicker(_event.StartTime);
        _end = MakeTimePicker(_event.EndTime);
        _location = MakeEntry(Loc.T("Aggiungi un luogo"), _event.Location);
        _notes = MakeEditor(Loc.T("Note"), _event.Notes, 90);

        _timeRow = new Grid { ColumnDefinitions = new ColumnDefinitionCollection(new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Star)), ColumnSpacing = 12, IsVisible = !_event.AllDay };
        _timeRow.Add(Group(Loc.T("Inizio"), Ui.Field(_start)));
        _timeRow.Add(Group(Loc.T("Fine"), Ui.Field(_end)), 1);

        var colors = new HorizontalStackLayout { Spacing = 4 };
        foreach (var key in EventColors)
        {
            var k = key;
            colors.Add(ColorDot(Ui.C(k), false, () => { _event.ColorKey = k; RefreshColors(); }, out var ring));
            _dots.Add((k, ring));
        }

        Body.Add(_title);
        Body.Add(Group(Loc.T("Giorno"), IconField(Ui.Icons.Calendar, _date)));
        Body.Add(SettingsCard(SettingRow(Ui.Icons.Clock, Loc.T("Tutto il giorno"), _event.AllDay, v => { _event.AllDay = v; _timeRow.IsVisible = !v; })));
        Body.Add(_timeRow);
        Body.Add(Group(Loc.T("Luogo"), IconField(Ui.Icons.Location, _location)));
        Body.Add(Group(Loc.T("Colore"), colors));
        Body.Add(Group(Loc.T("Note"), Ui.Field(_notes)));
        if (ev != null && !draft) Body.Add(DeleteButton(Loc.T("l'evento"), () => Db.DeleteEventAsync(_event), Loc.T("Elimina evento")));

        RefreshColors();
    }

    private void RefreshColors()
    {
        foreach (var (key, ring) in _dots) ring.IsVisible = (_event.ColorKey ?? "Accent5Color") == key;
    }

    protected override async Task<bool> SaveAsync()
    {
        if (string.IsNullOrWhiteSpace(_title.Text))
        {
            await DisplayAlert(Loc.T("Ops"), Loc.T("Dai un titolo all'evento."), Loc.T("OK"));
            return false;
        }
        _event.Title = _title.Text.Trim();
        _event.Date = ReadDate(_date);
        _event.StartTime = ReadTime(_start);
        _event.EndTime = ReadTime(_end);
        if (_event.EndTime < _event.StartTime) _event.EndTime = _event.StartTime.Add(TimeSpan.FromHours(1));
        _event.Location = _location.Text;
        _event.Notes = _notes.Text;
        await Db.SaveEventAsync(_event);
        return true;
    }
}
