using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using QuickReplace.Helpers;
using QuickReplace.Models;

namespace QuickReplace.Services
{
    public class KeyboardHookService : IDisposable
    {
        private readonly TextReplacementEngine _replacementEngine;
        private readonly ForegroundAppWatcher _appWatcher;
        private readonly AppSettings _settings;
        private readonly List<ShortcutItem> _shortcuts;
        private readonly List<ExclusionApp> _exclusions;

        private IntPtr _hookId = IntPtr.Zero;
        private NativeMethods.LowLevelKeyboardProc? _proc;
        private readonly StringBuilder _keyBuffer = new();
        private readonly object _bufferLock = new();

        public bool IsRunning => _hookId != IntPtr.Zero;

        public event Action<string, string>? TextReplaced;

        public KeyboardHookService(
            TextReplacementEngine replacementEngine,
            ForegroundAppWatcher appWatcher,
            AppSettings settings,
            List<ShortcutItem> shortcuts,
            List<ExclusionApp> exclusions)
        {
            _replacementEngine = replacementEngine;
            _appWatcher = appWatcher;
            _settings = settings;
            _shortcuts = shortcuts;
            _exclusions = exclusions;
        }

        public void Start()
        {
            if (_hookId != IntPtr.Zero) return;

            _proc = HookCallback;
            using var curProcess = Process.GetCurrentProcess();
            using var curModule = curProcess.MainModule;
            IntPtr moduleHandle = NativeMethods.GetModuleHandle(curModule?.ModuleName);

            _hookId = NativeMethods.SetWindowsHookEx(
                NativeMethods.WH_KEYBOARD_LL,
                _proc,
                moduleHandle,
                0);
        }

        public void Stop()
        {
            if (_hookId != IntPtr.Zero)
            {
                NativeMethods.UnhookWindowsHookEx(_hookId);
                _hookId = IntPtr.Zero;
                _proc = null;
            }
            ClearBuffer();
        }

        public void ClearBuffer()
        {
            lock (_bufferLock)
            {
                _keyBuffer.Clear();
            }
        }

        private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0 && (wParam == (IntPtr)NativeMethods.WM_KEYDOWN || wParam == (IntPtr)NativeMethods.WM_SYSKEYDOWN))
            {
                var kbd = Marshal.PtrToStructure<NativeMethods.KBDLLHOOKSTRUCT>(lParam);

                // 자체 주입 키 이벤트 무시
                if (kbd.dwExtraInfo == NativeMethods.CUSTOM_INJECTION_FLAG)
                {
                    return NativeMethods.CallNextHookEx(_hookId, nCode, wParam, lParam);
                }

                // 전역 비활성화 상태이거나 예외 프로그램인 경우 무조건 통과
                if (!_settings.IsGlobalEnabled || _appWatcher.IsExcluded(_exclusions))
                {
                    ClearBuffer();
                    return NativeMethods.CallNextHookEx(_hookId, nCode, wParam, lParam);
                }

                ushort vkCode = (ushort)kbd.vkCode;
                ushort scanCode = (ushort)kbd.scanCode;

                // 1. 단축키(트리거 키) 입력 검사
                if (IsTriggerHotkey(vkCode, out bool shouldSuppressKey))
                {
                    if (TryTriggerMatch(out var matchedShortcut))
                    {
                        // 트리거 키를 가로채어 앱에 전달하지 않고 치환 실행!
                        TriggerReplacement(matchedShortcut, extraBackspace: 0);
                        return (IntPtr)1; // 키 이벤트 차단 (Tab 등의 포커스 이동 방지)
                    }
                }

                // 2. 백스페이스 시 버퍼 한 글자 감소
                if (vkCode == NativeMethods.VK_BACK)
                {
                    lock (_bufferLock)
                    {
                        if (_keyBuffer.Length > 0)
                        {
                            _keyBuffer.Length--;
                        }
                    }
                    return NativeMethods.CallNextHookEx(_hookId, nCode, wParam, lParam);
                }

                // 3. ESC나 방향키, 마우스 클릭 등 시 버퍼 리셋
                if (vkCode == 0x1B || // ESC
                    (vkCode >= 0x25 && vkCode <= 0x28)) // Arrow Keys
                {
                    ClearBuffer();
                    return NativeMethods.CallNextHookEx(_hookId, nCode, wParam, lParam);
                }

                // 4. 일반 문자 버퍼링
                char typedChar = GetCharFromKey(vkCode, scanCode);
                if (typedChar != '\0')
                {
                    lock (_bufferLock)
                    {
                        _keyBuffer.Append(typedChar);
                        if (_keyBuffer.Length > 60)
                        {
                            _keyBuffer.Remove(0, _keyBuffer.Length - 60);
                        }
                    }
                }
            }

