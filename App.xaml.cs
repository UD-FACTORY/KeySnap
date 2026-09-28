using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Windows;
using QuickReplace.Models;
using QuickReplace.Services;
using QuickReplace.ViewModels;
using QuickReplace.Views;

namespace QuickReplace
{
    public partial class App : System.Windows.Application
    {
        private const string MutexName = @"Local\KeySnap_SingleInstance_Mutex_9B8F4A12";
        private const string EventName = @"Local\KeySnap_BringToFront_Event_9B8F4A12";

        private static Mutex? _singleInstanceMutex;
        private static EventWaitHandle? _bringToFrontEvent;
        private static RegisteredWaitHandle? _registeredWaitHandle;

        private StorageService? _storageService;
        private KeyboardHookService? _hookService;
        private TrayIconService? _trayService;
        private MainWindow? _mainWindow;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // 1. 단일 인스턴스 중복 실행 방지 (Single Instance Check)
            bool isNewInstance;
            try
            {
                _singleInstanceMutex = new Mutex(true, MutexName, out isNewInstance);
                if (!isNewInstance)
                {
                    // 이전 프로세스가 비정상 종료(Crash)되었는지 확인하여 소유권 획득 시도
                    try
                    {
                        if (_singleInstanceMutex.WaitOne(0, false))
                        {
                            isNewInstance = true;
                        }
                    }
                    catch (AbandonedMutexException)
                    {
                        isNewInstance = true;
                    }
                }
            }
            catch
            {
                isNewInstance = true;
            }

            if (!isNewInstance)
            {
                // 이미 실행 중인 인스턴스가 존재함
                bool startMinimized = e.Args.Any(a => a.Equals("--minimized", StringComparison.OrdinalIgnoreCase));
                if (!startMinimized)
                {
                    // 기존 실행 중인 창을 화면 맨 앞으로 띄우도록 이벤트 신호 전송
                    try
                    {
                        if (EventWaitHandle.TryOpenExisting(EventName, out var bringToFrontEvent))
                        {
                            bringToFrontEvent.Set();
                            bringToFrontEvent.Dispose();
                        }
                    }
                    catch
                    {
                    }
                }

                // 중복 실행 인스턴스는 즉시 종료
                Shutdown();
                return;
            }

            // 2. 첫 번째 인스턴스: 다른 인스턴스가 실행을 시도할 때 창을 띄워줄 이벤트 리스너 등록
            try
            {
                _bringToFrontEvent = new EventWaitHandle(false, EventResetMode.AutoReset, EventName);
                _registeredWaitHandle = ThreadPool.RegisterWaitForSingleObject(
                    _bringToFrontEvent,
                    (state, timedOut) =>
                    {
                        if (!timedOut)
                        {
                            Dispatcher.BeginInvoke(new Action(() =>
                            {
                                _mainWindow?.BringToFront();
                            }));
                        }
                    },
                    null,
                    Timeout.Infinite,
                    false
                );
            }
            catch
            {
            }

            try
            {
                // 3. 서비스 초기화
                _storageService = new StorageService();
                var settings = _storageService.LoadSettings();
                var shortcutsList = _storageService.LoadShortcuts();
                var exclusionsList = _storageService.LoadExclusions();

                var shortcuts = new ObservableCollection<ShortcutItem>(shortcutsList);
                var exclusions = new ObservableCollection<ExclusionApp>(exclusionsList);

                var macroService = new MacroService();
                var appWatcher = new ForegroundAppWatcher();
                var replacementEngine = new TextReplacementEngine(macroService);

                _hookService = new KeyboardHookService(
                    replacementEngine,
                    appWatcher,
                    settings,
                    shortcuts,
                    exclusions);

                // 트레이 서비스 초기화
                _trayService = new TrayIconService(
                    settings,
                    openSettingsAction: () =>
                    {
                        Dispatcher.Invoke(() => _mainWindow?.BringToFront());
                    },
                    toggleGlobalAction: enabled =>
                    {
                        settings.IsGlobalEnabled = enabled;
                        _storageService.SaveSettings(settings);
                    },
                    exitAction: () =>
                    {
                        Dispatcher.Invoke(ShutdownApp);
                    });

                // 치환 이벤트 시 알림
                _hookService.TextReplaced += (shortcut, replacement) =>
                {
                    string preview = replacement.Length > 25 ? replacement.Substring(0, 25) + "..." : replacement;
                    _trayService?.ShowNotification("텍스트 대치 완료", $"'{shortcut}' ➔ '{preview}'");
                };

                // 키보드 훅 시작
                _hookService.Start();

                // 4. ViewModel 및 MainWindow 생성
                var mainVm = new MainViewModel(
                    _storageService,
                    _hookService,
                    _trayService,
                    settings,
                    shortcuts,
                    exclusions);

                _mainWindow = new MainWindow(mainVm);

                // 5. 실행 모드 (부팅 시 자동실행인 경우 트레이로만 상주)
                bool startMinimized = e.Args.Any(a => a.Equals("--minimized", StringComparison.OrdinalIgnoreCase));
                if (!startMinimized)
                {
                    _mainWindow.Show();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"KeySnap 실행 중 오류가 발생했습니다: {ex.Message}", "시작 오류", MessageBoxButton.OK, MessageBoxImage.Error);
                Shutdown();
            }
        }

        private void ShutdownApp()
        {
            CleanupSingleInstanceResources();
            _hookService?.Dispose();
            _trayService?.Dispose();
            _mainWindow?.ExplicitExit();
            Shutdown();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            CleanupSingleInstanceResources();
            _hookService?.Dispose();
            _trayService?.Dispose();
            base.OnExit(e);
        }

        private void CleanupSingleInstanceResources()
        {
            try
            {
                _registeredWaitHandle?.Unregister(null);
                _registeredWaitHandle = null;

                _bringToFrontEvent?.Dispose();
                _bringToFrontEvent = null;

                if (_singleInstanceMutex != null)
                {
                    try
                    {
                        _singleInstanceMutex.ReleaseMutex();
                    }
                    catch
                    {
                    }
                    _singleInstanceMutex.Dispose();
                    _singleInstanceMutex = null;
                }
            }
            catch
            {
            }
        }
    }
}
