using System;
using System.Linq;
using System.Windows;
using QuickReplace.Services;
using QuickReplace.ViewModels;
using QuickReplace.Views;

namespace QuickReplace
{
    public partial class App : System.Windows.Application
    {
        private StorageService? _storageService;
        private KeyboardHookService? _hookService;
        private TrayIconService? _trayService;
        private MainWindow? _mainWindow;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            try
            {
                // 1. 서비스 초기화
                _storageService = new StorageService();
                var settings = _storageService.LoadSettings();
                var shortcuts = _storageService.LoadShortcuts();
                var exclusions = _storageService.LoadExclusions();

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

                // 2. ViewModel 및 MainWindow 생성
                var mainVm = new MainViewModel(
                    _storageService,
                    _hookService,
                    _trayService,
                    settings,
                    shortcuts,
                    exclusions);

                _mainWindow = new MainWindow(mainVm);

                // 3. 실행 모드 (부팅 시 자동실행인 경우 트레이로만 상주)
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
            _hookService?.Dispose();
            _trayService?.Dispose();
            _mainWindow?.ExplicitExit();
            Shutdown();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            _hookService?.Dispose();
            _trayService?.Dispose();
            base.OnExit(e);
        }
    }
}
