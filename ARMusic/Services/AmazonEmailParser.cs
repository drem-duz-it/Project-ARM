using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;
using ARMusic.Models;

namespace ARMusic.Services;

// Cosa si ricava da una email di Amazon (conferma, spedizione, in consegna, consegnato, annullato)
public class EmailOrdine
{
    public string NumeroOrdine;
    public StatoSpedizione? Stato;          // null = email non riconosciuta come aggiornamento d'ordine
    public bool Annullato;
    public string Prodotto;
    public DateTime? ConsegnaPrevista;
    public string FasciaOraria;
    public string LinkTracking;
    public DateTime DataEmail;
    public string MessageId;

    public bool Utile => Annullato || (Stato.HasValue && (NumeroOrdine != null || Prodotto != null));
}

// Legge le email di Amazon in italiano (con qualche parola inglese di riserva). Funzione pura: niente rete,
// niente database, così si prova con le email salvate come fixture. Se Amazon cambia i testi, si ritocca qui.
public static class AmazonEmailParser
{
    private static readonly CultureInfo It = new("it-IT");
    private static readonly RegexOptions Opt = RegexOptions.IgnoreCase | RegexOptions.CultureInvariant;

    public static readonly Regex NumeroRx = new(@"\b(\d{3}-\d{7}-\d{7})\b", RegexOptions.Compiled);

    private static readonly string[] Mesi = { "gennaio", "febbraio", "marzo", "aprile", "maggio", "giugno", "luglio", "agosto", "settembre", "ottobre", "novembre", "dicembre" };
    private static readonly string[] MesiBrevi = { "gen", "feb", "mar", "apr", "mag", "giu", "lug", "ago", "set", "ott", "nov", "dic" };
    private static readonly string[] MonthsEn = { "january", "february", "march", "april", "may", "june", "july", "august", "september", "october", "november", "december" };

