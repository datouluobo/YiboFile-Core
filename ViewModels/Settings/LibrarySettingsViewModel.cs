using System;
using System.Collections.ObjectModel;
using System.Windows.Input;
using Microsoft.Extensions.DependencyInjection;
using YiboFile.Services;
using WinForms = System.Windows.Forms;

namespace YiboFile.ViewModels.Settings
{
    public class LibrarySettingsViewModel : BaseViewModel
    {
        private ObservableCollection<LibraryItemViewModel> _libraries;
        public ObservableCollection<LibraryItemViewModel> Libraries
        {
            get => _libraries;
            set => SetProperty(ref _libraries, value);
        }

        private LibraryService _libraryService;
        private readonly Services.UI.IDialogService _dialogService;

        public ICommand ImportLibrariesCommand { get; }
        public ICommand ExportLibrariesCommand { get; }
        public ICommand OpenLibraryManagerCommand { get; }
        public ICommand AddLibraryCommand { get; }
        public ICommand RemoveLibraryCommand { get; }

        public event EventHandler OpenLibraryManagerRequested;

        public LibrarySettingsViewModel(Services.UI.IDialogService dialogService = null)
        {
            _dialogService = dialogService ?? App.ServiceProvider?.GetService<Services.UI.IDialogService>();
            ImportLibrariesCommand = new RelayCommand<string>(ImportLibraries);
            ExportLibrariesCommand = new RelayCommand<string>(ExportLibraries);
            OpenLibraryManagerCommand = new RelayCommand(OpenLibraryManager);
            AddLibraryCommand = new RelayCommand(AddLibrary);
            RemoveLibraryCommand = new RelayCommand<LibraryItemViewModel>(RemoveLibrary);

            LoadFromConfig();
        }

        public void LoadFromConfig()
        {
            InitializeLibraryManagement();
        }

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
            string path = _dialogService?.ShowFolderBrowser("选择文件夹作为新的库");
            
            if (!string.IsNullOrEmpty(path))
            {
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
                    _dialogService?.ShowError("添加库失败: " + ex.Message);
                }
            }
        }

        private void RemoveLibrary(LibraryItemViewModel item)
        {
            if (item == null) return;
            if (_dialogService?.Confirm($"确定要移除库 \"{item.Name}\" 吗？", "确认") == true)
            {
                try
                {
                    _libraryService?.DeleteLibrary(item.Id, item.Name);
                    RefreshLibraries();
                }
                catch (Exception ex)
                {
                    _dialogService?.ShowError("移除库失败: " + ex.Message);
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
                RefreshLibraries();
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
    }
}
