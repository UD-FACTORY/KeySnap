using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;
using Microsoft.Win32;
using QuickReplace.Models;

namespace QuickReplace.Services
{
    public class StorageService
    {
        private static readonly string AppDataFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "KeySnap");

        private static readonly string SettingsFile = Path.Combine(AppDataFolder, "settings.json");
        private static readonly string ShortcutsFile = Path.Combine(AppDataFolder, "shortcuts.json");
        private static readonly string ExclusionsFile = Path.Combine(AppDataFolder, "exclusions.json");

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true,
            Encoder = JavaScriptEncoder.Create(UnicodeRanges.All) // 한글 깨짐 방지
        };

        public StorageService()
        {
            // 구버전 QuickReplace 폴더 마이그레이션 지원
            string oldFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "QuickReplace");
            if (!Directory.Exists(AppDataFolder) && Directory.Exists(oldFolder))
            {
                try
                {
                    Directory.Move(oldFolder, AppDataFolder);
                }
                catch { }
            }

            if (!Directory.Exists(AppDataFolder))
            {
                Directory.CreateDirectory(AppDataFolder);
            }
        }

        public AppSettings LoadSettings()
        {
            try
            {
                if (File.Exists(SettingsFile))
                {
                    string json = File.ReadAllText(SettingsFile);
                    var settings = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions);
                    if (settings != null) return settings;
                }
            }
            catch { }
            return new AppSettings();
        }

        public void SaveSettings(AppSettings settings)
        {
            try
            {
                string json = JsonSerializer.Serialize(settings, JsonOptions);
                File.WriteAllText(SettingsFile, json);
            }
            catch { }
        }

        public List<ShortcutItem> LoadShortcuts()
        {
            try
            {
                if (File.Exists(ShortcutsFile))
                {
                    string json = File.ReadAllText(ShortcutsFile);
                    var list = JsonSerializer.Deserialize<List<ShortcutItem>>(json, JsonOptions);
                    if (list != null && list.Count > 0) return list;
                }
            }
            catch { }

            // 기본 예시 단축어 세트 제공
            var defaultList = GetDefaultShortcuts();
            SaveShortcuts(defaultList);
            return defaultList;
        }

        public void SaveShortcuts(List<ShortcutItem> list)
        {
            try
            {
                string json = JsonSerializer.Serialize(list, JsonOptions);
                File.WriteAllText(ShortcutsFile, json);
            }
            catch { }
        }

        public List<ExclusionApp> LoadExclusions()
        {
            try
            {
                if (File.Exists(ExclusionsFile))
                {
                    string json = File.ReadAllText(ExclusionsFile);
                    var list = JsonSerializer.Deserialize<List<ExclusionApp>>(json, JsonOptions);
                    if (list != null) return list;
                }
            }
            catch { }

            // 기본 예외 프로그램 추천 (게임/보안 등)
            var defaultExclusions = new List<ExclusionApp>
            {
                new() { ProcessName = "League of Legends", Description = "리그 오브 레전드", IsEnabled = true },
                new() { ProcessName = "Overwatch", Description = "오버워치", IsEnabled = true },
                new() { ProcessName = "Valorant", Description = "발로란트", IsEnabled = true }
            };
            SaveExclusions(defaultExclusions);
            return defaultExclusions;
        }

        public void SaveExclusions(List<ExclusionApp> list)
        {
            try
            {
                string json = JsonSerializer.Serialize(list, JsonOptions);
                File.WriteAllText(ExclusionsFile, json);
            }
            catch { }
        }

        public void ExportShortcuts(string filePath, List<ShortcutItem> items)
        {
            string json = JsonSerializer.Serialize(items, JsonOptions);
            File.WriteAllText(filePath, json);
        }

        public List<ShortcutItem>? ImportShortcuts(string filePath)
        {
            if (!File.Exists(filePath)) return null;
            string json = File.ReadAllText(filePath);
            return JsonSerializer.Deserialize<List<ShortcutItem>>(json, JsonOptions);
        }

        public void SetStartup(bool enable)
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", true);
                if (key == null) return;

                string? exePath = Environment.ProcessPath;
                if (string.IsNullOrEmpty(exePath)) return;

                if (enable)
                {
                    key.SetValue("KeySnap", $"\"{exePath}\" --minimized");
                    // 구버전 키 정리
                    if (key.GetValue("QuickReplace") != null)
                    {
                        key.DeleteValue("QuickReplace");
                    }
                }
                else
                {
                    if (key.GetValue("KeySnap") != null)
                    {
                        key.DeleteValue("KeySnap");
                    }
                    if (key.GetValue("QuickReplace") != null)
                    {
                        key.DeleteValue("QuickReplace");
                    }
                }
            }
            catch { }
        }

        private static List<ShortcutItem> GetDefaultShortcuts()
        {
            return new List<ShortcutItem>
            {
                new()
                {
                    Shortcut = "ㅇㅈ",
                    Replacement = "인정합니다. 👍",
                    Trigger = TriggerMode.Instant,
                    Group = "일상",
                    IsEnabled = true
                },
                new()
                {
                    Shortcut = "ㄱㅅ",
                    Replacement = "감사합니다! 좋은 하루 보내세요. 😊",
                    Trigger = TriggerMode.Instant,
                    Group = "인사",
                    IsEnabled = true
                },
                new()
                {
                    Shortcut = "ㅈㄱ",
                    Replacement = "지금 통화 가능하신가요?",
                    Trigger = TriggerMode.Instant,
                    Group = "업무",
                    IsEnabled = true
                },
                new()
                {
                    Shortcut = "!email",
                    Replacement = "user@example.com",
                    Trigger = TriggerMode.Instant,
                    Group = "개인정보",
                    IsEnabled = true
                },
                new()
                {
                    Shortcut = "!date",
                    Replacement = "{{today}}",
                    Trigger = TriggerMode.Instant,
                    Group = "매크로",
                    IsEnabled = true
                },
                new()
                {
                    Shortcut = "!time",
                    Replacement = "{{time}}",
                    Trigger = TriggerMode.Instant,
                    Group = "매크로",
                    IsEnabled = true
                },
                new()
                {
                    Shortcut = "btw",
                    Replacement = "by the way",
                    Trigger = TriggerMode.TriggerKey,
                    Group = "영어",
                    IsEnabled = true
                }
            };
        }
    }
}
