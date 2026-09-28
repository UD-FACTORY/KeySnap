namespace QuickReplace.Models
{
    public enum ReplacementMode
    {
        Hotkey,   // 설정된 단축키(트리거 키) 입력 시 변환 (기본 권장)
        Instant   // 단축어 타이핑 완료 시 즉시 변환
    }

    public class AppSettings
    {
        public bool IsGlobalEnabled { get; set; } = true;
        public bool StartWithWindows { get; set; } = false;
        public bool MinimizeToTrayOnClose { get; set; } = true;
        public int ClipboardRestoreDelayMs { get; set; } = 80;
        public bool ShowNotifications { get; set; } = true;
        public string TriggerHotkey { get; set; } = "Tab";
        public ReplacementMode ReplacementMode { get; set; } = ReplacementMode.Hotkey;
    }
}
