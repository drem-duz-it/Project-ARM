using Android.App;
using Android.Appwidget;
using Android.Content;
using Android.Widget;
using ARM.Data;
using ARM.Models;
using ARM.Services;

namespace ARM.Platforms.Android;

// Widget "ARM · Oggi" sulla home del telefono:
// - data e quante cose ci sono oggi (tocco → apre ARM)
// - "Scrivi al volo…" (tocco → apre ARM direttamente sul campo di scrittura libera)
// - fino a 5 righe: eventi di oggi, task da fare (il cerchio li spunta senza aprire l'app),
//   pagamenti e entrate dei prossimi 3 giorni, promemoria di oggi
[BroadcastReceiver(Label = "ARM · Oggi", Exported = true)]
[IntentFilter(new[] { AppWidgetManager.ActionAppwidgetUpdate })]
[MetaData(AppWidgetManager.MetaDataAppwidgetProvider, Resource = "@xml/widget_oggi_info")]
public class OggiWidget : AppWidgetProvider
{
    private const string ActionToggle = "com.companyname.armusic.WIDGET_TASK_FATTO";
    public const string ExtraAzione = "arm_azione";

    private static readonly int[] Rows = { Resource.Id.row0, Resource.Id.row1, Resource.Id.row2, Resource.Id.row3, Resource.Id.row4 };
    private static readonly int[] Icons = { Resource.Id.row0_icon, Resource.Id.row1_icon, Resource.Id.row2_icon, Resource.Id.row3_icon, Resource.Id.row4_icon };
    private static readonly int[] Texts = { Resource.Id.row0_text, Resource.Id.row1_text, Resource.Id.row2_text, Resource.Id.row3_text, Resource.Id.row4_text };
    private static readonly int[] Metas = { Resource.Id.row0_meta, Resource.Id.row1_meta, Resource.Id.row2_meta, Resource.Id.row3_meta, Resource.Id.row4_meta };

    private const int Corallo = unchecked((int)0xFFF0532F);   // colori del design 2.5 (uguali in chiaro e scuro)

    private record Row(string Text, string Meta, int Color, string TaskId);

    public override void OnUpdate(Context context, AppWidgetManager appWidgetManager, int[] appWidgetIds)
    {
        var pending = GoAsync();
        Task.Run(async () => { try { await UpdateAllAsync(context); } finally { pending.Finish(); } });
    }

