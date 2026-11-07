using Microsoft.Windows.AppNotifications;
using Microsoft.Windows.AppNotifications.Builder;

namespace ARMusic.Services;

// Su Windows le notifiche partono mentre l'app è aperta (anche ridotta a icona):
// un timer controlla ogni 30 secondi se è arrivato il momento di mostrarne una.
public partial class ReminderService
{
    private List<PlannedReminder> _plan = new();
    private IDispatcherTimer _timer;
    private bool _registered;

    private partial void PlatformSchedule(List<PlannedReminder> plan)
    {
        _plan = plan;
        MainThread.BeginInvokeOnMainThread(() =>
        {
            if (!_registered)
            {
                try
                {
                    // Va agganciato prima di Register, altrimenti un clic aprirebbe un nuovo processo
                    AppNotificationManager.Default.NotificationInvoked += OnInvoked;
                    AppNotificationManager.Default.Register();
                    _registered = true;
                }
                catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Notifiche Windows non disponibili: {ex.Message}"); }
            }

            if (_timer == null)
            {
                _timer = Application.Current.Dispatcher.CreateTimer();
                _timer.Interval = TimeSpan.FromSeconds(30);
                _timer.Tick += (s, e) => CheckDue();
                _timer.Start();
            }
            CheckDue();
        });
    }

    private void CheckDue()
    {
        if (!_registered) return;

        // Notifiche già mostrate (salvate per non ripeterle dopo un riavvio dell'app)
        var shown = Preferences.Default.Get("PromemoriaMostrati", "").Split(';', StringSplitOptions.RemoveEmptyEntries).ToHashSet();
        bool changed = false;

        // Le notifiche rimandate partono una volta sola e poi si tolgono dalla lista
        List<PlannedReminder> snoozed;
        lock (_snoozed) { snoozed = _snoozed.Where(r => r.At <= DateTime.Now).ToList(); _snoozed.RemoveAll(snoozed.Contains); }
        foreach (var r in snoozed) _ = ShowSnoozedAsync(r);

        foreach (var r in _plan.Where(r => r.At <= DateTime.Now))
        {
            string key = $"{r.Id}@{r.At.Ticks}";
            if (!shown.Add(key)) continue;
            changed = true;
            Show(r);
        }

        // Tiene in memoria solo le chiavi recenti
        if (changed)
        {
            var limite = DateTime.Now.AddDays(-2).Ticks;
            var keep = shown.Where(k => long.TryParse(k.Split('@').Last(), out var t) && t > limite);
            Preferences.Default.Set("PromemoriaMostrati", string.Join(";", keep));
        }
    }

    // Notifiche rimandate con i pulsanti (restano in memoria finché ARM è aperto)
    private readonly List<PlannedReminder> _snoozed = new();

    private static void Show(PlannedReminder r)
    {
        try
        {
            var b = new AppNotificationBuilder().AddText(r.Title).AddText(r.Body);
            if (!string.IsNullOrEmpty(r.ItemId))
                foreach (var a in ActionsFor(r.Kind))
                    b.AddButton(new AppNotificationButton(a.Label)
                        .AddArgument("azione", a.Key).AddArgument("tipo", r.Kind).AddArgument("elemento", r.ItemId)
                        .AddArgument("id", r.Id.ToString()).AddArgument("titolo", r.Title).AddArgument("testo", r.Body));
            AppNotificationManager.Default.Show(b.BuildNotification());
        }
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine(ex.Message); }
    }

    private async Task ShowSnoozedAsync(PlannedReminder r)
    {
        if (await StillRelevantAsync(r.Kind, r.ItemId)) Show(r);
    }

    // Clic su un pulsante della notifica (arriva su un thread di sistema)
    private async void OnInvoked(AppNotificationManager sender, AppNotificationActivatedEventArgs e)
    {
        try
        {
            if (!e.Arguments.TryGetValue("azione", out var action)) return;
            e.Arguments.TryGetValue("tipo", out var kind);
            e.Arguments.TryGetValue("elemento", out var item);
            e.Arguments.TryGetValue("titolo", out var title);
            e.Arguments.TryGetValue("testo", out var body);
            int.TryParse(e.Arguments.TryGetValue("id", out var id) ? id : "0", out var nid);

            var later = await HandleActionAsync(kind, item, action);
            if (later.HasValue)
                lock (_snoozed) _snoozed.Add(new PlannedReminder(nid, title, body, later.Value, kind, item));
        }
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Azione notifica: {ex}"); }
    }
}
