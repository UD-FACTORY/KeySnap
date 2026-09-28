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
        private readonly IList<ShortcutItem> _shortcuts;
        private readonly IList<ExclusionApp> _exclusions;

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
            IList<ShortcutItem> shortcuts,
            IList<ExclusionApp> exclusions)
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
                if (!_settings.IsGlobalEnabled || _appWatcher.IsExcluded(_exclusions.ToList()))
                {
                    ClearBuffer();
                    return NativeMethods.CallNextHookEx(_hookId, nCode, wParam, lParam);
                }

                ushort vkCode = (ushort)kbd.vkCode;
                ushort scanCode = (ushort)kbd.scanCode;

                // IME 입력 중인 경우 실제 가상 키 코드로 복원 (0xE5 = VK_PROCESSKEY)
                if (vkCode == NativeMethods.VK_PROCESSKEY)
                {
                    vkCode = (ushort)NativeMethods.MapVirtualKey(scanCode, 1);
                }

                // 1. 단축키 변환 모드: 트리거 키가 입력되었는지 검사
                if (_settings.ReplacementMode == ReplacementMode.Hotkey)
                {
                    if (IsTriggerHotkey(vkCode, out bool isWhitespaceKey))
                    {
                        if (TryTriggerMatch(out var matchedShortcut, out int typedCharCount))
                        {
                            // 트리거 키를 가로채어 앱에 전달하지 않고 치환 실행
                            TriggerReplacement(matchedShortcut, typedCharCount, extraBackspace: 0, delayMs: 0);
                            return (IntPtr)1; // 트리거 키 입력 차단
                        }
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

                // 3. ESC나 방향키 입력 시 버퍼 리셋
                if (vkCode == 0x1B || (vkCode >= 0x25 && vkCode <= 0x28))
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
                        if (_keyBuffer.Length > 80)
                        {
                            _keyBuffer.Remove(0, _keyBuffer.Length - 80);
                        }
                    }

                    // 5. 즉시 변환 모드: 문자가 입력된 즉시 단축어 일치 여부 검사
                    if (_settings.ReplacementMode == ReplacementMode.Instant)
                    {
                        if (TryTriggerMatch(out var matchedShortcut, out int typedCharCount))
                        {
                            ClearBuffer();
                            // 현재 입력된 마지막 문자가 대상 프로그램에 먼저 반영되도록 30ms 지연 후 치환
                            TriggerReplacement(matchedShortcut, typedCharCount, extraBackspace: 0, delayMs: 30);
                            return NativeMethods.CallNextHookEx(_hookId, nCode, wParam, lParam);
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
            bool ctrl = (NativeMethods.GetAsyncKeyState(NativeMethods.VK_CONTROL) & 0x8000) != 0;
            bool alt = (NativeMethods.GetAsyncKeyState(NativeMethods.VK_MENU) & 0x8000) != 0;
            bool shift = (NativeMethods.GetAsyncKeyState(NativeMethods.VK_SHIFT) & 0x8000) != 0;
            bool win = ((NativeMethods.GetAsyncKeyState(0x5B) & 0x8000) != 0) || ((NativeMethods.GetAsyncKeyState(0x5C) & 0x8000) != 0);

            var hotkeyDef = GetActiveTriggerDef();
            if (HotkeyHelper.Matches(vkCode, ctrl, alt, shift, win, hotkeyDef))
            {
                isWhitespaceKey = (vkCode == NativeMethods.VK_SPACE || vkCode == NativeMethods.VK_TAB || vkCode == NativeMethods.VK_RETURN);
                return true;
            }

            return false;
        }

        private bool TryTriggerMatch(out ShortcutItem matched, out int typedCharCount)
        {
            matched = null!;
            typedCharCount = 0;
            lock (_bufferLock)
            {
                string currentBuffer = _keyBuffer.ToString();
                if (string.IsNullOrEmpty(currentBuffer)) return false;

                // 일치 판정 시 더 긴 단축어에 우선순위 부여
                var enabledShortcuts = _shortcuts
                    .Where(s => s.IsEnabled && !string.IsNullOrEmpty(s.Shortcut))
                    .OrderByDescending(s => s.Shortcut.Length)
                    .ToList();

                foreach (var item in enabledShortcuts)
                {
                    string target = item.Shortcut;
                    string decomposedTarget = KoreanHelper.DecomposeToKeyStrokes(target);

                    // 1. 직접 일치 (예: !email, btw 등)
                    if (currentBuffer.EndsWith(target, StringComparison.OrdinalIgnoreCase))
                    {
                        matched = item;
                        typedCharCount = target.Length;
                        return true;
                    }

                    // 2. 한글 두벌식 키스트로크 일치 (예: ㅇㅈ -> dw, 인정 -> dlswjd 등)
                    if (!string.IsNullOrEmpty(decomposedTarget) &&
                        currentBuffer.EndsWith(decomposedTarget, StringComparison.OrdinalIgnoreCase))
                    {
                        matched = item;
                        typedCharCount = target.Length;
                        return true;
                    }
                }
            }
            return false;
        }

        private void TriggerReplacement(ShortcutItem item, int typedCharCount, int extraBackspace = 0, int delayMs = 0)
        {
            int backspaceCount = typedCharCount + extraBackspace;

            ClearBuffer();

            _ = Task.Run(async () =>
            {
                if (delayMs > 0)
                {
                    await Task.Delay(delayMs);
                }

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

            // Shift와 CapsLock의 물리적 키 상태 확인
            bool isShift = (NativeMethods.GetAsyncKeyState(NativeMethods.VK_SHIFT) & 0x8000) != 0;
            bool isCapsLock = (NativeMethods.GetKeyState(0x14) & 1) != 0;

            byte[] keyboardState = new byte[256];
            if (isShift)
            {
                keyboardState[NativeMethods.VK_SHIFT] = 0x80;
                keyboardState[0xA0] = 0x80; // VK_LSHIFT
            }
            if (isCapsLock)
            {
                keyboardState[0x14] = 0x01; // CapsLock Toggle
            }

            var sb = new StringBuilder(10);
            IntPtr hwnd = NativeMethods.GetForegroundWindow();
            uint threadId = NativeMethods.GetWindowThreadProcessId(hwnd, out _);
            IntPtr hkl = NativeMethods.GetKeyboardLayout(threadId);

            int result = NativeMethods.ToUnicodeEx(vkCode, scanCode, keyboardState, sb, sb.Capacity, 0, hkl);
            if (result > 0 && sb.Length > 0)
            {
                return sb[0];
            }

            // 1. 알파벳 (A-Z)
            if (vkCode >= 0x41 && vkCode <= 0x5A)
            {
                bool upper = isShift ^ isCapsLock;
                char c = (char)vkCode;
                return upper ? c : char.ToLower(c);
            }

            // 2. 숫자 및 Shift 기호 (0-9 -> !@#$%^&*())
            if (vkCode >= 0x30 && vkCode <= 0x39)
            {
                if (isShift)
                {
                    return vkCode switch
                    {
                        0x31 => '!',
                        0x32 => '@',
                        0x33 => '#',
                        0x34 => '$',
                        0x35 => '%',
                        0x36 => '^',
                        0x37 => '&',
                        0x38 => '*',
                        0x39 => '(',
                        0x30 => ')',
                        _ => (char)vkCode
                    };
                }
                return (char)vkCode;
            }

            // 3. OEM 특수기호
            return vkCode switch
            {
                0xBA => isShift ? ':' : ';',
                0xBB => isShift ? '+' : '=',
                0xBC => isShift ? '<' : ',',
                0xBD => isShift ? '_' : '-',
                0xBE => isShift ? '>' : '.',
                0xBF => isShift ? '?' : '/',
                0xC0 => isShift ? '~' : '`',
                0xDB => isShift ? '{' : '[',
                0xDC => isShift ? '|' : '\\',
                0xDD => isShift ? '}' : ']',
                0xDE => isShift ? '"' : '\'',
                _ => '\0'
            };
        }

        public void Dispose()
        {
            Stop();
            GC.SuppressFinalize(this);
        }
    }
}
