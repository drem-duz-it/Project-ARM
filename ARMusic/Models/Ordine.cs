using SQLite;

namespace ARMusic.Models
{
    // Stato di spedizione: le 4 tappe del tracciato. Lo stato può solo avanzare.
    public enum StatoSpedizione { Ordinato = 0, Spedito = 1, InConsegna = 2, Consegnato = 3 }

    // Un ordine online in arrivo (3.0). Lo crea il PC leggendo le email di Amazon da Gmail;
    // arriva sugli altri dispositivi con la sincronizzazione Wi-Fi.
    public class Ordine
    {
        [PrimaryKey]
        public string Id { get; set; } = System.Guid.NewGuid().ToString();
        public string Negozio { get; set; } = "Amazon";
        public string Prodotto { get; set; }
        public string NumeroOrdine { get; set; }           // es. 402-1234567-1234567
        public StatoSpedizione Stato { get; set; }
        public DateTime? ConsegnaPrevista { get; set; }      // giorno (l'ultimo, se Amazon dà un intervallo)
        public string FasciaOraria { get; set; }            // es. "Entro le 20:00" (facoltativa)
        public DateTime DataOrdine { get; set; } = DateTime.Now;
        public DateTime? ConsegnatoIl { get; set; }
        public string LinkTracking { get; set; }
        public string UltimaEmailId { get; set; }            // Message-Id dell'ultima email letta per questo ordine
        public DateTime Aggiornato { get; set; } = DateTime.Now;

        // Sincronizzazione: quando è stato modificato su QUESTO dispositivo (tick UTC, 0 = arrivato da un altro dispositivo)
        public long ChangedAt { get; set; }

        // "In ritardo" non si salva: si calcola
        [Ignore, System.Text.Json.Serialization.JsonIgnore]
        public bool InRitardo => Stato != StatoSpedizione.Consegnato && ConsegnaPrevista.HasValue && ConsegnaPrevista.Value.Date < DateTime.Today;

        [Ignore, System.Text.Json.Serialization.JsonIgnore]
        public bool Consegnato => Stato == StatoSpedizione.Consegnato;

        // Tappa del tracciato (1-4)
        [Ignore, System.Text.Json.Serialization.JsonIgnore]
        public int Tappa => (int)Stato + 1;

        [Ignore, System.Text.Json.Serialization.JsonIgnore]
        public string StatoTesto => InRitardo ? Loc.T("In ritardo") : Stato switch
        {
            StatoSpedizione.Ordinato => Loc.T("Ordinato"),
            StatoSpedizione.Spedito => Loc.T("Spedito"),
            StatoSpedizione.InConsegna => Loc.T("In consegna"),
            _ => Loc.T("Consegnato")
        };

        // Da mostrare nella dashboard: non consegnati, oppure consegnati da meno di 3 giorni
        [Ignore, System.Text.Json.Serialization.JsonIgnore]
        public bool Visibile => !Consegnato || (ConsegnatoIl ?? Aggiornato).Date >= DateTime.Today.AddDays(-3);

        [Ignore, System.Text.Json.Serialization.JsonIgnore]
        public int GiorniRitardo => InRitardo ? (DateTime.Today - ConsegnaPrevista.Value.Date).Days : 0;
    }
}
