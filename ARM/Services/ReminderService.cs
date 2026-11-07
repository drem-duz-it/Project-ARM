using ARM.Data;
using ARM.Models;

namespace ARM.Services;

// Una notifica da mostrare in un certo momento.
// Kind ("task", "pay", "in", "ev", "note") e ItemId servono ai pulsanti dentro la notifica (Fatto, Domani...).
public record PlannedReminder(int Id, string Title, string Body, DateTime At, string Kind = null, string ItemId = null);

// Un pulsante dentro la notifica
public record ReminderAction(string Key, string Label);

// Calcola le notifiche di eventi, task, pagamenti e promemoria e le affida alla piattaforma:
// - cose con un orario → 1 ora prima
// - cose con solo il giorno → alle 9:00 di quel giorno
public partial class ReminderService
{
    public static readonly TimeSpan OraMattina = new(9, 0, 0);
    public static readonly TimeSpan Anticipo = TimeSpan.FromHours(1);

    private readonly DatabaseService _db;
    private CancellationTokenSource _debounce;

    public ReminderService(DatabaseService db)
    {
        _db = db;
        // Ogni modifica ai dati riprogramma le notifiche (raggruppando i cambi ravvicinati)
        _db.DataChanged += () => _ = RescheduleAsync(delayMs: 500);
    }

    public async Task RescheduleAsync(int delayMs = 0)
    {
        _debounce?.Cancel();
        var cts = _debounce = new CancellationTokenSource();
        try
        {
            if (delayMs > 0) await Task.Delay(delayMs, cts.Token);
            var plan = Plan(await _db.GetAllNotesAsync(), await _db.GetAllTasksAsync(), await _db.GetAllEventsAsync(), DateTime.Now, await _db.GetAllOrdiniAsync());
            if (!cts.IsCancellationRequested) PlatformSchedule(plan);
        }
        catch (TaskCanceledException) { }
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Promemoria: {ex}"); }
    }

    // Implementata in Platforms/Android e Platforms/Windows
    private partial void PlatformSchedule(List<PlannedReminder> plan);

    public static List<PlannedReminder> Plan(List<Note> notes, List<TaskItem> tasks, List<CalendarEvent> events, DateTime now, List<Ordine> ordini = null)
    {
        // Ogni notifica ha anche una "scadenza": dopo l'inizio dell'evento (o a fine giornata) non ha più senso
        var list = new List<(PlannedReminder Reminder, DateTime Expires)>();

        void Add(string key, string title, string body, DateTime when, bool hasTime)
        {
            var at = hasTime ? when - Anticipo : when.Date + OraMattina;
            var expires = hasTime ? when : when.Date.AddDays(1);
            var parts = key.Split(':', 2);
            list.Add((new PlannedReminder(StableId(key), title, body, at, parts[0], parts[1]), expires));
        }

        foreach (var e in events)
        {
            var luogo = string.IsNullOrWhiteSpace(e.Location) ? "" : $" · {e.Location}";
            if (e.AllDay) Add("ev:" + e.Id, Loc.F("Oggi: {0}", e.Title), Loc.T("Tutto il giorno") + luogo, e.Date, false);
            else Add("ev:" + e.Id, Loc.F("Alle {0:hh\\:mm}: {1}", e.StartTime, e.Title), Loc.T("Evento tra poco") + luogo, e.Date.Date + e.StartTime, true);
        }

        foreach (var t in tasks.Where(t => !t.IsDone && t.DueDate.HasValue))
            Add("task:" + t.Id, Loc.F("Da fare oggi: {0}", t.Title), t.Priority == 2 ? Loc.T("Priorità alta") : Loc.T("Task in scadenza oggi"), t.DueDate.Value, false);

        foreach (var n in notes.Where(n => n.DueDate.HasValue))
        {
            if (n.IsPayment)
            {
                if (!n.IsPaid)
                {
                    if (n.IsIncome) Add("in:" + n.Id, Loc.F("Soldi in arrivo: {0}", n.Title), Loc.F("Oggi dovrebbero arrivare {0}", n.AmountText), n.DueDate.Value, false);
                    else Add("pay:" + n.Id, Loc.F("Pagamento in scadenza: {0}", n.Title), Loc.F("Oggi scade {0}", n.AmountText), n.DueDate.Value, false);
                }
                continue;
            }
            bool hasTime = n.DueDate.Value.TimeOfDay != TimeSpan.Zero;
            string body = string.IsNullOrWhiteSpace(n.Content) ? n.Type : n.Content.Trim().Split('\n')[0];
            string title = hasTime ? Loc.F("Alle {0:HH:mm}: {1}", n.DueDate.Value, n.Title) : Loc.F("Oggi: {0}", n.Title);
            Add("note:" + n.Id, title, body, n.DueDate.Value, hasTime);
        }

        // 3.0: pacchi in arrivo oggi (alle 9:00, con "Ricevuto")
        foreach (var o in (ordini ?? new()).Where(o => !o.Consegnato && o.ConsegnaPrevista.HasValue))
            Add("ord:" + o.Id, Loc.F("Arriva oggi: {0}", o.Prodotto), string.IsNullOrWhiteSpace(o.FasciaOraria) ? $"{o.Negozio} · {o.StatoTesto}" : $"{o.Negozio} · {o.FasciaOraria}", o.ConsegnaPrevista.Value, false);

        // Teniamo anche quelle già passate ma ancora valide: su Windows servono se l'app era chiusa
        // all'orario esatto (es. PC acceso alle 9:30 → arriva comunque il promemoria delle 9:00)
        return list.Where(x => x.Expires > now && x.Reminder.At < now.AddDays(60))
                   .Select(x => x.Reminder)
                   .OrderBy(r => r.At)
                   .Take(100)
                   .ToList();
    }

