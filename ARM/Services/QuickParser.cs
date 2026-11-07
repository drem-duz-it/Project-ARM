using System.Globalization;
using System.Text.RegularExpressions;
using ARM.Data;
using ARM.Models;

namespace ARM.Services;

// Cosa diventerà una frase scritta al volo
public enum QuickKind { Task, Evento, Pagamento, Promemoria, Idea, Nota }

public class QuickEntry
{
    public QuickKind Kind;
    public string Title = "";
    public DateTime? Date;          // solo il giorno
    public TimeSpan? Time;          // orario, se detto
    public double Amount;
    public bool IsIncome;
    public bool IsMonthly;
    public string Category = "";
    public int Priority = 1;

    public string KindName => Kind switch
    {
        QuickKind.Task => "Task",
        QuickKind.Evento => "Evento",
        QuickKind.Pagamento => IsIncome ? "Entrata" : "Pagamento",
        QuickKind.Promemoria => "Promemoria",
        QuickKind.Idea => "Idea",
        _ => "Nota"
    };

    public string ColorKey => Kind switch
    {
        QuickKind.Task => "AccentColor",
        QuickKind.Evento => "Accent5Color",
        QuickKind.Pagamento => IsIncome ? "Accent3Color" : "Accent2Color",
        QuickKind.Promemoria => "Accent4Color",
        QuickKind.Idea => "Accent3Color",
        _ => "AccentColor"
    };

    public string Icon => Kind switch
    {
        QuickKind.Task => Ui.Icons.Task,
        QuickKind.Evento => Ui.Icons.Calendar,
        QuickKind.Pagamento => Ui.Icons.Euro,
        QuickKind.Promemoria => Ui.Icons.Bell,
        QuickKind.Idea => Ui.Icons.Bulb,
        _ => Ui.Icons.Note
    };

    // Dettagli riconosciuti, da mostrare come etichette sotto il campo
    public List<string> Details()
    {
        var d = new List<string>();
        if (Kind == QuickKind.Pagamento) d.Add((IsIncome ? "+" : "−") + Amount.ToString("C", Ui.It));
        if (Date.HasValue) d.Add(Ui.RelativeDate(Date.Value) + (Time.HasValue ? $" {Time.Value:hh\\:mm}" : ""));
        else if (Time.HasValue) d.Add($"Oggi {Time.Value:hh\\:mm}");
        if (IsMonthly) d.Add("Ogni mese");
        if (!string.IsNullOrEmpty(Category))
        {
            var c = Models.Category.For(IsIncome).FirstOrDefault(x => x.Name == Category);
            if (c != null) d.Add(c.Label);
        }
        if (Priority == 2 && Kind == QuickKind.Task) d.Add("Priorità alta");
        return d;
    }

    // Oggetti pronti da salvare (o da aprire nel modulo per ritoccarli)
    public object ToItem()
    {
        switch (Kind)
        {
            case QuickKind.Task:
                return new TaskItem { Title = Title, DueDate = Date, Priority = Priority };
            case QuickKind.Evento:
                var start = Time ?? new TimeSpan(9, 0, 0);
                return new CalendarEvent
                {
                    Title = Title,
                    Date = Date ?? DateTime.Today,
                    AllDay = !Time.HasValue,
                    StartTime = start,
                    EndTime = start.Add(TimeSpan.FromHours(1)) > TimeSpan.FromHours(24) ? new TimeSpan(23, 59, 0) : start.Add(TimeSpan.FromHours(1))
                };
            default:
                var n = new Note
                {
                    Title = Title,
                    Type = Kind switch { QuickKind.Pagamento => "Pagamento", QuickKind.Promemoria => "Promemoria", QuickKind.Idea => "Idea", _ => "Nota" }
                };
                if (Kind == QuickKind.Pagamento)
                {
                    n.Amount = Amount;
                    n.IsIncome = IsIncome;
                    n.IsMonthly = IsMonthly;
                    n.Category = Category;
                    n.DueDate = (Date ?? DateTime.Today).Date;
                }
                else if (Date.HasValue || Time.HasValue)
                    n.DueDate = (Date ?? DateTime.Today).Date + (Time ?? TimeSpan.Zero);
                return n;
        }
    }

    public async Task SaveAsync(DatabaseService db)
    {
        switch (ToItem())
        {
            case TaskItem t: await db.SaveTaskAsync(t); break;
            case CalendarEvent e: await db.SaveEventAsync(e); break;
            case Note n: await db.SaveNoteAsync(n); break;
        }
    }
}

