using SQLite;

namespace ARMusic.Models
{
    // Ricordo di un elemento eliminato, per eliminarlo anche sull'altro dispositivo alla prossima sincronizzazione
    public class SyncTombstone
    {
        [PrimaryKey]
        public string Key { get; set; }        // "Tabella:Id"
        public string Table { get; set; }
        public string ItemId { get; set; }
        public long DeletedAt { get; set; }     // tick UTC
    }
}
