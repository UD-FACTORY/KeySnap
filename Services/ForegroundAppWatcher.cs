using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using QuickReplace.Helpers;
using QuickReplace.Models;

namespace QuickReplace.Services
{
    public class ForegroundAppWatcher
    {
        /// <summary>
        /// 현재 포커스된 전면 윈도우의 프로세스 이름을 반환합니다 (확장자 제외).
        /// </summary>
        public string GetActiveProcessName()
        {
            try
            {
                IntPtr hWnd = NativeMethods.GetForegroundWindow();
                if (hWnd == IntPtr.Zero) return string.Empty;

                NativeMethods.GetWindowThreadProcessId(hWnd, out uint processId);
                if (processId == 0) return string.Empty;

                using var process = Process.GetProcessById((int)processId);
                return process.ProcessName;
            }
            catch
            {
                return string.Empty;
            }
        }

        /// <summary>
        /// 현재 활성 프로세스가 예외 목록에 포함되어 있는지 확인합니다.
        /// </summary>
        public bool IsExcluded(IEnumerable<ExclusionApp> exclusions)
        {
            string currentApp = GetActiveProcessName();
            if (string.IsNullOrWhiteSpace(currentApp)) return false;

            return exclusions.Any(ex =>
                ex.IsEnabled &&
                (string.Equals(ex.ProcessName, currentApp, StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(ex.ProcessName, currentApp + ".exe", StringComparison.OrdinalIgnoreCase)));
        }

        /// <summary>
        /// 현재 실행 중인 GUI 응용 프로그램 목록(프로세스명 목록)을 반환합니다.
        /// (예외 프로그램 추가 시 편리하게 선택할 수 있도록 제공)
        /// </summary>
        public static List<(string ProcessName, string MainWindowTitle)> GetRunningGuiApplications()
        {
            var list = new List<(string ProcessName, string MainWindowTitle)>();
            try
            {
                var processes = Process.GetProcesses();
                foreach (var p in processes)
                {
                    try
                    {
                        if (p.MainWindowHandle != IntPtr.Zero && !string.IsNullOrWhiteSpace(p.MainWindowTitle))
                        {
                            list.Add((p.ProcessName, p.MainWindowTitle));
                        }
                    }
                    catch { }
                }
            }
            catch { }
            return list.OrderBy(x => x.MainWindowTitle).ToList();
        }
    }
}
