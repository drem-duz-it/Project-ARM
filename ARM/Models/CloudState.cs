using SQLite;

namespace ARM.Models
{
    // Sincronizzazione cloud: l'ultima versione del server nota per ogni elemento (serve a riconoscere i conflitti)
    public class CloudState
    {
        [PrimaryKey]
        public string Key { get; set; }        // identificativo sul server (cifrato)
        public long Version { get; set; }
    }

    // Contenuto di un elemento prima della cifratura
    public class CloudEnvelope
    {
        public string K { get; set; }          // chiave dell'elemento, es. "Note:<id>"
        public string T { get; set; }          // tipo: "Note", "TaskItem", ... oppure "del" per un'eliminazione
        public string D { get; set; }          // l'elemento in JSON
    }
}
