# MainWindow.xaml.cs 拆分计划

## 概述

`MainWindow.xaml.cs` 文件目前有 **12170 行代码**，包含了大量的业务逻辑和 UI 处理代码。为了提高代码的可维护性、可测试性和可扩展性，需要将其拆分为多个服务类和处理器。

## 当前状态

### 已拆分的模块

#### 服务层（Services）

- ✅ **文件操作服务**：`Services/FileOperations/`
  - `CopyOperation.cs` - 复制操作
  - `PasteOperation.cs` - 粘贴操作
  - `DeleteOperation.cs` - 删除操作
  - `RenameOperation.cs` - 重命名操作
  - `NewFileOperation.cs` - 新建文件操作
  - `NewFolderOperation.cs` - 新建文件夹操作
  - `FileClipboardManager.cs` - 剪贴板管理
  - `IFileOperationContext.cs` - 文件操作上下文接口
  - `PathOperationContext.cs` - 路径操作上下文
  - `LibraryOperationContext.cs` - 库操作上下文
  - `TagOperationContext.cs` - 标签操作上下文

- ✅ **库管理服务**：`Services/LibraryService.cs`
  - 库的加载、创建、更新、删除
  - 库文件的加载和合并显示
  - 库的高亮显示

- ✅ **标签管理服务**：`Services/Tag/TagService.cs`
  - 标签的加载和管理
  - 标签筛选功能
  - 标签的添加和批量操作

- ✅ **收藏管理服务**：`Services/Favorite/FavoriteService.cs`
  - 收藏项的加载和管理
  - 收藏项的拖拽排序
  - 收藏项的添加和删除

- ✅ **快速访问服务**：`Services/QuickAccess/QuickAccessService.cs`
  - 快速访问项的加载
  - 驱动器的加载

- ✅ **导航状态管理**：`Services/NavigationStateManager.cs`
  - 导航历史管理
  - 导航状态保存和恢复

- ✅ **拖拽管理**：`Services/DragDropManager.cs`
  - 文件拖拽功能
  - 拖拽事件处理

#### 控件层（Controls）

- ✅ **操作按钮控件**：`Controls/ActionButtonsControl.xaml/cs`
  - 操作按钮的显示和更新
  - 按钮拖拽功能

- ✅ **地址栏控件**：`Controls/AddressBarControl.xaml/cs`
  - 路径显示和编辑
  - 路径导航

- ✅ **文件浏览器控件**：`Controls/FileBrowserControl.xaml/cs`
  - 文件列表显示
  - 文件选择和处理

- ✅ **文件列表控件**：`Controls/FileListControl.xaml/cs`
  - 文件列表视图
  - 文件排序和筛选

- ✅ **导航面板控件**：`Controls/NavigationPanelControl.xaml/cs`
  - 左侧导航面板
  - 库、标签、收藏、快速访问显示

- ✅ **设置面板控件**：`Controls/SettingsPanelControl.xaml/cs`
  - 设置界面
  - 配置管理

- ✅ **标签页管理控件**：`Controls/TabManagerControl.xaml/cs`
  - 标签页的创建、关闭、切换
  - 标签页的拖拽排序
  - 标签页的固定功能

- ✅ **窗口控制按钮控件**：`Controls/WindowControlButtonsControl.xaml/cs`
  - 窗口最小化、最大化、关闭按钮

#### 预览层（Previews）

- ✅ **预览提供者**：`Previews/`
  - `IPreviewProvider.cs` - 预览接口
  - `PreviewFactory.cs` - 预览工厂
  - `ImagePreview_1.0.2.cs` - 图片预览
  - `PdfPreview_1.0.2.cs` - PDF预览
  - `VideoPreview.cs` - 视频预览
  - `AudioPreview.cs` - 音频预览
  - `DocumentPreview.cs` - 文档预览
  - `ExcelPreview.cs` - Excel预览
  - `PowerPointPreview.cs` - PowerPoint预览
  - `CadPreview.cs` - CAD预览
  - `ArchivePreview.cs` - 压缩包预览
  - `FolderPreview.cs` - 文件夹预览
  - `TextPreview.cs` - 文本预览
  - `HtmlPreview.cs` - HTML预览
  - `XmlPreview.cs` - XML预览
  - `LnkPreview.cs` - 快捷方式预览
  - `PreviewHelper.cs` - 预览辅助工具

