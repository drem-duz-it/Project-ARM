using SQLite;

namespace ARM.Models
{
    public class CalendarEvent
    {
        [PrimaryKey]
        public string Id { get; set; } = System.Guid.NewGuid().ToString();
        public string Title { get; set; }
        public DateTime Date { get; set; } = DateTime.Today;
        public bool AllDay { get; set; } = true;
        public TimeSpan StartTime { get; set; } = new TimeSpan(9, 0, 0);
        public TimeSpan EndTime { get; set; } = new TimeSpan(10, 0, 0);
        public string Location { get; set; }
        public string Notes { get; set; }
        public string ColorKey { get; set; } = "Accent5Color";

        // Sincronizzazione: quando è stato modificato su QUESTO dispositivo (tick UTC, 0 = arrivato da un altro dispositivo)
        public long ChangedAt { get; set; }

        [Ignore] public string TimeText => AllDay ? Loc.T("Tutto il giorno") : $"{StartTime:hh\\:mm} – {EndTime:hh\\:mm}";
    }
}
