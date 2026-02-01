using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using YiboFile.Previews;

namespace YiboFile.ViewModels.Previews
{
    public class FolderItemViewModel : BaseViewModel
    {
        public string Name { get; set; }
        public string FullPath { get; set; }
        public string Type { get; set; }
        public string Size { get; set; }
        public string ModifiedDate { get; set; }
        public bool IsDirectory { get; set; }
        public string Icon => IsDirectory ? "📁" : "📄";
    }

    public class FolderPreviewViewModel : BasePreviewViewModel
    {
        private ObservableCollection<FolderItemViewModel> _items = new ObservableCollection<FolderItemViewModel>();
        public ObservableCollection<FolderItemViewModel> Items
        {
            get => _items;
            set => SetProperty(ref _items, value);
        }

        private FolderItemViewModel _selectedItem;
        public FolderItemViewModel SelectedItem
        {
            get => _selectedItem;
            set => SetProperty(ref _selectedItem, value);
        }

        public FolderPreviewViewModel()
        {
            OpenExternalCommand = new RelayCommand(() => PreviewHelper.OpenInDefaultApp(FilePath));
        }

        public async Task LoadAsync(string folderPath, System.Threading.CancellationToken token = default)
        {
            FilePath = folderPath;
            Title = Path.GetFileName(folderPath);
            Icon = "📁";
            IsLoading = true;

            try
            {
                await Task.Run(() =>
                {
                    if (!Directory.Exists(folderPath) || token.IsCancellationRequested) return;

                    var di = new DirectoryInfo(folderPath);
                    var items = di.GetFileSystemInfos()
                        .Take(100)
                        .Select(info => new FolderItemViewModel
                        {
                            Name = info.Name,
                            FullPath = info.FullName,
                            IsDirectory = (info.Attributes & FileAttributes.Directory) != 0,
                            Size = (info is FileInfo fi) ? PreviewHelper.FormatFileSize(fi.Length) : "",
                            ModifiedDate = info.LastWriteTime.ToString("yyyy-MM-dd HH:mm")
                        })
                        .ToList();

                    if (token.IsCancellationRequested) return;

                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        Items.Clear();
                        foreach (var item in items) Items.Add(item);
                    });
                }, token);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Folder preview error: {ex.Message}");
                Title = "Error loading folder";
            }
            finally
            {
                IsLoading = false;
            }
        }
    }
}
