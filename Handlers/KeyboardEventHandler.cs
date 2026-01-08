using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using OoiMRR.Controls;
using OoiMRR.Services.Tabs;

namespace OoiMRR.Handlers
{
    /// <summary>
    /// 键盘事件处理器
    /// 处理所有键盘快捷键，包括窗口级和文件列表的键盘事件
    /// </summary>
    public class KeyboardEventHandler
    {
        private readonly FileBrowserControl _fileBrowser;
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
            FileBrowserControl fileBrowser,
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

            // Ctrl+N: 新建文件夹
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

            // Ctrl+Shift+N: 新建文件夹（Windows标准）
            if (e.Key == Key.N && Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Shift))
            {
                _newFolderClick();
                e.Handled = true;
                return;
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

            // Ctrl+A: 全选（在文件列表中）
            if (e.Key == Key.A && Keyboard.Modifiers == ModifierKeys.Control)
            {
                if (_fileBrowser?.FilesList != null && _fileBrowser.FilesList.Items.Count > 0)
                {
                    if (_fileBrowser?.FilesList != null)
                        _fileBrowser.FilesList.SelectAll();
                    e.Handled = true;
                    return;
                }
            }

            // Alt+D: 进入地址栏编辑模式
            // 注意:Alt+字母时,e.Key是System,需要检查e.SystemKey
            if ((e.Key == Key.D || e.SystemKey == Key.D) && Keyboard.Modifiers == ModifierKeys.Alt)
            {
                if (_fileBrowser?.AddressBar != null && !_fileBrowser.AddressBar.IsAddressTextBoxFocused)
                {
                    _fileBrowser.AddressBar.SwitchToEditMode();
                    e.Handled = true;
                    return;
                }
            }

            // Ctrl+C: 复制（排除文本框）
            if (e.Key == Key.C && Keyboard.Modifiers == ModifierKeys.Control)
            {
                var focusedElement = Keyboard.FocusedElement;
                // 只在文本框中才跳过，其他情况都执行复制
                if (focusedElement is System.Windows.Controls.TextBox ||
                    focusedElement is System.Windows.Controls.RichTextBox)
                {
                    return; // 让文本框处理自己的复制
                }

                if (_fileBrowser?.FilesSelectedItems != null && _fileBrowser.FilesSelectedItems.Count > 0)
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
                // 只在文本框中才跳过，其他情况都执行粘贴
                if (focusedElement is System.Windows.Controls.TextBox ||
                    focusedElement is System.Windows.Controls.RichTextBox)
                {
                    return; // 让文本框处理自己的粘贴
                }

                _pasteClick();
                e.Handled = true;
                return;
            }

            // Ctrl+X: 剪切（排除文本框）
            if (e.Key == Key.X && Keyboard.Modifiers == ModifierKeys.Control)
            {
                var focusedElement = Keyboard.FocusedElement;
                // 只在文本框中才跳过，其他情况都执行剪切
                if (focusedElement is System.Windows.Controls.TextBox ||
                    focusedElement is System.Windows.Controls.RichTextBox)
                {
                    return; // 让文本框处理自己的剪切
                }

                if (_fileBrowser?.FilesSelectedItems != null && _fileBrowser.FilesSelectedItems.Count > 0)
                {
                    _cutClick();
                    e.Handled = true;
                    return;
                }
            }

            // Delete: 删除（如果文件列表有焦点，且不在文本框中）
            // Shift+Delete: 永久删除（跳过回收站）
            if (e.Key == Key.Delete)
            {
                var focusedElement = Keyboard.FocusedElement;
                if (focusedElement is System.Windows.Controls.TextBox || focusedElement is System.Windows.Controls.TextBlock)
                {
                    // 在文本框中，不处理
                    return;
                }
                if (_fileBrowser?.FilesSelectedItems != null && _fileBrowser.FilesSelectedItems.Count > 0)
                {
                    bool isShiftPressed = Keyboard.Modifiers.HasFlag(ModifierKeys.Shift);
                    if (isShiftPressed && _permanentDeleteClick != null)
                    {
                        _permanentDeleteClick(); // Shift+Delete: 永久删除
                    }
                    else
                    {
                        _deleteClick(); // 普通删除（移到回收站）
                    }
                    e.Handled = true;
                    return;
                }
            }

            // F2: 重命名（如果文件列表有焦点）
            if (e.Key == Key.F2)
            {
                var focusedElement = Keyboard.FocusedElement;
                if (focusedElement is System.Windows.Controls.TextBox)
                {
                    // 在文本框中，不处理
                    return;
                }
                if (_fileBrowser?.FilesSelectedItem != null)
                {
                    _renameClick();
                    e.Handled = true;
                    return;
                }
            }

            // Ctrl+Z: 撤销
            if (e.Key == Key.Z && Keyboard.Modifiers == ModifierKeys.Control)
            {
                _undoClick?.Invoke();
                e.Handled = true;
                return;
            }

            // Ctrl+Y: 重做
            if (e.Key == Key.Y && Keyboard.Modifiers == ModifierKeys.Control)
            {
                _redoClick?.Invoke();
                e.Handled = true;
                return;
            }

            // 空格键触发 QuickLook 预览
            if (e.Key == Key.Space)
            {
                // 检查是否有选中的文件
                if (_fileBrowser?.FilesSelectedItem is FileSystemItem selectedItem && !selectedItem.IsDirectory)
                {
                    // 检查 QuickLook 是否安装
                    if (OoiMRR.Previews.PreviewHelper.IsQuickLookInstalled())
                    {
                        try
                        {
                            var quickLookPath = OoiMRR.Previews.PreviewHelper.GetQuickLookPath();
                            if (!string.IsNullOrEmpty(quickLookPath))
                            {
                                Process.Start(new ProcessStartInfo
                                {
                                    FileName = quickLookPath,
                                    Arguments = $@"""{selectedItem.Path}""",
                                    UseShellExecute = false
                                });
                                e.Handled = true; // 标记事件已处理
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

        public void MainWindow_KeyDown(object sender, KeyEventArgs e)
        {
            // 空格键触发 QuickLook 预览
            if (e.Key == Key.Space)
            {
                // 检查是否有选中的文件
                if (_fileBrowser?.FilesSelectedItem is FileSystemItem selectedItem && !selectedItem.IsDirectory)
                {
                    // 检查 QuickLook 是否安装
                    if (OoiMRR.Previews.PreviewHelper.IsQuickLookInstalled())
                    {
                        try
                        {
                            var quickLookPath = OoiMRR.Previews.PreviewHelper.GetQuickLookPath();
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

