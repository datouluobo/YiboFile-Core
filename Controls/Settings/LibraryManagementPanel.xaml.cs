using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using System.Windows.Input;
using YiboFile.Services.Core;
using YiboFile.Services.Navigation;
using YiboFile.Controls.Dialogs; // Use shared InputDialog

namespace YiboFile.Controls.Settings
{
    public partial class LibraryManagementPanel : System.Windows.Controls.UserControl
    {
        // Use a UI-specific model to support hierarchical binding with LibraryPath objects
        public class LibraryUiModel
        {
            public int Id { get; set; }
            public string Name { get; set; }
            public int DisplayOrder { get; set; }
            public List<LibraryPath> Paths { get; set; }
        }

        private List<LibraryUiModel> _libraries;
        private readonly YiboFile.Services.Data.Repositories.ILibraryRepository _repository;
        private readonly YiboFile.Services.LibraryService _libraryService;

        public LibraryManagementPanel()
        {
            InitializeComponent();
            _repository = App.ServiceProvider?.GetService(typeof(YiboFile.Services.Data.Repositories.ILibraryRepository)) as YiboFile.Services.Data.Repositories.ILibraryRepository;
            _libraryService = App.ServiceProvider?.GetService(typeof(YiboFile.Services.LibraryService)) as YiboFile.Services.LibraryService;
            RefreshLibraries();
        }

        private void RefreshLibraries()
        {
            try
            {
                if (_repository == null) return;
                var coreLibraries = _repository.GetAllLibraries();
                _libraries = new List<LibraryUiModel>();

                foreach (var lib in coreLibraries)
                {
                    // Fetch proper LibraryPath objects which include DisplayName
                    var paths = _repository.GetLibraryPaths(lib.Id) ?? new List<LibraryPath>();

                    // Ensure DisplayName is populated for UI
                    foreach (var path in paths)
                    {
                        if (string.IsNullOrEmpty(path.DisplayName))
                        {
                            path.DisplayName = Path.GetFileName(path.Path);
                            if (string.IsNullOrEmpty(path.DisplayName)) path.DisplayName = path.Path;
                        }
                    }

                    _libraries.Add(new LibraryUiModel
                    {
                        Id = lib.Id,
                        Name = lib.Name,
                        DisplayOrder = lib.DisplayOrder,
                        Paths = paths
                    });
                }

                LibrariesList.ItemsSource = _libraries;
            }
            catch (Exception ex)
            {
                ShowError($"加载库列表失败: {ex.Message}");
            }
        }

        private void ShowError(string message)
        {
            if (ErrorText == null || ErrorOverlay == null) return;

            ErrorText.Text = message;
            ErrorOverlay.Visibility = Visibility.Visible;

            // Auto hide after 3 seconds
            var timer = new System.Windows.Threading.DispatcherTimer();
            timer.Interval = TimeSpan.FromSeconds(3);
            timer.Tick += (s, e) =>
            {
                if (ErrorOverlay != null) ErrorOverlay.Visibility = Visibility.Collapsed;
                timer.Stop();
            };
            timer.Start();
        }

        // --- Library Actions ---

        private void NewLibrary_Click(object sender, RoutedEventArgs e)
        {
            CreateNewLibrary();
        }

