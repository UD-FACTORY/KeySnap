using System;
using System.ComponentModel;
using System.Windows;
using QuickReplace.Models;
using QuickReplace.ViewModels;

namespace QuickReplace.Views
{
    public partial class MainWindow : Window
    {
        private readonly MainViewModel _viewModel;
        private bool _isExplicitExit = false;

        public MainWindow(MainViewModel viewModel)
        {
            InitializeComponent();
            _viewModel = viewModel;
            DataContext = _viewModel;

            _viewModel.ShowEditDialogAction = ShowShortcutEditDialog;
            _viewModel.ShowAddExclusionDialogAction = ShowAddExclusionDialog;

            PreviewKeyDown += OnWindowPreviewKeyDown;
        }

        private void OnWindowPreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (!_viewModel.IsRecordingHotkey) return;

            e.Handled = true;

            System.Windows.Input.Key key = (e.Key == System.Windows.Input.Key.System) ? e.SystemKey : e.Key;

            // ESC를 누르면 녹화 취소
            if (key == System.Windows.Input.Key.Escape)
            {
                _viewModel.IsRecordingHotkey = false;
                return;
            }

            // 수정자 키 단독 누름은 무시하고 계속 대기
            if (key == System.Windows.Input.Key.LeftCtrl || key == System.Windows.Input.Key.RightCtrl ||
                key == System.Windows.Input.Key.LeftAlt || key == System.Windows.Input.Key.RightAlt ||
                key == System.Windows.Input.Key.LeftShift || key == System.Windows.Input.Key.RightShift ||
                key == System.Windows.Input.Key.LWin || key == System.Windows.Input.Key.RWin)
            {
                return;
            }

            // 수정자 키 조합 상태 확인
            bool ctrl = (System.Windows.Input.Keyboard.Modifiers & System.Windows.Input.ModifierKeys.Control) != 0;
            bool alt = (System.Windows.Input.Keyboard.Modifiers & System.Windows.Input.ModifierKeys.Alt) != 0;
            bool shift = (System.Windows.Input.Keyboard.Modifiers & System.Windows.Input.ModifierKeys.Shift) != 0;
            bool win = (System.Windows.Input.Keyboard.Modifiers & System.Windows.Input.ModifierKeys.Windows) != 0;

            int vk = System.Windows.Input.KeyInterop.VirtualKeyFromKey(key);
            if (vk > 0)
            {
                _viewModel.ApplyCustomHotkey(ctrl, alt, shift, win, (ushort)vk);
            }
        }

        public void ExplicitExit()
        {
            _isExplicitExit = true;
            Close();
        }

        public void BringToFront()
        {
            if (WindowState == WindowState.Minimized)
            {
                WindowState = WindowState.Normal;
            }
            Show();
            Activate();
        }

        private bool ShowShortcutEditDialog(ShortcutItem? item)
        {
            var editVm = new ShortcutEditViewModel(item);
            var dlg = new ShortcutEditDialog(editVm)
            {
                Owner = this
            };

            return dlg.ShowDialog() == true;
        }

        private string? ShowAddExclusionDialog()
        {
            var dlg = new ExclusionAddDialog
            {
                Owner = this
            };

            if (dlg.ShowDialog() == true)
            {
                return dlg.ResultProcessName;
            }
            return null;
        }

        private void ShortcutCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            _viewModel.PersistShortcuts();
        }

        private void ExclusionCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            _viewModel.PersistExclusions();
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            if (!_isExplicitExit && _viewModel.Settings.MinimizeToTrayOnClose)
            {
                e.Cancel = true;
                Hide();
            }
            else
            {
                base.OnClosing(e);
            }
        }
    }
}
