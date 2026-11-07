using Android.App;
using Android.Content;
using Android.OS;
using AndroidX.Core.App;
using ARM.Services;

namespace ARM.Services
{
    public partial class ReminderService
    {
        private partial void PlatformSchedule(List<PlannedReminder> plan) =>
            ARM.Platforms.Android.AndroidReminders.Schedule(global::Android.App.Application.Context, plan);
    }
}

namespace ARM.Platforms.Android
{
    // Programma le notifiche con AlarmManager: arrivano anche ad app chiusa
    public static class AndroidReminders
    {
        public const string ChannelId = "promemoria";
        private const string PrefsName = "armusic_promemoria";
        private const string PrefsKey = "piano";

        public static void Schedule(Context ctx, List<PlannedReminder> plan)
        {
            var alarms = (AlarmManager)ctx.GetSystemService(Context.AlarmService);

            // 1. Cancella le notifiche programmate in precedenza
            foreach (var old in Load(ctx))
                alarms.Cancel(MakeIntent(ctx, old));

            // 2. Programma solo quelle future (un orario passato farebbe scattare subito l'allarme)
            var future = plan.Where(r => r.At > DateTime.Now).ToList();
            foreach (var r in future)
            {
                long millis = new DateTimeOffset(r.At).ToUnixTimeMilliseconds();
                var pi = MakeIntent(ctx, r);
                bool exact = Build.VERSION.SdkInt < BuildVersionCodes.S || alarms.CanScheduleExactAlarms();
                if (exact) alarms.SetExactAndAllowWhileIdle(AlarmType.RtcWakeup, millis, pi);
                else alarms.SetAndAllowWhileIdle(AlarmType.RtcWakeup, millis, pi);
            }

            // 3. Salva il piano per poterlo ripristinare dopo un riavvio del telefono
            Save(ctx, future);
        }

        // Dopo il riavvio: riprogramma il piano salvato
        public static void Restore(Context ctx) => Schedule(ctx, Load(ctx));

        private static PendingIntent MakeIntent(Context ctx, PlannedReminder r, bool snooze = false)
        {
            var intent = new Intent(ctx, typeof(ReminderReceiver));
            intent.PutExtra("id", r.Id);
            intent.PutExtra("title", r.Title);
            intent.PutExtra("body", r.Body);
            intent.PutExtra("kind", r.Kind ?? "");
            intent.PutExtra("item", r.ItemId ?? "");
            intent.PutExtra("snooze", snooze);
            // Le notifiche rimandate hanno un codice diverso, così riprogrammare il piano non le cancella
            int code = snooze ? ReminderService.StableId("snooze:" + r.Id) : r.Id;
            return PendingIntent.GetBroadcast(ctx, code, intent, PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable);
        }

        // Ripropone una notifica più tardi (pulsanti "Tra 30 min", "Ricordamelo domani"...)
        public static void Snooze(Context ctx, PlannedReminder r, DateTime at)
        {
            var alarms = (AlarmManager)ctx.GetSystemService(Context.AlarmService);
            long millis = new DateTimeOffset(at).ToUnixTimeMilliseconds();
            var pi = MakeIntent(ctx, r, snooze: true);
            bool exact = Build.VERSION.SdkInt < BuildVersionCodes.S || alarms.CanScheduleExactAlarms();
            if (exact) alarms.SetExactAndAllowWhileIdle(AlarmType.RtcWakeup, millis, pi);
            else alarms.SetAndAllowWhileIdle(AlarmType.RtcWakeup, millis, pi);
        }

        // Formato semplice "id|ticks|titolo|testo|tipo|elemento" (base64) per evitare dipendenze dal serializzatore
        private static void Save(Context ctx, List<PlannedReminder> plan)
        {
            string B64(string s) => Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(s ?? ""));
            var data = string.Join("\n", plan.Select(r => $"{r.Id}|{r.At.Ticks}|{B64(r.Title)}|{B64(r.Body)}|{r.Kind}|{r.ItemId}"));
            ctx.GetSharedPreferences(PrefsName, FileCreationMode.Private).Edit().PutString(PrefsKey, data).Apply();
        }

