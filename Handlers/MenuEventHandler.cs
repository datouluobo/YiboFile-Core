using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using OoiMRR.Controls;
using OoiMRR.Services;
using OoiMRR.Services.FileOperations;

namespace OoiMRR.Handlers
{
    /// <summary>
    /// 菜单和按钮事件处理器
    /// 处理所有菜单项、按钮点击事件，包括文件操作、库管理、标签管理等
    /// </summary>
    public class MenuEventHandler
    {
        private readonly FileBrowserControl _fileBrowser;
        private readonly LibraryService _libraryService;
        private readonly Action _refreshFileList;
        private readonly Action _loadCurrentDirectory;
        private readonly Action _clearFilter;
        private readonly Action _closeWindow;
        private readonly Action _showSettings;
        private readonly Action _showAbout;
        private readonly Action _editNotes;
        private readonly Action _batchAddTags;
        private readonly Action _showTagStatistics;
        private readonly Action _importLibrary;
        private readonly Action _exportLibrary;
        private readonly Action _addFileToLibrary;
        private readonly Action _copyClick;
        private readonly Action _cutClick;
        private readonly Action _pasteClick;
        private readonly Action _deleteClick;
        private readonly Action _renameClick;
        private readonly Action _showPropertiesClick;
        private readonly Action<string> _navigateToPath;
        private readonly Action<string> _switchNavigationMode;
        private readonly Func<string> _getCurrentPath;
        private readonly Func<Library> _getCurrentLibrary;
        private readonly Func<List<FileSystemItem>> _getCurrentFiles;
        private readonly Action<List<FileSystemItem>> _setCurrentFiles;
        private readonly Func<Window> _getOwnerWindow;
        private readonly Action<Library> _openLibraryInTab;
        private readonly Action<Library> _highlightMatchingLibrary;
        private readonly Action _loadLibraries;
        private readonly Func<ListBox> _getLibrariesListBox;
        private readonly Func<ContextMenu> _getLibraryContextMenu;
        private readonly Action<string> _createNewFileWithExtension;
        private readonly Func<string, Task<bool>> _createFolder;
        private readonly Func<AppConfig> _getConfig;
        private readonly Action<AppConfig> _applyConfig;
        private readonly Action _saveCurrentConfig;