#### 视图模型层（ViewModels）

- ✅ **基础视图模型**：`ViewModels/BaseViewModel.cs`
  - 视图模型基类
  - 属性通知机制

- ✅ **文件列表视图模型**：`ViewModels/FileListViewModel.cs`
  - 文件列表数据绑定
  - 文件选择状态管理

- ✅ **库视图模型**：`ViewModels/LibraryViewModel.cs`
  - 库数据绑定
  - 库操作状态管理

- ✅ **导航视图模型**：`ViewModels/NavigationViewModel.cs`
  - 导航状态数据绑定
  - 导航历史管理

- ✅ **标签视图模型**：`ViewModels/TagViewModel.cs`
  - 标签数据绑定
  - 标签筛选状态管理

### 待拆分的主要模块

1. **文件列表加载服务**（FileListService）- 约 2000+ 行
   - 文件列表的加载和刷新
   - 文件系统监控
   - 文件夹大小计算

2. **搜索功能服务**（SearchService）- 约 800+ 行
   - 文件搜索功能
   - 搜索缓存管理
   - 搜索分页加载

3. **文件预览服务**（PreviewService）- 约 1000+ 行
   - 文件预览的加载和显示（预览提供者已拆分，但预览加载逻辑仍在 MainWindow）
   - 文件信息显示
   - 文件备注管理

4. **导航服务**（NavigationService）- 约 400+ 行
   - 路径导航（NavigationStateManager 已拆分，但导航逻辑仍在 MainWindow）
   - 导航历史管理
   - 导航模式切换

5. **UI 事件处理**（Event Handlers）- 约 2000+ 行
   - FileBrowser 事件处理
   - 键盘事件处理
   - 鼠标事件处理

6. **配置管理服务**（ConfigService）- 约 300+ 行
   - 配置的应用和保存
   - 窗口状态管理

## 拆分计划

### 第一阶段：数据加载服务（优先级：高）

#### 1. ✅ LibraryService.cs - 库管理服务（已完成）
**位置**：`Services/LibraryService.cs`

**状态**：✅ 已完成拆分

**职责**：
- 库的加载、创建、更新、删除
- 库文件的加载和合并显示
- 库的高亮显示

**已实现功能**：
- `LoadLibraries()` - 加载库列表
- `AddLibrary()` - 添加库
- `UpdateLibrary()` - 更新库
- `DeleteLibrary()` - 删除库
- `LoadLibraryFiles()` - 加载库文件
- `ShowMergedLibraryFiles()` - 显示合并的库文件
- `HighlightLibrary()` - 高亮库

**事件机制**：
- `LibrariesLoaded` - 库列表已加载
- `LibraryFilesLoaded` - 库文件已加载
- `LibraryHighlightRequested` - 库高亮请求

---

#### 2. ✅ TagService.cs - 标签管理服务（已完成）
**位置**：`Services/Tag/TagService.cs`

**状态**：✅ 已完成拆分

**职责**：
- 标签的加载和管理
- 标签筛选功能
- 标签的添加和批量操作

**已实现功能**：
- `LoadTags()` - 加载标签
- `CreateTag()` - 创建标签
- `UpdateTag()` - 更新标签
- `DeleteTag()` - 删除标签
- `FilterByTag()` - 按标签筛选
- `AddTagToFiles()` - 为文件添加标签
- `BatchAddTags()` - 批量添加标签

**事件机制**：
- `TagFilterRequested` - 标签筛选请求
- `TagTabRequested` - 标签页创建请求
- `TagsLoaded` - 标签列表已加载

---

#### 3. ✅ FavoriteService.cs - 收藏管理服务（已完成）
**位置**：`Services/Favorite/FavoriteService.cs`

**状态**：✅ 已完成拆分

**职责**：
- 收藏项的加载和管理
- 收藏项的拖拽排序
- 收藏项的添加和删除

