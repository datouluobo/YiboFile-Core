using System;
using System.Windows.Input;

namespace YiboFile.ViewModels
{
    /// <summary>
    /// 右键菜单项的 ViewModel
    /// </summary>
    public class ContextMenuItemViewModel : BaseViewModel
    {
        private string _header;
        public string Header
        {
            get => _header;
            set => SetProperty(ref _header, value);
        }

        private ICommand _command;
        public ICommand Command
        {
            get => _command;
            set => SetProperty(ref _command, value);
        }

        private object _commandParameter;
        public object CommandParameter
        {
            get => _commandParameter;
            set => SetProperty(ref _commandParameter, value);
        }

        private bool _isChecked;
        public bool IsChecked
        {
            get => _isChecked;
            set => SetProperty(ref _isChecked, value);
        }

        private bool _isCheckable;
        public bool IsCheckable
        {
            get => _isCheckable;
            set => SetProperty(ref _isCheckable, value);
        }

        private bool _isSeparator;
        public bool IsSeparator
        {
            get => _isSeparator;
            set => SetProperty(ref _isSeparator, value);
        }

        private object _icon;
        public object Icon
        {
            get => _icon;
            set => SetProperty(ref _icon, value);
        }

        private string _iconBrush;
        public string IconBrush
        {
            get => _iconBrush;
            set => SetProperty(ref _iconBrush, value);
        }
    }
}
