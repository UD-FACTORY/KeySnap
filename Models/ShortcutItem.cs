using System;

namespace QuickReplace.Models
{
    public enum TriggerMode
    {
        Instant,    // 단축어 입력 완료 시 즉시 대치
        TriggerKey  // 단축어 후 스페이스, 엔터, 탭 입력 시 대치
    }

    public class ShortcutItem
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Shortcut { get; set; } = string.Empty;
        public string Replacement { get; set; } = string.Empty;
        public bool IsEnabled { get; set; } = true;
        public TriggerMode Trigger { get; set; } = TriggerMode.Instant;
        public string Group { get; set; } = "기본";
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        // UI 표시용 (단축어 길이, 미리보기 등)
        public string TriggerDisplay => Trigger switch
        {
            TriggerMode.Instant => "즉시 변환",
            TriggerMode.TriggerKey => "트리거 키(Space/Enter)",
            _ => "즉시 변환"
        };
    }
}
