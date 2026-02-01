using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using YiboFile.Previews;

namespace YiboFile.ViewModels.Previews
{
    public class HtmlPreviewViewModel : BasePreviewViewModel
    {
        private string _sourceContent;
        public string SourceContent
        {
            get => _sourceContent;
            set => SetProperty(ref _sourceContent, value);
        }

        private bool _isEditMode;
        public bool IsEditMode
        {
            get => _isEditMode;
            set
            {
                if (SetProperty(ref _isEditMode, value))
                {
                    if (value) IsSourceView = true;
                }
            }
        }

        private bool _isSourceView;
        public bool IsSourceView
        {
            get => _isSourceView;
            set => SetProperty(ref _isSourceView, value);
        }

        private bool _isWordWrap = true;
        public bool IsWordWrap
        {
            get => _isWordWrap;
            set => SetProperty(ref _isWordWrap, value);
        }

        // Commands
        public RelayCommand ToggleViewCommand { get; }
        public RelayCommand ToggleEditCommand { get; }
        public RelayCommand SaveCommand { get; }
        public RelayCommand ToggleWordWrapCommand { get; }
        public RelayCommand ReloadCommand { get; }

        // Event to notify View to reload WebView
        public event EventHandler ReloadRequested;

        public HtmlPreviewViewModel()
        {
            ToggleViewCommand = new RelayCommand(() => IsSourceView = !IsSourceView);
            ToggleEditCommand = new RelayCommand(ToggleEdit);
            SaveCommand = new RelayCommand(Save);
            ToggleWordWrapCommand = new RelayCommand(() => IsWordWrap = !IsWordWrap);
            ReloadCommand = new RelayCommand(() => ReloadRequested?.Invoke(this, EventArgs.Empty));
            OpenExternalCommand = new RelayCommand(() => PreviewHelper.OpenInDefaultApp(FilePath));
        }

        public async Task LoadAsync(string filePath)
        {
            FilePath = filePath;
            Title = Path.GetFileName(filePath);
            Icon = "🌐";
            IsLoading = true;

            try
            {
                SourceContent = await Task.Run(() => ReadHtmlContent(filePath));
            }
            catch (Exception ex)
            {
                SourceContent = $"读取失败: {ex.Message}";
            }

            IsLoading = false;
        }

        private string ReadHtmlContent(string path)
        {
            var encodings = new List<Encoding> { Encoding.UTF8, Encoding.Default };
            try { Encoding.RegisterProvider(CodePagesEncodingProvider.Instance); } catch { }
            try { encodings.Add(Encoding.GetEncoding("GB2312")); } catch { }
            try { encodings.Add(Encoding.GetEncoding("GBK")); } catch { }

            foreach (var encoding in encodings)
            {
                try
                {
                    // Try to read a bit to validate? Or just read all. File is usually small enough for preview.
                    return File.ReadAllText(path, encoding);
                }
                catch { }
            }
            return File.ReadAllText(path); // Fallback
        }

        private void ToggleEdit()
        {
            IsEditMode = !IsEditMode;
        }

        private void Save()
        {
            try
            {
                // Simple save with UTF8. In a real editor we might want to preserve encoding.
                File.WriteAllText(FilePath, SourceContent);
                IsEditMode = false;
                ReloadRequested?.Invoke(this, EventArgs.Empty);
                Services.Core.NotificationService.ShowSuccess("文件已保存");
            }
            catch (Exception ex)
            {
                Services.Core.NotificationService.ShowError($"保存失败: {ex.Message}");
            }
        }
    }
}
