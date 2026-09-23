using System;
using System.Drawing;
using System.Windows.Forms;
using QuickReplace.Models;

namespace QuickReplace.Services
{
    public class TrayIconService : IDisposable
    {
        private readonly NotifyIcon _notifyIcon;
        private readonly AppSettings _settings;
        private readonly Action _openSettingsAction;
        private readonly Action _exitAction;
        private readonly Action<bool> _toggleGlobalAction;

        private readonly ToolStripMenuItem _toggleMenuItem;

        public TrayIconService(
            AppSettings settings,
            Action openSettingsAction,
            Action<bool> toggleGlobalAction,
            Action exitAction)
        {
            _settings = settings;
            _openSettingsAction = openSettingsAction;
            _toggleGlobalAction = toggleGlobalAction;
            _exitAction = exitAction;

            Icon appIcon = LoadAppIcon();
            _notifyIcon = new NotifyIcon
            {
                Icon = appIcon,
                Text = "KeySnap (텍스트 대치)",
                Visible = true
            };

            var contextMenu = new ContextMenuStrip();

            var openItem = new ToolStripMenuItem("설정 대시보드 열기", null, (s, e) => _openSettingsAction());
            openItem.Font = new Font(openItem.Font, FontStyle.Bold);

            _toggleMenuItem = new ToolStripMenuItem("텍스트 대치 사용", null, (s, e) =>
            {
                bool newState = !_settings.IsGlobalEnabled;
                _toggleGlobalAction(newState);
                UpdateMenuState();
            })
            {
                Checked = _settings.IsGlobalEnabled
            };

            var exitItem = new ToolStripMenuItem("종료", null, (s, e) => _exitAction());

            contextMenu.Items.Add(openItem);
            contextMenu.Items.Add(_toggleMenuItem);
            contextMenu.Items.Add(new ToolStripSeparator());
            contextMenu.Items.Add(exitItem);

            _notifyIcon.ContextMenuStrip = contextMenu;
            _notifyIcon.DoubleClick += (s, e) => _openSettingsAction();
        }

        public void UpdateMenuState()
        {
            _toggleMenuItem.Checked = _settings.IsGlobalEnabled;
            _notifyIcon.Text = _settings.IsGlobalEnabled
                ? "KeySnap - 활성 (동작 중)"
                : "KeySnap - 비활성 (일시 중지됨)";
        }

        public void ShowNotification(string title, string message)
        {
            if (_settings.ShowNotifications)
            {
                _notifyIcon.ShowBalloonTip(1500, title, message, ToolTipIcon.Info);
            }
        }

        private static Icon LoadAppIcon()
        {
            try
            {
                string path = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "app_icon.ico");
                if (System.IO.File.Exists(path))
                {
                    return new Icon(path);
                }
                var uri = new Uri("pack://application:,,,/app_icon.ico");
                var streamInfo = System.Windows.Application.GetResourceStream(uri);
                if (streamInfo != null)
                {
                    return new Icon(streamInfo.Stream);
                }
            }
            catch { }
            return SystemIcons.Application;
        }

        public void Dispose()
        {
            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}
