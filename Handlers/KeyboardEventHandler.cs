using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using YiboFile.Controls;
using YiboFile.Services.Tabs;
using YiboFile.Services.Config;

namespace YiboFile.Handlers
{
    /// <summary>
    /// 键盘事件处理器
    /// 处理所有键盘快捷键，包括窗口级和文件列表的键盘事件
    /// </summary>
    public class KeyboardEventHandler
    {
        private readonly FileBrowserControl _fileBrowser;
        private readonly Func<FileBrowserControl> _getActiveBrowser;
        private readonly Func<TabService> _getTabService;
        private readonly Action<PathTab> _closeTab;
        private readonly Action<string> _createTab;
        private readonly Action<PathTab> _switchToTab;
        private readonly Action _newFolderClick;
        private readonly Action _refreshClick;
        private readonly Action _copyClick;
        private readonly Action _pasteClick;
        private readonly Action _cutClick;
        private readonly Action _deleteClick;
        private readonly Action _permanentDeleteClick; // Shift+Delete
        private readonly Action _renameClick;
        private readonly Action<string> _navigateToPath;
        private readonly Action<string> _switchNavigationMode;
        private readonly Func<bool> _isLibraryMode;
        private readonly Action _navigateBack;
        private readonly Action<int> _switchLayoutMode;
        private readonly Func<bool> _isDualListMode;
        private readonly Action _switchDualPaneFocus;
        private readonly Action _undoClick;
        private readonly Action _redoClick;

        public KeyboardEventHandler(
            FileBrowserControl fileBrowser, // Keep for backward compatibility or primary ref
             Func<FileBrowserControl> getActiveBrowser, // New delegate
            Func<TabService> getTabService,
            Action<PathTab> closeTab,
            Action<string> createTab,
            Action<PathTab> switchToTab,
            Action newFolderClick,
            Action refreshClick,
            Action copyClick,
            Action pasteClick,
            Action cutClick,
            Action deleteClick,
            Action permanentDeleteClick, // Shift+Delete
            Action renameClick,
            Action<string> navigateToPath,
            Action<string> switchNavigationMode,
            Func<bool> isLibraryMode,
            Action navigateBack,
            Action undoClick,
            Action redoClick,
            Action<int> switchLayoutMode = null,
            Func<bool> isDualListMode = null,
            Action switchDualPaneFocus = null)
        {
            _fileBrowser = fileBrowser ?? throw new ArgumentNullException(nameof(fileBrowser));
            _getActiveBrowser = getActiveBrowser ?? (() => fileBrowser); // Default to main if null
            _getTabService = getTabService ?? throw new ArgumentNullException(nameof(getTabService));
            _closeTab = closeTab ?? throw new ArgumentNullException(nameof(closeTab));
            _createTab = createTab ?? throw new ArgumentNullException(nameof(createTab));
            _switchToTab = switchToTab ?? throw new ArgumentNullException(nameof(switchToTab));
            _newFolderClick = newFolderClick ?? throw new ArgumentNullException(nameof(newFolderClick));
            _refreshClick = refreshClick ?? throw new ArgumentNullException(nameof(refreshClick));
            _copyClick = copyClick ?? throw new ArgumentNullException(nameof(copyClick));
            _pasteClick = pasteClick ?? throw new ArgumentNullException(nameof(pasteClick));
            _cutClick = cutClick ?? throw new ArgumentNullException(nameof(cutClick));
            _deleteClick = deleteClick ?? throw new ArgumentNullException(nameof(deleteClick));
            _permanentDeleteClick = permanentDeleteClick; // 可为null，回退到普通删除
            _renameClick = renameClick ?? throw new ArgumentNullException(nameof(renameClick));
            _navigateToPath = navigateToPath ?? throw new ArgumentNullException(nameof(navigateToPath));
            _switchNavigationMode = switchNavigationMode ?? throw new ArgumentNullException(nameof(switchNavigationMode));
            _isLibraryMode = isLibraryMode ?? throw new ArgumentNullException(nameof(isLibraryMode));
            _navigateBack = navigateBack ?? throw new ArgumentNullException(nameof(navigateBack));
            _undoClick = undoClick;
            _redoClick = redoClick;
            _switchLayoutMode = switchLayoutMode; // 可选参数
            _isDualListMode = isDualListMode;
            _switchDualPaneFocus = switchDualPaneFocus;
        }

        public void MainWindow_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            // Ctrl+W 或 Ctrl+F4: 关闭当前标签页
            if ((e.Key == Key.W || e.Key == Key.F4) && Keyboard.Modifiers == ModifierKeys.Control)
            {
                var tabService = _getTabService();
                if (tabService != null)
                {
                    var activeTab = tabService.ActiveTab;
                    if (activeTab != null && tabService.TabCount > 1)
                    {
                        _closeTab(activeTab);
                        e.Handled = true;
                        return;
                    }
                }
            }

