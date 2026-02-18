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
using YiboFile.ViewModels.Messaging;
using YiboFile.ViewModels.Messaging.Messages;

namespace YiboFile.Handlers
{
    /// <summary>
    /// 键盘事件处理器（全局快捷键 Handler - 合理保留）
    /// 处理所有键盘快捷键，包括窗口级和文件列表的键盘事件
    /// 
    /// 设计说明：
    /// - 此 Handler 是合理的 UI 层组件，用于集中管理全局快捷键
    /// - 支持用户自定义快捷键（通过 ConfigurationService）
    /// - 所有业务逻辑委托给注入的回调/服务，符合 MVVM
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
        // private readonly Action _refreshClick; // Migrated to InputBinding
        // private readonly Action _copyClick; // Migrated
        // private readonly Action _pasteClick; // Migrated
        // private readonly Action _cutClick; // Migrated
        // private readonly Action _deleteClick; // Migrated
        // private readonly Action _permanentDeleteClick; // Migrated
        // private readonly Action _renameClick; // Migrated
        private readonly Action<string> _navigateToPath;
        private readonly Action<string> _switchNavigationMode;
        private readonly Func<bool> _isLibraryMode;
        private readonly Action _closeOverlays;
        private readonly Action _navigateBack;

        // private readonly Action _undoClick; // Migrated
        // private readonly Action _redoClick; // Migrated
        private readonly IMessageBus _messageBus;

        public KeyboardEventHandler(
            FileBrowserControl fileBrowser, // Keep for backward compatibility or primary ref
             Func<FileBrowserControl> getActiveBrowser, // New delegate
            Func<TabService> getTabService,
            Action<PathTab> closeTab,
            Action<string> createTab,
            Action<PathTab> switchToTab,
            Action newFolderClick,
            // Removed unused params migrated to InputBindings
            Action<string> navigateToPath,
            Action<string> switchNavigationMode,
            Func<bool> isLibraryMode,
            Action closeOverlays,
            Action navigateBack,
            IMessageBus messageBus = null)
        {
            _fileBrowser = fileBrowser ?? throw new ArgumentNullException(nameof(fileBrowser));
            _getActiveBrowser = getActiveBrowser ?? (() => fileBrowser); // Default to main if null
            _getTabService = getTabService ?? throw new ArgumentNullException(nameof(getTabService));
            _closeTab = closeTab ?? throw new ArgumentNullException(nameof(closeTab));
            _createTab = createTab ?? throw new ArgumentNullException(nameof(createTab));
            _switchToTab = switchToTab ?? throw new ArgumentNullException(nameof(switchToTab));
            _newFolderClick = newFolderClick ?? throw new ArgumentNullException(nameof(newFolderClick));

            _navigateToPath = navigateToPath ?? throw new ArgumentNullException(nameof(navigateToPath));
            _switchNavigationMode = switchNavigationMode ?? throw new ArgumentNullException(nameof(switchNavigationMode));
            _isLibraryMode = isLibraryMode ?? throw new ArgumentNullException(nameof(isLibraryMode));
            _closeOverlays = closeOverlays ?? throw new ArgumentNullException(nameof(closeOverlays));
            _navigateBack = navigateBack ?? throw new ArgumentNullException(nameof(navigateBack));

            _messageBus = messageBus;

            if (_messageBus != null)
            {
                _messageBus.Subscribe<WindowPreviewKeyDownMessage>(m => MainWindow_PreviewKeyDown(null, m.EventArgs));
                _messageBus.Subscribe<WindowKeyDownMessage>(m => MainWindow_KeyDown(null, m.EventArgs));
            }
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

            // Ctrl+T: 新建标签页 (UX Spec: 默认新建为当前标签页的副本)
            if (IsActionTriggered(e, "新建标签页", "Ctrl+T"))
            {
                _createTab(null);
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


