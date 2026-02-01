using YiboFile.ViewModels;

namespace YiboFile.ViewModels.Previews
{
    public class ErrorPreviewViewModel : BasePreviewViewModel
    {
        private string _errorMessage;
        public string ErrorMessage
        {
            get => _errorMessage;
            set => SetProperty(ref _errorMessage, value);
        }

        private string _subMessage;
        public string SubMessage
        {
            get => _subMessage;
            set => SetProperty(ref _subMessage, value);
        }
    }
}