        private void NewLibraryNameTextBox_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                CreateNewLibrary();
            }
        }

        private void CreateNewLibrary()
        {
            var categoryName = NewLibraryNameTextBox.Text?.Trim();
            if (string.IsNullOrWhiteSpace(categoryName))
            {
                DialogService.Warning("请输入库名称", "提示", Window.GetWindow(this));
                NewLibraryNameTextBox.Focus();
                return;
            }

            try
            {
                if (_libraryService == null)
                {
                    ShowError("Library service not available");
                    return;
                }

                var libraryId = _libraryService.AddLibrary(categoryName);
                if (libraryId > 0)
                {
                    NewLibraryNameTextBox.Text = "";
                    RefreshLibraries(); // Refresh to show new library
                }
                else if (libraryId < 0)
                {
                    // _libraryService already shows dialog, but we keep this for focus
                    // ShowError("库名称已存在");
                    NewLibraryNameTextBox.SelectAll();
                    NewLibraryNameTextBox.Focus();
                }
                else
                {
                    // _libraryService already shows dialog
                    // ShowError("创建库失败");
                }
            }
            catch (Exception ex)
            {
                ShowError($"创建库失败: {ex.Message}");
            }
        }

        private void RenameLibrary_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as FrameworkElement)?.DataContext is LibraryUiModel library)
            {
                // Use shared InputDialog
                var dialog = new InputDialog("重命名库", "请输入新的库名称:", library.Name);
                dialog.Owner = Window.GetWindow(this);

                if (dialog.ShowDialog() == true && !string.IsNullOrWhiteSpace(dialog.InputText))
                {
                    var newName = dialog.InputText.Trim();
                    try
                    {
                        _libraryService?.UpdateLibraryName(library.Id, newName);
                        RefreshLibraries();
                    }
                    catch (Exception ex)
                    {
                        ShowError($"重命名失败: {ex.Message}");
                    }
                }
            }
        }

        private void DeleteLibrary_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as FrameworkElement)?.DataContext is LibraryUiModel library)
            {
                if (System.Windows.MessageBox.Show(Window.GetWindow(this),
                    $"确定要删除库 \"{library.Name}\" 吗？\n\n删除后，该库的所有位置将被移除，但不会删除实际文件。",
                    "确认删除",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning) == MessageBoxResult.Yes)
                {
                    try
                    {
                        _libraryService?.DeleteLibrary(library.Id, library.Name);
                        RefreshLibraries();
                    }
                    catch (Exception ex)
                    {
                        ShowError($"删除失败: {ex.Message}");
                    }
                }
            }
        }

        private void MoveLibraryUp_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as FrameworkElement)?.DataContext is LibraryUiModel library)
            {
                try
                {
                    _repository.MoveLibraryUp(library.Id);
                    _libraryService?.LoadLibraries();
                    RefreshLibraries();
                }
                catch (Exception ex)
                {
                    ShowError($"移动失败: {ex.Message}");
                }
            }
        }

        private void MoveLibraryDown_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as FrameworkElement)?.DataContext is LibraryUiModel library)
            {
                try
                {
                    _repository.MoveLibraryDown(library.Id);
                    _libraryService?.LoadLibraries();
                    RefreshLibraries();
                }
                catch (Exception ex)
                {
                    ShowError($"移动失败: {ex.Message}");
                }
            }
        }

        // --- Path Actions ---

        private void AddPath_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as FrameworkElement)?.DataContext is LibraryUiModel library)
            {
                using (var dialog = new FolderBrowserDialog())
                {
                    dialog.Description = $"选择要添加到库 \"{library.Name}\" 的文件夹:";
                    dialog.ShowNewFolderButton = false;
                    dialog.SelectedPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);

                    if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                    {
                        var path = dialog.SelectedPath;
                        try
                        {
                            // Check duplicates
                            var existingPaths = _repository.GetLibraryPaths(library.Id);
                            if (existingPaths.Any(p => p.Path.Equals(path, StringComparison.OrdinalIgnoreCase)))
                            {
                                ShowError("该路径已存在于库中");
                                return;
                            }

                            _libraryService?.AddLibraryPath(library.Id, path);
                            RefreshLibraries();
                        }
                        catch (Exception ex)
                        {
                            ShowError($"添加位置失败: {ex.Message}");
                        }
                    }
                }
            }
        }

        private void EditPath_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as FrameworkElement)?.DataContext is LibraryPath path)
            {
                var dialog = new InputDialog("编辑显示名称", "请输入显示名称:", path.DisplayName);
                dialog.Owner = Window.GetWindow(this);
                if (dialog.ShowDialog() == true)
                {
                    var newName = dialog.InputText.Trim();
                    if (string.IsNullOrEmpty(newName)) newName = null;

                    try
                    {
                        _repository.UpdateLibraryPathDisplayName(path.LibraryId, path.Path, newName);
                        _libraryService?.LoadLibraries();
                        RefreshLibraries();
                    }
                    catch (Exception ex)
                    {
                        ShowError($"更新显示名称失败: {ex.Message}");
                    }
                }
            }
        }

        private void RemovePath_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as FrameworkElement)?.DataContext is LibraryPath path)
            {
                if (System.Windows.MessageBox.Show(Window.GetWindow(this),
                    $"确定要从库中移除位置 \"{path.Path}\" 吗？",
                    "确认移除",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question) == MessageBoxResult.Yes)
                {
                    try
                    {
                        _libraryService?.RemoveLibraryPath(path.LibraryId, path.Path);
                        RefreshLibraries();
                    }
                    catch (Exception ex)
                    {
                        ShowError($"移除位置失败: {ex.Message}");
                    }
                }
            }
        }

        // --- Import / Export ---
        private void ImportBtn_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "JSON 文件 (*.json)|*.json|所有文件 (*.*)|*.*",
                Title = "导入库配置"
            };

            if (dialog.ShowDialog() == true)
            {
                try
                {
                    string json = System.IO.File.ReadAllText(dialog.FileName);
                    var libraryService = new YiboFile.Services.LibraryService(Dispatcher, null, _repository);
                    libraryService.ImportLibrariesFromJson(json);
                    RefreshLibraries(); // Refresh after import
                }
                catch (Exception ex)
                {
                    System.Windows.MessageBox.Show(Window.GetWindow(this), $"读取文件失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void ExportBtn_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "JSON 文件 (*.json)|*.json|所有文件 (*.*)|*.*",
                FileName = $"Libraries_Backup_{DateTime.Now:yyyyMMdd}.json",
                Title = "导出库配置"
            };

            if (dialog.ShowDialog() == true)
            {
                try
                {
                    var libraryService = new YiboFile.Services.LibraryService(Dispatcher, null, _repository);
                    string json = libraryService.ExportLibrariesToJson();
                    if (!string.IsNullOrEmpty(json))
                    {
                        System.IO.File.WriteAllText(dialog.FileName, json);
                        System.Windows.MessageBox.Show(Window.GetWindow(this), "库配置已导出", "成功", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                }
                catch (Exception ex)
                {
                    System.Windows.MessageBox.Show(Window.GetWindow(this), $"保存文件失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
    }
}
