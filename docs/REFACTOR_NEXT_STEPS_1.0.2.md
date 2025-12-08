# MainWindow 拆分后续计划

## 当前进度

**已完成拆分**：约 60%
- ✅ 7 个主要服务（LibraryService, TagService, FavoriteService, QuickAccessService, NavigationStateManager, DragDropManager, FileOperations）
- ✅ 8 个控件（ActionButtonsControl, AddressBarControl, FileBrowserControl, FileListControl, NavigationPanelControl, SettingsPanelControl, TabManagerControl, WindowControlButtonsControl）
- ✅ 15+ 个预览提供者
- ✅ 5 个视图模型

**待拆分**：约 40%
- MainWindow.xaml.cs 仍有约 10700+ 行代码
- 核心业务逻辑和事件处理仍集中在 MainWindow

---

## 后续拆分计划（按优先级）

### 阶段一：核心服务拆分（优先级：最高）

#### 1. FileListService - 文件列表加载服务
**位置**：`Services/FileListService.cs`

**优先级**：⭐⭐⭐⭐⭐（最高）

**需要迁移的方法**：
- `LoadFiles()` - 文件列表加载（约 600 行）
- `LoadCurrentDirectory()` - 加载当前目录
- `RefreshFileList()` - 刷新文件列表
- `SetupFileWatcher()` - 设置文件系统监控
- `OnFileSystemChanged()` - 文件系统变化处理
- `CalculateAndUpdateFolderSize()` - 文件夹大小计算
- `CalculateAllSubfolderSizesOnFirstOpen()` - 首次打开时计算所有子文件夹大小
- `CalculateSubfolderSizesBatch()` - 批量计算子文件夹大小
- `CleanupFolderSizeCacheOnStartup()` - 启动时清理文件夹大小缓存

**需要迁移的字段**：
- `_currentFiles` - 当前文件列表
- `_isLoadingFiles` - 加载标志
- `_loadFilesSemaphore` - 加载信号量
- `_fileWatcher` - 文件系统监控器
- `_refreshDebounceTimer` - 刷新防抖定时器
- `_folderSizeCalculationSemaphore` - 文件夹大小计算信号量
- `_folderSizeCalculationCancellation` - 文件夹大小计算取消令牌
- `_pendingFolderSizeCalculations` - 待计算的文件夹队列
- `_idleFolderSizeCalculationTimer` - 闲置计算定时器

**事件设计**：
```csharp
public event EventHandler<List<FileSystemItem>> FilesLoaded;
public event EventHandler<string> FileSystemChanged;
public event EventHandler<FileSystemItem> FolderSizeCalculated;
```

**依赖关系**：
- `DatabaseManager` - 文件夹大小缓存
- `FileBrowserControl` - 文件列表显示（通过事件注入）

**预计工作量**：2-3 天

---

#### 2. SearchService - 搜索功能服务
**位置**：`Services/SearchService.cs`

**优先级**：⭐⭐⭐⭐（高）

**需要迁移的方法**：
- `FileBrowser_SearchClicked()` - 搜索按钮点击
- `FileBrowser_FilterClicked()` - 筛选按钮点击
- `FileBrowser_LoadMoreClicked()` - 加载更多
- `PerformSearch()` - 执行搜索
- `CheckAndRefreshSearchTab()` - 检查并刷新搜索标签页
- `RefreshActiveSearchTab()` - 刷新活动搜索标签页
- `UpdateSearchCache()` - 更新搜索缓存
- `ApplyTypeFilter()` - 应用类型筛选
- `BuildItemsFromPaths()` - 从路径构建文件项

**需要迁移的字段**：
- `_searchOffset` - 搜索偏移量
- `_searchPageSize` - 搜索分页大小
- `_searchMax` - 搜索最大结果数
- `_searchKeywordPaging` - 搜索关键词（分页）
- `_searchHasMore` - 是否有更多结果
- `_searchCacheTTL` - 搜索缓存TTL
- `_searchCache` - 搜索缓存字典
- `_searchOptions` - 搜索选项
- `_searchCts` - 搜索取消令牌

**事件设计**：
```csharp
public event EventHandler<SearchResultEventArgs> SearchCompleted;
public event EventHandler<SearchResultEventArgs> SearchPageLoaded;
public event EventHandler<string> SearchTabRefreshRequested;
```

**依赖关系**：
- `EverythingHelper` - Everything 搜索工具
- `FileBrowserControl` - 搜索结果显示（通过事件注入）