**已实现功能**：
- `LoadFavorites()` - 加载收藏
- `AddFavorite()` - 添加收藏
- `DeleteFavorite()` - 删除收藏
- `UpdateFavoriteOrder()` - 更新收藏顺序
- 收藏拖拽排序功能

**事件机制**：
- `NavigateRequested` - 路径导航请求
- `FileOpenRequested` - 文件打开请求
- `CreateTabRequested` - 新标签页创建请求
- `FavoritesLoaded` - 收藏列表已加载

---

#### 4. ✅ QuickAccessService.cs - 快速访问服务（已完成）
**位置**：`Services/QuickAccess/QuickAccessService.cs`

**状态**：✅ 已完成拆分

**职责**：
- 快速访问项的加载
- 驱动器的加载

**已实现功能**：
- `LoadQuickAccess()` - 加载快速访问
- `LoadDrives()` - 加载驱动器

**事件机制**：
- `NavigateRequested` - 路径导航请求
- `CreateTabRequested` - 新标签页创建请求

---

### 第二阶段：UI 管理服务（优先级：高）

#### 5. ✅ TabManagerControl - 标签页管理控件（已完成）
**位置**：`Controls/TabManagerControl.xaml/cs`

**状态**：✅ 已完成拆分（作为控件实现）

**职责**：
- 标签页的创建、关闭、切换
- 标签页的拖拽排序
- 标签页的固定功能

**已实现功能**：
- 标签页的创建、关闭、切换
- 标签页的拖拽排序
- 标签页的固定功能
- 标签页显示标题管理

**说明**：标签页管理已作为控件拆分，但部分业务逻辑可能仍在 MainWindow 中

---

#### 6. FileListService.cs - 文件列表加载服务
**位置**：`Services/FileListService.cs`

**职责**：
- 文件列表的加载和刷新
- 文件系统监控
- 文件夹大小计算

**需要迁移的方法**：
- `LoadFiles()` - 加载文件列表
- `LoadCurrentDirectory()` - 加载当前目录
- `RefreshFileList()` - 刷新文件列表
- `SetupFileWatcher()` - 设置文件监控
- `OnFileSystemChanged()` - 文件系统变化处理
- 文件夹大小计算相关方法

**依赖关系**：
- `FileSystemWatcher` - 文件系统监控
- `DatabaseManager` - 文件夹大小缓存

---

#### 7. SearchService.cs - 搜索功能服务
**位置**：`Services/SearchService.cs`

**职责**：
- 文件搜索功能
- 搜索缓存管理
- 搜索分页加载

**需要迁移的方法**：
- `FileBrowser_SearchClicked()` - 搜索按钮点击
- `FileBrowser_FilterClicked()` - 筛选按钮点击
- `FileBrowser_LoadMoreClicked()` - 加载更多
- `CheckAndRefreshSearchTab()` - 检查并刷新搜索标签页
- `RefreshActiveSearchTab()` - 刷新活动搜索标签页
- `UpdateSearchCache()` - 更新搜索缓存

**依赖关系**：
- `EverythingHelper` - Everything 搜索工具

---

### 第三阶段：预览和导航服务（优先级：中）

#### 8. PreviewService.cs - 文件预览服务
**位置**：`Services/PreviewService.cs`

**职责**：
- 文件预览的加载和显示
- 文件信息显示
- 文件备注管理

**需要迁移的方法**：
- `LoadFilePreview()` - 加载文件预览
- `ShowFileInfo()` - 显示文件信息
- `LoadFileNotes()` - 加载文件备注
- `CleanupPreviousPreview()` - 清理之前的预览
- `AttachPreviewButtonEvents()` - 附加预览按钮事件

**依赖关系**：
- `PreviewFactory` - 预览工厂
- `RightPanelControl` - 右侧面板控件

---

#### 9. NavigationService.cs - 导航管理服务
**位置**：`Services/NavigationService.cs`

**职责**：
- 路径导航
- 导航历史管理
- 导航模式切换

**需要迁移的方法**：
- `NavigateToPath()` - 导航到路径
- `NavigateBack_Click()` - 后退
- `NavigateForward_Click()` - 前进
- `NavigateUp_Click()` - 向上
- `AddToHistory()` - 添加到历史
- `SwitchNavigationMode()` - 切换导航模式