    // es. "martedì 11 novembre", "11 nov 2026", "11/11/2026"
    private static readonly Regex DataRx = new(
        @"(?:(?:luned[iì]|marted[iì]|mercoled[iì]|gioved[iì]|venerd[iì]|sabato|domenica)[,\s]+)?(\d{1,2})\s+(gennaio|febbraio|marzo|aprile|maggio|giugno|luglio|agosto|settembre|ottobre|novembre|dicembre|gen|feb|mar|apr|mag|giu|lug|ago|set|ott|nov|dic|january|february|march|april|may|june|july|august|september|october|november|december)\.?(?:\s+(\d{4}))?\b" +
        @"|\b(\d{1,2})/(\d{1,2})/(\d{4})\b" +
        @"|\b(oggi|domani|today|tomorrow)\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // Parole che precedono la data di consegna
    private static readonly Regex ChiaveDataRx = new(
        @"(arrivo previsto|arrivo stimato|arrivo|arriver[aà]|arriva|consegna prevista|consegna stimata|data di consegna( prevista| stimata)?|consegna|in consegna|nuova data di consegna|arriving|estimated delivery|delivery date)\s*[:\-]?",
        Opt | RegexOptions.Compiled);

    public static EmailOrdine Parse(string subject, string textBody, string htmlBody, DateTime dataEmail, string messageId = null)
    {
        subject = Pulisci(subject ?? "");
        string text = !string.IsNullOrWhiteSpace(textBody) ? Pulisci(textBody) : Pulisci(HtmlToText(htmlBody ?? ""));
        var r = new EmailOrdine { DataEmail = dataEmail, MessageId = messageId };

        var tutto = subject + "\n" + text;
        var m = NumeroRx.Match(tutto);
        if (m.Success) r.NumeroOrdine = m.Groups[1].Value;

        // ---- Stato (prima l'oggetto, poi il testo) ----
        r.Annullato = Rx(subject, @"\b(annullat[oa]|cancellat[oa]|cancelled|canceled)\b") ||
                      (Rx(text, @"\b(è stato annullato|abbiamo annullato|ordine annullato|richiesta di annullamento .* completata)\b") && !Rx(subject, @"spedit|consegn"));
        r.Stato = StatoPrefisso(subject) ?? StatoDa(subject) ?? StatoDa(Inizio(text, 600));
        if (Rx(subject + " " + Inizio(text, 400), @"\b(reso|rimborso|restituzione|refund|return)\b") && !Rx(subject, @"^(ordinato|spedito|in consegna|consegnato)")) r.Stato = null;

        // ---- Prodotto ----
        r.Prodotto = ProdottoDaOggetto(subject) ?? ProdottoDaTesto(text);

        // ---- Data di consegna ----
        if (r.Stato == StatoSpedizione.Consegnato) r.ConsegnaPrevista = dataEmail.Date;
        else
        {
            r.ConsegnaPrevista = DataDopoChiave(subject, dataEmail) ?? DataDopoChiave(text, dataEmail);
            if (r.ConsegnaPrevista == null && r.Stato == StatoSpedizione.InConsegna) r.ConsegnaPrevista = dataEmail.Date;
        }

        // ---- Fascia oraria ----
        var f = Regex.Match(subject + " " + Inizio(text, 1500), @"entro le (?:ore )?(\d{1,2})[:.](\d{2})", Opt);
        if (f.Success) r.FasciaOraria = $"Entro le {int.Parse(f.Groups[1].Value):00}:{f.Groups[2].Value}";
        else
        {
            f = Regex.Match(subject + " " + Inizio(text, 1500), @"tra le (?:ore )?(\d{1,2})[:.](\d{2}) e le (?:ore )?(\d{1,2})[:.](\d{2})", Opt);
            if (f.Success) r.FasciaOraria = $"{int.Parse(f.Groups[1].Value):00}:{f.Groups[2].Value}–{int.Parse(f.Groups[3].Value):00}:{f.Groups[4].Value}";
        }

        // ---- Link di tracciamento (dall'HTML) ----
        if (!string.IsNullOrEmpty(htmlBody))
        {
            var l = Regex.Match(htmlBody, @"href=""(https?://[^""]*(?:progress-tracker|ship-track|shiptrack|track-package|your-orders/order-details)[^""]*)""", Opt);
            if (l.Success) r.LinkTracking = WebUtility.HtmlDecode(l.Groups[1].Value);
        }
        return r;
    }

    // "Spedito: …", "In consegna: …": il prefisso dell'oggetto decide, senza guardare il nome del prodotto
    private static StatoSpedizione? StatoPrefisso(string subject)
    {
        var m = Regex.Match(subject, @"^\s*(ordinato|ordered|spedit[oa]|shipped|in consegna(?: oggi)?|oggi in consegna|arriva oggi|out for delivery|consegnat[oa]|delivered)\s*:", Opt);
        if (!m.Success) return null;
        var k = m.Groups[1].Value.ToLowerInvariant();
        return k.StartsWith("consegnat") || k == "delivered" ? StatoSpedizione.Consegnato
             : k.Contains("consegna") || k.Contains("oggi") || k.StartsWith("out") ? StatoSpedizione.InConsegna
             : k.StartsWith("sped") || k == "shipped" ? StatoSpedizione.Spedito
             : StatoSpedizione.Ordinato;
    }

    private static StatoSpedizione? StatoDa(string s)
    {
        if (string.IsNullOrEmpty(s)) return null;
        if (Rx(s, @"(^|\b)(consegnato|consegnata|è stato consegnato|il tuo pacco è stato consegnato|delivered)\b") && !Rx(s, @"\b(non (è stato|ancora) consegnat|tentativo di consegna)")) return StatoSpedizione.Consegnato;
        if (Rx(s, @"\b(in consegna( oggi)?|oggi in consegna|arriva oggi|arriverà oggi|out for delivery|arriving today)\b")) return StatoSpedizione.InConsegna;
        if (Rx(s, @"\b(spedito|spedita|spediti|è stato spedito|è in viaggio|shipped|dispatched)\b")) return StatoSpedizione.Spedito;
        if (Rx(s, @"\b(ordinato|ordinati|conferma (del(l')? ?)?ordine|grazie per (il tuo )?(ordine|l'ordine|aver ordinato)|il tuo ordine amazon|ordine effettuato|ordered|order confirmation)\b")) return StatoSpedizione.Ordinato;
        return null;
    }

    // "Spedito: "Logitech MX Keys S, tastiera…" e altri 2 articoli" → Logitech MX Keys S, tastiera…
    private static string ProdottoDaOggetto(string subject)
    {
        var m = Regex.Match(subject, @"^\s*(?:ordinato|spedito|spedita|in consegna(?: oggi)?|oggi in consegna|consegnato|consegnata|arriva oggi|ordered|shipped|out for delivery|delivered)\s*:\s*(.+)$", Opt);
        string p = null;
        if (m.Success) p = m.Groups[1].Value;
        else
        {
            var q = Regex.Match(subject, @"[""“«](.+?)[""”»]");
            if (q.Success) p = q.Groups[1].Value;
            else
            {
                var d = Regex.Match(subject, @"(?:ordine|order)[^""]*? di (.+?)(?: è stato| e altri|$)", Opt);
                if (d.Success) p = d.Groups[1].Value;
            }
        }
        return PulisciProdotto(p);
    }

    private static string ProdottoDaTesto(string text)
    {
        // Riga dopo "Articolo"/"Articoli ordinati"/"Prodotto"
        var m = Regex.Match(text, @"(?:articol[oi](?: ordinat[oi]| spedit[oi])?|prodott[oi]|item)\s*[:\n]\s*(.{4,160}?)(?:\n|\s{2,}|quantità|qtà|€|EUR|$)", Opt);
        return m.Success ? PulisciProdotto(m.Groups[1].Value) : null;
    }

    private static string PulisciProdotto(string p)
    {
        if (string.IsNullOrWhiteSpace(p)) return null;
        p = Regex.Replace(p, @"\s+e\s+(altri|ancora)\s+\d+\s+articol[oi].*$", "", Opt);
        p = Regex.Replace(p, @"\s+and\s+\d+\s+more\s+items?.*$", "", Opt);
        p = Regex.Replace(p, @"\s*(\.\.\.|…)\s*$", "");
        p = p.Trim().Trim('"', '“', '”', '«', '»', '\'', ' ', '.');
        p = NumeroRx.Replace(p, "").Trim(' ', '#', '-', '–');
        if (p.Length < 3 || Rx(p, @"^(il tuo|your) (pacco|ordine|package|order)$")) return null;
        return p.Length > 140 ? p[..140] : p;
    }

    // Prima data dopo la parola chiave; se Amazon dà un intervallo ("12 - 14 novembre") prende l'ultima
    private static DateTime? DataDopoChiave(string s, DateTime dataEmail)
    {
        if (string.IsNullOrEmpty(s)) return null;
        foreach (Match k in ChiaveDataRx.Matches(s))
        {
            var finestra = s.Substring(k.Index + k.Length, Math.Min(90, s.Length - k.Index - k.Length));
            // Intervallo con un solo mese: "12 - 14 novembre"
            var rng = Regex.Match(finestra, @"^\s*(?:\w+\s+)?(\d{1,2})\s*[-–]\s*(?:\w+\s+)?(\d{1,2})\s+(\p{L}+)", Opt);
            if (rng.Success && IndiceMese(rng.Groups[3].Value) > 0)
            {
                var d = Componi(int.Parse(rng.Groups[2].Value), IndiceMese(rng.Groups[3].Value), null, dataEmail);
                if (d != null) return d;
            }
            DateTime? ultima = null;
            int fine = -1;
            foreach (Match d in DataRx.Matches(finestra))
            {
                // Solo date attaccate tra loro (intervallo "mer 12 nov - ven 14 nov"), non quelle lontane
                if (fine >= 0 && !Regex.IsMatch(finestra.Substring(fine, d.Index - fine), @"^\s*[-–]\s*$|^\s*(e|al|fino al)\s*$", Opt)) break;
                var v = Valore(d, dataEmail);
                if (v != null) ultima = v;
                fine = d.Index + d.Length;
            }
            if (ultima != null) return ultima;
        }
        return null;
    }

    private static DateTime? Valore(Match d, DateTime dataEmail)
    {
        if (d.Groups[7].Success)
            return d.Groups[7].Value.ToLowerInvariant() is "domani" or "tomorrow" ? dataEmail.Date.AddDays(1) : dataEmail.Date;
        if (d.Groups[4].Success)
        {
            int gg = int.Parse(d.Groups[4].Value), mm = int.Parse(d.Groups[5].Value), aa = int.Parse(d.Groups[6].Value);
            return mm is >= 1 and <= 12 && gg is >= 1 and <= 31 ? Componi(gg, mm, aa, dataEmail) : null;
        }
        int mese = IndiceMese(d.Groups[2].Value);
        return mese == 0 ? null : Componi(int.Parse(d.Groups[1].Value), mese, d.Groups[3].Success ? int.Parse(d.Groups[3].Value) : null, dataEmail);
    }

    private static int IndiceMese(string s)
    {
        s = s.ToLowerInvariant().TrimEnd('.');
        int i = Array.IndexOf(Mesi, s);
        if (i < 0) i = Array.IndexOf(MesiBrevi, s);
        if (i < 0) i = Array.IndexOf(MonthsEn, s);
        return i + 1;
    }

    // Senza anno: l'anno dell'email, o il successivo se la data cadrebbe molto prima (ordine a dicembre, consegna a gennaio)
    private static DateTime? Componi(int giorno, int mese, int? anno, DateTime dataEmail)
    {
        int a = anno ?? dataEmail.Year;
        if (giorno > DateTime.DaysInMonth(a, mese)) return null;
        var d = new DateTime(a, mese, giorno);
        if (anno == null && d < dataEmail.Date.AddMonths(-6)) d = d.AddYears(1);
        return d;
    }

    public static string HtmlToText(string html)
    {
        if (string.IsNullOrEmpty(html)) return "";
        var s = Regex.Replace(html, @"<(script|style|head)[^>]*>.*?</\1>", " ", RegexOptions.Singleline | RegexOptions.IgnoreCase);
        s = Regex.Replace(s, @"<\s*(br|/p|/div|/tr|/td|/h\d|/li|/table)[^>]*>", "\n", RegexOptions.IgnoreCase);
        s = Regex.Replace(s, @"<[^>]+>", " ");
        return WebUtility.HtmlDecode(s);
    }

    private static string Pulisci(string s)
    {
        s = s.Replace(' ', ' ').Replace("\r", "");
        s = Regex.Replace(s, @"[ \t]+", " ");
        s = Regex.Replace(s, @"\n\s*\n+", "\n");
        return s.Trim();
    }

    private static string Inizio(string s, int n) => s.Length <= n ? s : s[..n];
    private static bool Rx(string s, string pattern) => !string.IsNullOrEmpty(s) && Regex.IsMatch(s, pattern, Opt);

    // ---- Unione con gli ordini già noti (sempre in ordine cronologico delle email) ----
    // Restituisce gli ordini cambiati e quelli da eliminare (annullati).
    public static (List<Ordine> Cambiati, List<Ordine> Annullati) Unisci(List<Ordine> esistenti, IEnumerable<EmailOrdine> email)
    {
        var cambiati = new Dictionary<string, Ordine>();
        var annullati = new List<Ordine>();
        foreach (var e in email.Where(x => x.Utile).OrderBy(x => x.DataEmail))
        {
            Ordine o = null;
            if (e.NumeroOrdine != null) o = esistenti.FirstOrDefault(x => x.NumeroOrdine == e.NumeroOrdine);
            // Senza numero: lo stesso prodotto tra quelli non ancora consegnati
            if (o == null && e.NumeroOrdine == null && e.Prodotto != null)
                o = esistenti.Where(x => !x.Consegnato && x.Prodotto != null)
                             .FirstOrDefault(x => x.Prodotto.StartsWith(e.Prodotto[..Math.Min(24, e.Prodotto.Length)], StringComparison.OrdinalIgnoreCase)
                                               || e.Prodotto.StartsWith(x.Prodotto[..Math.Min(24, x.Prodotto.Length)], StringComparison.OrdinalIgnoreCase));

            if (e.Annullato)
            {
                if (o != null) { esistenti.Remove(o); cambiati.Remove(o.Id); annullati.Add(o); }
                continue;
            }
            if (o == null)
            {
                if (e.NumeroOrdine == null) continue;   // senza numero né ordine noto: non si sa a cosa si riferisce
                o = new Ordine { NumeroOrdine = e.NumeroOrdine, Stato = e.Stato.Value, DataOrdine = e.DataEmail, Aggiornato = e.DataEmail };
                esistenti.Add(o);
            }
            else if (o.UltimaEmailId != null && o.UltimaEmailId == e.MessageId) continue;

            bool c = false;
            if (e.Stato.Value > o.Stato) { o.Stato = e.Stato.Value; c = true; }
            if (o.Stato == StatoSpedizione.Consegnato && o.ConsegnatoIl == null) { o.ConsegnatoIl = e.DataEmail; c = true; }
            if (e.ConsegnaPrevista.HasValue && e.Stato.Value >= StatoSpedizione.Ordinato && o.ConsegnaPrevista != e.ConsegnaPrevista && !o.Consegnato) { o.ConsegnaPrevista = e.ConsegnaPrevista; c = true; }
            if (e.Prodotto != null && (o.Prodotto == null || o.Prodotto.Length < 6)) { o.Prodotto = e.Prodotto; c = true; }
            if (e.FasciaOraria != null && o.FasciaOraria != e.FasciaOraria && e.Stato.Value >= StatoSpedizione.Spedito) { o.FasciaOraria = e.FasciaOraria; c = true; }
            if (e.LinkTracking != null && o.LinkTracking == null) { o.LinkTracking = e.LinkTracking; c = true; }
            if (o.Prodotto == null) { o.Prodotto = "Ordine Amazon"; c = true; }
            if (c || o.UltimaEmailId != e.MessageId)
            {
                o.UltimaEmailId = e.MessageId;
                o.Aggiornato = e.DataEmail > o.Aggiornato ? e.DataEmail : o.Aggiornato;
                cambiati[o.Id] = o;
            }
        }
        return (cambiati.Values.ToList(), annullati);
    }
}
