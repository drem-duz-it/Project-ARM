namespace ARM.Models
{
    // Categoria di un pagamento (per i grafici del mese). I colori sono fissi, uguali in tutti i temi.
    public record Category(string Name, string Emoji, string Hex)
    {
        public string Label => Loc.T(Name);   // 3.0: niente emoji, il colore della categoria è un punto
        public Color Color => Color.FromArgb(Hex);

        public const string Altro = "Altro";

        // Uscite (soldi da pagare)
        public static readonly Category[] Uscite =
        {
            new("Casa", "🏠", "#6C4DFF"),
            new("Bollette", "💡", "#FFB020"),
            new("Spesa", "🛒", "#12B886"),
            new("Trasporti", "🚗", "#2F80ED"),
            new("Abbonamenti", "📺", "#E64980"),
            new("Salute", "💊", "#15AABF"),
            new("Svago", "🎉", "#FF6B4A"),
            new("Shopping", "🛍️", "#9C36B5"),
            new("Tasse", "🧾", "#5C5A70"),
            new(Altro, "📦", "#ADB5BD"),
        };

        // Entrate (soldi da ricevere)
        public static readonly Category[] Entrate =
        {
            new("Stipendio", "💼", "#12B886"),
            new("Rimborso", "↩️", "#2F80ED"),
            new("Vendita", "🏷️", "#FFB020"),
            new("Prestito", "🤝", "#6C4DFF"),
            new("Regalo", "🎁", "#E64980"),
            new(Altro, "📦", "#ADB5BD"),
        };

        public static Category[] For(bool income) => income ? Entrate : Uscite;

        // Categoria di una nota (vuota o sconosciuta → "Altro")
        public static Category Of(Note n) =>
            For(n.IsIncome).FirstOrDefault(c => c.Name == n.Category) ?? For(n.IsIncome).Last();

        // Indovina la categoria da qualche parola del titolo (usata dalla scrittura libera)
        public static string Guess(string text, bool income)
        {
            var t = (text ?? "").ToLowerInvariant();
            bool Has(params string[] words) => words.Any(w => t.Contains(w));
            if (income)
            {
                if (Has("stipendio", "busta paga", "paga ")) return "Stipendio";
                if (Has("rimborso", "rimborsa")) return "Rimborso";
                if (Has("vendita", "vendo", "venduto", "vinted", "subito")) return "Vendita";
                if (Has("prestito", "prestat", "mi deve", "restituisc")) return "Prestito";
                if (Has("regalo", "compleanno")) return "Regalo";
                return "";
            }
            if (Has("affitto", "mutuo", "condominio", "casa")) return "Casa";
            if (Has("bolletta", "luce", "gas", "acqua", "internet", "wifi", "fibra", "telefono", "enel", "tim ", "vodafone", "iliad")) return "Bollette";
            if (Has("spesa", "supermercato", "esselunga", "coop", "conad", "lidl")) return "Spesa";
            if (Has("benzina", "carburante", "auto", "bollo", "assicurazione", "treno", "autobus", "abbonamento atm", "taxi", "parcheggio", "metro")) return "Trasporti";
            if (Has("netflix", "spotify", "disney", "prime", "youtube", "abbonamento", "icloud", "palestra", "dazn", "now tv")) return "Abbonamenti";
            if (Has("medico", "dentista", "farmacia", "visita", "analisi", "ticket")) return "Salute";
            if (Has("cena", "pranzo", "cinema", "concerto", "viaggio", "vacanza", "pizza", "aperitivo", "biglietto")) return "Svago";
            if (Has("amazon", "vestiti", "scarpe", "regalo", "zalando", "acquisto")) return "Shopping";
            if (Has("tasse", "f24", "imu", "tari", "multa", "canone")) return "Tasse";
            return "";
        }
    }

    // Una voce di una lista con le spunte dentro una nota
    public class ChecklistItem
    {
        public string Text { get; set; } = "";
        public bool Done { get; set; }

        // Salvata in Note.Checklist come righe "1|testo" (fatta) o "0|testo" (da fare)
        public static List<ChecklistItem> Parse(string raw) =>
            (raw ?? "").Split('\n', StringSplitOptions.RemoveEmptyEntries)
                       .Select(l => l.TrimEnd('\r'))
                       .Where(l => l.Length >= 2 && l[1] == '|')
                       .Select(l => new ChecklistItem { Done = l[0] == '1', Text = l[2..] })
                       .ToList();

        public static string Serialize(IEnumerable<ChecklistItem> items) =>
            string.Join("\n", items.Where(i => !string.IsNullOrWhiteSpace(i.Text))
                                   .Select(i => (i.Done ? "1|" : "0|") + i.Text.Replace("\n", " ").Trim()));
    }
}
