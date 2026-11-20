using SQLite;

namespace ARM.Models
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

        // Miniatura piccola (320×180) per gli elenchi: ThumbnailUrl è la più grande di YouTube (fino a 1280×720)
        // e scaricarla per un quadratino rallenta molto lo scorrimento. I brani nascono tutti da YouTube (Id = id del video).
        [Ignore, System.Text.Json.Serialization.JsonIgnore]
        public string CoverSmall => IsYouTubeId(Id) ? $"https://i.ytimg.com/vi/{Id}/mqdefault.jpg" : ThumbnailUrl;

        private static bool IsYouTubeId(string id) =>
            id != null && id.Length == 11 && id.All(c => char.IsAsciiLetterOrDigit(c) || c == '-' || c == '_');
    }
}