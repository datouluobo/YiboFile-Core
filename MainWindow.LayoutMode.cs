using System;
using YiboFile.Models;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.IO;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using YiboFile.Controls;
using YiboFile.Services.Tabs;
using YiboFile.Services.FileOperations;
using YiboFile.Services.Core;
using YiboFile.Services.Navigation;
using YiboFile.Models.Navigation;
using YiboFile.ViewModels.Messaging.Messages;
using YiboFile.Services.Config;

namespace YiboFile
{
    /// <summary>
    /// 布局模式切换功能
    /// Refactored to delegate to Handlers.LayoutEventHandler
    /// </summary>
    public partial class MainWindow
    {
        internal Handlers.LayoutEventHandler _layoutEventHandler;

        #region 布局模式与双列表状态

        public bool IsDualListMode => _layoutModule?.IsDualListMode ?? false;
        public bool IsSecondPaneFocused => _layoutModule?.IsSecondPaneFocused ?? false;

        internal void SwitchLayoutModeByIndex(int index) => _layoutEventHandler?.SwitchLayoutModeByIndex(index);

        internal void SetDualListMode(bool enable) => _layoutEventHandler?.SetDualListMode(enable);

        internal void SwitchFocusedPane() => _layoutEventHandler?.SwitchFocusedPane();
        internal void SwitchFocusedPaneFromKeyboard() => _layoutEventHandler?.SwitchFocusedPaneFromKeyboard();

        internal void UpdateFocusBorders() => _layoutEventHandler?.UpdateFocusBorders();

        internal void UpdateTabManagerLayout() => _layoutEventHandler?.UpdateTabManagerLayout();

        // 仅供 WindowOrchestrator 调用，确保初始化顺序
        internal void AttachSecondTabServiceUiContext() => _layoutEventHandler?.AttachSecondTabServiceUiContext();

        internal void InitializeLayoutMode() => _layoutEventHandler?.Initialize();

        #endregion

        #region 上下文辅助

        internal (Controls.FileBrowserControl browser, string path, Library library) GetActiveContext()
        {
            if (_layoutEventHandler != null) return _layoutEventHandler.GetActiveContext();
            return (FileBrowser, _currentPath, _currentLibrary);
        }

        internal void RefreshActiveFileList() => _layoutEventHandler?.RefreshActiveFileList();

        internal void NavigateSecondaryPaneToLibrary(Library library) => _layoutEventHandler?.NavigateSecondaryPaneToLibrary(library);
        internal void NavigateSecondaryPaneToTag(Models.TagViewModel tag) => _layoutEventHandler?.NavigateSecondaryPaneToTag(tag);
        internal void LoadSecondFileBrowserDirectory(string path) => _layoutEventHandler?.LoadSecondFileBrowserDirectory(path);

        #endregion
    }
}
