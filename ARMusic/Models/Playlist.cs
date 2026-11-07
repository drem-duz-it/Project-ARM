using SQLite;

namespace ARMusic.Models
{
    public class Playlist
    {
        [PrimaryKey]
        public string Id { get; set; } = System.Guid.NewGuid().ToString();
        public string Name { get; set; }
        public string Description { get; set; }
        public string ImagePath { get; set; }      // percorso della copertina su QUESTO dispositivo
        public string ImageId { get; set; }        // identifica la copertina tra i dispositivi (cambia quando cambi immagine)

        // Sincronizzazione: quando è stato modificato su QUESTO dispositivo (tick UTC, 0 = arrivato da un altro dispositivo)
        public long ChangedAt { get; set; }

        // Immagine da mostrare: letta direttamente dal file (su Windows le immagini con percorso completo non si caricano)
        [Ignore, System.Text.Json.Serialization.JsonIgnore]
        public ImageSource CoverSource => string.IsNullOrEmpty(ImagePath) || !File.Exists(ImagePath)
            ? null
            : ImageSource.FromStream(() => File.OpenRead(ImagePath));
    }
}