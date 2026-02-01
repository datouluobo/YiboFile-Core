using System.Windows.Input;
using YiboFile.ViewModels;

namespace YiboFile.ViewModels.Previews
{
    public interface IPreviewViewModel
    {
        string Title { get; }
        string Icon { get; }
        string FilePath { get; }
        bool IsLoading { get; }
        ICommand OpenExternalCommand { get; }
    }

    public abstract class BasePreviewViewModel : BaseViewModel, IPreviewViewModel
    {
        private string _title;
        public string Title
        {
            get => _title;
            set => SetProperty(ref _title, value);
        }

        private string _icon;
        public string Icon
        {
            get => _icon;
            set => SetProperty(ref _icon, value);
        }

        private string _filePath;
        public string FilePath
        {
            get => _filePath;
            set => SetProperty(ref _filePath, value);
        }

        private bool _isLoading;
        public bool IsLoading
        {
            get => _isLoading;
            set => SetProperty(ref _isLoading, value);
        }

        public ICommand OpenExternalCommand { get; protected set; }
    }
}