    public override void OnReceive(Context context, Intent intent)
    {
        if (intent.Action != ActionToggle) { base.OnReceive(context, intent); return; }

        // Cerchio di un task premuto: lo segna come fatto e ridisegna il widget
        string id = intent.GetStringExtra("task");
        var pending = GoAsync();
        Task.Run(async () =>
        {
            try
            {
                var db = IPlatformApplication.Current?.Services.GetService<DatabaseService>();
                var task = db == null ? null : (await db.GetAllTasksAsync()).FirstOrDefault(t => t.Id == id);
                if (task != null && !task.IsDone)
                {
                    await db.ToggleTaskAsync(task);
                    ReminderService.NotifyExternalChange();
                    var reminders = IPlatformApplication.Current?.Services.GetService<ReminderService>();
                    if (reminders != null) await reminders.RescheduleAsync();
                }
                await UpdateAllAsync(context);
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Widget: {ex}"); }
            finally { pending.Finish(); }
        });
    }

    // Ridisegna tutti i widget ARM presenti sulla home (chiamato quando i dati cambiano)
    public static void UpdateAll(Context context) => _ = UpdateAllAsync(context);

    public static async Task UpdateAllAsync(Context context)
    {
        try
        {
            var manager = AppWidgetManager.GetInstance(context);
            var ids = manager.GetAppWidgetIds(new ComponentName(context, Java.Lang.Class.FromType(typeof(OggiWidget))));
            if (ids == null || ids.Length == 0) return;

            var db = IPlatformApplication.Current?.Services.GetService<DatabaseService>();
            if (db == null) return;
            var rows = BuildRows(await db.GetAllNotesAsync(), await db.GetAllTasksAsync(), await db.GetAllEventsAsync(), await db.GetAllOrdiniAsync(), out int total);

            var views = new RemoteViews(context.PackageName, Resource.Layout.widget_oggi);
            views.SetTextViewText(Resource.Id.widget_date, "ARM · " + DateTime.Today.ToString("dddd d MMMM", Ui.It).ToUpper(Ui.It));
            views.SetTextViewText(Resource.Id.widget_title, total switch { 0 => Loc.T("Giornata libera"), 1 => Loc.T("1 cosa per oggi"), _ => Loc.F("{0} cose per oggi", total) });
            views.SetViewVisibility(Resource.Id.widget_empty, rows.Count == 0 ? global::Android.Views.ViewStates.Visible : global::Android.Views.ViewStates.Gone);

            views.SetOnClickPendingIntent(Resource.Id.widget_header, OpenApp(context, null, 1));
            views.SetOnClickPendingIntent(Resource.Id.widget_quick, OpenApp(context, "scrivi", 2));

            for (int i = 0; i < Rows.Length; i++)
            {
                if (i >= rows.Count) { views.SetViewVisibility(Rows[i], global::Android.Views.ViewStates.Gone); continue; }
                var r = rows[i];
                views.SetViewVisibility(Rows[i], global::Android.Views.ViewStates.Visible);
                views.SetTextViewText(Texts[i], r.Text);
                views.SetTextViewText(Metas[i], r.Meta);
                // Il colore normale viene dal layout (chiaro/scuro); in corallo solo uscite e cose in ritardo
                if (r.Color == Corallo) views.SetTextColor(Metas[i], new global::Android.Graphics.Color(Corallo));
                views.SetOnClickPendingIntent(Rows[i], OpenApp(context, null, 10 + i));

                if (r.TaskId != null)
                {
                    views.SetImageViewResource(Icons[i], Resource.Drawable.widget_check);
                    views.SetInt(Icons[i], "setColorFilter", r.Color);
                    var toggle = new Intent(context, typeof(OggiWidget)).SetAction(ActionToggle).PutExtra("task", r.TaskId);
                    views.SetOnClickPendingIntent(Icons[i], PendingIntent.GetBroadcast(context, 100 + i, toggle, PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable));
                }
                else
                {
                    views.SetImageViewResource(Icons[i], Resource.Drawable.widget_dot);
                    views.SetInt(Icons[i], "setColorFilter", r.Color);
                    views.SetOnClickPendingIntent(Icons[i], OpenApp(context, null, 10 + i));
                }
            }

            manager.UpdateAppWidget(ids, views);
        }
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Widget: {ex}"); }
    }

    private static PendingIntent OpenApp(Context context, string azione, int code)
    {
        var intent = new Intent(context, typeof(MainActivity));
        intent.SetFlags(ActivityFlags.NewTask | ActivityFlags.ClearTop | ActivityFlags.SingleTop);
        if (azione != null) intent.PutExtra(ExtraAzione, azione);
        return PendingIntent.GetActivity(context, code, intent, PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable);
    }

    // Cosa mostrare oggi, in ordine: eventi (per orario), task, soldi in scadenza, promemoria
    private static List<Row> BuildRows(List<Note> notes, List<TaskItem> tasks, List<CalendarEvent> events, List<Ordine> ordini, out int total)
    {
        const int viola = unchecked((int)0xFF6C4DFF), blu = unchecked((int)0xFF2F80ED), corallo = Corallo,
                  verde = unchecked((int)0xFF0FA678), giallo = unchecked((int)0xFFFFB020);
        var today = DateTime.Today;
        var now = DateTime.Now;
        var rows = new List<Row>();

        foreach (var e in events.Where(e => e.Date.Date == today && (e.AllDay || today + e.EndTime > now)).OrderBy(e => e.AllDay ? TimeSpan.Zero : e.StartTime))
            rows.Add(new Row(e.Title, e.AllDay ? Loc.T("tutto il giorno") : e.StartTime.ToString(@"hh\:mm"), blu, null));

        foreach (var t in tasks.Where(t => !t.IsDone && t.DueDate.HasValue && t.DueDate.Value.Date <= today).OrderByDescending(t => t.Priority).ThenBy(t => t.DueDate))
            rows.Add(new Row(t.Title, t.IsOverdue ? Loc.T("in ritardo") : "", t.Priority == 2 ? corallo : viola, t.Id));

        foreach (var n in notes.Where(n => n.IsPayment && !n.IsPaid && n.DueDate.HasValue && n.DueDate.Value.Date <= today.AddDays(3)).OrderBy(n => n.DueDate))
        {
            string when = n.DueDate.Value.Date < today ? Loc.T("scaduto") : Ui.RelativeDate(n.DueDate.Value).ToLower(Ui.It);
            rows.Add(new Row(n.Title, $"{Ui.Euro(n.IsIncome ? n.Amount : -n.Amount, true)} · {when}", n.IsIncome ? verde : corallo, null));
        }

        // 3.0: pacchi in arrivo oggi (o in ritardo)
        foreach (var o in ordini.Where(o => !o.Consegnato && o.ConsegnaPrevista.HasValue && o.ConsegnaPrevista.Value.Date <= today))
            rows.Add(new Row(o.Prodotto, o.InRitardo ? Loc.T("pacco in ritardo") : string.IsNullOrWhiteSpace(o.FasciaOraria) ? Loc.T("pacco in arrivo") : o.FasciaOraria.ToLower(Ui.It), o.InRitardo ? corallo : blu, null));

        foreach (var n in notes.Where(n => !n.IsPayment && n.DueDate.HasValue && n.DueDate.Value.Date == today).OrderBy(n => n.DueDate))
            rows.Add(new Row(n.Title, n.DueDate.Value.TimeOfDay == TimeSpan.Zero ? Loc.T("oggi") : n.DueDate.Value.ToString("HH:mm"), giallo, null));

        total = rows.Count;
        return rows.Take(5).ToList();
    }
}