**预计工作量**：1-2 天

---

#### 3. PreviewService - 文件预览服务
**位置**：`Services/PreviewService.cs`

**优先级**：⭐⭐⭐⭐（高）

**需要迁移的方法**：
- `LoadFilePreview()` - 加载文件预览
- `ShowFileInfo()` - 显示文件信息
- `LoadFileNotes()` - 加载文件备注
- `CleanupPreviousPreview()` - 清理之前的预览
- `ClearPreviewAndInfo()` - 清空预览和信息

**需要迁移的字段**：
- `_previousPreviewProvider` - 之前的预览提供者（如果存在）

**事件设计**：
```csharp
public event EventHandler<FileSystemItem> PreviewRequested;
public event EventHandler PreviewCleared;
public event EventHandler<FileInfoEventArgs> FileInfoRequested;
```

**依赖关系**：
- `PreviewFactory` - 预览工厂（已拆分）
- `RightPanelControl` - 右侧面板控件（通过事件注入）

**预计工作量**：1 天

---

### 阶段二：导航和配置服务（优先级：中高）

#### 4. NavigationService - 导航管理服务
**位置**：`Services/NavigationService.cs`

**优先级**：⭐⭐⭐（中高）

**需要迁移的方法**：
- `NavigateToPath()` - 导航到路径
- `NavigateToPathInternal()` - 导航到路径（内部实现）
- `NavigateBack_Click()` - 后退
- `NavigateForward_Click()` - 前进
- `NavigateUp_Click()` - 向上
- `AddToHistory()` - 添加到历史
- `SwitchNavigationMode()` - 切换导航模式
- `HighlightMatchingItems()` - 高亮匹配项
- `ClearItemHighlights()` - 清空高亮
- `UpdateAddressBar()` - 更新地址栏
- `UpdateBreadcrumb()` - 更新面包屑

**需要迁移的字段**：
- `_currentPath` - 当前路径
- `_navigationHistory` - 导航历史
- `_currentHistoryIndex` - 当前历史索引
- `_lastLeftNavSource` - 最后左侧导航源

**事件设计**：
```csharp
public event EventHandler<string> NavigationRequested;
public event EventHandler<string> PathChanged;
public event EventHandler<string> NavigationModeChanged;
public event EventHandler<string> AddressBarUpdateRequested;
public event EventHandler<string> BreadcrumbUpdateRequested;
```

**依赖关系**：
- `NavigationStateManager` - 导航状态管理（已拆分）
- `FileListService` - 文件列表服务（依赖）
- `AddressBarControl` - 地址栏控件（通过事件注入）

**预计工作量**：1-2 天

---

#### 5. ConfigService - 配置管理服务
**位置**：`Services/ConfigService.cs`

**优先级**：⭐⭐⭐（中）

**需要迁移的方法**：
- `ApplyConfig()` - 应用配置
- `SaveCurrentConfig()` - 保存当前配置
- `UpdateActionButtons()` - 更新操作按钮
- `RefreshActionButtons()` - 刷新操作按钮
- `GetClickHandlerByButtonName()` - 获取按钮点击处理器

**需要迁移的字段**：
- `_config` - 应用配置
- `_isApplyingConfig` - 是否正在应用配置
- `_saveTimer` - 保存定时器
- `_columnWidthSaveTimer` - 列宽保存定时器

**事件设计**：
```csharp
public event EventHandler<AppConfig> ConfigApplied;
public event EventHandler<AppConfig> ConfigSaved;
public event EventHandler<string> ActionButtonsUpdateRequested;
```

**依赖关系**：
- `AppConfig` - 配置类
- `ActionButtonsControl` - 操作按钮控件（通过事件注入）

**预计工作量**：1 天

---

### 阶段三：事件处理拆分（优先级：中）

#### 6. FileBrowserEventHandler - FileBrowser 事件处理
**位置**：`Handlers/FileBrowserEventHandler.cs`

**优先级**：⭐⭐⭐（中）

