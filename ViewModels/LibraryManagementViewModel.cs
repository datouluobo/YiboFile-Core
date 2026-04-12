using System;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Input;
using YiboFile.Models;
using YiboFile.Models.UI;
using YiboFile.Services;
using YiboFile.Services.Data.Repositories;
using YiboFile;

namespace YiboFile.ViewModels
{

    public class LibraryManagementViewModel : BaseViewModel
    {
        private readonly ILibraryRepository _repository;
        private readonly LibraryService _libraryService;
        private readonly Services.UI.IDialogService _dialogService;

        private ObservableCollection<LibraryUiModel> _libraries;
        private string _newLibraryName;
        private string _errorMessage;
        private bool _isErrorVisible;

        public LibraryManagementViewModel(
            LibraryService libraryService,
            ILibraryRepository repository,
            Services.UI.IDialogService dialogService = null)
        {
            _libraryService = libraryService ?? throw new ArgumentNullException(nameof(libraryService));
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
            _dialogService = dialogService ?? App.ServiceProvider?.GetService<Services.UI.IDialogService>();

            CreateLibraryCommand = new RelayCommand(CreateNewLibrary);
            RenameLibraryCommand = new RelayCommand<LibraryUiModel>(RenameLibrary);
            DeleteLibraryCommand = new RelayCommand<LibraryUiModel>(DeleteLibrary);
            MoveLibraryUpCommand = new RelayCommand<LibraryUiModel>(MoveLibraryUp);
            MoveLibraryDownCommand = new RelayCommand<LibraryUiModel>(MoveLibraryDown);
            AddPathCommand = new RelayCommand<LibraryUiModel>(AddPath);
            EditPathCommand = new RelayCommand<LibraryPath>(EditPath);
            RemovePathCommand = new RelayCommand<LibraryPath>(RemovePath);

            RefreshLibraries();
        }

        public ObservableCollection<LibraryUiModel> Libraries
        {
            get => _libraries;
            set => SetProperty(ref _libraries, value);
        }

        public string NewLibraryName
        {
            get => _newLibraryName;
            set
            {
                if (SetProperty(ref _newLibraryName, value))
                {
                    OnPropertyChanged(nameof(CanCreateLibrary));
                }
            }
        }

        public bool CanCreateLibrary => !string.IsNullOrWhiteSpace(NewLibraryName);

        public string ErrorMessage
        {
            get => _errorMessage;
            set => SetProperty(ref _errorMessage, value);
        }

        public bool IsErrorVisible
        {
            get => _isErrorVisible;
            set => SetProperty(ref _isErrorVisible, value);
        }

        public ICommand CreateLibraryCommand { get; }
        public ICommand RenameLibraryCommand { get; }
        public ICommand DeleteLibraryCommand { get; }
        public ICommand MoveLibraryUpCommand { get; }
        public ICommand MoveLibraryDownCommand { get; }
        public ICommand AddPathCommand { get; }
        public ICommand EditPathCommand { get; }
        public ICommand RemovePathCommand { get; }

        private void RefreshLibraries()
        {
            try
            {
                var coreLibraries = _repository.GetAllLibraries();
                var uiModels = new List<LibraryUiModel>();

                foreach (var lib in coreLibraries)
                {
                    var paths = _repository.GetLibraryPaths(lib.Id) ?? new List<LibraryPath>();

                    foreach (var path in paths)
                    {
                        if (string.IsNullOrEmpty(path.DisplayName))
                        {
                            path.DisplayName = Path.GetFileName(path.Path);
                            if (string.IsNullOrEmpty(path.DisplayName)) path.DisplayName = path.Path;
                        }
                    }

                    uiModels.Add(new LibraryUiModel
                    {
                        Id = lib.Id,
                        Name = lib.Name,
                        DisplayOrder = lib.DisplayOrder,
                        Paths = paths
                    });
                }

                Libraries = new ObservableCollection<LibraryUiModel>(uiModels);
            }
            catch (Exception ex)
            {
                ShowError($"加载库列表失败: {ex.Message}");
            }
        }

        private void CreateNewLibrary()
        {
            var categoryName = NewLibraryName?.Trim();
            if (string.IsNullOrWhiteSpace(categoryName))
            {
                _dialogService.ShowWarning("请输入库名称");
                return;
            }

            try
            {
                var libraryId = _libraryService.AddLibrary(categoryName);
                if (libraryId > 0)
                {
                    NewLibraryName = "";
                    RefreshLibraries();
                }
                else if (libraryId < 0)
                {
                    // Service handles dialog usually but here we rely on return code
                    _dialogService.ShowWarning("库名称已存在");
                }
            }
            catch (Exception ex)
            {
                ShowError($"创建库失败: {ex.Message}");
            }
        }

