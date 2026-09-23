using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using QuickReplace.Helpers;

namespace QuickReplace.Services
{
    public class TextReplacementEngine
    {
        private readonly MacroService _macroService;

        public TextReplacementEngine(MacroService macroService)
        {
            _macroService = macroService;
        }

        /// <summary>
        /// 단축어 텍스트를 대상 대치어로 교체합니다.
        /// </summary>
        /// <param name="backspaceCount">지워야 할 글자 수 (백스페이스 누를 횟수)</param>
        /// <param name="replacementTemplate">치환할 텍스트 (매크로 포함 가능)</param>
        /// <param name="restoreDelayMs">치환 후 클립보드 복원 대기 시간</param>
        public async Task ReplaceTextAsync(int backspaceCount, string replacementTemplate, int restoreDelayMs = 80)
        {
            await Task.Run(() =>
            {
                // 1. 기존 클립보드 백업
                var clipboardBackup = ClipboardHelper.BackupClipboard();

                // 2. 매크로 확장
                string finalText = _macroService.ResolveMacros(replacementTemplate);

                // 3. 백스페이스 전송하여 입력된 단축어 지우기
                SendBackspaces(backspaceCount);

                // 지움 처리가 대상 앱에 전달될 약간의 지연
                Thread.Sleep(20);

                // 4. 대치어 클립보드 설정
                if (ClipboardHelper.SetText(finalText))
                {
                    // 5. Ctrl + V 전송
                    SendPaste();

                    // 6. 대상 앱이 붙여넣기를 완료하도록 대기 후 클립보드 복원
                    Thread.Sleep(Math.Max(50, restoreDelayMs));
                    ClipboardHelper.RestoreClipboard(clipboardBackup);
                }
            });
        }

        private static void SendBackspaces(int count)
        {
            if (count <= 0) return;

            var inputs = new NativeMethods.INPUT[count * 2];
            for (int i = 0; i < count; i++)
            {
                // Key Down
                inputs[i * 2] = new NativeMethods.INPUT
                {
                    type = NativeMethods.INPUT_KEYBOARD,
                    U = new NativeMethods.InputUnion
                    {
                        ki = new NativeMethods.KEYBDINPUT
                        {
                            wVk = NativeMethods.VK_BACK,
                            wScan = 0,
                            dwFlags = 0,
                            time = 0,
                            dwExtraInfo = NativeMethods.CUSTOM_INJECTION_FLAG
                        }
                    }
                };

                // Key Up
                inputs[i * 2 + 1] = new NativeMethods.INPUT
                {
                    type = NativeMethods.INPUT_KEYBOARD,
                    U = new NativeMethods.InputUnion
                    {
                        ki = new NativeMethods.KEYBDINPUT
                        {
                            wVk = NativeMethods.VK_BACK,
                            wScan = 0,
                            dwFlags = NativeMethods.KEYEVENTF_KEYUP,
                            time = 0,
                            dwExtraInfo = NativeMethods.CUSTOM_INJECTION_FLAG
                        }
                    }
                };
            }

            NativeMethods.SendInput((uint)inputs.Length, inputs, Marshal.SizeOf(typeof(NativeMethods.INPUT)));
        }

        private static void SendPaste()
        {
            var inputs = new NativeMethods.INPUT[4];

            // Ctrl Down
            inputs[0] = new NativeMethods.INPUT
            {
                type = NativeMethods.INPUT_KEYBOARD,
                U = new NativeMethods.InputUnion
                {
                    ki = new NativeMethods.KEYBDINPUT
                    {
                        wVk = NativeMethods.VK_CONTROL,
                        dwFlags = 0,
                        dwExtraInfo = NativeMethods.CUSTOM_INJECTION_FLAG
                    }
                }
            };

            // V Down
            inputs[1] = new NativeMethods.INPUT
            {
                type = NativeMethods.INPUT_KEYBOARD,
                U = new NativeMethods.InputUnion
                {
                    ki = new NativeMethods.KEYBDINPUT
                    {
                        wVk = NativeMethods.VK_V,
                        dwFlags = 0,
                        dwExtraInfo = NativeMethods.CUSTOM_INJECTION_FLAG
                    }
                }
            };

            // V Up
            inputs[2] = new NativeMethods.INPUT
            {
                type = NativeMethods.INPUT_KEYBOARD,
                U = new NativeMethods.InputUnion
                {
                    ki = new NativeMethods.KEYBDINPUT
                    {
                        wVk = NativeMethods.VK_V,
                        dwFlags = NativeMethods.KEYEVENTF_KEYUP,
                        dwExtraInfo = NativeMethods.CUSTOM_INJECTION_FLAG
                    }
                }
            };

            // Ctrl Up
            inputs[3] = new NativeMethods.INPUT
            {
                type = NativeMethods.INPUT_KEYBOARD,
                U = new NativeMethods.InputUnion
                {
                    ki = new NativeMethods.KEYBDINPUT
                    {
                        wVk = NativeMethods.VK_CONTROL,
                        dwFlags = NativeMethods.KEYEVENTF_KEYUP,
                        dwExtraInfo = NativeMethods.CUSTOM_INJECTION_FLAG
                    }
                }
            };

            NativeMethods.SendInput((uint)inputs.Length, inputs, Marshal.SizeOf(typeof(NativeMethods.INPUT)));
        }
    }
}
