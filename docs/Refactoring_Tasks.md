# YiboFile 重构任务清单

> **状态**: 进行中 (自 2026-02-16 分析报告迁移)

## 📊 进度概览

| 优先级 | 任务 | 状态 | 完成度 | 备注 |
| :--- | :--- | :---: | :---: | :--- |
| **P0** (严重) | **WindowOrchestrator 拆分** | ✅ 已完成 | 100% | 代码行数从 981 降至 313 |
| **P0** (严重) | **MainWindow.Tabs.cs 逻辑迁移** | ✅ 已完成 | 100% | 文件已移除 |
| **P1** (高) | **Service 层事件迁移 (第一批)** | ✅ 已完成 | 100% | 核心导航服务已迁移 |
| **P1** (高) | Handler 层解耦 (IShellWindow) | ✅ 已完成 | 100% | LibraryEventHandler 已重构，关键 Handler 已全部解耦 |
| **P1** (高) | **FileListEventHandler 拆分** | ✅ 已完成 | 100% | 已拆分为 Mouse, Keyboard 子处理器，行数由 760+ 降至 110 (facade) |
| **P1** (高) | **统一主副栏导航架构** | ✅ 已完成 | 100% | 消除双轨制：委托回调 → 消息驱动 |
| **P2** (中) | Service 层事件迁移 (第二批) | ⬜ 待办 | 0% | 辅助服务 |
| **P2** (中) | PaneViewModel 逻辑优化 | ⬜ 待办 | 0% | |
| **P2** (中) | LibraryManagementWindow MVVM 化 | ⬜ 待办 | 0% | |
| **P2** (中) | App.xaml.cs 启动逻辑拆分 | ⬜ 待办 | 0% | |
| **P2** (中) | Bug 001/003 修复 | ⬜ 待办 | 0% | |

---

## 📝 详细任务清单

### 🔴 P0: 关键架构债务 (已完成!)

- [x] **WindowOrchestrator 拆分**
  - [x] 提取 `ModuleInitializer.cs` (`MainWindow.InitializeMvvmModules` 移至此处)
  - [x] 提取 `HandlerInitializer.cs` (`MainWindow.InitializeHandlers` 移至此处)
  - [x] 提取 `MessageBridgeSetup.cs`
  - [x] 缩减 `WindowOrchestrator.cs` 体积 (目标: < 400 行)

- [x] **MainWindow.Tabs.cs 逻辑迁移**
  - [x] 移动搜索刷新逻辑至 `SearchModule`
  - [x] 移动标签页操作至 `TabsModule`
  - [x] 删除 `MainWindow.Tabs.cs`

### 🟡 P1: 模块解耦与标准化 (进行中)

- [x] **Service 层事件迁移 (第一批: 核心导航)**
  - [x] `TabService` (5 个事件) -> `TabMessages.cs`
  - [x] `NavigationCoordinator` (2 个事件) -> `NavigationMessages.cs`
  - [x] 移除 `TabsModule`, `NavigationModule`, `PreviewService` 中的 `Action<>` 回调

- [ ] **Handler 层抽象化 (IShellWindow)**
  - [x] 在 `Interfaces` 命名空间中定义 `IShellWindow` (或 `IMainWindowAccessor`) 接口
  - [x] 在 `MainWindow.xaml.cs` 中实现 `IShellWindow`
  - [ ] 更新 `FileListEventHandler` 构造函数以接收 `IShellWindow`
  - [x] 更新 `LayoutEventHandler` 构造函数以接收 `IShellWindow`
  - [x] 更新 `ColumnInteractionHandler` (已移除 MainWindow 依赖)
  - [x] 更新 `WindowLifecycleHandler`
  - [x] 更新 `FileOperationHandler`
  - [x] 更新 `DragDropEventHandler`
  - [x] 更新 `LibraryEventHandler` (Refactored to use IShellWindow)
  - [x] 更新 `SelectionEventHandler` (Already decoupled via Delegates, acceptable state)
  - [x] 更新剩余 Handler (`Keyboard`, `Mouse` - standardized)

