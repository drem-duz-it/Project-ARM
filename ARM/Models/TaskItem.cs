using SQLite;

namespace ARM.Models
{
    public class TaskItem
    {
        [PrimaryKey]
        public string Id { get; set; } = System.Guid.NewGuid().ToString();
        public string Title { get; set; }
        public string Notes { get; set; }
        public DateTime? DueDate { get; set; }
        public int Priority { get; set; } = 1;        // 0 = bassa, 1 = normale, 2 = alta
        public bool IsDone { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime? CompletedAt { get; set; }

        // Sincronizzazione: quando è stato modificato su QUESTO dispositivo (tick UTC, 0 = arrivato da un altro dispositivo)
        public long ChangedAt { get; set; }

        [Ignore] public bool IsOverdue => !IsDone && DueDate.HasValue && DueDate.Value.Date < DateTime.Today;
        [Ignore] public bool IsToday => DueDate.HasValue && DueDate.Value.Date == DateTime.Today;
    }
}
