using ARMusic.Data;
using ARMusic.Models;
using ARMusic.Services;

namespace ARMusic.Views;

// Task (ARM 2.5): titolo, aggiunta rapida, sezioni In ritardo · Da fare · Fatti oggi · Completati.
// Righe da 60: cerchio nel colore della priorità, titolo, "● Alta · Oggi".
public class TasksPage : ContentView
{
    private readonly DatabaseService _db;
    private readonly VerticalStackLayout _body = new() { Padding = new Thickness(20, 28, 20, 100), Spacing = 18 };
    private readonly ContentView _head = new();
    private readonly VerticalStackLayout _sections = new() { Spacing = 18 };
    private Border _addField;
    private Entry _quickAdd;
    private string _addTheme;
    private List<TaskItem> _tasks = new();

    public TasksPage(DatabaseService db)
    {
        _db = db;
        var root = new Grid();
        root.Add(new ScrollView { Content = _body });
        root.Add(Ui.Fab(() => Open(new TaskEditorPage(_db, Refresh))));
        Content = root;
    }

    public async void Refresh()
    {
        _tasks = await _db.GetAllTasksAsync();
        Render();
    }

    private static void Open(Page page) => Application.Current.MainPage.Navigation.PushModalAsync(page);

    private async Task QuickAddAsync()
    {
        var text = _quickAdd.Text?.Trim();
        if (string.IsNullOrEmpty(text)) return;
        // Anche qui funziona la scrittura libera: "chiamare Luca domani urgente" → task di domani, priorità alta
        var parsed = QuickParser.Parse("task: " + text);
        await _db.SaveTaskAsync(new TaskItem { Title = parsed.Title, DueDate = parsed.Date, Priority = parsed.Priority });
        _quickAdd.Text = "";
        Refresh();
    }

    private void Render()
    {
        BackgroundColor = Ui.C("BgColor");
        _body.Padding = Width >= 720 ? new Thickness(32, 32, 32, 100) : new Thickness(20, 28, 20, 100);

        // Struttura fissa: il campo di aggiunta non viene mai spostato (si ricrea solo col tema)
        if (_body.Children.Count == 0) { _body.Add(_head); _body.Add(_sections); }
        if (_addField == null || _addTheme != ThemeService.TemaAttuale)
        {
            if (_addField != null) _body.Remove(_addField);
            _quickAdd = Ui.Entry(Loc.T("Aggiungi un task e premi invio..."));
            _quickAdd.ReturnType = ReturnType.Done;
            _quickAdd.Completed += async (s, e) => await QuickAddAsync();
            var row = new Grid { ColumnDefinitions = new ColumnDefinitionCollection(new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Star)), ColumnSpacing = 10 };
            var plus = Ui.Icon(Ui.Icons.Plus, Ui.C("AccentColor"), 20);
            Ui.OnTap(plus, async () => await QuickAddAsync());
            row.Add(plus);
            row.Add(_quickAdd, 1);
            _addField = Ui.Field(row);
            _addTheme = ThemeService.TemaAttuale;
            _body.Insert(1, _addField);
        }

        int aperti = _tasks.Count(t => !t.IsDone);
        int fattiOggi = _tasks.Count(t => t.IsDone && t.CompletedAt?.Date == DateTime.Today);
        _head.Content = new VerticalStackLayout { Spacing = 4, Children = { Ui.PageTitle(Loc.T("Task")), Ui.Small(Loc.F("{0} da fare · {1} completati oggi", aperti, fattiOggi)) } };

        _sections.Children.Clear();
        var open = _tasks.Where(t => !t.IsDone).ToList();
        Section(Loc.T("In ritardo"), "Accent2Color", open.Where(t => t.IsOverdue).OrderBy(t => t.DueDate).ThenByDescending(t => t.Priority));
        Section(Loc.T("Da fare"), "SubTextColor", open.Where(t => !t.IsOverdue).OrderBy(t => t.DueDate ?? DateTime.MaxValue).ThenByDescending(t => t.Priority));
        Section(Loc.T("Fatti oggi"), "SubTextColor", _tasks.Where(t => t.IsDone && t.CompletedAt?.Date == DateTime.Today).OrderByDescending(t => t.CompletedAt));
        Section(Loc.T("Completati"), "SubTextColor", _tasks.Where(t => t.IsDone && t.CompletedAt?.Date != DateTime.Today).OrderByDescending(t => t.CompletedAt).Take(30));

        if (_sections.Children.Count == 0)
            _sections.Add(Ui.EmptyCard(Loc.T("Scrivi qui sopra la prima cosa da fare e premi invio."), Ui.Icons.Task));
    }

    private void Section(string title, string colorKey, IEnumerable<TaskItem> items)
    {
        var list = items.ToList();
        if (list.Count == 0) return;
        var rows = new VerticalStackLayout();
        foreach (var t in list) rows.Add(TaskRow(t));
        _sections.Add(new VerticalStackLayout { Spacing = 8, Children = { Ui.Caps(title, colorKey), Ui.Card(rows, new Thickness(0, 2)) } });
    }

    private View TaskRow(TaskItem t)
    {
        var g = new Grid
        {
            ColumnDefinitions = new ColumnDefinitionCollection(new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Star)),
            ColumnSpacing = 4,
            Padding = new Thickness(8, 6, 16, 6),
            MinimumHeightRequest = 60
        };
        string prio = Ui.PriorityColorKey(t.Priority);
        g.Add(Ui.CheckCircle(t.IsDone, prio, async () => { await _db.ToggleTaskAsync(t); Refresh(); }));

        var info = new VerticalStackLayout { Spacing = 3, VerticalOptions = LayoutOptions.Center, Padding = new Thickness(0, 6) };
        var title = Ui.Body(t.Title);
        title.FontFamily = Ui.FontSemi;
        if (t.IsDone) { title.TextDecorations = TextDecorations.Strikethrough; title.Opacity = 0.55; }
        info.Add(title);

        string due = !t.DueDate.HasValue ? Loc.T("Nessuna scadenza")
                   : t.IsOverdue ? Loc.T("In ritardo · ") + t.DueDate.Value.ToString("d MMM", Ui.It)
                   : Ui.RelativeDate(t.DueDate.Value);
        if (t.IsDone && t.CompletedAt.HasValue) due = Loc.T("Fatto ") + Ui.RelativeDate(t.CompletedAt.Value).ToLower(Ui.It);
        var meta = new HorizontalStackLayout { Spacing = 5, Children =
        {
            new BoxView { WidthRequest = 8, HeightRequest = 8, CornerRadius = 4, Color = Ui.C(prio), VerticalOptions = LayoutOptions.Center },
            new Label { Text = $"{Ui.PriorityText(t.Priority)} · {due}", FontSize = 12, FontFamily = Ui.FontSemi, TextColor = Ui.C("SubTextColor"), VerticalOptions = LayoutOptions.Center }
        } };
        if (!string.IsNullOrWhiteSpace(t.Notes)) meta.Add(Ui.Icon(Ui.Icons.Note, Ui.C("SubTextColor"), 13));
        info.Add(meta);

        g.Add(info, 1);
        Ui.OnTap(info, () => Open(new TaskEditorPage(_db, Refresh, t)));
        return g;
    }
}
