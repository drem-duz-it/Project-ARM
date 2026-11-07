using ARM.Models;
using ARM.Services;

namespace SQLite { public class PrimaryKeyAttribute : Attribute { } public class IgnoreAttribute : Attribute { } }

public static class Program
{
    static int fail = 0;
    static void Check(string name, bool ok, string info = "") { if (!ok) fail++; Console.WriteLine($"{(ok ? "ok  " : "FAIL")} {name} {info}"); }

    public static void Main()
    {
        var d0 = new DateTime(2026, 11, 3, 9, 12, 0);
        var emails = new List<EmailOrdine>();

        // 1. Conferma d'ordine (formato 2024+: oggetto "Ordinato: ...")
        var e1 = AmazonEmailParser.Parse("Ordinato: \"Logitech MX Keys S, tastiera wireless retroilluminata...\" e altri 1 articoli",
            "Grazie per il tuo ordine, Mario.\nArrivo: venerdì 7 novembre\nOrdine n. 402-1234567-1234567\nLogitech MX Keys S, tastiera wireless retroilluminata\nQuantità: 1\n€ 109,99",
            "<a href=\"https://www.amazon.it/gp/your-account/order-details?orderID=402-1234567-1234567\">Dettagli</a>", d0, "m1");
        Check("1 stato", e1.Stato == StatoSpedizione.Ordinato, e1.Stato?.ToString());
        Check("1 numero", e1.NumeroOrdine == "402-1234567-1234567");
        Check("1 prodotto", e1.Prodotto == "Logitech MX Keys S, tastiera wireless retroilluminata", e1.Prodotto);
        Check("1 data", e1.ConsegnaPrevista == new DateTime(2026, 11, 7), e1.ConsegnaPrevista?.ToString("d"));
        emails.Add(e1);

        // 2. Spedito, con intervallo
        var e2 = AmazonEmailParser.Parse("Spedito: \"Logitech MX Keys S, tastiera wireless...\"",
            null, "<html><body><p>Il tuo pacco è in viaggio.</p><p>Arrivo previsto: <b>mercoledì 5 - venerdì 7 novembre</b></p><p>Ordine n. 402-1234567-1234567</p><a href=\"https://www.amazon.it/progress-tracker/package/ref=pe_x?_encoding=UTF8&amp;orderId=402-1234567-1234567\">Traccia il pacco</a></body></html>",
            d0.AddDays(1), "m2");
        Check("2 stato", e2.Stato == StatoSpedizione.Spedito, e2.Stato?.ToString());
        Check("2 data ultima", e2.ConsegnaPrevista == new DateTime(2026, 11, 7), e2.ConsegnaPrevista?.ToString("d"));
        Check("2 link", e2.LinkTracking?.Contains("orderId=402") == true, e2.LinkTracking);
        emails.Add(e2);

        // 3. In consegna oggi con fascia
        var e3 = AmazonEmailParser.Parse("In consegna: \"Logitech MX Keys S, tastiera wireless...\"",
            "Il tuo pacco arriverà oggi entro le 20:00.\nOrdine n. 402-1234567-1234567", null, d0.AddDays(2), "m3");
        Check("3 stato", e3.Stato == StatoSpedizione.InConsegna, e3.Stato?.ToString());
        Check("3 data oggi", e3.ConsegnaPrevista == d0.AddDays(2).Date, e3.ConsegnaPrevista?.ToString("d"));
        Check("3 fascia", e3.FasciaOraria == "Entro le 20:00", e3.FasciaOraria);
        emails.Add(e3);

        // 4. Ordine diverso: vecchio formato oggetto
        var e4 = AmazonEmailParser.Parse("Il tuo ordine Amazon.it di \"Filtri per caraffa Maxtra Pro, confezione da 6\"",
            "Ciao Mario,\ngrazie per il tuo ordine.\nConsegna prevista: lunedì 16 novembre 2026\nNumero d'ordine #171-7654321-7654321", null, d0, "m4");
        Check("4 stato", e4.Stato == StatoSpedizione.Ordinato, e4.Stato?.ToString());
        Check("4 prodotto", e4.Prodotto == "Filtri per caraffa Maxtra Pro, confezione da 6", e4.Prodotto);
        Check("4 data", e4.ConsegnaPrevista == new DateTime(2026, 11, 16), e4.ConsegnaPrevista?.ToString("d"));
        emails.Add(e4);

        // 5. Consegnato
        var e5 = AmazonEmailParser.Parse("Consegnato: il tuo pacco",
            "Il tuo pacco è stato consegnato.\nConsegnato alla porta d'ingresso.\nOrdine n. 402-1234567-1234567", null, d0.AddDays(2).AddHours(8), "m5");
        Check("5 stato", e5.Stato == StatoSpedizione.Consegnato, e5.Stato?.ToString());
        Check("5 prodotto nullo", e5.Prodotto == null, e5.Prodotto);
        emails.Add(e5);

        // 6. Promozione Amazon: da ignorare
        var e6 = AmazonEmailParser.Parse("Offerte del Black Friday: fino al 40% di sconto", "Scopri le offerte. Consegna gratuita con Prime.", null, d0, "m6");
        Check("6 ignorata", !e6.Utile, e6.Stato?.ToString());

        // 7. Annullato
        var e7 = AmazonEmailParser.Parse("Il tuo ordine è stato annullato", "Abbiamo annullato l'ordine n. 402-7740215-3318840 come richiesto.", null, d0.AddDays(1), "m7");
        Check("7 annullato", e7.Annullato && e7.NumeroOrdine == "402-7740215-3318840");

        // 8. Reso: ignorare
        var e8 = AmazonEmailParser.Parse("Il tuo rimborso è stato emesso", "Rimborso per l'ordine 403-8817264-5530127: € 39,90", null, d0, "m8");
        Check("8 reso ignorato", !e8.Utile, e8.Stato?.ToString());

        // 9. Dicembre → gennaio
        var e9 = AmazonEmailParser.Parse("Spedito: \"Il nome della rosa\"", "Arrivo: martedì 5 gennaio\nOrdine n. 404-3398102-9152263", null, new DateTime(2026, 12, 28), "m9");
        Check("9 anno dopo", e9.ConsegnaPrevista == new DateTime(2027, 1, 5), e9.ConsegnaPrevista?.ToString("d"));

        // 10. "Arriva domani"
        var e10 = AmazonEmailParser.Parse("Spedito: \"Cavo HDMI 2.1 da 2 m\"", "Arriva domani\nOrdine n. 402-1111111-2222222", null, d0, "m10");
        Check("10 domani", e10.ConsegnaPrevista == d0.Date.AddDays(1), e10.ConsegnaPrevista?.ToString("d"));

        // Unione
        var esistenti = new List<Ordine>();
        emails.Add(e7);
        var (cambiati, annullati) = AmazonEmailParser.Unisci(esistenti, emails);
        var mx = esistenti.First(o => o.NumeroOrdine == "402-1234567-1234567");
        Check("U stato finale", mx.Stato == StatoSpedizione.Consegnato, mx.Stato.ToString());
        Check("U prodotto", mx.Prodotto.StartsWith("Logitech MX Keys S"), mx.Prodotto);
        Check("U consegnato il", mx.ConsegnatoIl?.Date == d0.AddDays(2).Date);
        Check("U link", mx.LinkTracking != null);
        Check("U fascia", mx.FasciaOraria == "Entro le 20:00");
        Check("U due ordini", esistenti.Count == 2, esistenti.Count.ToString());
        Check("U cambiati", cambiati.Count == 2, cambiati.Count.ToString());
        // Rilettura delle stesse email: nessun cambiamento
        var (c2, _) = AmazonEmailParser.Unisci(esistenti, new[] { e5 });
        Check("U idempotente", c2.Count == 0, c2.Count.ToString());
        // Stato non torna indietro
        var (c3, _) = AmazonEmailParser.Unisci(esistenti, new[] { e2 });
        Check("U non arretra", mx.Stato == StatoSpedizione.Consegnato);

        Console.WriteLine(fail == 0 ? "TUTTO OK" : $"{fail} FALLITI");
    }
}