**需要迁移的方法**：
- `FileBrowser_PathChanged()` - 路径变化
- `FileBrowser_BreadcrumbClicked()` - 面包屑点击
- `FileBrowser_FilesSelectionChanged()` - 文件选择变化
- `FileBrowser_FilesMouseDoubleClick()` - 双击文件
- `FileBrowser_FilesPreviewMouseDoubleClick()` - 预览双击
- `FileBrowser_FilesPreviewKeyDown()` - 预览按键
- `FileBrowser_FilesPreviewMouseLeftButtonDown()` - 预览鼠标左键按下
- `FileBrowser_FilesMouseLeftButtonUp()` - 鼠标左键释放
- `FileBrowser_FilesPreviewMouseDown()` - 预览鼠标按下
- `FileBrowser_GridViewColumnHeaderClick()` - 列头点击
- `FileBrowser_FilesSizeChanged()` - 文件列表大小变化
- `FilesListView_SelectionChanged()` - 列表选择变化
- `FilesListView_PreviewMouseDoubleClick()` - 列表预览双击
- `FilesListView_MouseDoubleClick()` - 列表双击
- `HandleDoubleClick()` - 处理双击

**需要迁移的字段**：
- `_lastSortColumn` - 最后排序列
- `_sortAscending` - 排序方向
- `_mouseDownPoint` - 鼠标按下点
- `_isMouseDownOnListView` - 是否在列表视图上按下鼠标
- `_isMouseDownOnColumnHeader` - 是否在列头上按下鼠标

**事件设计**：
```csharp
public event EventHandler<string> PathChangeRequested;
public event EventHandler<FileSystemItem> FileDoubleClickRequested;
public event EventHandler<SelectionChangedEventArgs> SelectionChanged;
public event EventHandler<string> SortRequested;
```

**依赖关系**：
- `FileBrowserControl` - 文件浏览器控件
- `FileListService` - 文件列表服务
- `PreviewService` - 预览服务
- `NavigationService` - 导航服务

**预计工作量**：2 天

---

#### 7. KeyboardEventHandler - 键盘事件处理
**位置**：`Handlers/KeyboardEventHandler.cs`

**优先级**：⭐⭐（中低）

**需要迁移的方法**：
- `MainWindow_PreviewKeyDown()` - 预览按键
- `MainWindow_KeyDown()` - 按键处理

**需要迁移的字段**：
- 无（主要处理快捷键）

**事件设计**：
```csharp
public event EventHandler<KeyEventArgs> KeyDown;
public event EventHandler<KeyEventArgs> PreviewKeyDown;
```

**依赖关系**：
- `FileListService` - 文件列表服务
- `NavigationService` - 导航服务
- `FileOperations` - 文件操作服务

**预计工作量**：1 天

---

#### 8. MouseEventHandler - 鼠标事件处理
**位置**：`Handlers/MouseEventHandler.cs`

**优先级**：⭐⭐（中低）

**需要迁移的方法**：
- `FilesListView_PreviewMouseLeftButtonDown()` - 列表预览鼠标左键按下
- `FilesListView_PreviewMouseDown()` - 列表预览鼠标按下
- `DrivesListBox_PreviewMouseDown()` - 驱动器列表预览鼠标按下
- `QuickAccessListBox_PreviewMouseDown()` - 快速访问列表预览鼠标按下
- `GridSplitter_DragDelta()` - 分割器拖拽

**需要迁移的字段**：
- `_isSplitterDragging` - 是否正在拖拽分割器

**事件设计**：
```csharp
public event EventHandler<MouseButtonEventArgs> MouseLeftButtonDown;
public event EventHandler<MouseButtonEventArgs> MouseDown;
```

**依赖关系**：
- `FileBrowserControl` - 文件浏览器控件
- `QuickAccessService` - 快速访问服务

**预计工作量**：1 天

---

### 阶段四：标签页管理优化（优先级：低）

#### 9. TabService - 标签页管理服务（优化）
**位置**：`Services/TabService.cs`

**优先级**：⭐⭐（低）

**说明**：虽然 `TabManagerControl` 已拆分，但标签页的业务逻辑（创建、切换、关闭等）仍在 MainWindow 中。

**需要迁移的方法**：
- `CreateTab()` - 创建标签页
- `CreateTabInternal()` - 创建标签页（内部实现）
- `OpenLibraryInTab()` - 在标签页中打开库
- `OpenTagInTab()` - 在标签页中打开标签
- `CloseTab()` - 关闭标签页
- `SwitchToTab()` - 切换到标签页
- `TabButton_Click()` - 标签页按钮点击
- `UpdateTabTitle()` - 更新标签页标题
- `RenameDisplayTitle()` - 重命名显示标题
- `TogglePinTab()` - 固定/取消固定标签页
- `ReorderTabs()` - 重新排序标签页
- `ApplyTabOverrides()` - 应用标签页覆盖
- `SetupLibraryTabs()` - 设置库标签页
- `ClearTabsInLibraryMode()` - 清空库模式下的标签页
- `UpdateTabStyles()` - 更新标签页样式

