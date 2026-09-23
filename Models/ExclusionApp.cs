using System;

namespace QuickReplace.Models
{
    public class ExclusionApp
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string ProcessName { get; set; } = string.Empty; // ex: League of Legends, notepad
        public string Description { get; set; } = string.Empty;
        public bool IsEnabled { get; set; } = true;
        public DateTime AddedAt { get; set; } = DateTime.Now;
    }
}
