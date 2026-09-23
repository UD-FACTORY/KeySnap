using System;
using System.Collections.Generic;
using System.Text;
using System.Windows.Input;

namespace QuickReplace.Helpers
{
    public class HotkeyDef
    {
        public bool Ctrl { get; set; }
        public bool Alt { get; set; }
        public bool Shift { get; set; }
        public bool Win { get; set; }
        public ushort VkCode { get; set; }
        public string RawText { get; set; } = string.Empty;

        public bool IsValid => VkCode != 0;
    }

    public static class HotkeyHelper
    {
        private static readonly Dictionary<string, ushort> NameToVk = new(StringComparer.OrdinalIgnoreCase)
        {
            ["tab"] = NativeMethods.VK_TAB,
            ["space"] = NativeMethods.VK_SPACE,
            ["enter"] = NativeMethods.VK_RETURN,
            ["return"] = NativeMethods.VK_RETURN,
            ["back"] = NativeMethods.VK_BACK,
            ["backspace"] = NativeMethods.VK_BACK,
            ["esc"] = 0x1B,
            ["escape"] = 0x1B,
            ["insert"] = 0x2D,
            ["delete"] = 0x2E,
            ["home"] = 0x24,
            ["end"] = 0x23,
            ["pageup"] = 0x21,
            ["pagedown"] = 0x22,
            ["up"] = 0x26,
            ["down"] = 0x28,
            ["left"] = 0x25,
            ["right"] = 0x27,
            ["capslock"] = 0x14,
            ["numlock"] = 0x90,
            ["scrolllock"] = 0x91,
            ["printscreen"] = 0x2C,
            ["pause"] = 0x13,
            ["f1"] = 0x70,
            ["f2"] = 0x71,
            ["f3"] = 0x72,
            ["f4"] = 0x73,
            ["f5"] = 0x74,
            ["f6"] = 0x75,
            ["f7"] = 0x76,
            ["f8"] = 0x77,
            ["f9"] = 0x78,
            ["f10"] = 0x79,
            ["f11"] = 0x7A,
            ["f12"] = 0x7B,
            ["`"] = 0xC0,
            ["~"] = 0xC0,
            ["-"] = 0xBD,
            ["="] = 0xBB,
            ["["] = 0xDB,
            ["]"] = 0xDD,
            ["\\"] = 0xDC,
            [";"] = 0xBA,
            ["'"] = 0xDE,
            [","] = 0xBC,
            ["."] = 0xBE,
            ["/"] = 0xBF
        };

        private static readonly Dictionary<ushort, string> VkToName = new()
        {
            [NativeMethods.VK_TAB] = "Tab",
            [NativeMethods.VK_SPACE] = "Space",
            [NativeMethods.VK_RETURN] = "Enter",
            [NativeMethods.VK_BACK] = "Backspace",
            [0x1B] = "Esc",
            [0x2D] = "Insert",
            [0x2E] = "Delete",
            [0x24] = "Home",
            [0x23] = "End",
            [0x21] = "PageUp",
            [0x22] = "PageDown",
            [0x26] = "Up",
            [0x28] = "Down",
            [0x25] = "Left",
            [0x27] = "Right",
            [0x14] = "CapsLock",
            [0x90] = "NumLock",
            [0x91] = "ScrollLock",
            [0x2C] = "PrintScreen",
            [0x13] = "Pause",
            [0x70] = "F1",
            [0x71] = "F2",
            [0x72] = "F3",
            [0x73] = "F4",
            [0x74] = "F5",
            [0x75] = "F6",
            [0x76] = "F7",
            [0x77] = "F8",
            [0x78] = "F9",
            [0x79] = "F10",
            [0x7A] = "F11",
            [0x7B] = "F12",
            [0xC0] = "`",
            [0xBD] = "-",
            [0xBB] = "=",
            [0xDB] = "[",
            [0xDD] = "]",
            [0xDC] = "\\",
            [0xBA] = ";",
            [0xDE] = "'",
            [0xBC] = ",",
            [0xBE] = ".",
            [0xBF] = "/"
        };

        public static HotkeyDef Parse(string? text)
        {
            var def = new HotkeyDef();
            if (string.IsNullOrWhiteSpace(text))
            {
                // 기본값 Tab
                def.VkCode = NativeMethods.VK_TAB;
                def.RawText = "Tab";
                return def;
            }

            def.RawText = text.Trim();
            var parts = text.Split(new[] { '+', ' ' }, StringSplitOptions.RemoveEmptyEntries);

            foreach (var rawPart in parts)
            {
                string p = rawPart.Trim().ToLowerInvariant();
                if (p == "ctrl" || p == "control")
                {
                    def.Ctrl = true;
                }
                else if (p == "alt" || p == "menu")
                {
                    def.Alt = true;
                }
                else if (p == "shift")
                {
                    def.Shift = true;
                }
                else if (p == "win" || p == "windows")
                {
                    def.Win = true;
                }
                else
                {
                    // 주요 키
                    if (NameToVk.TryGetValue(p, out ushort vk))
                    {
                        def.VkCode = vk;
                    }
                    else if (p.Length == 1)
                    {
                        char c = char.ToUpperInvariant(p[0]);
                        if ((c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9'))
                        {
                            def.VkCode = (ushort)c;
                        }
                    }
                    else
                    {
                        // Enum 파싱 시도 (Key)
                        if (Enum.TryParse<Key>(p, true, out var keyVal))
                        {
                            int vkFromKey = KeyInterop.VirtualKeyFromKey(keyVal);
                            if (vkFromKey > 0)
                            {
                                def.VkCode = (ushort)vkFromKey;
                            }
                        }
                    }
                }
            }

            // 키 코드를 찾지 못한 경우 기본값 Tab
            if (def.VkCode == 0)
            {
                def.VkCode = NativeMethods.VK_TAB;
            }

            return def;
        }

        public static string Format(bool ctrl, bool alt, bool shift, bool win, ushort vkCode)
        {
            var sb = new StringBuilder();
            if (ctrl) sb.Append("Ctrl + ");
            if (alt) sb.Append("Alt + ");
            if (shift) sb.Append("Shift + ");
            if (win) sb.Append("Win + ");

            string keyName = GetKeyName(vkCode);
            sb.Append(keyName);

            return sb.ToString();
        }

        public static string GetKeyName(ushort vkCode)
        {
            if (VkToName.TryGetValue(vkCode, out var name))
            {
                return name;
            }

            if ((vkCode >= 'A' && vkCode <= 'Z') || (vkCode >= '0' && vkCode <= '9'))
            {
                return ((char)vkCode).ToString();
            }

            Key key = KeyInterop.KeyFromVirtualKey(vkCode);
            if (key != Key.None)
            {
                return key.ToString();
            }

            return $"Key_{vkCode}";
        }

        public static bool Matches(ushort vkCode, bool ctrl, bool alt, bool shift, bool win, HotkeyDef target)
        {
            if (target.VkCode != vkCode) return false;
            if (target.Ctrl != ctrl) return false;
            if (target.Alt != alt) return false;
            if (target.Shift != shift) return false;
            if (target.Win != win) return false;
            return true;
        }
    }
}
