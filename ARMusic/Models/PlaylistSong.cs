using SQLite;

namespace ARMusic.Models
{
    public class PlaylistSong
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }
        public string PlaylistId { get; set; } // L'ID della Playlist
        public string SongId { get; set; }     // L'ID della Canzone

        // Sincronizzazione: quando è stato modificato su QUESTO dispositivo (tick UTC, 0 = arrivato da un altro dispositivo)
        public long ChangedAt { get; set; }
    }
}