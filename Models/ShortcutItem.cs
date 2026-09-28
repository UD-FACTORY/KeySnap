using System;

namespace QuickReplace.Models
{
    public class ShortcutItem
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Shortcut { get; set; } = string.Empty;
        public string Replacement { get; set; } = string.Empty;
        public bool IsEnabled { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}
