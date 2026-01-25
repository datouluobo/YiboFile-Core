using System;
using System.Collections.ObjectModel;
using Microsoft.Extensions.DependencyInjection;
using YiboFile.Services;

namespace YiboFile.ViewModels
{
    public partial class SettingsViewModel
    {
        #region Library Management
        private ObservableCollection<LibraryItemViewModel> _libraries;
        public ObservableCollection<LibraryItemViewModel> Libraries
        {
            get => _libraries;
            set => SetProperty(ref _libraries, value);
        }

        private LibraryService _libraryService;

        private void InitializeLibraryManagement()
        {
            _libraryService = App.ServiceProvider?.GetService<LibraryService>();
            RefreshLibraries();
        }

        public void RefreshLibraries()
        {
            if (_libraryService == null)
            {
                _libraryService = App.ServiceProvider?.GetService<LibraryService>();
            }

            if (_libraryService == null) return;

            var libs = _libraryService.LoadLibraries();
            var vmList = new ObservableCollection<LibraryItemViewModel>();
            foreach (var lib in libs)
            {
                vmList.Add(new LibraryItemViewModel(lib));
            }
            Libraries = vmList;
        }

        private void AddLibrary()
        {
            var dialog = new System.Windows.Forms.FolderBrowserDialog();
            dialog.Description = "选择文件夹作为新的库";
            dialog.UseDescriptionForTitle = true;
            dialog.ShowNewFolderButton = true;

            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                string path = dialog.SelectedPath;
                string name = new System.IO.DirectoryInfo(path).Name;

                try
                {
                    if (_libraryService != null)
                    {
                        _libraryService.AddLibrary(name, path);
                        RefreshLibraries();
                    }
                }
                catch (Exception ex)
                {
                    YiboFile.DialogService.Error("添加库失败: " + ex.Message);
                }
            }
        }

        private void RemoveLibrary(LibraryItemViewModel item)
        {
            if (item == null) return;
            if (YiboFile.DialogService.Ask($"确定要移除库 \"{item.Name}\" 吗？", "确认"))
            {
                try
                {
                    _libraryService?.DeleteLibrary(item.Id, item.Name);
                    RefreshLibraries();
                }
                catch (Exception ex)
                {
                    YiboFile.DialogService.Error("移除库失败: " + ex.Message);
                }
            }
        }

        private void OpenLibraryManager()
        {
            OpenLibraryManagerRequested?.Invoke(this, EventArgs.Empty);
        }

        private void ImportLibraries(string file)
        {
            if (string.IsNullOrEmpty(file)) return;
            try
            {
                string json = System.IO.File.ReadAllText(file);
                var libraryService = new YiboFile.Services.LibraryService(System.Windows.Application.Current.Dispatcher, null);
                libraryService.ImportLibrariesFromJson(json);
            }
            catch (Exception ex)
            {
                throw new Exception($"导入库配置失败: {ex.Message}");
            }
        }

        private void ExportLibraries(string file)
        {
            if (string.IsNullOrEmpty(file)) return;
            try
            {
                var libraryService = new YiboFile.Services.LibraryService(System.Windows.Application.Current.Dispatcher, null);
                string json = libraryService.ExportLibrariesToJson();
                if (!string.IsNullOrEmpty(json))
                {
                    System.IO.File.WriteAllText(file, json);
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"导出库配置失败: {ex.Message}");
            }
        }
        #endregion
    }
}