            return NativeMethods.CallNextHookEx(_hookId, nCode, wParam, lParam);
        }

        private string? _lastTriggerHotkeyText;
        private HotkeyDef? _cachedTriggerDef;

        private HotkeyDef GetActiveTriggerDef()
        {
            string currentText = _settings.TriggerHotkey ?? "Tab";
            if (_cachedTriggerDef == null || _lastTriggerHotkeyText != currentText)
            {
                _cachedTriggerDef = HotkeyHelper.Parse(currentText);
                _lastTriggerHotkeyText = currentText;
            }
            return _cachedTriggerDef;
        }

        private bool IsTriggerHotkey(ushort vkCode, out bool isWhitespaceKey)
        {
            isWhitespaceKey = false;
            bool ctrl = (NativeMethods.GetKeyState(NativeMethods.VK_CONTROL) & 0x8000) != 0;
            bool alt = (NativeMethods.GetKeyState(NativeMethods.VK_MENU) & 0x8000) != 0;
            bool shift = (NativeMethods.GetKeyState(NativeMethods.VK_SHIFT) & 0x8000) != 0;
            bool win = ((NativeMethods.GetKeyState(0x5B) & 0x8000) != 0) || ((NativeMethods.GetKeyState(0x5C) & 0x8000) != 0);

            var hotkeyDef = GetActiveTriggerDef();
            if (HotkeyHelper.Matches(vkCode, ctrl, alt, shift, win, hotkeyDef))
            {
                isWhitespaceKey = (vkCode == NativeMethods.VK_SPACE || vkCode == NativeMethods.VK_TAB || vkCode == NativeMethods.VK_RETURN);
                return true;
            }

            return false;
        }

        private bool TryTriggerMatch(out ShortcutItem matched)
        {
            matched = null!;
            lock (_bufferLock)
            {
                string currentBuffer = _keyBuffer.ToString();
                if (string.IsNullOrEmpty(currentBuffer)) return false;

                var enabledShortcuts = _shortcuts.Where(s => s.IsEnabled && !string.IsNullOrEmpty(s.Shortcut)).ToList();

                foreach (var item in enabledShortcuts)
                {
                    string target = item.Shortcut;
                    string decomposedTarget = KoreanHelper.DecomposeToKeyStrokes(target);

                    if (MatchBuffer(currentBuffer, target, decomposedTarget))
                    {
                        matched = item;
                        return true;
                    }
                }
            }
            return false;
        }

        private static bool MatchBuffer(string buffer, string target, string decomposedTarget)
        {
            if (string.IsNullOrEmpty(buffer)) return false;

            if (buffer.EndsWith(target, StringComparison.OrdinalIgnoreCase)) return true;

            if (!string.IsNullOrEmpty(decomposedTarget) &&
                buffer.EndsWith(decomposedTarget, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return false;
        }

        private void TriggerReplacement(ShortcutItem item, int extraBackspace)
        {
            int backspaceCount = item.Shortcut.Length + extraBackspace;

            _keyBuffer.Clear();

            _ = Task.Run(async () =>
            {
                await _replacementEngine.ReplaceTextAsync(
                    backspaceCount,
                    item.Replacement,
                    _settings.ClipboardRestoreDelayMs);

                TextReplaced?.Invoke(item.Shortcut, item.Replacement);
            });
        }

        private static char GetCharFromKey(ushort vkCode, ushort scanCode)
        {
            if (vkCode == NativeMethods.VK_SPACE) return ' ';
            if (vkCode == NativeMethods.VK_RETURN) return '\n';
            if (vkCode == NativeMethods.VK_TAB) return '\t';

            byte[] keyboardState = new byte[256];
            NativeMethods.GetKeyboardState(keyboardState);

            var sb = new StringBuilder(10);
            IntPtr hkl = NativeMethods.GetKeyboardLayout(0);

            int result = NativeMethods.ToUnicodeEx(vkCode, scanCode, keyboardState, sb, sb.Capacity, 0, hkl);
            if (result > 0 && sb.Length > 0)
            {
                return sb[0];
            }

            if (vkCode >= 0x41 && vkCode <= 0x5A) // A-Z
            {
                bool isShift = (NativeMethods.GetKeyState(NativeMethods.VK_SHIFT) & 0x8000) != 0;
                char c = (char)vkCode;
                return isShift ? c : char.ToLower(c);
            }
            if (vkCode >= 0x30 && vkCode <= 0x39) // 0-9
            {
                return (char)vkCode;
            }

            return '\0';
        }

        public void Dispose()
        {
            Stop();
            GC.SuppressFinalize(this);
        }
    }
}