    // ================= PULSANTI DELLE NOTIFICHE =================

    // Avvisa le pagine aperte che qualcosa è cambiato fuori dall'app (notifica, widget)
    public static event Action ExternalChange;
    public static void NotifyExternalChange() => ExternalChange?.Invoke();

    // Pulsanti da mostrare per ciascun tipo di notifica
    public static List<ReminderAction> ActionsFor(string kind) => kind switch
    {
        "task" => new() { new("done", Loc.T("Fatto")), new("tomorrow", Loc.T("Domani")) },
        "pay" => new() { new("done", Loc.T("Pagato")), new("tomorrow", Loc.T("Ricordamelo domani")) },
        "in" => new() { new("done", Loc.T("Ricevuto")), new("tomorrow", Loc.T("Ricordamelo domani")) },
        "ev" => new() { new("snooze30", Loc.T("Tra 30 min")) },
        "ord" => new() { new("done", Loc.T("Ricevuto")) },
        "note" => new() { new("snooze60", Loc.T("Tra 1 ora")), new("tomorrow", Loc.T("Domani")) },
        _ => new()
    };

    // Esegue il pulsante premuto. Se la notifica va riproposta più tardi, restituisce quando.
    public async Task<DateTime?> HandleActionAsync(string kind, string itemId, string action)
    {
        var now = DateTime.Now;
        if (action == "snooze30") return now.AddMinutes(30);
        if (action == "snooze60") return now.AddHours(1);

        switch (kind)
        {
            case "task":
                var t = (await _db.GetAllTasksAsync()).FirstOrDefault(x => x.Id == itemId);
                if (t == null) return null;
                if (action == "done" && !t.IsDone) await _db.ToggleTaskAsync(t);
                if (action == "tomorrow") { t.DueDate = DateTime.Today.AddDays(1); await _db.SaveTaskAsync(t); }
                break;

            case "pay":
            case "in":
                var p = (await _db.GetAllNotesAsync()).FirstOrDefault(x => x.Id == itemId);
                if (p == null) return null;
                if (action == "done" && !p.IsPaid) await _db.TogglePaymentAsync(p);   // se è mensile passa al mese dopo
                if (action == "tomorrow") return DateTime.Today.AddDays(1) + OraMattina;  // la scadenza resta quella
                break;

            case "ord":
                var o = (await _db.GetAllOrdiniAsync()).FirstOrDefault(x => x.Id == itemId);
                if (o == null) return null;
                if (action == "done" && !o.Consegnato) await _db.SegnaConsegnatoAsync(o);
                break;

            case "note":
                var n = (await _db.GetAllNotesAsync()).FirstOrDefault(x => x.Id == itemId);
                if (n == null) return null;
                if (action == "tomorrow" && n.DueDate.HasValue)
                {
                    // Sposta il promemoria a domani, alla stessa ora
                    n.DueDate = DateTime.Today.AddDays(1) + n.DueDate.Value.TimeOfDay;
                    await _db.SaveNoteAsync(n);
                }
                break;
        }
        NotifyExternalChange();
        return null;
    }

    // Una notifica rimandata ha ancora senso? (task già fatto, pagamento già pagato, elemento eliminato → no)
    public async Task<bool> StillRelevantAsync(string kind, string itemId)
    {
        if (string.IsNullOrEmpty(itemId)) return true;
        switch (kind)
        {
            case "task": return (await _db.GetAllTasksAsync()).Any(x => x.Id == itemId && !x.IsDone);
            case "pay":
            case "in": return (await _db.GetAllNotesAsync()).Any(x => x.Id == itemId && !x.IsPaid);
            case "note": return (await _db.GetAllNotesAsync()).Any(x => x.Id == itemId);
            case "ev": return (await _db.GetAllEventsAsync()).Any(x => x.Id == itemId);
            case "ord": return (await _db.GetAllOrdiniAsync()).Any(x => x.Id == itemId && !x.Consegnato);
            default: return true;
        }
    }

    // Id numerico stabile tra un avvio e l'altro (string.GetHashCode cambia a ogni avvio)
    public static int StableId(string key)
    {
        unchecked
        {
            uint h = 2166136261;
            foreach (char c in key) { h ^= c; h *= 16777619; }
            return (int)(h & 0x7FFFFFFF);
        }
    }
}
