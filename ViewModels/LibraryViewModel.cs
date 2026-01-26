using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using YiboFile.Controls;

namespace YiboFile.ViewModels
{
    /// <summary>
    /// 库管理 ViewModel
    /// 负责管理库的加载、添加、删除、重命名等功能
    /// </summary>
    public class LibraryViewModel : BaseViewModel
    {
        private readonly ListBox _librariesListBox;
        private readonly Window _ownerWindow;
        private readonly Action<Library> _onLibrarySelected;
        private readonly Action _onLibraryListChanged;

        private ObservableCollection<Library> _libraries = new ObservableCollection<Library>();
        private Library _currentLibrary;
        private Library _selectedLibrary;

        public ObservableCollection<Library> Libraries
        {
            get => _libraries;
            set => SetProperty(ref _libraries, value);
        }

        public Library CurrentLibrary
        {
            get => _currentLibrary;
            set => SetProperty(ref _currentLibrary, value);
        }

        public Library SelectedLibrary
        {
            get => _selectedLibrary;
            set => SetProperty(ref _selectedLibrary, value);
        }

        public LibraryViewModel(ListBox librariesListBox, Window ownerWindow, Action<Library> onLibrarySelected, Action onLibraryListChanged)
        {
            _librariesListBox = librariesListBox ?? throw new ArgumentNullException(nameof(librariesListBox));
            _ownerWindow = ownerWindow ?? throw new ArgumentNullException(nameof(ownerWindow));
            _onLibrarySelected = onLibrarySelected;
            _onLibraryListChanged = onLibraryListChanged;
        }

        /// <summary>
        /// 加载库列表
        /// </summary>
        public void LoadLibraries()
        {
            var libraries = DatabaseManager.GetAllLibraries();
            var currentSelected = _librariesListBox.SelectedItem;
            
            Libraries.Clear();
            foreach (var lib in libraries)
            {
                Libraries.Add(lib);
            }

            _librariesListBox.ItemsSource = Libraries;
            _librariesListBox.Items.Refresh();

            // 恢复选中状态
            if (currentSelected != null)
            {
                _ownerWindow.Dispatcher.BeginInvoke(new Action(() =>
                {
                    _librariesListBox.SelectedItem = currentSelected;
                }), System.Windows.Threading.DispatcherPriority.Loaded);
            }

            _onLibraryListChanged?.Invoke();
        }

        /// <summary>
        /// 添加库
        /// </summary>
        public bool AddLibrary(string name, string initialPath = null)
        {
            try
            {
                var libraryId = DatabaseManager.AddLibrary(name);
                if (libraryId > 0)
                {
                    if (!string.IsNullOrWhiteSpace(initialPath))
                    {
                        DatabaseManager.AddLibraryPath(libraryId, initialPath);
                    }
                    LoadLibraries();
                    return true;
                }
                else if (libraryId < 0)
                {
                    LoadLibraries();
                    MessageBox.Show(_ownerWindow, "库名称已存在，已刷新库列表", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                    return false;
                }
                return false;
            }
            catch (Exception ex)
            {
                MessageBox.Show(_ownerWindow, $"创建库失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }

        /// <summary>
        /// 重命名库
        /// </summary>
        public bool RenameLibrary(Library library, string newName)
        {
            if (library == null || string.IsNullOrEmpty(newName))
                return false;

            try
            {
                DatabaseManager.UpdateLibraryName(library.Id, newName);
                LoadLibraries();

                // 如果当前库被重命名，更新当前库引用
                if (CurrentLibrary != null && CurrentLibrary.Id == library.Id)
                {
                    var updatedLibrary = DatabaseManager.GetLibrary(library.Id);
                    if (updatedLibrary != null)
                    {
                        CurrentLibrary = updatedLibrary;
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show(_ownerWindow, $"重命名失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }

        /// <summary>
        /// 删除库
        /// </summary>
        public bool DeleteLibrary(Library library)
        {
            if (library == null)
                return false;

            try
            {
                DatabaseManager.DeleteLibrary(library.Id);
                LoadLibraries();

                // 如果删除的是当前库，清空当前库引用
                if (CurrentLibrary != null && CurrentLibrary.Id == library.Id)
                {
                    CurrentLibrary = null;
                }

                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show(_ownerWindow, $"删除库失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }

        /// <summary>
        /// 选择库
        /// </summary>
        public void SelectLibrary(Library library)
        {
            SelectedLibrary = library;
            CurrentLibrary = library;
            _onLibrarySelected?.Invoke(library);
        }

        /// <summary>
        /// 获取库
        /// </summary>
        public Library GetLibrary(int libraryId)
        {
            return DatabaseManager.GetLibrary(libraryId);
        }
    }
}





























