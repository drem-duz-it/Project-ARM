using System.Text.Json;
using ARM.Data;
using ARM.Models;

namespace ARM.Services.Sync;

// Un elemento cambiato da entrambe le parti: decide l'utente quale versione tenere
public class SyncConflict
{
    public string Key;
    public string Kind;              // "Nota", "Task", "Evento", "Playlist", "Brano"
    public string Title;
    public string LocalSummary;      // com'è su questo dispositivo
    public string RemoteSummary;     // com'è sull'altro
    public SyncMerge.Entry Local;
    public SyncMerge.Entry Remote;
    public bool UseLocal = true;
}

// Unisce le modifiche dei due dispositivi
public class SyncMerge
{
    // Una modifica: un elemento salvato oppure un'eliminazione
    public class Entry
    {
        public string Key;
        public object Record;
        public SyncTombstone Deletion;
        public bool IsDeletion => Deletion != null;
    }

    public SyncPayload ToLocal { get; } = new();     // da applicare qui
    public SyncPayload ToRemote { get; } = new();    // da mandare all'altro dispositivo
    public List<SyncConflict> Conflicts { get; } = new();

    public static SyncMerge Compute(SyncPayload local, SyncPayload remote)
    {
        var merge = new SyncMerge();
        var l = Index(local);
        var r = Index(remote);

        foreach (var key in l.Keys.Union(r.Keys))
        {
            bool inL = l.TryGetValue(key, out var le);
            bool inR = r.TryGetValue(key, out var re);

            if (inL && !inR) { Add(merge.ToRemote, le); continue; }
            if (inR && !inL) { Add(merge.ToLocal, re); continue; }

            // Cambiato da entrambe le parti
            if (le.IsDeletion && re.IsDeletion) continue;
            if (!le.IsDeletion && !re.IsDeletion && Fingerprint(le.Record) == Fingerprint(re.Record)) continue;

            // Ordini: li scrive la posta, non l'utente. Niente da chiedere: vince lo stato più avanti,
            // a parità l'aggiornamento più recente; un'eliminazione vince sempre.
            if (key.StartsWith(nameof(Ordine) + ":"))
            {
                if (le.IsDeletion) { Add(merge.ToRemote, le); continue; }
                if (re.IsDeletion) { Add(merge.ToLocal, re); continue; }
                var lo = (Ordine)le.Record; var ro = (Ordine)re.Record;
                bool localWins = lo.Stato != ro.Stato ? lo.Stato > ro.Stato : lo.Aggiornato >= ro.Aggiornato;
                if (localWins) Add(merge.ToRemote, le); else Add(merge.ToLocal, re);
                continue;
            }

            // Collegamenti playlist-brano: vince chi lo tiene (niente da chiedere)
            if (key.StartsWith(nameof(PlaylistSong) + ":"))
            {
                if (le.IsDeletion) Add(merge.ToLocal, re); else Add(merge.ToRemote, le);
                continue;
            }

            merge.Conflicts.Add(Describe(key, le, re));
        }
        return merge;
    }

    // Dopo la scelta dell'utente, sposta ogni conflitto nella direzione giusta
    public void ApplyDecisions()
    {
        foreach (var c in Conflicts)
        {
            if (c.UseLocal) Add(ToRemote, c.Local);
            else Add(ToLocal, c.Remote);
        }
    }

    private static Dictionary<string, Entry> Index(SyncPayload p)
    {
        var d = new Dictionary<string, Entry>();
        void Put(string key, object rec) => d[key] = new Entry { Key = key, Record = rec };

        foreach (var x in p.Notes) Put($"{nameof(Note)}:{x.Id}", x);
        foreach (var x in p.Tasks) Put($"{nameof(TaskItem)}:{x.Id}", x);
        foreach (var x in p.Events) Put($"{nameof(CalendarEvent)}:{x.Id}", x);
        foreach (var x in p.Ordini ?? new()) Put($"{nameof(Ordine)}:{x.Id}", x);
        foreach (var x in p.Playlists) Put($"{nameof(Playlist)}:{x.Id}", x);
        foreach (var x in p.Songs) Put($"{nameof(Song)}:{x.Id}", x);
        foreach (var x in p.Links) Put($"{nameof(PlaylistSong)}:{DatabaseService.LinkKey(x)}", x);
        foreach (var x in p.Deletions) d[x.Key] = new Entry { Key = x.Key, Deletion = x };
        return d;
    }

