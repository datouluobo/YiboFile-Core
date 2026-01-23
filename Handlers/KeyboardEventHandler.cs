using System;
using YiboFile.Models;
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
        private readonly Action _closeOverlays;
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
            Action closeOverlays,
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
            _closeOverlays = closeOverlays ?? throw new ArgumentNullException(nameof(closeOverlays));
            _navigateBack = navigateBack ?? throw new ArgumentNullException(nameof(navigateBack));
            _undoClick = undoClick;
            _redoClick = redoClick;
            _switchLayoutMode = switchLayoutMode; // 可选参数
            _isDualListMode = isDualListMode;
            _switchDualPaneFocus = switchDualPaneFocus;
        }

        /// <summary>
        /// 检查是否触发了指定动作的快捷键
        /// </summary>
        internal bool IsActionTriggered(KeyEventArgs e, string actionName, string defaultKey)
        {
            var config = ConfigurationService.Instance.GetSnapshot();
            var hotkeyStr = defaultKey;

            // 尝试获取用户自定义快捷键
            if (config.CustomHotkeys != null && config.CustomHotkeys.TryGetValue(actionName, out var customKey))
            {
                hotkeyStr = customKey;
            }

            if (string.IsNullOrEmpty(hotkeyStr)) return false;

            // 解析快捷键字符串 (例如 "Ctrl+Shift+T" 或 "Backspace")
            var parts = hotkeyStr.Split('+');
            bool ctrlRequired = false;
            bool altRequired = false;
            bool shiftRequired = false;
            bool winRequired = false;
            string mainKeyStr = "";

            foreach (var part in parts)
            {
                var p = part.Trim();
                if (p.Equals("Ctrl", StringComparison.OrdinalIgnoreCase)) ctrlRequired = true;
                else if (p.Equals("Alt", StringComparison.OrdinalIgnoreCase)) altRequired = true;
                else if (p.Equals("Shift", StringComparison.OrdinalIgnoreCase)) shiftRequired = true;
                else if (p.Equals("Win", StringComparison.OrdinalIgnoreCase)) winRequired = true;
                else mainKeyStr = p;
            }

            // 验证修饰符
            var modifiers = Keyboard.Modifiers;
            if (ctrlRequired != modifiers.HasFlag(ModifierKeys.Control)) return false;
            if (altRequired != modifiers.HasFlag(ModifierKeys.Alt)) return false;
            if (shiftRequired != modifiers.HasFlag(ModifierKeys.Shift)) return false;
            if (winRequired != modifiers.HasFlag(ModifierKeys.Windows)) return false;

            // 验证主键
            if (string.IsNullOrEmpty(mainKeyStr)) return true; // 仅有修饰符的情况（通常不建议）

            var currentKey = e.Key == Key.System ? e.SystemKey : e.Key;
            string currentKeyStr = currentKey.ToString();

            // 兼容性映射
            if (currentKeyStr == mainKeyStr) return true;

            // 处理数字键 (D1 -> 1, NumPad1 -> 1)
            if (currentKey >= Key.D0 && currentKey <= Key.D9)
            {
                if (mainKeyStr == (currentKey - Key.D0).ToString()) return true;
            }
            if (currentKey >= Key.NumPad0 && currentKey <= Key.NumPad9)
            {
                if (mainKeyStr == (currentKey - Key.NumPad0).ToString()) return true;
            }

            return false;
        }

        public void MainWindow_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            // Esc: 关闭全屏覆盖层 (设置、关于)
            if (e.Key == Key.Escape && Keyboard.Modifiers == ModifierKeys.None)
            {
                _closeOverlays?.Invoke();
                // 如果覆盖层是打开的，我们可能想标记 e.Handled = true
                // 但为了不破坏其他可能的 Esc 逻辑，我们这里取决于 closeOverlays 逻辑
                // 实际上 CloseOverlays 在 MainWindow 中会检查可见性
            }

            // Ctrl+W 或 Ctrl+F4: 关闭当前标签页
            // 我们保留 F4 作为硬编码备选，但 Ctrl+W 改为动态
            if (IsActionTriggered(e, "关闭标签页", "Ctrl+W") || (e.Key == Key.F4 && Keyboard.Modifiers == ModifierKeys.Control))
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

            // Ctrl+T: 新建标签页
            if (IsActionTriggered(e, "新建标签页", "Ctrl+T"))
            {
                var desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                _createTab(desktopPath);
                e.Handled = true;
                return;
            }

            // Ctrl+Tab: 切换到下一个标签页
            if (IsActionTriggered(e, "下一个标签", "Ctrl+Tab"))
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
            if (IsActionTriggered(e, "上一个标签", "Ctrl+Shift+Tab"))
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
            if (IsActionTriggered(e, "新建文件夹", "Ctrl+N"))
            {
                _newFolderClick();
                e.Handled = true;
                return;
            }

            // Tab键（无修饰符）：在双列表模式下切换主副面板焦点
            if (IsActionTriggered(e, "切换双面板焦点", "Tab"))
            {
                if (_isDualListMode?.Invoke() == true)
                {
                    _switchDualPaneFocus?.Invoke();
                    e.Handled = true;
                    return;
                }
            }

            // Ctrl+Shift+N: 新建窗口
            if (IsActionTriggered(e, "新建窗口", "Ctrl+Shift+N"))
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
                    _newFolderClick();
                    e.Handled = true;
                    return;
                }
            }

            // F5: 刷新
            if (IsActionTriggered(e, "刷新", "F5"))
            {
                _refreshClick();
                e.Handled = true;
                return;
            }

            // Ctrl+Shift+F: 专注模式
            if (IsActionTriggered(e, "专注模式", "Ctrl+Shift+F"))
            {
                _switchLayoutMode?.Invoke(0);
                e.Handled = true;
                return;
            }

            // Ctrl+Shift+W: 工作模式
            if (IsActionTriggered(e, "工作模式", "Ctrl+Shift+W"))
            {
                _switchLayoutMode?.Invoke(1);
                e.Handled = true;
                return;
            }

            // Ctrl+Shift+A: 完整模式
            if (IsActionTriggered(e, "完整模式", "Ctrl+Shift+A"))
            {
                _switchLayoutMode?.Invoke(2);
                e.Handled = true;
                return;
            }

            // Alt+D: 聚焦地址栏
            if (IsActionTriggered(e, "地址栏编辑", "Alt+D"))
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
            if (IsActionTriggered(e, "全选", "Ctrl+A"))
            {
                var activeBrowser = _getActiveBrowser();
                if (activeBrowser?.FilesList != null && !(e.OriginalSource is System.Windows.Controls.TextBox))
                {
                    activeBrowser.FilesList.SelectAll();
                    e.Handled = true;
                    return;
                }
            }

            // Ctrl+C: 复制
            if (IsActionTriggered(e, "复制", "Ctrl+C"))
            {
                var focusedElement = Keyboard.FocusedElement;
                if (!(focusedElement is System.Windows.Controls.TextBox || focusedElement is System.Windows.Controls.RichTextBox))
                {
                    var activeBrowser = _getActiveBrowser();
                    if (activeBrowser?.FilesSelectedItems != null && activeBrowser.FilesSelectedItems.Count > 0)
                    {
                        _copyClick();
                        e.Handled = true;
                        return;
                    }
                }
            }

            // Ctrl+V: 粘贴
            if (IsActionTriggered(e, "粘贴", "Ctrl+V"))
            {
                var focusedElement = Keyboard.FocusedElement;
                if (!(focusedElement is System.Windows.Controls.TextBox || focusedElement is System.Windows.Controls.RichTextBox))
                {
                    _pasteClick();
                    e.Handled = true;
                    return;
                }
            }

            // Ctrl+X: 剪切
            if (IsActionTriggered(e, "剪切", "Ctrl+X"))
            {
                var focusedElement = Keyboard.FocusedElement;
                if (!(focusedElement is System.Windows.Controls.TextBox || focusedElement is System.Windows.Controls.RichTextBox))
                {
                    var activeBrowser = _getActiveBrowser();
                    if (activeBrowser?.FilesSelectedItems != null && activeBrowser.FilesSelectedItems.Count > 0)
                    {
                        _cutClick();
                        e.Handled = true;
                        return;
                    }
                }
            }

            // Delete: 删除
            if (IsActionTriggered(e, "删除 (移到回收站)", "Delete"))
            {
                // 注意：这里需要确保 Shift+Delete 不会命入这个逻辑，除非 Shift 在 default 字符串中
                var focusedElement = Keyboard.FocusedElement;
                if (!(focusedElement is System.Windows.Controls.TextBox || focusedElement is System.Windows.Controls.TextBlock))
                {
                    var activeBrowser = _getActiveBrowser();
                    if (activeBrowser?.FilesSelectedItems != null && activeBrowser.FilesSelectedItems.Count > 0)
                    {
                        _deleteClick();
                        e.Handled = true;
                        return;
                    }
                }
            }

            // Shift+Delete: 永久删除
            if (IsActionTriggered(e, "永久删除", "Shift+Delete"))
            {
                var focusedElement = Keyboard.FocusedElement;
                if (!(focusedElement is System.Windows.Controls.TextBox || focusedElement is System.Windows.Controls.TextBlock))
                {
                    var activeBrowser = _getActiveBrowser();
                    if (activeBrowser?.FilesSelectedItems != null && activeBrowser.FilesSelectedItems.Count > 0)
                    {
                        if (_permanentDeleteClick != null) _permanentDeleteClick();
                        else _deleteClick();
                        e.Handled = true;
                        return;
                    }
                }
            }

            // F2: 重命名
            if (IsActionTriggered(e, "重命名", "F2"))
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
            if (IsActionTriggered(e, "撤销", "Ctrl+Z"))
            {
                if (_undoClick != null)
                {
                    _undoClick();
                    e.Handled = true;
                    return;
                }
            }

            // Ctrl+Y: 重做
            if (IsActionTriggered(e, "重做", "Ctrl+Y"))
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
            if (IsActionTriggered(e, "QuickLook 预览", "Space"))
            {
                // 检查是否有选中的文件
                if (_getActiveBrowser()?.FilesSelectedItem is FileSystemItem selectedItem && !selectedItem.IsDirectory)
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
                                return;
                            }
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show($"无法启动 QuickLook: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                        }
                    }
                }
            }

            // Enter: 打开文件/文件夹
            if (IsActionTriggered(e, "打开文件/文件夹", "Enter"))
            {
                var activeBrowser = _getActiveBrowser();
                if (activeBrowser?.FilesSelectedItem is FileSystemItem selectedItem)
                {
                    if (selectedItem.IsRenaming) return;

                    if (selectedItem.IsDirectory)
                    {
                        if (_isLibraryMode())
                        {
                            _switchNavigationMode("Path");
                        }
                        _navigateToPath(selectedItem.Path);
                    }
                    else
                    {
                        try
                        {
                            Process.Start(new ProcessStartInfo { FileName = selectedItem.Path, UseShellExecute = true });
                        }
                        catch (Exception ex) { MessageBox.Show($"无法打开文件: {ex.Message}"); }
                    }
                }
                e.Handled = true;
                return;
            }

            // Backspace: 返回上级目录
            if (IsActionTriggered(e, "返回上级目录", "Backspace"))
            {
                _navigateBack();
                e.Handled = true;
                return;
            }

            // Alt+Enter: 属性
            if (IsActionTriggered(e, "属性", "Alt+Enter"))
            {
                // 由于我们没有属性点击的回调注入，这里暂时保留或通过其他方式调用
                // e.Handled = true;
            }
        }
    }
}


