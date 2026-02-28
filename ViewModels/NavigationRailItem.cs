using System.Windows.Input;

namespace YiboFile.ViewModels
{
    public class NavigationRailItem : BaseViewModel
    {
        private string _id;
        private string _iconKey;
        private string _toolTip;
        private ICommand _command;
        private bool _isActive;
        private bool _isVisible = true;

        public string Id
        {
            get => _id;
            set => SetProperty(ref _id, value);
        }

        public string IconKey
        {
            get => _iconKey;
            set => SetProperty(ref _iconKey, value);
        }

        public string ToolTip
        {
            get => _toolTip;
            set => SetProperty(ref _toolTip, value);
        }

        public ICommand Command
        {
            get => _command;
            set => SetProperty(ref _command, value);
        }

        public bool IsActive
        {
            get => _isActive;
            set => SetProperty(ref _isActive, value);
        }

        public bool IsVisible
        {
            get => _isVisible;
            set => SetProperty(ref _isVisible, value);
        }
    }
}
