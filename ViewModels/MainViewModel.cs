using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using Microsoft.Win32;
using QuickReplace.Helpers;
using QuickReplace.Models;
using QuickReplace.Services;

namespace QuickReplace.ViewModels
{
    public class MainViewModel : ObservableObject
    {
        private readonly StorageService _storageService;
        private readonly KeyboardHookService _hookService;
        private readonly TrayIconService _trayService;

        private AppSettings _settings;
        private string _searchText = string.Empty;
        private ShortcutItem? _selectedShortcut;
        private ExclusionApp? _selectedExclusion;

        private ICollectionView _shortcutsView;

        public ObservableCollection<ShortcutItem> Shortcuts { get; }
        public ObservableCollection<ExclusionApp> Exclusions { get; }

        public AppSettings Settings
        {
            get => _settings;
            set => SetField(ref _settings, value);
        }

        public bool IsGlobalEnabled
        {
            get => Settings.IsGlobalEnabled;
            set
            {
                if (Settings.IsGlobalEnabled != value)
                {
                    Settings.IsGlobalEnabled = value;
                    _storageService.SaveSettings(Settings);
                    _trayService.UpdateMenuState();
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(Settings));
                }
            }
        }

        private bool _isRecordingHotkey;
        public bool IsRecordingHotkey
        {
            get => _isRecordingHotkey;
            set => SetField(ref _isRecordingHotkey, value);
        }

        private string _recordingPrompt = "원하는 키 또는 조합키를 누르세요... (취소: ESC)";
        public string RecordingPrompt
        {
            get => _recordingPrompt;
            set => SetField(ref _recordingPrompt, value);
        }

        public ICommand StartRecordingHotkeyCommand { get; }
        public ICommand CancelRecordingHotkeyCommand { get; }
        public ICommand ResetTriggerHotkeyCommand { get; }

        public void ApplyCustomHotkey(bool ctrl, bool alt, bool shift, bool win, ushort vkCode)
        {
            string formatted = HotkeyHelper.Format(ctrl, alt, shift, win, vkCode);
            SelectedTriggerHotkey = formatted;
            IsRecordingHotkey = false;
        }

        public List<string> AvailableTriggerHotkeys { get; } = new()
        {
            "Tab",
            "Space",
            "Ctrl + Space",
            "Shift + Space",
            "Enter",
            "Alt + Enter",
            "F8"
        };