        public MenuEventHandler(
            FileBrowserControl fileBrowser,
            LibraryService libraryService,
            Action refreshFileList,
            Action loadCurrentDirectory,
            Action clearFilter,
            Action closeWindow,
            Action showSettings,
            Action showAbout,
            Action editNotes,
            Action batchAddTags,
            Action showTagStatistics,
            Action importLibrary,
            Action exportLibrary,
            Action addFileToLibrary,
            Action copyClick,
            Action cutClick,
            Action pasteClick,
            Action deleteClick,
            Action renameClick,
            Action showPropertiesClick,
            Action<string> navigateToPath,
            Action<string> switchNavigationMode,
            Func<string> getCurrentPath,
            Func<Library> getCurrentLibrary,
            Func<List<FileSystemItem>> getCurrentFiles,
            Action<List<FileSystemItem>> setCurrentFiles,
            Func<Window> getOwnerWindow,
            Action<Library> openLibraryInTab,
            Action<Library> highlightMatchingLibrary,
            Action loadLibraries,
            Func<ListBox> getLibrariesListBox,
            Func<ContextMenu> getLibraryContextMenu,
            Action<string> createNewFileWithExtension,
            Func<string, Task<bool>> createFolder, // Changed from Action<string> createNewFolder
            Func<AppConfig> getConfig,
            Action<AppConfig> applyConfig,
            Action saveCurrentConfig)
        {
            _fileBrowser = fileBrowser ?? throw new ArgumentNullException(nameof(fileBrowser));
            _libraryService = libraryService ?? throw new ArgumentNullException(nameof(libraryService));
            _refreshFileList = refreshFileList ?? throw new ArgumentNullException(nameof(refreshFileList));
            _loadCurrentDirectory = loadCurrentDirectory ?? throw new ArgumentNullException(nameof(loadCurrentDirectory));
            _clearFilter = clearFilter ?? throw new ArgumentNullException(nameof(clearFilter));
            _closeWindow = closeWindow ?? throw new ArgumentNullException(nameof(closeWindow));
            _showSettings = showSettings ?? throw new ArgumentNullException(nameof(showSettings));
            _showAbout = showAbout ?? throw new ArgumentNullException(nameof(showAbout));
            _editNotes = editNotes ?? throw new ArgumentNullException(nameof(editNotes));
            _batchAddTags = batchAddTags ?? throw new ArgumentNullException(nameof(batchAddTags));
            _showTagStatistics = showTagStatistics ?? throw new ArgumentNullException(nameof(showTagStatistics));
            _importLibrary = importLibrary ?? throw new ArgumentNullException(nameof(importLibrary));
            _exportLibrary = exportLibrary ?? throw new ArgumentNullException(nameof(exportLibrary));
            _addFileToLibrary = addFileToLibrary ?? throw new ArgumentNullException(nameof(addFileToLibrary));
            _copyClick = copyClick ?? throw new ArgumentNullException(nameof(copyClick));
            _cutClick = cutClick ?? throw new ArgumentNullException(nameof(cutClick));
            _pasteClick = pasteClick ?? throw new ArgumentNullException(nameof(pasteClick));
            _deleteClick = deleteClick ?? throw new ArgumentNullException(nameof(deleteClick));
            _renameClick = renameClick ?? throw new ArgumentNullException(nameof(renameClick));
            _showPropertiesClick = showPropertiesClick ?? throw new ArgumentNullException(nameof(showPropertiesClick));
            _navigateToPath = navigateToPath ?? throw new ArgumentNullException(nameof(navigateToPath));
            _switchNavigationMode = switchNavigationMode ?? throw new ArgumentNullException(nameof(switchNavigationMode));
            _getCurrentPath = getCurrentPath ?? throw new ArgumentNullException(nameof(getCurrentPath));
            _getCurrentLibrary = getCurrentLibrary ?? throw new ArgumentNullException(nameof(getCurrentLibrary));
            _getCurrentFiles = getCurrentFiles ?? throw new ArgumentNullException(nameof(getCurrentFiles));
            _setCurrentFiles = setCurrentFiles ?? throw new ArgumentNullException(nameof(setCurrentFiles));
            _getOwnerWindow = getOwnerWindow ?? throw new ArgumentNullException(nameof(getOwnerWindow));
            _openLibraryInTab = openLibraryInTab ?? throw new ArgumentNullException(nameof(openLibraryInTab));
            _highlightMatchingLibrary = highlightMatchingLibrary ?? throw new ArgumentNullException(nameof(highlightMatchingLibrary));
            _loadLibraries = loadLibraries ?? throw new ArgumentNullException(nameof(loadLibraries));
            _getLibrariesListBox = getLibrariesListBox ?? throw new ArgumentNullException(nameof(getLibrariesListBox));
            _getLibraryContextMenu = getLibraryContextMenu ?? throw new ArgumentNullException(nameof(getLibraryContextMenu));
            _createNewFileWithExtension = createNewFileWithExtension ?? throw new ArgumentNullException(nameof(createNewFileWithExtension));
            _createFolder = createFolder ?? throw new ArgumentNullException(nameof(createFolder));
            _getConfig = getConfig ?? throw new ArgumentNullException(nameof(getConfig));
            _applyConfig = applyConfig ?? throw new ArgumentNullException(nameof(applyConfig));
            _saveCurrentConfig = saveCurrentConfig ?? throw new ArgumentNullException(nameof(saveCurrentConfig));
        }

        public void Refresh_Click(object sender, RoutedEventArgs e)
        {
            _clearFilter();
            _refreshFileList();
        }

        public void ClearFilter_Click(object sender, RoutedEventArgs e)
        {
            _clearFilter();
            _loadCurrentDirectory();
        }

        public void Exit_Click(object sender, RoutedEventArgs e)
        {
            _closeWindow();
        }

        public void SelectAll_Click(object sender, RoutedEventArgs e)
        {
            _fileBrowser?.FilesList?.SelectAll();
        }

