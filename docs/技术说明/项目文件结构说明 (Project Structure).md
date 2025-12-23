# 项目文件结构说明 (Project Structure)

本文档旨在提供 OoiMRR 项目的文件结构概览及各模块的主要职责说明，帮助开发人员快速理解代码组织方式。

> **注意**：该文档应随着项目重构和迭代保持更新。

## 根目录

*   **OoiMRR.sln**: 项目解决方案文件。
*   **OoiMRR.csproj**: 项目工程文件，定义了依赖项和构建配置。
*   **App.xaml / App.xaml.cs**: 应用程序入口，定义了全局资源、异常处理和应用程序生命周期。
*   **MainWindow.xaml**: 主窗口的 XAML 布局定义。
*   **MainWindow.xaml.cs**: 主窗口的代码逻辑（UI 初始化、交互事件）。
*   **MainWindow.Navigation.cs**: 主窗口的 Partial Class，专门处理**文件导航**和**列表加载**逻辑。
*   **MainWindow.Tabs.cs**: 主窗口的 Partial Class，专门处理**多标签页**的管理逻辑。

## 目录结构详解

### 1. Services (服务层)

存放业务逻辑、核心功能实现和 UI 辅助服务。

*   **Core/**: 核心基础设施。
    *   `DatabaseManager.cs`: SQLite 数据库管理，处理标签、库、备注等的持久化。
    *   `McpClient.cs` / `UiLayoutMcp.cs`: MCP (Model Context Protocol) 相关实现，用于与外部 AI 服务交互。
*   **FileSystem/**: 文件系统操作。
    *   `FileListService.cs`: 核心服务，负责异步加载文件列表、排序和过滤。
    *   `FileOperations/`: 文件操作逻辑（复制、移动、删除等）。
    *   `FileSystemWatcherService.cs`: 监控文件夹变化并通知 UI 刷新。
    *   `FileInfoService.cs`: 获取文件详细信息。
*   **Search/**: 搜索功能。
    *   `SearchService.cs`: 搜索服务入口。
    *   `EverythingHelper.cs` / `EverythingSearchExecutor.cs`: 集成 "Everything" 搜索引擎的高速搜索实现。
    *   `NotesSearchExecutor.cs`: 备注内容搜索。
*   **Media/**: 媒体处理。
    *   `Preview/`: 预览生成服务 (`PreviewService.cs`)。
    *   `CadImageCache.cs` / `DwgConverter.cs`: CAD 文件预览支持。
    *   `ChmCacheManager.cs`: CHM 文件支持。
*   **Features/**: 特定功能模块。
    *   `Tag/`: 标签管理服务 (`TagService.cs`, `TagUIHandler.cs`)。
    *   `Favorite/`: 收藏夹服务 (`FavoriteService.cs`)。
    *   `QuickAccess/`: 快速访问服务 (`QuickAccessService.cs`)。
    *   `FileNotes/`: 文件备注服务 (`FileNotesService.cs`)。
    *   `LibraryService.cs`: 库管理逻辑。
*   **UI/**: UI 逻辑拆分。
    *   `Tabs/`: 标签页服务 (`TabService.cs`) 和模型 (`TabModels.cs`)。
    *   `Navigation/`: 导航模式管理 (`NavigationService.cs`, `NavigationModeService.cs`)。
    *   `UIHelper/`: 通用 UI 辅助方法 (`UIHelperService.cs`)。
    *   `ColumnManagement/`: 文件列表列的保存与恢复 (`ColumnService.cs`)。
    *   `Settings/`: 设置面板逻辑。

### 2. Controls (控件)

可复用的自定义 UI 控件。

*   **FileBrowserControl.xaml**: 核心文件浏览控件，包含地址栏、工具栏和文件列表。
*   **FileListControl.xaml**: 专门显示文件列表（ListView）的控件。
*   **NavigationPanelControl.xaml**: 左侧导航栏（库、驱动器、标签树）。
*   **SettingsPanelControl.xaml**: 设置界面。
*   **TabManagerControl.xaml**: 顶部标签页栏。
*   **AddressBarControl.xaml**: 地址栏组件。

### 3. Models (数据模型)

定义数据结构和实体。

*   **UI/**: UI 相关模型。
    *   `ActionItem.cs`: 定义工具栏按钮或分隔符。
    *   `DraggableButton.cs`: 可拖拽按钮的数据结构。
*   `FileSystemItem.cs` (可能位于 Services 或根目录，需确认): 文件项的基本数据结构。

### 4. ViewModels (视图模型)

MVVM 模式的 ViewModel（目前项目处于混合模式，部分逻辑在 Code-behind，部分在 ViewModel）。

*   `FileListViewModel.cs`: 文件列表的视图模型。
*   `NavigationViewModel.cs`: 导航逻辑的视图模型。

### 5. Windows (窗口)

除主窗口外的其他独立窗口。

*   **TagManagementWindow.xaml**: 标签管理窗口。
*   **LibraryManagementWindow.xaml**: 库管理窗口。
*   **ConfigWindow.xaml**: 设置/配置窗口。
*   **Dialogs**: 各类对话框 (`ConfirmDialog`, `TagDialog`, `PathInputDialog` 等)。

### 6. Handlers (事件处理器)

用于减轻 `MainWindow.xaml.cs` 负担的专用事件处理类。

*   `MenuEventHandler.cs`: 处理菜单点击事件。
*   `MouseEventHandler.cs`: 处理全局或特定区域的鼠标事件。
*   `FileBrowserEventHandler.cs`: 处理文件浏览器的交互事件。

## 主要工作流

1.  **启动**: `App.xaml.cs` -> `MainWindow.xaml.cs` 初始化服务 -> 加载上次路径。
2.  **导航**: 用户点击 -> `MainWindow.Navigation.cs` (`NavigateToPath`) -> `NavigationService` 更新状态 -> `FileListService` 异步加载数据 -> `MainWindow` 更新 UI。
3.  **标签页**: 用户操作 -> `MainWindow.Tabs.cs` -> `TabService` 维护 `ObservableCollection<PathTab>` -> `TabManagerControl` 绑定显示。

## 待重构项

*   目前 `MainWindow` 仍承担了部分 UI 协调工作，计划进一步将特定区域的逻辑下沉到对应的 `UserControl` 或 `Service` 中。
