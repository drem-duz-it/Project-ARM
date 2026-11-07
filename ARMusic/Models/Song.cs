using SQLite;

namespace ARMusic.Models
{
    public class Song
    {
        [PrimaryKey] // Dice a SQLite che questo è l'identificativo unico
        public string Id { get; set; }
        public string Title { get; set; }
        public string Artist { get; set; }
        public string ThumbnailUrl { get; set; }

        public string CloudUrl { get; set; }
        public string LocalFilePath { get; set; }

        // Sincronizzazione: quando è stato modificato su QUESTO dispositivo (tick UTC, 0 = arrivato da un altro dispositivo)
        public long ChangedAt { get; set; }

        [Ignore] // Dice a SQLite di NON salvare questo campo nel database, perché si calcola da solo
        public bool IsDownloaded => !string.IsNullOrEmpty(LocalFilePath);
    }
}