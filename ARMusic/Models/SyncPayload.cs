namespace ARMusic.Models
{
    // Pacchetto di modifiche scambiato tra telefono e PC
    public class SyncPayload
    {
        public List<Note> Notes { get; set; } = new();
        public List<TaskItem> Tasks { get; set; } = new();
        public List<CalendarEvent> Events { get; set; } = new();
        public List<Ordine> Ordini { get; set; } = new();          // 3.0 (i dispositivi più vecchi lo ignorano)
        public List<Playlist> Playlists { get; set; } = new();
        public List<Song> Songs { get; set; } = new();
        public List<PlaylistSong> Links { get; set; } = new();
        public List<SyncTombstone> Deletions { get; set; } = new();

        public int Count => Notes.Count + Tasks.Count + Events.Count + (Ordini?.Count ?? 0) + Playlists.Count + Songs.Count + Links.Count + Deletions.Count;
    }
}
