using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using YiboFile.Controls;
using YiboFile.ViewModels;

namespace YiboFile.Interfaces
{
    /// <summary>
    /// Shell 窗口抽象接口，供 Handler 层访问 UI 元素
    /// Handler 通过此接口获取必要的 UI 引用，而非直接持有 MainWindow
    /// </summary>
    public interface IShellWindow
    {
        bool IsLoaded { get; }
        void UpdateLayout();

        // UI Structure
        Grid RootGrid { get; }
        ColumnDefinition ColLeft { get; }
        ColumnDefinition ColCenter { get; }
        ColumnDefinition ColRight { get; }
        ColumnDefinition ColRail { get; }
        Button TitleBarMaxRestoreButton { get; }

        // State
        bool IsSplitterDragging { get; }

        // Core UI Elements线程调度器
        Dispatcher Dispatcher { get; }

        // 核心控件
        FileBrowserControl FileBrowser { get; }
        FileBrowserControl SecondFileBrowser { get; }
        TabManagerControl TabManager { get; }
        TabManagerControl SecondTabManager { get; }

        // Navigation & Panels
        NavigationPanelControl NavigationPanelControl { get; }
        ListBox LibrariesListBox { get; }
        ListBox QuickAccessListBox { get; }

        bool IsDualListMode { get; }

        // ViewModel 与上下文
        MainWindowViewModel ViewModel { get; }
        object DataContext { get; set; }

        // 窗口操作与资源
        void UpdateTabManagerMargin();
        void RefreshFileList();
        object TryFindResource(object resourceKey);
        void DragMove();
        void Close();
        void AutoSizeGridViewColumn(GridViewColumn column);

        // 窗口属性
        WindowState WindowState { get; set; }
        double Width { get; set; }
        double Height { get; set; }
        double Left { get; set; }
        double Top { get; set; }
        bool Topmost { get; set; }

        // 窗口事件
        event EventHandler StateChanged;
        event EventHandler LocationChanged;
        event SizeChangedEventHandler SizeChanged;
        event EventHandler Closed;
        event EventHandler Activated;
        event EventHandler Deactivated;

        // 扩展支持 (For Handlers)
        ContextMenu LibraryContextMenu { get; }
        Services.Navigation.PaneId GetActivePaneId();
        bool IsInternalUiUpdate { get; }
        void ClearLegacyFileState(); // 用于清除遗留的 _currentFiles 等状态
    }
}
