using System.Text.Json.Serialization;
using SQLite;

namespace ARMusic.Models
{
    // Una nota generica: può essere un appunto, un pagamento, un promemoria o un'idea
    public class Note
    {
        public static readonly string[] Tipi = { "Nota", "Pagamento", "Promemoria", "Idea" };

        [PrimaryKey]
        public string Id { get; set; } = System.Guid.NewGuid().ToString();
        public string Title { get; set; }
        public string Content { get; set; }
        public string Type { get; set; } = "Nota";
        public string ColorHex { get; set; } = "";   // Vuoto = colore della card del tema
        public bool IsPinned { get; set; }

        // Campi per Pagamenti / Promemoria
        public double Amount { get; set; }
        public DateTime? DueDate { get; set; }
        public bool IsPaid { get; set; }
        public bool IsMonthly { get; set; }           // Pagamento ricorrente ogni mese
        public bool IsIncome { get; set; }            // false = debito (soldi da pagare), true = credito (soldi da ricevere)
        public DateTime? LastPaidAt { get; set; }
        public string Category { get; set; } = "";     // Categoria del pagamento (vedi Models/Category.cs)

        // Lista con le spunte dentro la nota: righe "1|testo" (fatta) / "0|testo" (da fare)
        public string Checklist { get; set; } = "";

        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime UpdatedAt { get; set; } = DateTime.Now;

        // Sincronizzazione: quando è stato modificato su QUESTO dispositivo (tick UTC, 0 = arrivato da un altro dispositivo)
        public long ChangedAt { get; set; }

        [Ignore] public bool IsPayment => Type == "Pagamento";
        [Ignore] public bool HasDate => DueDate.HasValue;
        [Ignore] public bool IsOverdue => DueDate.HasValue && DueDate.Value.Date < DateTime.Today && !(IsPayment && IsPaid);
        [Ignore] public string AmountText => Amount.ToString("C", Ui.It);
        [Ignore] public string SignedAmountText => (IsIncome ? "+" : "−") + AmountText;
        [Ignore] public string PaymentStatusText => IsIncome ? (IsPaid ? Loc.T("Ricevuto") : Loc.T("Da ricevere")) : (IsPaid ? Loc.T("Pagato") : Loc.T("Da pagare"));
        [Ignore] public string PaymentColorKey => IsIncome ? "Accent3Color" : "TextColor";   // 3.0: uscite neutre, entrate nell'accento
        [Ignore, JsonIgnore] public List<ChecklistItem> ChecklistItems => ChecklistItem.Parse(Checklist);
        [Ignore, JsonIgnore] public bool HasChecklist => !string.IsNullOrWhiteSpace(Checklist);
    }
}
