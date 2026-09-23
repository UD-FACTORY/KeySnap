using System;
using System.Text.RegularExpressions;
using System.Windows;

namespace QuickReplace.Services
{
    public class MacroService
    {
        public string ResolveMacros(string text, string? currentClipboardText = null)
        {
            if (string.IsNullOrEmpty(text)) return text;

            var now = DateTime.Now;

            // 기본 치환 (날짜/시간)
            string result = text
                .Replace("{{date}}", now.ToString("yyyy-MM-dd"), StringComparison.OrdinalIgnoreCase)
                .Replace("{{today}}", now.ToString("yyyy-MM-dd"), StringComparison.OrdinalIgnoreCase)
                .Replace("!오늘날짜", now.ToString("yyyy-MM-dd"), StringComparison.OrdinalIgnoreCase)
                .Replace("{{time}}", now.ToString("HH:mm:ss"), StringComparison.OrdinalIgnoreCase)
                .Replace("!현재시간", now.ToString("HH:mm:ss"), StringComparison.OrdinalIgnoreCase)
                .Replace("{{datetime}}", now.ToString("yyyy-MM-dd HH:mm:ss"), StringComparison.OrdinalIgnoreCase)
                .Replace("{{year}}", now.ToString("yyyy"), StringComparison.OrdinalIgnoreCase)
                .Replace("{{month}}", now.ToString("MM"), StringComparison.OrdinalIgnoreCase)
                .Replace("{{day}}", now.ToString("dd"), StringComparison.OrdinalIgnoreCase)
                .Replace("{{hour}}", now.ToString("HH"), StringComparison.OrdinalIgnoreCase)
                .Replace("{{minute}}", now.ToString("mm"), StringComparison.OrdinalIgnoreCase);

            // 클립보드 매크로
            if (result.Contains("{{clipboard}}", StringComparison.OrdinalIgnoreCase) || result.Contains("!클립보드"))
            {
                string clip = currentClipboardText ?? GetClipboardTextSafe();
                result = result.Replace("{{clipboard}}", clip, StringComparison.OrdinalIgnoreCase)
                               .Replace("!클립보드", clip);
            }

            return result;
        }

        private string GetClipboardTextSafe()
        {
            try
            {
                string clip = "";
                var thread = new System.Threading.Thread(() =>
                {
                    try
                    {
                        if (Clipboard.ContainsText())
                        {
                            clip = Clipboard.GetText();
                        }
                    }
                    catch { }
                });
                thread.SetApartmentState(System.Threading.ApartmentState.STA);
                thread.Start();
                thread.Join(300);
                return clip;
            }
            catch
            {
                return "";
            }
        }
    }
}