            // Ctrl+T: 新建标签页（打开桌面）
            if (e.Key == Key.T && Keyboard.Modifiers == ModifierKeys.Control)
            {
                var desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                _createTab(desktopPath);
                e.Handled = true;
                return;
            }

            // Ctrl+Tab: 切换到下一个标签页
            if (e.Key == Key.Tab && Keyboard.Modifiers == ModifierKeys.Control && !Keyboard.IsKeyDown(Key.LeftShift) && !Keyboard.IsKeyDown(Key.RightShift))
            {
                var tabService = _getTabService();
                if (tabService != null)
                {
                    var tabs = tabService.Tabs.ToList();
                    if (tabs.Count > 1)
                    {
                        var activeTab = tabService.ActiveTab;
                        var currentIndex = tabs.IndexOf(activeTab);
                        var nextIndex = (currentIndex + 1) % tabs.Count;
                        _switchToTab(tabs[nextIndex]);
                        e.Handled = true;
                        return;
                    }
                }
            }

            // Ctrl+Shift+Tab: 切换到上一个标签页
            if (e.Key == Key.Tab && Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Shift))
            {
                var tabService = _getTabService();
                if (tabService != null)
                {
                    var tabs = tabService.Tabs.ToList();
                    if (tabs.Count > 1)
                    {
                        var activeTab = tabService.ActiveTab;
                        var currentIndex = tabs.IndexOf(activeTab);
                        var prevIndex = (currentIndex - 1 + tabs.Count) % tabs.Count;
                        _switchToTab(tabs[prevIndex]);
                        e.Handled = true;
                        return;
                    }
                }
            }

            // Ctrl+N: 新建文件夹（恢复标准行为）
            if (e.Key == Key.N && Keyboard.Modifiers == ModifierKeys.Control)
            {
                _newFolderClick();
                e.Handled = true;
                return;
            }

            // Tab键（无修饰符）：在双列表模式下切换主副面板焦点
            if (e.Key == Key.Tab && Keyboard.Modifiers == ModifierKeys.None)
            {
                if (_isDualListMode?.Invoke() == true)
                {
                    _switchDualPaneFocus?.Invoke();
                    e.Handled = true;
                    return;
                }
            }