        private void RenameLibrary(LibraryUiModel library)
        {
            if (library == null) return;
            var newName = _dialogService?.ShowInput("请输入新的库名称:", library.Name, "重命名库");
            if (!string.IsNullOrWhiteSpace(newName))
            {
                try
                {
                    _libraryService.UpdateLibraryName(library.Id, newName.Trim());
                    RefreshLibraries();
                }
                catch (Exception ex)
                {
                    ShowError($"重命名失败: {ex.Message}");
                }
            }
        }

        private void DeleteLibrary(LibraryUiModel library)
        {
            if (library == null) return;
            if (_dialogService.Confirm($"确定要删除库 \"{library.Name}\" 吗？\n\n删除后，该库的所有位置将被移除，但不会删除实际文件。", "确认删除"))
            {
                try
                {
                    _libraryService.DeleteLibrary(library.Id, library.Name);
                    RefreshLibraries();
                }
                catch (Exception ex)
                {
                    ShowError($"删除失败: {ex.Message}");
                }
            }
        }

        private void MoveLibraryUp(LibraryUiModel library)
        {
            if (library == null) return;
            try
            {
                _repository.MoveLibraryUp(library.Id);
                _libraryService.LoadLibraries(); // Notify main app
                RefreshLibraries();
            }
            catch (Exception ex)
            {
                ShowError($"移动失败: {ex.Message}");
            }
        }

        private void MoveLibraryDown(LibraryUiModel library)
        {
            if (library == null) return;
            try
            {
                _repository.MoveLibraryDown(library.Id);
                _libraryService.LoadLibraries(); // Notify main app
                RefreshLibraries();
            }
            catch (Exception ex)
            {
                ShowError($"移动失败: {ex.Message}");
            }
        }

        private void AddPath(LibraryUiModel library)
        {
            if (library == null) return;
            var path = _dialogService?.ShowFolderBrowser($"选择要添加到库 \"{library.Name}\" 的文件夹:");
            if (!string.IsNullOrEmpty(path))
            {
                try
                {
                    var existingPaths = _repository.GetLibraryPaths(library.Id);
                    if (existingPaths.Any(p => p.Path.Equals(path, StringComparison.OrdinalIgnoreCase)))
                    {
                        ShowError("该路径已存在于库中");
                        return;
                    }

                    _libraryService.AddLibraryPath(library.Id, path);
                    RefreshLibraries();
                }
                catch (Exception ex)
                {
                    ShowError($"添加位置失败: {ex.Message}");
                }
            }
        }

        private void EditPath(LibraryPath path)
        {
            if (path == null) return;
            var newName = _dialogService?.ShowInput("请输入显示名称:", path.DisplayName, "编辑显示名称");
            // InputDialog returns null if cancelled. Logic allows empty string (reset to default?)
            // Existing logic: if (dialog.ShowDialog() == true) ... if (string.IsNullOrEmpty(newName)) newName = null;
            // The ShowInputDialog implementation should return null on cancel.

            if (newName != null) // User committed
            {
                var finalName = string.IsNullOrWhiteSpace(newName) ? null : newName.Trim();
                try
                {
                    _repository.UpdateLibraryPathDisplayName(path.LibraryId, path.Path, finalName);
                    _libraryService.LoadLibraries(); // Reload globally
                    RefreshLibraries();
                }
                catch (Exception ex)
                {
                    ShowError($"更新显示名称失败: {ex.Message}");
                }
            }
        }

        private void RemovePath(LibraryPath path)
        {
            if (path == null) return;
            if (_dialogService.Confirm($"确定要从库中移除位置 \"{path.Path}\" 吗？", "确认移除"))
            {
                try
                {
                    _libraryService.RemoveLibraryPath(path.LibraryId, path.Path);
                    RefreshLibraries();
                }
                catch (Exception ex)
                {
                    ShowError($"移除位置失败: {ex.Message}");
                }
            }
        }

        private void ShowError(string message)
        {
            ErrorMessage = message;
            IsErrorVisible = true;

            // Auto hide logic in ViewModel? Or View?
            // Pure MVVM: View subscribes to ErrorMessage change or similar.
            // But implementing Timer in VM is acceptable for transient state.
            Task.Delay(3000).ContinueWith(_ =>
            {
                IsErrorVisible = false;
                ErrorMessage = null;
            }, TaskScheduler.FromCurrentSynchronizationContext());
        }
    }
}
