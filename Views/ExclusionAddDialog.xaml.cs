using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using QuickReplace.Services;

namespace QuickReplace.Views
{
    public partial class ExclusionAddDialog : Window
    {
        public class ProcessItem
        {
            public string ProcessName { get; set; } = string.Empty;
            public string Display { get; set; } = string.Empty;
        }

        public string ResultProcessName { get; private set; } = string.Empty;

        public ExclusionAddDialog()
        {
            InitializeComponent();
            LoadRunningApps();
        }

        private void LoadRunningApps()
        {
            var apps = ForegroundAppWatcher.GetRunningGuiApplications();
            var list = apps.Select(a => new ProcessItem
            {
                ProcessName = a.ProcessName,
                Display = $"{a.MainWindowTitle} ({a.ProcessName}.exe)"
            }).ToList();

            RunningAppsCombo.ItemsSource = list;
        }

        private void RunningAppsCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (RunningAppsCombo.SelectedItem is ProcessItem item)
            {
                ProcessNameBox.Text = item.ProcessName;
            }
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(ProcessNameBox.Text))
            {
                MessageBox.Show("프로세스 이름을 입력하거나 선택해주세요.", "알림", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            ResultProcessName = ProcessNameBox.Text.Trim();
            DialogResult = true;
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
