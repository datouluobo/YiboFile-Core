# MainWindow.xaml.cs 拆分计划

## 概述

`MainWindow.xaml.cs` 文件目前有 **12170 行代码**，包含了大量的业务逻辑和 UI 处理代码。为了提高代码的可维护性、可测试性和可扩展性，需要将其拆分为多个服务类和处理器。

## 当前状态

### 已拆分的模块

- ✅ **文件操作服务**：`Services/FileOperations/`（Copy, Paste, Delete, Rename 等）
- ✅ **导航状态管理**：`Services/NavigationStateManager.cs`
- ✅ **拖拽管理**：`Services/DragDropManager.cs`（已移动到 Services 文件夹）
- ✅ **部分控件**：`Controls/NavigationPanelControl`, `Controls/TabManagerControl`

### 待拆分的主要模块

1. **标签页管理**（Tab Management）- 约 500+ 行
2. **库管理**（Library Management）- 约 1000+ 行
3. **标签管理**（Tag Management）- 约 800+ 行
4. **收藏管理**（Favorite Management）- 约 500+ 行
5. **文件列表加载**（File Loading）- 约 2000+ 行
6. **文件预览**（File Preview）- 约 1000+ 行
7. **搜索功能**（Search）- 约 800+ 行
8. **UI 事件处理**（Event Handlers）- 约 2000+ 行
9. **配置管理**（Configuration）- 约 300+ 行
10. **导航历史**（Navigation History）- 约 400+ 行

## 拆分计划

### 第一阶段：数据加载服务（优先级：高）

#### 1. LibraryService.cs - 库管理服务
**位置**：`Services/LibraryService.cs`

**职责**：
- 库的加载、创建、更新、删除
- 库文件的加载和合并显示
- 库的高亮显示

**需要迁移的方法**：
- `LoadLibraries()` - 加载库列表
- `AddLibrary_Click()` - 添加库
- `ManageLibraries_Click()` - 管理库
- `LibraryRename_Click()` - 重命名库
- `LibraryDelete_Click()` - 删除库
- `LibraryManage_Click()` - 管理库（快捷方式）
- `LibraryOpenInExplorer_Click()` - 在资源管理器中打开
- `LibraryRefresh_Click()` - 刷新库列表
- `LoadLibraryFiles()` - 加载库文件
- `ShowMergedLibraryFiles()` - 显示合并的库文件
- `HighlightMatchingLibrary()` - 高亮匹配的库

**依赖关系**：
- `DatabaseManager` - 数据库操作
- `LibraryDialog` - 库创建对话框
- `LibraryManagementWindow` - 库管理窗口
- `FileBrowser` - 文件浏览器控件（UI 依赖，通过事件/接口注入）

**接口设计**：
```csharp
public interface ILibraryService
{
    event EventHandler<Library> LibrarySelected;
    event EventHandler LibraryListChanged;
    
    void LoadLibraries();
    int AddLibrary(string name, string initialPath = null);
    void UpdateLibrary(int libraryId, string newName);
    void DeleteLibrary(int libraryId);
    void LoadLibraryFiles(Library library);
    void HighlightLibrary(Library library);
}
```

---

#### 2. TagService.cs - 标签管理服务
**位置**：`Services/TagService.cs`

**职责**：
- 标签的加载和管理
- 标签筛选功能
- 标签的添加和批量操作

**需要迁移的方法**：
- `LoadTags()` - 加载标签
- `NewTag_Click()` - 新建标签
- `ManageTags_Click()` - 管理标签
- `FilterByTag()` - 按标签筛选
- `AddTagToFile_Click()` - 为文件添加标签
- `BatchAddTags_Click()` - 批量添加标签
- `TagStatistics_Click()` - 标签统计

**依赖关系**：
- `OoiMRRIntegration` - TagTrain 集成
- `TagDialog` - 标签对话框
- `TagManagementWindow` - 标签管理窗口

---

#### 3. FavoriteService.cs - 收藏管理服务
**位置**：`Services/FavoriteService.cs`

**职责**：
- 收藏项的加载和管理
- 收藏项的拖拽排序
- 收藏项的添加和删除

**需要迁移的方法**：
- `LoadFavorites()` - 加载收藏
- `AddFavorite_Click()` - 添加收藏
- 收藏拖拽排序相关方法

**依赖关系**：
- `DatabaseManager` - 数据库操作

---

#### 4. QuickAccessService.cs - 快速访问服务
**位置**：`Services/QuickAccessService.cs`

**职责**：
- 快速访问项的加载
- 驱动器的加载

**需要迁移的方法**：
- `LoadQuickAccess()` - 加载快速访问
- `LoadDrives()` - 加载驱动器

---

### 第二阶段：UI 管理服务（优先级：高）

#### 5. TabManagementService.cs - 标签页管理服务
**位置**：`Services/TabManagementService.cs`

**职责**：
- 标签页的创建、关闭、切换
- 标签页的拖拽排序
- 标签页的固定功能

**需要迁移的方法**：
- `CreateTab()` - 创建标签页
- `CloseTab()` - 关闭标签页
- `SwitchToTab()` - 切换到标签页
- `InitializeTabsDragDrop()` - 初始化标签页拖拽
- `TogglePinTab()` - 固定/取消固定标签页
- `ReorderTabs()` - 重新排序标签页
- `RenameDisplayTitle()` - 重命名显示标题

**依赖关系**：
- `PathTab` 类（需要提取为独立类或放在服务中）

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

- [ ] LibraryService - 库管理服务
- [ ] TagService - 标签管理服务
- [ ] FavoriteService - 收藏管理服务
- [ ] QuickAccessService - 快速访问服务
- [ ] TabManagementService - 标签页管理服务
- [ ] FileListService - 文件列表加载服务
- [ ] SearchService - 搜索功能服务
- [ ] PreviewService - 文件预览服务
- [ ] NavigationService - 导航管理服务
- [ ] FileBrowserEventHandler - FileBrowser 事件处理
- [ ] KeyboardEventHandler - 键盘事件处理
- [ ] MouseEventHandler - 鼠标事件处理
- [ ] ActionButtonService - 操作按钮管理服务
- [ ] WindowStateService - 窗口状态服务

---

**最后更新**：2025-01-XX  
**状态**：进行中