        private static List<PlannedReminder> Load(Context ctx)
        {
            string Txt(string s) => System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(s));
            var data = ctx.GetSharedPreferences(PrefsName, FileCreationMode.Private).GetString(PrefsKey, "");
            var list = new List<PlannedReminder>();
            foreach (var line in data.Split('\n', StringSplitOptions.RemoveEmptyEntries))
            {
                var p = line.Split('|');
                if (p.Length >= 4 && int.TryParse(p[0], out var id) && long.TryParse(p[1], out var ticks))
                    list.Add(new PlannedReminder(id, Txt(p[2]), Txt(p[3]), new DateTime(ticks), p.Length > 4 ? p[4] : null, p.Length > 5 ? p[5] : null));
            }
            return list;
        }

        public static void EnsureChannel(Context ctx)
        {
            if (Build.VERSION.SdkInt < BuildVersionCodes.O) return;
            var manager = (NotificationManager)ctx.GetSystemService(Context.NotificationService);
            if (manager.GetNotificationChannel(ChannelId) != null) return;
            var channel = new NotificationChannel(ChannelId, Loc.T("Promemoria e scadenze"), NotificationImportance.High)
            {
                Description = Loc.T("Eventi, task, pagamenti e promemoria")
            };
            manager.CreateNotificationChannel(channel);
        }
    }

    // Riceve l'allarme e mostra la notifica (con i pulsanti Fatto / Domani / Tra 30 min...)
    [BroadcastReceiver(Enabled = true, Exported = false)]
    public class ReminderReceiver : BroadcastReceiver
    {
        public override void OnReceive(Context context, Intent intent)
        {
            var r = new PlannedReminder(intent.GetIntExtra("id", 0), intent.GetStringExtra("title"), intent.GetStringExtra("body"), DateTime.Now,
                                        intent.GetStringExtra("kind"), intent.GetStringExtra("item"));
            if (!intent.GetBooleanExtra("snooze", false)) { Show(context, r); return; }

            // Notifica rimandata: la mostra solo se ha ancora senso (task non fatto, pagamento non pagato...)
            var pending = GoAsync();
            Task.Run(async () =>
            {
                try
                {
                    var svc = IPlatformApplication.Current?.Services.GetService<ReminderService>();
                    if (svc == null || await svc.StillRelevantAsync(r.Kind, r.ItemId)) Show(context, r);
                }
                catch { Show(context, r); }
                finally { pending.Finish(); }
            });
        }

        public static void Show(Context context, PlannedReminder r)
        {
            AndroidReminders.EnsureChannel(context);

            var open = context.PackageManager.GetLaunchIntentForPackage(context.PackageName);
            var contentIntent = PendingIntent.GetActivity(context, r.Id, open, PendingIntentFlags.Immutable | PendingIntentFlags.UpdateCurrent);

            var builder = new NotificationCompat.Builder(context, AndroidReminders.ChannelId)
                .SetSmallIcon(Resource.Drawable.ic_notifica)
                .SetColor(unchecked((int)0xFF6C4DFF))
                .SetContentTitle(r.Title)
                .SetContentText(r.Body)
                .SetStyle(new NotificationCompat.BigTextStyle().BigText(r.Body))
                .SetPriority(NotificationCompat.PriorityHigh)
                .SetCategory(NotificationCompat.CategoryReminder)
                .SetContentIntent(contentIntent)
                .SetAutoCancel(true);

            if (!string.IsNullOrEmpty(r.ItemId))
            {
                foreach (var a in ReminderService.ActionsFor(r.Kind))
                {
                    var i = new Intent(context, typeof(ReminderActionReceiver));
                    i.PutExtra("action", a.Key);
                    i.PutExtra("id", r.Id);
                    i.PutExtra("title", r.Title);
                    i.PutExtra("body", r.Body);
                    i.PutExtra("kind", r.Kind);
                    i.PutExtra("item", r.ItemId);
                    var pi = PendingIntent.GetBroadcast(context, ReminderService.StableId(a.Key + ":" + r.Id), i, PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable);
                    builder.AddAction(0, a.Label, pi);
                }
            }

            try { NotificationManagerCompat.From(context).Notify(r.Id, builder.Build()); }
            catch (Java.Lang.SecurityException) { /* permesso notifiche negato */ }
        }
    }

    // Pulsante premuto dentro una notifica: modifica il dato (task fatto, pagamento pagato...) o la rimanda
    [BroadcastReceiver(Enabled = true, Exported = false)]
    public class ReminderActionReceiver : BroadcastReceiver
    {
        public override void OnReceive(Context context, Intent intent)
        {
            var r = new PlannedReminder(intent.GetIntExtra("id", 0), intent.GetStringExtra("title"), intent.GetStringExtra("body"), DateTime.Now,
                                        intent.GetStringExtra("kind"), intent.GetStringExtra("item"));
            string action = intent.GetStringExtra("action");
            NotificationManagerCompat.From(context).Cancel(r.Id);

            var pending = GoAsync();
            Task.Run(async () =>
            {
                try
                {
                    var svc = IPlatformApplication.Current?.Services.GetService<ReminderService>();
                    if (svc == null) return;
                    var later = await svc.HandleActionAsync(r.Kind, r.ItemId, action);
                    if (later.HasValue) AndroidReminders.Snooze(context, r, later.Value);
                    // L'app potrebbe non essere aperta: aggiorna subito notifiche e widget prima di chiudere
                    await svc.RescheduleAsync();
                    OggiWidget.UpdateAll(context);
                }
                catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Azione notifica: {ex}"); }
                finally { pending.Finish(); }
            });
        }
    }

    // Al riavvio del telefono gli allarmi vengono persi: li rimettiamo
    [BroadcastReceiver(Enabled = true, Exported = true)]
    [IntentFilter(new[] { Intent.ActionBootCompleted, "android.intent.action.QUICKBOOT_POWERON" })]
    public class BootReceiver : BroadcastReceiver
    {
        public override void OnReceive(Context context, Intent intent) => AndroidReminders.Restore(context);
    }
}
