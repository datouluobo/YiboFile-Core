using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using YiboFile.Previews;

using System.Threading;

namespace YiboFile.ViewModels.Previews
{
    public class TextPreviewViewModel : BasePreviewViewModel
    {
        private CancellationTokenSource _cts;
        private string _content;
        public string Content
        {
            get => _content;
            set => SetProperty(ref _content, value);
        }

        private bool _isWordWrap = true;
        public bool IsWordWrap
        {
            get => _isWordWrap;
            set => SetProperty(ref _isWordWrap, value);
        }

        private bool _isEditMode;
        public bool IsEditMode
        {
            get => _isEditMode;
            set => SetProperty(ref _isEditMode, value);
        }

        private Encoding _encoding = Encoding.UTF8;
        public Encoding Encoding
        {
            get => _encoding;
            set
            {
                if (SetProperty(ref _encoding, value))
                {
                    _cts?.Cancel();
                    _cts?.Dispose();
                    _cts = new CancellationTokenSource();
                    _ = LoadAsync(FilePath, _cts.Token);
                }
            }
        }

        public ICommand SaveCommand { get; }
        public ICommand ToggleWrapCommand { get; }

        public TextPreviewViewModel()
        {
            ToggleWrapCommand = new RelayCommand(() => IsWordWrap = !IsWordWrap);
            OpenExternalCommand = new RelayCommand(() => PreviewHelper.OpenInDefaultApp(FilePath));
            SaveCommand = new RelayCommand(async () => await SaveAsync());
        }

        public async Task LoadAsync(string filePath, System.Threading.CancellationToken token = default)
        {
            FilePath = filePath;
            Title = Path.GetFileName(filePath);
            Icon = "📄";
            IsLoading = true;

            try
            {
                await Task.Run(() =>
                {
                    if (token.IsCancellationRequested) return;
                    if (Encoding == null)
                    {
                        // Auto detect encoding logic here or use a helper
                        Encoding = Encoding.Default;
                    }

                    using (var reader = new StreamReader(filePath, Encoding))
                    {
                        // Read first 100KB for preview
                        char[] buffer = new char[1024 * 100];
                        int read = reader.ReadBlock(buffer, 0, buffer.Length);
                        var contentStr = new string(buffer, 0, read);
                        
                        if (!token.IsCancellationRequested)
                        {
                            System.Windows.Application.Current.Dispatcher.Invoke(() => Content = contentStr);
                        }
                    }
                }, token);
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {

                Content = $"Error loading file: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task SaveAsync()
        {
            if (string.IsNullOrEmpty(FilePath)) return;
            try
            {
                await File.WriteAllTextAsync(FilePath, Content, Encoding);
                IsEditMode = false;
            }
            catch (Exception)
            {

            }
        }
    }
}

