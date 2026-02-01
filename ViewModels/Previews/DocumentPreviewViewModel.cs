using System;
using System.IO;
using System.Threading.Tasks;
using YiboFile.Previews;

namespace YiboFile.ViewModels.Previews
{
    public class DocumentPreviewViewModel : BasePreviewViewModel
    {
        private string _fileInfo;
        public string FileInfo
        {
            get => _fileInfo;
            set => SetProperty(ref _fileInfo, value);
        }

        public DocumentPreviewViewModel()
        {
            OpenExternalCommand = new RelayCommand(() => PreviewHelper.OpenInDefaultApp(FilePath));
        }

        public async Task LoadAsync(string filePath)
        {
            FilePath = filePath;
            Title = Path.GetFileName(filePath);
            Icon = "📄";
            IsLoading = true;

            try
            {
                await Task.Run(() =>
                {
                    var fi = new FileInfo(filePath);
                    FileInfo = $"文件名: {fi.Name}\n类型: {fi.Extension}\n大小: {PreviewHelper.FormatFileSize(fi.Length)}\n修改日期: {fi.LastWriteTime}";
                });
            }
            catch (Exception ex)
            {
                FileInfo = $"错误: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
            }
        }
    }
}