            // Ctrl+Shift+N: 新建窗口（独立进程）
            if (e.Key == Key.N && Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Shift))
            {
                var config = ConfigurationService.Instance.GetSnapshot();
                if (config.EnableMultiWindow)
                {
                    try
                    {
                        var exePath = Environment.ProcessPath;
                        if (!string.IsNullOrEmpty(exePath))
                        {
                            Process.Start(exePath);
                            e.Handled = true;
                            return;
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"无法启动新窗口: {ex.Message}");
                    }
                }
                else
                {
                    // 如果禁用，则保留原逻辑（也是新建文件夹？）
                    // Windows默认为Ctrl+Shift+N新建文件夹。这里如果多窗口启用，则覆盖为新窗口。
                    // 按照用户需求：Ctrl+N=新建文件夹，Ctrl+Shift+N=新建窗口。
                    // 如果禁用多窗口，Ctrl+Shift+N 回退为 新建文件夹。
                    _newFolderClick();
                    e.Handled = true;
                    return;
                }
            }

            // F5: 刷新
            if (e.Key == Key.F5)
            {
                _refreshClick();
                e.Handled = true;
                return;
            }

            // Ctrl+Shift+F: 专注模式
            if (e.Key == Key.F && Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Shift))
            {
                _switchLayoutMode?.Invoke(0); // 专注模式 = 0
                e.Handled = true;
                return;
            }

            // Ctrl+Shift+W: 工作模式
            if (e.Key == Key.W && Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Shift))
            {
                _switchLayoutMode?.Invoke(1); // 工作模式 = 1
                e.Handled = true;
                return;
            }

            // Ctrl+Shift+A: 完整模式
            if (e.Key == Key.A && Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Shift))
            {
                _switchLayoutMode?.Invoke(2); // 完整模式 = 2
                e.Handled = true;
                return;
            }

            // Alt+D: 聚焦地址栏
            // 必须同时检查 Key.D 和 SystemKey.D，因为按下 Alt 时 Key 可能是 System
            bool isAltD = (e.Key == Key.D && Keyboard.Modifiers == ModifierKeys.Alt) ||
                          (e.SystemKey == Key.D && Keyboard.Modifiers == ModifierKeys.Alt);

            if (isAltD)
            {
                var activeBrowser = _getActiveBrowser();
                if (activeBrowser != null)
                {
                    // 确保切换到编辑模式并全选
                    activeBrowser.AddressBarControl?.SwitchToEditMode();
                    e.Handled = true;
                    return;
                }
            }

            // Alt+A: 聚焦地址栏 (Alias for Alt+D)
            // 修复文件列表拦截 Alt+A 的问题
            bool isAltA = (e.Key == Key.A && Keyboard.Modifiers == ModifierKeys.Alt) ||
                          (e.SystemKey == Key.A && Keyboard.Modifiers == ModifierKeys.Alt);

            if (isAltA)
            {
                var activeBrowser = _getActiveBrowser();
                if (activeBrowser != null)
                {
                    activeBrowser.AddressBarControl?.SwitchToEditMode();
                    e.Handled = true;
                    return;
                }
            }

            // Ctrl+A: 全选当前列表
            if (e.Key == Key.A && Keyboard.Modifiers == ModifierKeys.Control)
            {
                var activeBrowser = _getActiveBrowser();
                if (activeBrowser?.FilesList != null && !(e.OriginalSource is System.Windows.Controls.TextBox))
                {
                    activeBrowser.FilesList.SelectAll();
                    e.Handled = true;
                    return;
                }
            }

            // Ctrl+C: 复制（排除文本框）
            if (e.Key == Key.C && Keyboard.Modifiers == ModifierKeys.Control)
            {
                var focusedElement = Keyboard.FocusedElement;
                if (focusedElement is System.Windows.Controls.TextBox ||
                    focusedElement is System.Windows.Controls.RichTextBox)
                {
                    return;
                }

                var activeBrowser = _getActiveBrowser();
                if (activeBrowser?.FilesSelectedItems != null && activeBrowser.FilesSelectedItems.Count > 0)
                {
                    _copyClick();
                    e.Handled = true;
                    return;
                }
            }

            // Ctrl+V: 粘贴（排除文本框）
            if (e.Key == Key.V && Keyboard.Modifiers == ModifierKeys.Control)
            {
                var focusedElement = Keyboard.FocusedElement;
                if (focusedElement is System.Windows.Controls.TextBox ||
                    focusedElement is System.Windows.Controls.RichTextBox)
                {
                    return;
                }

                _pasteClick();
                e.Handled = true;
                return;
            }

            // Ctrl+X: 剪切（排除文本框）
            if (e.Key == Key.X && Keyboard.Modifiers == ModifierKeys.Control)
            {
                var focusedElement = Keyboard.FocusedElement;
                if (focusedElement is System.Windows.Controls.TextBox ||
                    focusedElement is System.Windows.Controls.RichTextBox)
                {
                    return;
                }

                var activeBrowser = _getActiveBrowser();
                if (activeBrowser?.FilesSelectedItems != null && activeBrowser.FilesSelectedItems.Count > 0)
                {
                    _cutClick();
                    e.Handled = true;
                    return;
                }
            }

            // Delete: 删除
            if (e.Key == Key.Delete)
            {
                var focusedElement = Keyboard.FocusedElement;
                if (focusedElement is System.Windows.Controls.TextBox || focusedElement is System.Windows.Controls.TextBlock)
                {
                    return;
                }

                var activeBrowser = _getActiveBrowser();
                if (activeBrowser?.FilesSelectedItems != null && activeBrowser.FilesSelectedItems.Count > 0)
                {
                    bool isShiftPressed = Keyboard.Modifiers.HasFlag(ModifierKeys.Shift);
                    if (isShiftPressed && _permanentDeleteClick != null)
                    {
                        _permanentDeleteClick();
                    }
                    else
                    {
                        _deleteClick();
                    }
                    e.Handled = true;
                    return;
                }
            }

            // F2: 重命名
            if (e.Key == Key.F2)
            {
                var activeBrowser = _getActiveBrowser();
                if (activeBrowser?.FilesSelectedItem != null)
                {
                    _renameClick();
                    e.Handled = true;
                    return;
                }
            }

            // Ctrl+Z: 撤销
            if (e.Key == Key.Z && Keyboard.Modifiers == ModifierKeys.Control)
            {
                if (_undoClick != null)
                {
                    _undoClick();
                    e.Handled = true;
                    return;
                }
            }

            // Ctrl+Y: 重做
            if (e.Key == Key.Y && Keyboard.Modifiers == ModifierKeys.Control)
            {
                if (_redoClick != null)
                {
                    _redoClick();
                    e.Handled = true;
                    return;
                }
            }
        }

        public void MainWindow_KeyDown(object sender, KeyEventArgs e)
        {
            // 空格键触发 QuickLook 预览
            if (e.Key == Key.Space)
            {
                // 检查是否有选中的文件
                if (_fileBrowser?.FilesSelectedItem is FileSystemItem selectedItem && !selectedItem.IsDirectory)
                {
                    // 检查 QuickLook 是否安装
                    if (YiboFile.Previews.PreviewHelper.IsQuickLookInstalled())
                    {
                        try
                        {
                            var quickLookPath = YiboFile.Previews.PreviewHelper.GetQuickLookPath();
                            if (!string.IsNullOrEmpty(quickLookPath))
                            {
                                Process.Start(new ProcessStartInfo
                                {
                                    FileName = quickLookPath,
                                    Arguments = $@"""{selectedItem.Path}""",
                                    UseShellExecute = false
                                });
                                e.Handled = true;
                            }
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show($"无法启动 QuickLook: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                        }
                    }
                }
            }
        }
    }
}


