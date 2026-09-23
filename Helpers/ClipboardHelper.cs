using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;

namespace QuickReplace.Helpers
{
    public static class ClipboardHelper
    {
        private const int MaxRetries = 6;
        private const int RetryDelayMs = 15;

        /// <summary>
        /// 안전하게 클립보드 데이터를 백업합니다.
        /// </summary>
        public static IDataObject? BackupClipboard()
        {
            IDataObject? data = null;
            ExecuteInSta(() =>
            {
                for (int i = 0; i < MaxRetries; i++)
                {
                    try
                    {
                        data = Clipboard.GetDataObject();
                        break;
                    }
                    catch (COMException)
                    {
                        Thread.Sleep(RetryDelayMs);
                    }
                    catch (Exception)
                    {
                        break;
                    }
                }
            });
            return data;
        }

        /// <summary>
        /// 안전하게 클립보드에 텍스트를 주입합니다.
        /// </summary>
        public static bool SetText(string text)
        {
            bool success = false;
            ExecuteInSta(() =>
            {
                for (int i = 0; i < MaxRetries; i++)
                {
                    try
                    {
                        Clipboard.SetDataObject(text, true);
                        success = true;
                        break;
                    }
                    catch (COMException)
                    {
                        Thread.Sleep(RetryDelayMs);
                    }
                    catch (Exception)
                    {
                        break;
                    }
                }
            });
            return success;
        }

        /// <summary>
        /// 백업해 둔 데이터를 클립보드에 복원합니다.
        /// </summary>
        public static void RestoreClipboard(IDataObject? data)
        {
            if (data == null) return;

            ExecuteInSta(() =>
            {
                for (int i = 0; i < MaxRetries; i++)
                {
                    try
                    {
                        Clipboard.SetDataObject(data, true);
                        break;
                    }
                    catch (COMException)
                    {
                        Thread.Sleep(RetryDelayMs);
                    }
                    catch (Exception)
                    {
                        break;
                    }
                }
            });
        }

        private static void ExecuteInSta(Action action)
        {
            if (Thread.CurrentThread.GetApartmentState() == ApartmentState.STA)
            {
                action();
            }
            else
            {
                var staThread = new Thread(() => action())
                {
                    IsBackground = true
                };
                staThread.SetApartmentState(ApartmentState.STA);
                staThread.Start();
                staThread.Join(500); // 최대 500ms 대기
            }
        }
    }
}