// Capisce frasi come:
//   "bollo auto 85€ venerdì"          → pagamento, 85 €, venerdì, categoria Trasporti
//   "dentista domani alle 15:30"      → evento domani 15:30
//   "comprare le lampadine"           → task
//   "ricordami di chiamare Luca alle 18" → promemoria oggi 18:00
//   "Marco mi deve 20 euro entro il 15" → entrata di 20 € entro il 15
// Tutto in locale, nessun servizio esterno.
public static class QuickParser
{
    private const RegexOptions O = RegexOptions.IgnoreCase | RegexOptions.CultureInvariant;

    private static readonly string[] Mesi = { "gennaio", "febbraio", "marzo", "aprile", "maggio", "giugno", "luglio", "agosto", "settembre", "ottobre", "novembre", "dicembre" };
    private static readonly string[] MesiCorti = { "gen", "feb", "mar", "apr", "mag", "giu", "lug", "ago", "set", "ott", "nov", "dic" };
    private static readonly string[] Giorni = { "domenica", "lunedì", "martedì", "mercoledì", "giovedì", "venerdì", "sabato" };

    private static readonly Dictionary<string, int> Numeri = new()
    {
        ["un"] = 1, ["uno"] = 1, ["una"] = 1, ["due"] = 2, ["tre"] = 3, ["quattro"] = 4, ["cinque"] = 5,
        ["sei"] = 6, ["sette"] = 7, ["otto"] = 8, ["nove"] = 9, ["dieci"] = 10, ["quindici"] = 15, ["venti"] = 20, ["trenta"] = 30
    };

    private static readonly string[] ParoleEvento =
    {
        "appuntamento", "riunione", "meeting", "call", "videochiamata", "cena", "pranzo", "colazione", "aperitivo", "festa",
        "colloquio", "visita", "dentista", "medico", "dottore", "compleanno", "concerto", "partita", "lezione", "esame",
        "volo", "treno", "cinema", "teatro", "matrimonio", "incontro", "palestra", "allenamento", "parrucchiere", "barbiere"
    };

    private static readonly string[] ParoleEntrata =
    {
        "mi deve", "mi devono", "mi ridà", "mi ridanno", "mi restituisce", "incasso", "incassare", "ricevo", "ricevere",
        "riceverò", "devo ricevere", "rimborso", "stipendio", "entrata", "mi pagano", "mi paga", "accredito"
    };