        public string SelectedTriggerHotkey
        {
            get => Settings.TriggerHotkey;
            set
            {
                if (Settings.TriggerHotkey != value)
                {
                    Settings.TriggerHotkey = value;
                    _storageService.SaveSettings(Settings);
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(SelectedTriggerHotkeyDisplay));
                }
            }
        }

        public string SelectedTriggerHotkeyDisplay => string.IsNullOrWhiteSpace(SelectedTriggerHotkey) ? "Tab" : SelectedTriggerHotkey;

        public string SearchText
        {
            get => _searchText;
            set
            {
                if (SetField(ref _searchText, value))
                {
                    _shortcutsView.Refresh();
                }
            }
        }

        public ShortcutItem? SelectedShortcut
        {
            get => _selectedShortcut;
            set => SetField(ref _selectedShortcut, value);
        }

        public ExclusionApp? SelectedExclusion
        {
            get => _selectedExclusion;
            set => SetField(ref _selectedExclusion, value);
        }

        private string _exclusionSearchText = string.Empty;
        private ICollectionView _exclusionsView;

        public string ExclusionSearchText
        {
            get => _exclusionSearchText;
            set
            {
                if (SetField(ref _exclusionSearchText, value))
                {
                    _exclusionsView.Refresh();
                }
            }
        }

        public ICollectionView FilteredShortcuts => _shortcutsView;
        public ICollectionView FilteredExclusions => _exclusionsView;

        public int TotalShortcutCount => Shortcuts.Count;
        public int EnabledShortcutCount => Shortcuts.Count(s => s.IsEnabled);

        public int TotalExclusionCount => Exclusions.Count;
        public int EnabledExclusionCount => Exclusions.Count(e => e.IsEnabled);

        public ICommand OpenBlogCommand { get; }

        // Commands
        public ICommand AddShortcutCommand { get; }
        public ICommand EditShortcutCommand { get; }
        public ICommand DeleteShortcutCommand { get; }
        public ICommand ToggleShortcutCommand { get; }
        public ICommand ExportShortcutsCommand { get; }
        public ICommand ImportShortcutsCommand { get; }

        public ICommand AddExclusionCommand { get; }
        public ICommand BrowseExclusionCommand { get; }
        public ICommand DeleteExclusionCommand { get; }

        public ICommand ToggleGlobalCommand { get; }
        public ICommand SaveSettingsCommand { get; }

        public Func<ShortcutItem?, bool>? ShowEditDialogAction { get; set; }

        public MainViewModel(
            StorageService storageService,
            KeyboardHookService hookService,
            TrayIconService trayService,
            AppSettings settings,
            List<ShortcutItem> shortcuts,
            List<ExclusionApp> exclusions)
        {
            _storageService = storageService;
            _hookService = hookService;
            _trayService = trayService;
            _settings = settings;

            Shortcuts = new ObservableCollection<ShortcutItem>(shortcuts);
            Exclusions = new ObservableCollection<ExclusionApp>(exclusions);

            _shortcutsView = CollectionViewSource.GetDefaultView(Shortcuts);
            _shortcutsView.Filter = FilterShortcutItem;

            _exclusionsView = CollectionViewSource.GetDefaultView(Exclusions);
            _exclusionsView.Filter = FilterExclusionItem;

            OpenBlogCommand = new RelayCommand(ExecuteOpenBlog);

            // 단축어 관련 커맨드
            AddShortcutCommand = new RelayCommand(ExecuteAddShortcut);
            EditShortcutCommand = new RelayCommand(ExecuteEditShortcut, () => SelectedShortcut != null);
            DeleteShortcutCommand = new RelayCommand(ExecuteDeleteShortcut, () => SelectedShortcut != null);
            ToggleShortcutCommand = new RelayCommand(param =>
            {
                if (param is ShortcutItem item)
                {
                    item.IsEnabled = !item.IsEnabled;
                    PersistShortcuts();
                    UpdateCounts();
                }
            });

            ExportShortcutsCommand = new RelayCommand(ExecuteExportShortcuts);
            ImportShortcutsCommand = new RelayCommand(ExecuteImportShortcuts);

            // 예외 프로그램 커맨드
            AddExclusionCommand = new RelayCommand(ExecuteAddExclusion);
            BrowseExclusionCommand = new RelayCommand(ExecuteBrowseExclusion);
            DeleteExclusionCommand = new RelayCommand(ExecuteDeleteExclusion, () => SelectedExclusion != null);

            // 전역 토글 & 설정
            ToggleGlobalCommand = new RelayCommand(() =>
            {
                Settings.IsGlobalEnabled = !Settings.IsGlobalEnabled;
                _trayService.UpdateMenuState();
                _storageService.SaveSettings(Settings);
                OnPropertyChanged(nameof(Settings));
            });

            SaveSettingsCommand = new RelayCommand(() =>
            {
                _storageService.SaveSettings(Settings);
                _storageService.SetStartup(Settings.StartWithWindows);
                _trayService.UpdateMenuState();
            });

            StartRecordingHotkeyCommand = new RelayCommand(() =>
            {
                IsRecordingHotkey = true;
                RecordingPrompt = "원하는 키 또는 조합키를 누르세요... (취소: ESC)";
            });

            CancelRecordingHotkeyCommand = new RelayCommand(() =>
            {
                IsRecordingHotkey = false;
            });

            ResetTriggerHotkeyCommand = new RelayCommand(() =>
            {
                SelectedTriggerHotkey = "Tab";
                IsRecordingHotkey = false;
            });
        }

        private bool FilterShortcutItem(object obj)
        {
            if (obj is not ShortcutItem item) return false;
            if (string.IsNullOrWhiteSpace(SearchText)) return true;

            string query = SearchText.Trim();
            return (item.Shortcut?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false) ||
                   (item.Replacement?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false) ||
                   (item.Group?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false);
        }

        private void ExecuteAddShortcut()
        {
            var newItem = new ShortcutItem();
            if (ShowEditDialogAction?.Invoke(newItem) == true)
            {
                Shortcuts.Add(newItem);
                PersistShortcuts();
                UpdateCounts();
            }
        }

        private void ExecuteEditShortcut()
        {
            if (SelectedShortcut == null) return;
            if (ShowEditDialogAction?.Invoke(SelectedShortcut) == true)
            {
                _shortcutsView.Refresh();
                PersistShortcuts();
                UpdateCounts();
            }
        }

        private void ExecuteDeleteShortcut()
        {
            if (SelectedShortcut == null) return;

            var result = MessageBox.Show(
                $"단축어 '{SelectedShortcut.Shortcut}'을(를) 정말 삭제하시겠습니까?",
                "단축어 삭제",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                Shortcuts.Remove(SelectedShortcut);
                PersistShortcuts();
                UpdateCounts();
            }
        }

        private void ExecuteExportShortcuts()
        {
            var sfd = new SaveFileDialog
            {
                Title = "단축어 리스트 내보내기",
                Filter = "JSON 파일 (*.json)|*.json",
                FileName = $"QuickReplace_Shortcuts_{DateTime.Now:yyyyMMdd}.json"
            };

            if (sfd.ShowDialog() == true)
            {
                try
                {
                    _storageService.ExportShortcuts(sfd.FileName, Shortcuts.ToList());
                    MessageBox.Show("단축어 목록을 성공적으로 내보냈습니다.", "내보내기 완료", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"내보내기 중 오류가 발생했습니다: {ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void ExecuteImportShortcuts()
        {
            var ofd = new OpenFileDialog
            {
                Title = "단축어 리스트 가져오기",
                Filter = "JSON 파일 (*.json)|*.json"
            };

            if (ofd.ShowDialog() == true)
            {
                try
                {
                    var imported = _storageService.ImportShortcuts(ofd.FileName);
                    if (imported != null && imported.Count > 0)
                    {
                        var result = MessageBox.Show(
                            $"가져온 단축어 {imported.Count}개를 추가하시겠습니까? (기존 단축어는 유지됩니다)",
                            "가져오기 확인",
                            MessageBoxButton.YesNo,
                            MessageBoxImage.Question);

                        if (result == MessageBoxResult.Yes)
                        {
                            foreach (var item in imported)
                            {
                                item.Id = Guid.NewGuid();
                                Shortcuts.Add(item);
                            }
                            PersistShortcuts();
                            UpdateCounts();
                            MessageBox.Show($"{imported.Count}개의 단축어를 성공적으로 가져왔습니다.", "가져오기 완료", MessageBoxButton.OK, MessageBoxImage.Information);
                        }
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"가져오기 중 오류가 발생했습니다: {ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void ExecuteBrowseExclusion()
        {
            var ofd = new OpenFileDialog
            {
                Title = "예외 프로그램(.exe) 선택",
                Filter = "실행 파일 (*.exe)|*.exe"
            };

            if (ofd.ShowDialog() == true)
            {
                string fileName = Path.GetFileNameWithoutExtension(ofd.FileName);
                if (!Exclusions.Any(e => string.Equals(e.ProcessName, fileName, StringComparison.OrdinalIgnoreCase)))
                {
                    var item = new ExclusionApp
                    {
                        ProcessName = fileName,
                        Description = Path.GetFileName(ofd.FileName),
                        IsEnabled = true
                    };
                    Exclusions.Add(item);
                    PersistExclusions();
                }
            }
        }

        public Func<string?>? ShowAddExclusionDialogAction { get; set; }

        private void ExecuteAddExclusion()
        {
            string? procName = ShowAddExclusionDialogAction?.Invoke();
            if (!string.IsNullOrWhiteSpace(procName))
            {
                string cleanName = procName.Trim().Replace(".exe", "");
                if (!Exclusions.Any(e => string.Equals(e.ProcessName, cleanName, StringComparison.OrdinalIgnoreCase)))
                {
                    var item = new ExclusionApp
                    {
                        ProcessName = cleanName,
                        Description = cleanName,
                        IsEnabled = true
                    };
                    Exclusions.Add(item);
                    PersistExclusions();
                }
            }
        }

        private void ExecuteDeleteExclusion()
        {
            if (SelectedExclusion == null) return;
            Exclusions.Remove(SelectedExclusion);
            PersistExclusions();
        }

        private bool FilterExclusionItem(object obj)
        {
            if (obj is not ExclusionApp item) return false;
            if (string.IsNullOrWhiteSpace(ExclusionSearchText)) return true;

            string query = ExclusionSearchText.Trim();
            return (item.ProcessName?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false) ||
                   (item.Description?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false);
        }

        private void ExecuteOpenBlog()
        {
            try
            {
                Process.Start(new ProcessStartInfo("https://blog.naver.com/factoryud")
                {
                    UseShellExecute = true
                });
            }
            catch { }
        }

        public void PersistShortcuts()
        {
            _storageService.SaveShortcuts(Shortcuts.ToList());
        }

        public void PersistExclusions()
        {
            _storageService.SaveExclusions(Exclusions.ToList());
            UpdateExclusionCounts();
        }

        private void UpdateCounts()
        {
            OnPropertyChanged(nameof(TotalShortcutCount));
            OnPropertyChanged(nameof(EnabledShortcutCount));
        }

        private void UpdateExclusionCounts()
        {
            OnPropertyChanged(nameof(TotalExclusionCount));
            OnPropertyChanged(nameof(EnabledExclusionCount));
        }
    }
}