    private static void Add(SyncPayload p, Entry e)
    {
        if (e.IsDeletion) { p.Deletions.Add(e.Deletion); return; }
        switch (e.Record)
        {
            case Note n: p.Notes.Add(n); break;
            case TaskItem t: p.Tasks.Add(t); break;
            case CalendarEvent ev: p.Events.Add(ev); break;
            case Ordine o: p.Ordini.Add(o); break;
            case Playlist pl: p.Playlists.Add(pl); break;
            case Song s: p.Songs.Add(s); break;
            case PlaylistSong ps: p.Links.Add(ps); break;
        }
    }

    // "Impronta" del contenuto, ignorando i campi che cambiano da dispositivo a dispositivo
    private static string Fingerprint(object rec)
    {
        switch (rec)
        {
            case Note n:
                var (c1, u1) = (n.ChangedAt, n.UpdatedAt);
                n.ChangedAt = 0; n.UpdatedAt = default;
                var s1 = JsonSerializer.Serialize(n, SyncJson.Default.Note);
                (n.ChangedAt, n.UpdatedAt) = (c1, u1);
                return s1;
            case TaskItem t: return WithoutStamp(t, x => x.ChangedAt, (x, v) => x.ChangedAt = v, x => JsonSerializer.Serialize(x, SyncJson.Default.TaskItem));
            case CalendarEvent e: return WithoutStamp(e, x => x.ChangedAt, (x, v) => x.ChangedAt = v, x => JsonSerializer.Serialize(x, SyncJson.Default.CalendarEvent));
            case Ordine o: return WithoutStamp(o, x => x.ChangedAt, (x, v) => x.ChangedAt = v, x => JsonSerializer.Serialize(x, SyncJson.Default.Ordine));
            case Playlist p: return WithoutStamp(p, x => x.ChangedAt, (x, v) => x.ChangedAt = v, x => JsonSerializer.Serialize(x, SyncJson.Default.Playlist));
            case Song s: return WithoutStamp(s, x => x.ChangedAt, (x, v) => x.ChangedAt = v, x => JsonSerializer.Serialize(x, SyncJson.Default.Song));
            default: return rec?.ToString();
        }
    }

    private static string WithoutStamp<T>(T item, Func<T, long> get, Action<T, long> set, Func<T, string> serialize)
    {
        var old = get(item);
        set(item, 0);
        var json = serialize(item);
        set(item, old);
        return json;
    }

    // Testi leggibili per la schermata dei conflitti
    private static SyncConflict Describe(string key, Entry local, Entry remote)
    {
        var any = local.Record ?? remote.Record;
        var (kind, title) = any switch
        {
            Note n => (n.IsPayment ? "Pagamento" : n.Type, n.Title),
            TaskItem t => ("Task", t.Title),
            CalendarEvent e => ("Evento", e.Title),
            Playlist p => ("Playlist", p.Name),
            Song s => ("Brano", s.Title),
            _ => ("Elemento", "")
        };
        return new SyncConflict
        {
            Key = key, Kind = kind, Title = title,
            Local = local, Remote = remote,
            LocalSummary = Summary(local), RemoteSummary = Summary(remote)
        };
    }

    private static string Summary(Entry e)
    {
        if (e.IsDeletion) return "Eliminato";
        string When(DateTime? d) => d.HasValue ? Ui.RelativeDate(d.Value) : "senza data";
        // Tutto il testo su una riga (le righe separate da " / "), così si vede dove differiscono
        string Text(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return "";
            var flat = string.Join(" / ", s.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
            return " · " + (flat.Length > 140 ? flat[..140] + "…" : flat);
        }

        return e.Record switch
        {
            Note n when n.IsPayment => $"{n.Title} · {n.SignedAmountText} · {(n.IsPaid ? n.PaymentStatusText.ToLower() : (n.IsIncome ? "arriva " : "scade ") + When(n.DueDate).ToLower())}",
            Note n => $"{n.Title}{Text(n.Content)}",
            TaskItem t => $"{t.Title} · {When(t.DueDate)}{(t.IsDone ? " · fatto" : "")}{Text(t.Notes)}",
            CalendarEvent ev => $"{ev.Title} · {Ui.RelativeDate(ev.Date)} · {ev.TimeText}{(string.IsNullOrWhiteSpace(ev.Location) ? "" : " · " + ev.Location)}",
            Playlist p => $"{p.Name}{Text(p.Description)}",
            Song s => $"{s.Title} · {s.Artist}",
            _ => ""
        };
    }
}