**需要迁移的字段**：
- `_tabs` - 标签页列表
- `_currentTab` - 当前标签页

**事件设计**：
```csharp
public event EventHandler<TabEventArgs> TabCreated;
public event EventHandler<TabEventArgs> TabClosed;
public event EventHandler<TabEventArgs> TabSwitched;
public event EventHandler<TabEventArgs> TabPinned;
```

**依赖关系**：
- `TabManagerControl` - 标签页管理控件
- `LibraryService` - 库管理服务
- `TagService` - 标签管理服务

**预计工作量**：1-2 天

---

## 实施建议

### 拆分顺序

1. **FileListService**（最高优先级）
   - 核心功能，影响面大
   - 完成后可大幅减少 MainWindow 代码量

2. **SearchService**（高优先级）
   - 功能相对独立
   - 搜索逻辑复杂，拆分后便于维护

3. **PreviewService**（高优先级）
   - 预览提供者已拆分，只需迁移加载逻辑
   - 工作量较小

4. **NavigationService**（中高优先级）
   - 与 FileListService 有依赖关系
   - 导航逻辑相对独立

5. **ConfigService**（中优先级）
   - 配置管理逻辑简单
   - 工作量小

6. **事件处理器**（中优先级）
   - 可以并行拆分
   - 拆分后 MainWindow 代码量显著减少

7. **TabService**（低优先级）
   - 标签页控件已拆分，主要是业务逻辑迁移
   - 可以最后处理

### 拆分原则

1. **保持向后兼容**
   - 拆分过程中不改变公共 API
   - 保持原有功能不变

2. **使用事件机制解耦**
   - 服务通过事件通知 MainWindow
   - MainWindow 通过事件注入 UI 依赖

3. **逐步迁移**
   - 每次拆分一个服务
   - 拆分后立即测试验证

4. **保持线程安全**
   - UI 更新必须在 UI 线程
   - 使用 Dispatcher 或 SynchronizationContext

### 测试策略

1. **单元测试**
   - 每个服务拆分后编写单元测试
   - 测试核心业务逻辑

2. **集成测试**
   - 测试服务与 MainWindow 的集成
   - 测试事件机制

3. **功能测试**
   - 测试原有功能是否正常
   - 确保无回归问题

---

## 预期收益

### 代码量减少
- **当前**：MainWindow.xaml.cs 约 10700+ 行
- **目标**：MainWindow.xaml.cs 约 2000-3000 行
- **减少**：约 70-80% 代码量

### 可维护性提升
- 代码结构清晰，职责分明
- 每个服务独立，便于理解和修改

### 可测试性提升
- 服务可以独立测试
- 减少 UI 依赖，便于单元测试

### 可扩展性提升
- 新功能可以独立添加
- 服务可以在其他地方复用

---

## 时间估算

| 阶段 | 服务 | 预计工作量 | 累计时间 |
|------|------|-----------|---------|
| 阶段一 | FileListService | 2-3 天 | 2-3 天 |
| 阶段一 | SearchService | 1-2 天 | 3-5 天 |
| 阶段一 | PreviewService | 1 天 | 4-6 天 |
| 阶段二 | NavigationService | 1-2 天 | 5-8 天 |
| 阶段二 | ConfigService | 1 天 | 6-9 天 |
| 阶段三 | FileBrowserEventHandler | 2 天 | 8-11 天 |
| 阶段三 | KeyboardEventHandler | 1 天 | 9-12 天 |
| 阶段三 | MouseEventHandler | 1 天 | 10-13 天 |
| 阶段四 | TabService | 1-2 天 | 11-15 天 |

**总计**：约 11-15 个工作日（2-3 周）

---

## 风险与应对

### 风险 1：依赖关系复杂
**应对**：使用事件机制解耦，避免直接依赖

### 风险 2：线程安全问题
**应对**：统一使用 Dispatcher 进行 UI 更新

### 风险 3：功能回归
**应对**：每次拆分后立即测试，确保功能正常

### 风险 4：性能影响
**应对**：保持原有异步逻辑，避免阻塞 UI 线程

---

**最后更新**：2025-01-27  
**状态**：计划中