        public void ViewLargeIcons_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("大图标视图功能待实现", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        public void ViewSmallIcons_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("小图标视图功能待实现", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        public void ViewList_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("列表视图功能待实现", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        public void ViewDetails_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("详细信息视图功能待实现", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        public void Settings_Click(object sender, RoutedEventArgs e)
        {
            _showSettings();
        }

        public void ImportConfig_Click(object sender, RoutedEventArgs e)
        {
            var ofd = new OpenFileDialog
            {
                Filter = "配置文件 (*.json)|*.json|所有文件 (*.*)|*.*"
            };
            if (ofd.ShowDialog() == true)
            {
                try
                {
                    ConfigManager.Import(ofd.FileName);
                    var config = ConfigManager.Load();
                    _applyConfig(config);
                    MessageBox.Show("配置已导入并应用。", "成功", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"导入失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        public void ExportConfig_Click(object sender, RoutedEventArgs e)
        {
            var sfd = new SaveFileDialog
            {
                FileName = "config.json",
                Filter = "配置文件 (*.json)|*.json|所有文件 (*.*)|*.*"
            };
            if (sfd.ShowDialog() == true)
            {
                try
                {
                    _saveCurrentConfig();
                    ConfigManager.Export(sfd.FileName);
                    MessageBox.Show("配置已导出。", "成功", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"导出失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        public void EditNotes_Click(object sender, RoutedEventArgs e)
        {
            _editNotes();
        }

        public void About_Click(object sender, RoutedEventArgs e)
        {
            _showAbout();
        }

        public void BatchAddTags_Click(object sender, RoutedEventArgs e)
        {
            _batchAddTags();
        }

        public void TagStatistics_Click(object sender, RoutedEventArgs e)
        {
            _showTagStatistics();
        }

        public void ImportLibrary_Click(object sender, RoutedEventArgs e)
        {
            _importLibrary();
        }

        public void ExportLibrary_Click(object sender, RoutedEventArgs e)
        {
            _exportLibrary();
        }

        public void AddFileToLibrary_Click(object sender, RoutedEventArgs e)
        {
            _addFileToLibrary();
        }

        public void Copy_Click(object sender, RoutedEventArgs e)
        {
            _copyClick();
        }

        public void Cut_Click(object sender, RoutedEventArgs e)
        {
            _cutClick();
        }

        public void Paste_Click(object sender, RoutedEventArgs e)
        {
            _pasteClick();
        }

        public void Delete_Click(object sender, RoutedEventArgs e)
        {
            _deleteClick();
        }

        public void Rename_Click(object sender, RoutedEventArgs e)
        {
            _renameClick();
        }

        public void ShowProperties_Click(object sender, RoutedEventArgs e)
        {
            _showPropertiesClick();
        }

        public async void NewFolder_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string targetPath = null;
                var currentLibrary = _getCurrentLibrary();

                // 判断当前模式：库模式还是路径模式
                if (currentLibrary != null)
                {
                    // 库模式：使用库的第一个位置
                    if (currentLibrary.Paths == null || currentLibrary.Paths.Count == 0)
                    {
                        MessageBox.Show("当前库没有添加任何位置，请先在管理库中添加位置", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                        return;
                    }

                    // 如果有多个位置，让用户选择
                    if (currentLibrary.Paths.Count > 1)
                    {
                        var paths = string.Join("\n", currentLibrary.Paths.Select((p, i) => $"{i + 1}. {p}"));
                        var result = MessageBox.Show(
                            $"当前库有多个位置，将在第一个位置创建文件夹：\n\n{currentLibrary.Paths[0]}\n\n是否继续？\n\n所有位置：\n{paths}",
                            "选择位置",
                            MessageBoxButton.YesNo,
                            MessageBoxImage.Question);

                        if (result != MessageBoxResult.Yes)
                        {
                            return;
                        }
                    }

                    targetPath = currentLibrary.Paths[0];
                    if (!Directory.Exists(targetPath))
                    {
                        MessageBox.Show($"库位置不存在: {targetPath}", "错误", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }
                }
                else
                {
                    var currentPath = _getCurrentPath();
                    if (!string.IsNullOrEmpty(currentPath) && Directory.Exists(currentPath))
                    {
                        // 路径模式：使用当前路径
                        targetPath = currentPath;
                    }
                    else
                    {
                        MessageBox.Show("当前没有可用的路径", "错误", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }
                }

                // 使用简单的输入对话框
                var dialog = new PathInputDialog
                {
                    Title = "新建文件夹",
                    PromptText = "请输入文件夹名称：",
                    InputText = "新建文件夹",
                    Owner = _getOwnerWindow()
                };

                if (dialog.ShowDialog() == true)
                {
                    var folderName = dialog.InputText.Trim();

                    // 验证文件夹名称
                    if (string.IsNullOrEmpty(folderName))
                    {
                        MessageBox.Show("文件夹名称不能为空", "错误", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }

                    // 检查非法字符
                    char[] invalidChars = Path.GetInvalidFileNameChars();
                    if (folderName.IndexOfAny(invalidChars) >= 0)
                    {
                        MessageBox.Show("文件夹名称包含非法字符", "错误", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }

                    var folderPath = Path.Combine(targetPath, folderName);

                    // 如果已存在，自动添加序号
                    if (Directory.Exists(folderPath))
                    {
                        int counter = 2;
                        string newFolderName;
                        do
                        {
                            newFolderName = $"{folderName} ({counter})";
                            folderPath = Path.Combine(targetPath, newFolderName);
                            counter++;
                        }
                        while (Directory.Exists(folderPath));
                    }

                    // 创建文件夹 (通过服务)
                    if (_createFolder != null)
                    {
                        await _createFolder(folderPath);
                    }
                    else
                    {
                        Directory.CreateDirectory(folderPath); // Fallback
                    }

                    // 刷新显示
                    _refreshFileList();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"创建文件夹失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public void NewFile_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // 显示文件类型选择菜单
                var contextMenu = new ContextMenu
                {
                    Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom,
                    PlacementTarget = sender as UIElement
                };

                // 常用文件类型列表
                var fileTypes = new[]
                {
                    ("📄 文本文件", ".txt"),
                    ("📝 Word 文档", ".docx"),
                    ("📊 Excel 表格", ".xlsx"),
                    ("📽️ PowerPoint", ".pptx"),
                    ("🖼️ PNG 图片", ".png"),
                    ("🖼️ JPEG 图片", ".jpg"),
                    ("🖼️ GIF 图片", ".gif"),
                    ("🖼️ BMP 图片", ".bmp"),
                    ("🖼️ SVG 矢量图", ".svg"),
                    ("💻 C# 代码", ".cs"),
                    ("🌐 HTML 网页", ".html"),
                    ("🎨 CSS 样式", ".css"),
                    ("⚡ JavaScript", ".js"),
                    ("🐍 Python", ".py"),
                    ("☕ Java", ".java"),
                    ("📋 JSON", ".json"),
                    ("📋 XML", ".xml"),
                    ("📋 Markdown", ".md"),
                    ("⚙️ 配置文件", ".ini"),
                    ("📦 批处理", ".bat"),
                    ("🔧 PowerShell", ".ps1")
                };

                foreach (var (name, extension) in fileTypes)
                {
                    var menuItem = new MenuItem
                    {
                        Header = name,
                        Tag = extension,
                        Padding = new Thickness(10, 5, 10, 5)
                    };
                    menuItem.Click += (s, args) =>
                    {
                        _createNewFileWithExtension(extension);
                    };
                    contextMenu.Items.Add(menuItem);
                }

                // 添加分隔符和自定义选项
                contextMenu.Items.Add(new Separator());

                var customMenuItem = new MenuItem
                {
                    Header = "✏️ 自定义扩展名...",
                    Padding = new Thickness(10, 5, 10, 5)
                };
                customMenuItem.Click += (s, args) =>
                {
                    var dialog = new PathInputDialog
                    {
                        Title = "新建文件",
                        PromptText = "请输入文件扩展名（如 .txt）：",
                        InputText = ".txt",
                        Owner = _getOwnerWindow()
                    };

                    if (dialog.ShowDialog() == true)
                    {
                        var extension = dialog.InputText.Trim();
                        if (!extension.StartsWith("."))
                        {
                            extension = "." + extension;
                        }
                        _createNewFileWithExtension(extension);
                    }
                };
                contextMenu.Items.Add(customMenuItem);

                // 显示菜单
                contextMenu.IsOpen = true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"创建文件失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public void AddLibrary_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new LibraryDialog();
            var owner = _getOwnerWindow();
            dialog.Owner = owner;
            if (dialog.ShowDialog() == true)
            {
                var libraryId = _libraryService.AddLibrary(dialog.LibraryName, dialog.LibraryPath);
                if (libraryId > 0)
                {
                    // 创建后自动打开库标签页并高亮
                    var newLibrary = _libraryService.GetLibrary(libraryId);
                    if (newLibrary != null)
                    {
                        _openLibraryInTab(newLibrary);
                        _highlightMatchingLibrary(newLibrary);
                    }
                }
            }
        }

        public void ManageLibraries_Click(object sender, RoutedEventArgs e)
        {
            var manageWindow = new LibraryManagementWindow();
            var owner = _getOwnerWindow();
            manageWindow.Owner = owner;
            manageWindow.ShowDialog();
            _loadLibraries();
        }

        public void LibraryRename_Click(object sender, RoutedEventArgs e)
        {
            var librariesListBox = _getLibrariesListBox();
            if (librariesListBox?.SelectedItem is Library selectedLibrary)
            {
                var dialog = new PathInputDialog("请输入新的库名称:");
                dialog.InputText = selectedLibrary.Name;
                var owner = _getOwnerWindow();
                dialog.Owner = owner;
                if (dialog.ShowDialog() == true)
                {
                    var newName = dialog.InputText.Trim();
                    if (_libraryService.UpdateLibraryName(selectedLibrary.Id, newName))
                    {
                        // 如果当前库被重命名，更新当前库引用并恢复选中状态
                        var currentLibrary = _getCurrentLibrary();
                        if (currentLibrary != null && currentLibrary.Id == selectedLibrary.Id)
                        {
                            var updatedLibrary = _libraryService.GetLibrary(selectedLibrary.Id);
                            if (updatedLibrary != null)
                            {
                                // 确保重命名后的库仍然被选中并正确显示
                                Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                                {
                                    // EnsureSelectedItemVisible(librariesListBox, updatedLibrary);
                                    // LoadLibraryFiles(updatedLibrary);
                                }), System.Windows.Threading.DispatcherPriority.Loaded);
                            }
                        }
                    }
                }
            }
        }

        public void LibraryDelete_Click(object sender, RoutedEventArgs e)
        {
            var librariesListBox = _getLibrariesListBox();
            if (librariesListBox?.SelectedItem is Library selectedLibrary)
            {
                var owner = _getOwnerWindow();
                if (!ConfirmDialog.Show(
                    $"确定要删除库 \"{selectedLibrary.Name}\" 吗？\n这将删除库及其所有位置，但不会删除实际文件。",
                    "确认删除",
                    ConfirmDialog.DialogType.Question,
                    owner))
                {
                    return;
                }

                if (_libraryService.DeleteLibrary(selectedLibrary.Id, selectedLibrary.Name))
                {
                    // 如果删除的是当前库，清空显示
                    var currentLibrary = _getCurrentLibrary();
                    if (currentLibrary != null && currentLibrary.Id == selectedLibrary.Id)
                    {
                        var currentFiles = _getCurrentFiles();
                        currentFiles.Clear();
                        _setCurrentFiles(currentFiles);
                        if (_fileBrowser != null)
                        {
                            _fileBrowser.FilesItemsSource = null;
                            _fileBrowser.AddressText = "";
                        }
                    }
                }
            }
        }

        public void LibraryManage_Click(object sender, RoutedEventArgs e)
        {
            ManageLibraries_Click(sender, e);
        }

        public void LibraryRefresh_Click(object sender, RoutedEventArgs e)
        {
            var currentLibrary = _getCurrentLibrary();
            if (currentLibrary != null)
            {
                // 刷新当前库的文件列表
                _refreshFileList?.Invoke();
            }
        }

        public void LibraryOpenInExplorer_Click(object sender, RoutedEventArgs e)
        {
            var librariesListBox = _getLibrariesListBox();
            if (librariesListBox?.SelectedItem is Library selectedLibrary)
            {
                _libraryService.OpenLibraryInExplorer(selectedLibrary.Id);
            }
        }


        public void WindowMinimize_Click(object sender, RoutedEventArgs e)
        {
            var owner = _getOwnerWindow();
            if (owner != null)
            {
                owner.WindowState = WindowState.Minimized;
            }
        }

        public void WindowMaximize_Click(object sender, RoutedEventArgs e)
        {
            var owner = _getOwnerWindow();
            if (owner != null)
            {
                owner.WindowState = owner.WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
            }
        }

        public void WindowClose_Click(object sender, RoutedEventArgs e)
        {
            _closeWindow();
        }
    }
}