    public static QuickEntry Parse(string input, DateTime? nowOverride = null)
    {
        var now = nowOverride ?? DateTime.Now;
        var today = now.Date;
        var e = new QuickEntry();
        string s = " " + (input ?? "").Trim() + " ";
        QuickKind? forced = null;

        // 1. Parola iniziale che dice esplicitamente cosa è
        var pre = Regex.Match(s, @"^\s*(task|todo|da fare|evento|appuntamento|pagamento|entrata|idea|nota|appunto|promemoria|ricordami(?: di| che)?|ricorda(?:mi)?)\b\s*[:\-–]?\s*", O);
        if (pre.Success)
        {
            string w = pre.Groups[1].Value.ToLowerInvariant();
            forced = w switch
            {
                "task" or "todo" or "da fare" => QuickKind.Task,
                "evento" => QuickKind.Evento,
                "appuntamento" => QuickKind.Evento,
                "pagamento" or "entrata" => QuickKind.Pagamento,
                "idea" => QuickKind.Idea,
                "nota" or "appunto" => QuickKind.Nota,
                _ => QuickKind.Promemoria
            };
            if (w == "entrata") e.IsIncome = true;
            // "appuntamento" resta anche nel titolo, il resto si toglie
            s = w is "appuntamento" ? s : " " + s[(pre.Index + pre.Length)..];
        }

        // 2. Importo: "85€", "€ 85", "85,50 euro", "1.200 €"
        var amount = Regex.Match(s, @"(?<![\w,.])(?:€\s*(\d{1,3}(?:\.\d{3})+|\d+)(?:,(\d{1,2}))?|(\d{1,3}(?:\.\d{3})+|\d+)(?:[,.](\d{1,2}))?\s*(?:€|euro\b|eur\b))", O);
        if (amount.Success)
        {
            string intPart = (amount.Groups[1].Success ? amount.Groups[1].Value : amount.Groups[3].Value).Replace(".", "");
            string dec = amount.Groups[2].Success ? amount.Groups[2].Value : amount.Groups[4].Success ? amount.Groups[4].Value : "";
            if (double.TryParse(intPart + (dec.Length > 0 ? "." + dec : ""), NumberStyles.Any, CultureInfo.InvariantCulture, out var a))
            {
                e.Amount = a;
                s = Cut(s, amount);
            }
        }

        // 3. Ricorrenza mensile
        var monthly = Regex.Match(s, @"\b(ogni mese|mensile|mensilmente|al mese|tutti i mesi)\b", O);
        if (monthly.Success) { e.IsMonthly = true; s = Cut(s, monthly); }

        // 4. Priorità
        var urgent = Regex.Match(s, @"(\b(urgente|importante|priorità alta)\b|!{1,3}(?=\s*$))", O);
        if (urgent.Success) { e.Priority = 2; s = Cut(s, urgent); }

        // 5. Orario
        TimeSpan? time = null;
        var t = Regex.Match(s, @"\b(?:alle|ore|h|verso le|dalle)\s+(\d{1,2})(?:[:.](\d{2}))?(?:\s*(di mattina|del mattino|di pomeriggio|del pomeriggio|di sera|della sera|di notte))?\b", O);
        if (!t.Success) t = Regex.Match(s, @"\b(\d{1,2}):(\d{2})()\b", O);
        if (!t.Success)
        {
            var dotted = Regex.Match(s, @"\b(\d{1,2})\.(\d{2})()\b", O);
            // "15.30" è un orario, "15.11" è una data: decide il valore
            if (dotted.Success && int.Parse(dotted.Groups[2].Value) > 12) t = dotted;
        }
        if (t.Success)
        {
            int h = int.Parse(t.Groups[1].Value);
            int m = t.Groups[2].Success && t.Groups[2].Value.Length > 0 ? int.Parse(t.Groups[2].Value) : 0;
            string part = t.Groups[3].Value.ToLowerInvariant();
            if ((part.Contains("pomeriggio") || part.Contains("sera")) && h < 12) h += 12;
            else if (!part.Contains("mattin") && h >= 1 && h <= 5 && !t.Value.Contains(':')) h += 12;   // "alle 3" = 15:00
            if (h <= 23 && m <= 59) { time = new TimeSpan(h, m, 0); s = Cut(s, t); }
        }
        var noon = Regex.Match(s, @"\b(a )?mezzogiorno\b", O);
        if (time == null && noon.Success) { time = new TimeSpan(12, 0, 0); s = Cut(s, noon); }

        // 6. Giorno
        DateTime? date = null;
        Match d;
        if ((d = Regex.Match(s, @"\bstasera\b", O)).Success) { date = today; time ??= new TimeSpan(20, 0, 0); s = Cut(s, d); }
        else if ((d = Regex.Match(s, @"\b(stamattina|stamani|oggi)\b", O)).Success) { date = today; s = Cut(s, d); }
        else if ((d = Regex.Match(s, @"\bdopodomani\b", O)).Success) { date = today.AddDays(2); s = Cut(s, d); }
        else if ((d = Regex.Match(s, @"\bdomani(?: (mattina|sera|pomeriggio))?\b", O)).Success)
        {
            date = today.AddDays(1);
            if (d.Groups[1].Value.Equals("sera", StringComparison.OrdinalIgnoreCase)) time ??= new TimeSpan(20, 0, 0);
            s = Cut(s, d);
        }
        else if ((d = Regex.Match(s, @"\b(?:tra|fra)\s+(\d+|\p{L}+)\s+(giorn[oi]|settiman[ae]|mes[ei])\b", O)).Success
                 && (int.TryParse(d.Groups[1].Value, out var qn) || Numeri.TryGetValue(d.Groups[1].Value.ToLowerInvariant(), out qn)))
        {
            string unit = d.Groups[2].Value.ToLowerInvariant();
            date = unit.StartsWith("giorn") ? today.AddDays(qn) : unit.StartsWith("settiman") ? today.AddDays(7 * qn) : today.AddMonths(qn);
            s = Cut(s, d);
        }
        else if ((d = Regex.Match(s, @"\b(?:la )?settimana prossima\b|\b(?:la )?prossima settimana\b", O)).Success)
        {
            int toMonday = ((int)DayOfWeek.Monday - (int)today.DayOfWeek + 7) % 7;
            date = today.AddDays(toMonday == 0 ? 7 : toMonday);
            s = Cut(s, d);
        }
        else if ((d = Regex.Match(s, @"\b(?:a )?fine mese\b", O)).Success)
        {
            date = new DateTime(today.Year, today.Month, DateTime.DaysInMonth(today.Year, today.Month));
            s = Cut(s, d);
        }
        else if ((d = Regex.Match(s, @"\b(?:il )?(?:mese prossimo|prossimo mese|inizio mese)\b", O)).Success)
        {
            date = new DateTime(today.Year, today.Month, 1).AddMonths(1);
            s = Cut(s, d);
        }
        else if ((d = Regex.Match(s, @"\b(lunedi|lunedì|martedi|martedì|mercoledi|mercoledì|giovedi|giovedì|venerdi|venerdì|sabato|domenica)(\s+prossim[oa])?\b", O)).Success)
        {
            int target = Array.IndexOf(Giorni, d.Groups[1].Value.ToLowerInvariant());
            if (target < 0) target = Array.FindIndex(Giorni, g => g.Replace("ì", "i") == d.Groups[1].Value.ToLowerInvariant());
            if (target >= 0)
            {
                int diff = (target - (int)today.DayOfWeek + 7) % 7;
                if (diff == 0 && d.Groups[2].Success) diff = 7;
                date = today.AddDays(diff);
                s = Cut(s, d);
            }
        }
        else if ((d = Regex.Match(s, @"\b(\d{1,2})\s+(" + string.Join("|", Mesi) + "|" + string.Join("|", MesiCorti) + @")\.?(?:\s+(\d{4}))?\b", O)).Success)
        {
            int day = int.Parse(d.Groups[1].Value);
            string mn = d.Groups[2].Value.ToLowerInvariant();
            int month = Array.IndexOf(Mesi, mn) + 1;
            if (month == 0) month = Array.IndexOf(MesiCorti, mn[..3]) + 1;
            date = MakeDate(today, day, month, d.Groups[3].Success ? int.Parse(d.Groups[3].Value) : null);
            if (date != null) s = Cut(s, d);
        }
        else if ((d = Regex.Match(s, @"\b(\d{1,2})[/\-.](\d{1,2})(?:[/\-.](\d{2,4}))?\b", O)).Success)
        {
            int? year = d.Groups[3].Success ? int.Parse(d.Groups[3].Value) : null;
            if (year < 100) year += 2000;
            date = MakeDate(today, int.Parse(d.Groups[1].Value), int.Parse(d.Groups[2].Value), year);
            if (date != null) s = Cut(s, d);
        }
        else if ((d = Regex.Match(s, @"\b(?:entro il|per il|il giorno|il)\s+(\d{1,2})\b(?!\s*(?:€|euro|eur|:|\.\d))", O)).Success)
        {
            int day = int.Parse(d.Groups[1].Value);
            if (day >= 1 && day <= 31)
            {
                var candidate = MakeDate(today, day, today.Month, today.Year);
                if (candidate == null || candidate < today) candidate = MakeDate(today, day, today.AddMonths(1).Month, today.AddMonths(1).Year);
                date = candidate;
                if (date != null) s = Cut(s, d);
            }
        }

        // 7. Che cosa è
        string lower = s.ToLowerInvariant();
        bool income = e.IsIncome || ParoleEntrata.Any(w => lower.Contains(w));
        if (forced.HasValue) e.Kind = forced.Value;
        else if (e.Amount > 0) e.Kind = QuickKind.Pagamento;
        else if (Regex.IsMatch(lower, @"^\s*(ricordami|ricorda)\b")) e.Kind = QuickKind.Promemoria;
        else if (time.HasValue || ParoleEvento.Any(w => Regex.IsMatch(lower, $@"\b{w}\b"))) e.Kind = date.HasValue || time.HasValue ? QuickKind.Evento : QuickKind.Task;
        else e.Kind = QuickKind.Task;

        // Un orario già passato oggi, senza giorno detto → domani
        if (e.Kind == QuickKind.Evento && date == null && time.HasValue && today + time.Value < now) date = today.AddDays(1);

        e.Date = date;
        e.Time = time;
        if (e.Kind == QuickKind.Pagamento)
        {
            e.IsIncome = income;
            e.Time = null;
        }

        // 8. Titolo: quello che resta, pulito dalle parole di collegamento rimaste appese
        string title = Regex.Replace(s, @"\s{2,}", " ").Trim();
        if (e.Kind == QuickKind.Promemoria) title = Regex.Replace(title, @"^(ricordami|ricorda)( di| che)?\s+", "", O);
        string glue = @"(il|lo|la|i|gli|le|di|del|della|per|entro|da|a|alle|ore|e|,|-|–|:|;)";
        for (int i = 0; i < 3; i++)
        {
            title = Regex.Replace(title, $@"\s+{glue}$", "", O).Trim();
            title = Regex.Replace(title, $@"^{glue}\s+", "", O).Trim();
        }
        title = title.Trim(' ', ',', '-', '–', ':', ';', '.');
        e.Title = title.Length == 0 ? e.KindName : char.ToUpper(title[0], Ui.It) + title[1..];

        if (e.Kind == QuickKind.Pagamento) e.Category = Category.Guess(input, e.IsIncome);
        return e;
    }

    private static string Cut(string s, Match m) => s[..m.Index] + " " + s[(m.Index + m.Length)..];

    private static DateTime? MakeDate(DateTime today, int day, int month, int? year)
    {
        if (month < 1 || month > 12) return null;
        int y = year ?? today.Year;
        if (day < 1 || day > DateTime.DaysInMonth(y, month)) return null;
        var date = new DateTime(y, month, day);
        // senza anno, una data già passata è quella dell'anno prossimo
        if (year == null && date < today.AddDays(-1) && DateTime.DaysInMonth(y + 1, month) >= day) date = date.AddYears(1);
        return date;
    }
}
