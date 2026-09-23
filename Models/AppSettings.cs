namespace QuickReplace.Models
{
    public class AppSettings
    {
        public bool IsGlobalEnabled { get; set; } = true;
        public bool StartWithWindows { get; set; } = false;
        public bool MinimizeToTrayOnClose { get; set; } = true;
        public int ClipboardRestoreDelayMs { get; set; } = 80;
        public bool ShowNotifications { get; set; } = true;
        public string TriggerHotkey { get; set; } = "Tab";
    }
}