- [x] **FileListEventHandler 拆分**
  - [x] 分析 `FileListEventHandler.cs` 依赖
  - [x] 提取 `FileListKeyboardHandler.cs` (键盘快捷键)
  - [x] 提取 `FileListMouseHandler.cs` (鼠标交互)
  - [x] 保留 `FileListEventHandler` 作为协调器并完成 IShellWindow 解耦

- [x] **统一主副栏导航架构** (消除双轨制)
  > **背景**：主面板通过消息总线 (`NavigateToPathMessage` → `NavigationModule` → `NavigationCoordinator` → `PaneViewModel`) 驱动导航；副面板通过 `TabUiContext` 中的 `Action<>` 委托直接调用 `PaneViewModel` 方法。两套并行机制是 BUG-019+ 等问题的根源。
  - [x] **Step 1: 副面板导航消息化** (~3-4h)
    - 将 `LayoutEventHandler.LoadSecondFileBrowserDirectory/Library/Tag` 改为发布 `NavigateToPathMessage(Pane=Second)` 或 `NavigateToLibraryMessage(Pane=Second)`
    - 移除 `TabUiContext` 中 `NavigateToPathInternal`、`SetNavigationCurrentPath`、`LoadLibraryFiles` 委托
    - 确保 `NavigationCoordinator.HandlePathNavigation` 和 `NavigateAsync` 正确处理 `PaneId.Second`
  - [x] **Step 2: 标签页切换同步统一** (~2-3h)
    - 将 `LayoutEventHandler.SyncSecondUiWithActiveTab` 改为通过消息触发 (`RestoreNavigationStateMessage` 或新消息)
    - 让 `TabsModule` 统一处理主副面板的标签页激活同步逻辑
    - 移除 `LayoutEventHandler._lastSecondActiveTab` 和 `OnSecondActiveTabPropertyChanged` 监听
  - [x] **Step 3: 清理 LayoutEventHandler 导航逻辑** (~2-3h)
    - 移除 `LoadSecondFileBrowserDirectory`、`LoadSecondFileBrowserLibrary`、`LoadSecondFileBrowserTag` 方法 (部分保留作为 Coordinator 包装器，逻辑已重构)
    - 保留 `LayoutEventHandler` 仅负责 UI 布局（焦点边框、面板显隐、标签页控件布局）
    - `NavigateSecondaryPaneToLibrary/Tag` 改为发布消息 (包装器委托给协调器)
  - [x] **Step 4: 精简 TabUiContext** (~1h)
    - 移除所有导航相关委托，仅保留纯 UI 委托 (`FindResource`、`Dispatcher`、`GetConfig` 等)
    - 评估主面板是否也需要 `AttachUiContext`，或可完全依赖消息驱动

### 🟢 P2: 清理与维护 (计划中)

- [ ] **Service 层事件迁移 (第二批: 辅助服务)**
  - [ ] `FileListService` (3 个事件)
  - [ ] `LibraryService` (3 个事件)
  - [ ] `FileOperationService` (3 个事件)
  - [ ] `FavoriteService` (4 个事件)
  - [ ] `QuickAccessService` (2 个事件)
  - [ ] `UndoService` (3 个事件)
  - [ ] `ClipboardService` (1 个事件)
  - [ ] `TagService` (2 个事件)
  - [ ] `NotesService` (1 个事件)
  - [ ] `ConfigurationService` (1 个事件)
  - [ ] `ErrorService` (1 个事件)
  - [ ] `FileSystemWatcherService` (2 个事件)

- [ ] **LibraryManagementWindow MVVM 化**
  - [ ] 创建 `LibraryManagementViewModel`
  - [ ] 绑定视图与 ViewModel
  - [ ] 移除 Code-behind 逻辑

- [ ] **App.xaml.cs 启动逻辑拆分**
  - [ ] 提取 `StartupService`

- [ ] **Bug 修复**
  - [ ] **BUG-001**: 地址栏库指示器错误
  - [ ] **BUG-003**: 第二面板库路径识别失败

### 🔵 P3: 文档

- [ ] 更新 `Project_Evaluation_and_Roadmap.md` 中的完成状态