**依赖关系**：
- `NavigationStateManager` - 导航状态管理

---

### 第四阶段：事件处理服务（优先级：中）

#### 10. FileBrowserEventHandler.cs - FileBrowser 事件处理
**位置**：`Handlers/FileBrowserEventHandler.cs`

**职责**：
- 处理 FileBrowser 控件的所有事件

**需要迁移的方法**：
- `FileBrowser_PathChanged()` - 路径变化
- `FileBrowser_FilesSelectionChanged()` - 文件选择变化
- `FileBrowser_FilesMouseDoubleClick()` - 双击文件
- `FileBrowser_GridViewColumnHeaderClick()` - 列头点击
- `FileBrowser_FilesSizeChanged()` - 文件列表大小变化

---

#### 11. KeyboardEventHandler.cs - 键盘事件处理
**位置**：`Handlers/KeyboardEventHandler.cs`

**职责**：
- 处理所有键盘快捷键

**需要迁移的方法**：
- `MainWindow_PreviewKeyDown()` - 预览按键
- `MainWindow_KeyDown()` - 按键处理

---

#### 12. MouseEventHandler.cs - 鼠标事件处理
**位置**：`Handlers/MouseEventHandler.cs`

**职责**：
- 处理所有鼠标事件

**需要迁移的方法**：
- `FilesListView_PreviewMouseLeftButtonDown()` - 鼠标左键按下
- `FilesListView_MouseDoubleClick()` - 双击
- 各种鼠标事件处理方法

---

### 第五阶段：UI 状态管理（优先级：低）

#### 13. ActionButtonService.cs - 操作按钮管理服务
**位置**：`Services/ActionButtonService.cs`

**职责**：
- 操作按钮的显示和更新
- 按钮拖拽功能

**需要迁移的方法**：
- `UpdateActionButtons()` - 更新操作按钮
- `RefreshActionButtons()` - 刷新操作按钮
- `GetClickHandlerByButtonName()` - 获取按钮点击处理器

---

#### 14. WindowStateService.cs - 窗口状态服务
**位置**：`Services/WindowStateService.cs`

**职责**：
- 窗口位置和大小管理
- 配置的应用和保存

**需要迁移的方法**：
- `ApplyConfig()` - 应用配置
- `SaveCurrentConfig()` - 保存当前配置
- 窗口状态相关方法

---

## 实施步骤

### 步骤 1：创建服务基类/接口（可选）

创建通用的服务接口，便于依赖注入和测试：

```csharp
// Services/IService.cs
public interface IService
{
    void Initialize();
    void Cleanup();
}
```

### 步骤 2：按模块逐步迁移

**推荐顺序**：

1. **LibraryService**（优先级最高）
   - 逻辑清晰，依赖较少
   - 功能相对独立
   - 便于测试

2. **FileListService**（核心功能）
   - 核心功能，但依赖较多
   - 需要仔细处理异步操作

3. **TabManagementService**（UI 相关）
   - UI 相关，但逻辑独立
   - 需要处理 UI 更新

4. **其他服务**（按优先级依次迁移）

### 步骤 3：重构 MainWindow

- 将 MainWindow 作为协调器（Coordinator）
- 各服务负责具体业务逻辑
- MainWindow 只负责 UI 绑定和事件转发

### 步骤 4：测试验证

- 每个服务迁移后测试对应功能
- 确保无回归问题
- 确保性能不受影响

## 注意事项

### 1. 保持向后兼容

- 拆分过程中不改变公共 API
- 保持原有功能不变
- 逐步迁移，避免大爆炸式重构

### 2. 依赖管理

- 注意服务间的依赖关系
- 避免循环依赖
- 使用依赖注入或事件机制解耦

### 3. 线程安全

- 文件加载、搜索等异步操作需注意线程安全
- UI 更新必须在 UI 线程
- 使用 Dispatcher 或 SynchronizationContext

### 4. 配置访问

- 服务可能需要访问 `AppConfig`
- 考虑通过接口注入或事件通知
- 避免直接访问全局配置

