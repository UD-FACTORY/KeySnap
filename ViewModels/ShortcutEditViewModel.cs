using System;
using System.Windows.Input;
using QuickReplace.Helpers;
using QuickReplace.Models;

namespace QuickReplace.ViewModels
{
    public class ShortcutEditViewModel : ObservableObject
    {
        private string _shortcut = string.Empty;
        private string _replacement = string.Empty;
        private bool _isEnabled = true;
        private TriggerMode _trigger = TriggerMode.Instant;
        private string _group = "기본";
        private string _errorMessage = string.Empty;

        public ShortcutItem OriginalItem { get; }
        public bool IsNew { get; }

        public string Shortcut
        {
            get => _shortcut;
            set { SetField(ref _shortcut, value); Validate(); }
        }

        public string Replacement
        {
            get => _replacement;
            set { SetField(ref _replacement, value); Validate(); }
        }

        public bool IsEnabled
        {
            get => _isEnabled;
            set => SetField(ref _isEnabled, value);
        }

        public TriggerMode Trigger
        {
            get => _trigger;
            set => SetField(ref _trigger, value);
        }

        public bool IsInstantTrigger
        {
            get => _trigger == TriggerMode.Instant;
            set
            {
                if (value) Trigger = TriggerMode.Instant;
            }
        }

        public bool IsKeyTrigger
        {
            get => _trigger == TriggerMode.TriggerKey;
            set
            {
                if (value) Trigger = TriggerMode.TriggerKey;
            }
        }

        public string Group
        {
            get => _group;
            set => SetField(ref _group, value);
        }

        public string ErrorMessage
        {
            get => _errorMessage;
            set => SetField(ref _errorMessage, value);
        }

        public bool HasError => !string.IsNullOrEmpty(_errorMessage);

        public ICommand InsertMacroCommand { get; }

        public event Action<bool>? RequestClose;

        public ShortcutEditViewModel(ShortcutItem? item = null)
        {
            if (item != null)
            {
                OriginalItem = item;
                IsNew = false;
                _shortcut = item.Shortcut;
                _replacement = item.Replacement;
                _isEnabled = item.IsEnabled;
                _trigger = item.Trigger;
                _group = string.IsNullOrEmpty(item.Group) ? "기본" : item.Group;
            }
            else
            {
                OriginalItem = new ShortcutItem();
                IsNew = true;
            }

            InsertMacroCommand = new RelayCommand(param =>
            {
                if (param is string macroTag)
                {
                    Replacement = (Replacement ?? "") + macroTag;
                }
            });
        }

        private void Validate()
        {
            if (string.IsNullOrWhiteSpace(Shortcut))
            {
                ErrorMessage = "단축어를 입력해주세요.";
            }
            else if (string.IsNullOrEmpty(Replacement))
            {
                ErrorMessage = "대치어를 입력해주세요.";
            }
            else
            {
                ErrorMessage = string.Empty;
            }
            OnPropertyChanged(nameof(HasError));
        }

        public bool Save()
        {
            Validate();
            if (HasError) return false;

            OriginalItem.Shortcut = Shortcut.Trim();
            OriginalItem.Replacement = Replacement;
            OriginalItem.IsEnabled = IsEnabled;
            OriginalItem.Trigger = Trigger;
            OriginalItem.Group = string.IsNullOrWhiteSpace(Group) ? "기본" : Group.Trim();

            RequestClose?.Invoke(true);
            return true;
        }

        public void Cancel()
        {
            RequestClose?.Invoke(false);
        }
    }
}
