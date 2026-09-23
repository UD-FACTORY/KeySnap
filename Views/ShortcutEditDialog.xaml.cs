using System.Windows;
using QuickReplace.ViewModels;

namespace QuickReplace.Views
{
    public partial class ShortcutEditDialog : Window
    {
        private readonly ShortcutEditViewModel _viewModel;

        public ShortcutEditDialog(ShortcutEditViewModel viewModel)
        {
            InitializeComponent();
            _viewModel = viewModel;
            DataContext = _viewModel;

            _viewModel.RequestClose += success =>
            {
                DialogResult = success;
                Close();
            };
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            _viewModel.Save();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            _viewModel.Cancel();
        }
    }
}