### 5. UI 控件访问

- 服务不应直接访问 UI 控件
- 通过事件、接口或回调函数传递
- 保持服务层的独立性

## 预期收益

1. **可维护性提升**：代码结构清晰，职责分明
2. **可测试性提升**：服务可以独立测试
3. **可扩展性提升**：新功能可以独立添加
4. **代码复用**：服务可以在其他地方复用
5. **团队协作**：不同开发者可以并行开发不同服务

## 进度跟踪

### 服务层（Services）

- [x] **LibraryService** - 库管理服务 ✅
- [x] **TagService** - 标签管理服务 ✅
- [x] **FavoriteService** - 收藏管理服务 ✅
- [x] **QuickAccessService** - 快速访问服务 ✅
- [x] **NavigationStateManager** - 导航状态管理 ✅
- [x] **DragDropManager** - 拖拽管理 ✅
- [x] **FileOperations** - 文件操作服务 ✅
  - [x] CopyOperation
  - [x] PasteOperation
  - [x] DeleteOperation
  - [x] RenameOperation
  - [x] NewFileOperation
  - [x] NewFolderOperation
  - [x] FileClipboardManager
- [ ] **FileListService** - 文件列表加载服务
- [ ] **SearchService** - 搜索功能服务
- [ ] **PreviewService** - 文件预览服务（预览提供者已拆分）
- [ ] **NavigationService** - 导航管理服务（部分已拆分）
- [ ] **ConfigService** - 配置管理服务

### 控件层（Controls）

- [x] **ActionButtonsControl** - 操作按钮控件 ✅
- [x] **AddressBarControl** - 地址栏控件 ✅
- [x] **FileBrowserControl** - 文件浏览器控件 ✅
- [x] **FileListControl** - 文件列表控件 ✅
- [x] **NavigationPanelControl** - 导航面板控件 ✅
- [x] **SettingsPanelControl** - 设置面板控件 ✅
- [x] **TabManagerControl** - 标签页管理控件 ✅
- [x] **WindowControlButtonsControl** - 窗口控制按钮控件 ✅

### 预览层（Previews）

- [x] **Preview Providers** - 预览提供者 ✅
  - [x] IPreviewProvider 接口
  - [x] PreviewFactory 工厂
  - [x] ImagePreview
  - [x] PdfPreview
  - [x] VideoPreview
  - [x] AudioPreview
  - [x] DocumentPreview
  - [x] ExcelPreview
  - [x] PowerPointPreview
  - [x] CadPreview
  - [x] ArchivePreview
  - [x] FolderPreview
  - [x] TextPreview
  - [x] HtmlPreview
  - [x] XmlPreview
  - [x] LnkPreview

### 视图模型层（ViewModels）

- [x] **BaseViewModel** - 基础视图模型 ✅
- [x] **FileListViewModel** - 文件列表视图模型 ✅
- [x] **LibraryViewModel** - 库视图模型 ✅
- [x] **NavigationViewModel** - 导航视图模型 ✅
- [x] **TagViewModel** - 标签视图模型 ✅

### 事件处理层（Handlers）

- [ ] **FileBrowserEventHandler** - FileBrowser 事件处理
- [ ] **KeyboardEventHandler** - 键盘事件处理
- [ ] **MouseEventHandler** - 鼠标事件处理

---

## 拆分统计

### 已完成拆分
- **服务**：7 个主要服务（LibraryService, TagService, FavoriteService, QuickAccessService, NavigationStateManager, DragDropManager, FileOperations）
- **控件**：8 个控件（ActionButtonsControl, AddressBarControl, FileBrowserControl, FileListControl, NavigationPanelControl, SettingsPanelControl, TabManagerControl, WindowControlButtonsControl）
- **预览**：15+ 个预览提供者
- **视图模型**：5 个视图模型

### 待拆分
- **服务**：5 个服务（FileListService, SearchService, PreviewService, NavigationService, ConfigService）
- **事件处理**：3 个事件处理器（FileBrowserEventHandler, KeyboardEventHandler, MouseEventHandler）

---

**最后更新**：2025-01-27  
**状态**：进行中（约 60% 完成）


