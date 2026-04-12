using System.IO;
using System.Threading.Tasks;
using YiboFile.Previews;

namespace YiboFile.ViewModels.Previews
{
    public class PdfPreviewViewModel : BasePreviewViewModel
    {
        public PdfPreviewViewModel()
        {
            OpenExternalCommand = new RelayCommand(() => PreviewHelper.OpenInDefaultApp(FilePath));
        }

        public override async Task LoadAsync(string filePath, System.Threading.CancellationToken token = default)
        {
            FilePath = filePath;
            Title = Path.GetFileName(filePath);
            Icon = "📄";
            IsLoading = false; // Loading handled by WebView
            await Task.CompletedTask;
        }
    }
}

