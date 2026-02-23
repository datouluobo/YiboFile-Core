# 配置系统架构规范 (Configuration Architecture Specification)

| 版本 | 日期 | 作者 | 状态 | 关联文档 |
|------|------|------|------|----------|
| v1.0 | 2026-02-18 | Antigravity | **设计稿** | Refactoring_Tasks.md (P2-1) |

---

## 1. 概述 (Overview)

本文档定义 **YiboFile** 配置系统的目标架构、数据分层策略、存储方案和导入导出设计。  
目标是解决现有系统中配置管理器并行、模型单体化、路径硬编码和导入导出碎片化等问题。

### 1.1 核心设计原则

1. **单一数据源 (Single Source of Truth)**：应用配置统一由 `IConfigurationService` 管理，消除双管理器并行
2. **按本质分层 (Layered by Nature)**：数据按其本质属性分类，而非按技术实现分类
3. **路径集中管理 (Centralized Path Management)**：所有文件/目录路径通过 `IConfigPathProvider` 获取，禁止硬编码
4. **导出粒度可控 (Granular Export)**：用户可按模块选择性导出，而非全量打包
5. **向前兼容 (Forward Compatibility)**：支持从旧格式 (`ooi_config.json`) 自动迁移

---

## 2. 数据分层模型 (Data Layering Model)

所有持久化数据按其**本质属性**分为 5 个层次：

### 层次 1：用户偏好 (User Preferences)

> 用户主动配置的外观、行为、快捷键等设置

| 配置域 | 包含项 | 说明 |
| :--- | :--- | :--- |
| `appearance` | ThemeMode, WindowOpacity, AnimationsEnabled, IconStyle, CustomThemeId | 外观与主题 |
| `behavior` | ReuseTabTimeWindow, AlwaysReuseTab, NeverReuseTab, ActivateNewTabOnMiddleClick, EnableMultiWindow, TabWidthMode, PinnedTabWidth | 操作行为 |
| `fonts` | UIFontSize, TagFontSize, TagBoxWidth | 字体与布局尺寸 |
| `search` | IsEnableFullTextSearch, FullTextIndexPaths, HistoryMaxCount, AutoExpandHistory | 搜索策略 |
| `hotkeys` | CustomHotkeys | 自定义快捷键映射 |
| `navigation` | NavigationSectionsOrder | 导航栏区域排序 |
| `backup` | BackupDirectory, BackupRetentionDays | 备份策略 |
| `tabs` | PinnedTabs, TabTitleOverrides | 固定标签页 |

**特征**：轻量、稳定、跨设备同步价值高  
**存储**：`yibofile_settings.json`  
**导出**：✅ 支持

### 层次 2：组织结构数据 (Organizational Structure)

> 库 / 收藏夹 / 标签定义 — 用户构建的分类组织体系，存储的是路径与名称

| 数据 | 内容描述 | 依赖关系 |
| :--- | :--- | :--- |
| Libraries | `{ name, sortOrder }` | 依赖目录路径存在 |
| LibraryPaths | `{ libraryId, path }` | 依赖目录路径存在 |
| FavoriteGroups | `{ name, sortOrder }` | 无外部依赖 |
| Favorites | `{ path, isDirectory, displayName, groupId, sortOrder }` | 依赖路径存在 |
| TagGroups | `{ name, color }` | 无外部依赖 |
| Tags | `{ name, color, groupId }` | 依赖 TagGroups |

**特征**：轻量（通常 < 50KB），依赖目录/路径存在性  
**存储**：`yibofile_data.db`（SQLite）  
**导出**：✅ 支持（序列化为 JSON）  
**导入时路径处理**：弹窗统计"成功/缺失"，缺失项提供三种选项：忽略 / 创建目录 / 路径映射

### 层次 3：文件关联数据 (Per-File Association Data)

> 绑定到具体文件路径的附加元数据

| 数据 | 内容描述 | 体量特征 |
| :--- | :--- | :--- |
| FileTags | `{ filePath, tagId }` | 可能上万条 |
| Notes | `{ filePath, content, updatedAt }` | 不确定（取决于用户备注习惯） |

**特征**：
- 强依赖具体文件路径存在
- 体量不可预测
- 跨设备迁移困难（路径不同）
- 备注与文件标签关联属于同一类数据（都是附着在文件上的元数据）

**存储**：`yibofile_data.db`（SQLite，与层次 2 同一数据库）  
**导出**：✅ 支持（序列化为 JSON，独立于层次 2）  
**未来方向**：后续版本可考虑将元数据写入文件自身属性（如 NTFS ADS）以实现随文件迁移，但需解决云同步和跨文件系统兼容性问题，当前版本暂不实现

### 层次 4：应用状态 (Application State)

> 运行时状态的持久化，仅本机有意义

| 状态域 | 包含项 |
| :--- | :--- |
| `window` | WindowWidth, WindowHeight, WindowTop, WindowLeft, IsMaximized |
| `layout` | ColLeftWidth, ColCenterWidth, ColRightWidth, IsSidebarCollapsed, IsPreviewCollapsed, IsRightPanelVisible, RightPanelNotesHeight, CenterPanelInfoHeight, IsDualListMode |
| `columns` | ColNameWidth, ColSizeWidth, ColDateWidth, ColTypeWidth, ColNotesWidth, ColumnOrder, VisibleColumns_* |
| `session` | LastPath, LastNavigationMode, LastLibraryId, OpenTabs, ActiveTabKey, OpenTabsSecondary, ActiveTabKeySecondary |
| `view` | FileViewMode, SortColumn, SortDirection |
| `sidebar` | SidebarExpanderStates |
| `misc` | BackupBrowserWidth, BackupBrowserHeight |

**特征**：高频变动、仅本机有意义  
**存储**：`yibofile_state.json`  
**导出**：❌ 不支持

### 层次 5：本机运行数据 (Local Runtime Data)

> 本机积累的缓存和历史，丢失后可重建

| 数据 | 内容描述 | 存储 |
| :--- | :--- | :--- |
| 搜索/地址栏历史 | 搜索查询、路径输入、全文搜索记录（合并管理） | `yibofile_history.json` |
| 文件夹大小缓存 | FolderSizes 表 | `yibofile_data.db` 内 |

**特征**：纯本机积累，无迁移价值  
**导出**：❌ 不支持  
**说明**：搜索历史和地址栏历史合并到同一个 JSON 文件统一管理，因为它们都来自地址栏输入，合并对用户体验更好

### 层次 6（特殊）：自定义主题 (Custom Themes)

> 用户创建的自定义颜色主题

**存储**：`<BaseDirectory>/CustomThemes/{uuid}.json`  
**导出**：✅ 支持（打包整个文件夹）

---

## 3. 存储架构 (Storage Architecture)

### 3.1 目录结构

```
<BaseDirectory>/                            ← 用户可配置，默认 ./AppData/
│
├── yibofile_settings.json                  ← 层次1: 用户偏好
├── yibofile_state.json                     ← 层次4: 应用状态
├── yibofile_data.db                        ← 层次2+3+5(缓存): SQLite 核心数据
├── yibofile_history.json                   ← 层次5: 搜索/地址栏历史
│
└── CustomThemes/                           ← 层次6: 自定义主题
    ├── {uuid1}.json
    └── {uuid2}.json
```

### 3.2 文件命名规范

- **统一前缀**：所有由 YiboFile 管理的文件使用 `yibofile_` 前缀
- **旧前缀淘汰**：`ooi_` 前缀的文件名在迁移完成后不再使用
- **自动迁移**：首次启动时检测旧文件并自动迁移（重命名 + 内容拆分）

### 3.3 SQLite 数据库策略

采用 **方案 2C：SQLite 统一存储 + JSON 导出分组**。

- **物理上**：单一数据库 `yibofile_data.db`
- **逻辑上**：表按数据层次分组
- **导出时**：按层次序列化为独立 JSON，不导出整个数据库文件

优势：
1. 保留 SQLite JOIN 的查询便利性
2. 事务一致性有天然保障
3. 启动时单一数据库连接
4. 导出粒度由导出逻辑控制，灵活度高

```
yibofile_data.db
│
│  ─── 组织结构 (层次2，导出到 structure 模块) ───
├── Libraries (id, name, sortOrder)
├── LibraryPaths (id, libraryId, path)
├── FavoriteGroups (id, name, sortOrder, createdAt)
├── Favorites (id, path, isDirectory, displayName, groupId, sortOrder)
├── TagGroups (id, name, color)
├── Tags (id, name, color, groupId)
│
│  ─── 文件关联 (层次3，导出到 filedata 模块) ───
├── FileTags (filePath, tagId)
├── Notes (filePath, content, updatedAt)
│
│  ─── 本机缓存 (层次5，不导出) ───
└── FolderSizes (folderPath, size, updatedAt)
```

### 3.4 JSON 文件结构预览

#### `yibofile_settings.json`

```json
{
  "$schema": "yibofile-settings-v1",
  "appearance": {
    "themeMode": "Dark",
    "windowOpacity": 0.98,
    "animationsEnabled": true,
    "iconStyle": "Colored",
    "customThemeId": null
  },
  "behavior": {
    "reuseTabTimeWindow": 1000,
    "alwaysReuseTab": false,
    "neverReuseTab": false,
    "activateNewTabOnMiddleClick": false,
    "enableMultiWindow": false,
    "tabWidthMode": "FixedWidth",
    "pinnedTabWidth": 150
  },
  "fonts": {
    "uiFontSize": 14,
    "tagFontSize": 12,
    "tagBoxWidth": 0
  },
  "search": {
    "isEnableFullTextSearch": false,
    "fullTextIndexPaths": [],
    "historyMaxCount": 50,
    "autoExpandHistory": true
  },
  "hotkeys": {},
  "navigation": {
    "sectionsOrder": ["Drives", "QuickAccess"]
  },
  "backup": {
    "backupDirectory": "",
    "retentionDays": 30
  },
  "tabs": {
    "pinnedTabs": [],
    "tabTitleOverrides": {}
  }
}
```

#### `yibofile_state.json`

```json
{
  "$schema": "yibofile-state-v1",
  "window": {
    "width": 1280, "height": 720, "top": 100, "left": 100,
    "isMaximized": false
  },
  "layout": {
    "colLeftWidth": 200, "colCenterWidth": 600, "colRightWidth": 300,
    "isSidebarCollapsed": false, "isPreviewCollapsed": true,
    "isRightPanelVisible": false, "isDualListMode": false,
    "rightPanelNotesHeight": 200, "centerPanelInfoHeight": 28
  },
  "columns": {
    "colNameWidth": 250, "colSizeWidth": 80, "colDateWidth": 150,
    "colTypeWidth": 80, "colNotesWidth": 150,
    "columnOrder": "Name,Size,Date,Type,Notes",
    "visibleColumns": {}
  },
  "session": {
    "lastPath": "C:\\",
    "lastNavigationMode": "FileSystem",
    "lastLibraryId": -1,
    "openTabs": [], "activeTabKey": "",
    "openTabsSecondary": [], "activeTabKeySecondary": ""
  },
  "view": {
    "fileViewMode": "Details",
    "sortColumn": "Name", "sortDirection": "Ascending"
  },
  "sidebar": {
    "expanderStates": {}
  }
}
```

#### `yibofile_history.json`

```json
{
  "$schema": "yibofile-history-v1",
  "items": [
    { "type": "Search", "content": "*.pdf", "timestamp": "2026-02-18T12:00:00" },
    { "type": "LocalPath", "content": "D:\\Projects", "timestamp": "2026-02-18T11:55:00" },
    { "type": "FullTextSearch", "content": "报告", "timestamp": "2026-02-18T11:50:00" }
  ]
}
```

---

## 4. 路径管理 (Path Management)

### 4.1 IConfigPathProvider 接口

所有配置/数据文件的路径必须通过此接口获取，**禁止在任何服务中硬编码路径**。

```csharp
public interface IConfigPathProvider
{
    /// <summary>用户可配置的基础目录（默认 ./AppData/）</summary>
    string BaseDirectory { get; }

    /// <summary>用户偏好文件路径</summary>
    string SettingsFilePath { get; }        // → {Base}/yibofile_settings.json

    /// <summary>应用状态文件路径</summary>
    string StateFilePath { get; }           // → {Base}/yibofile_state.json

    /// <summary>核心数据库路径</summary>
    string DatabaseFilePath { get; }        // → {Base}/yibofile_data.db

    /// <summary>搜索/地址栏历史文件路径</summary>
    string HistoryFilePath { get; }         // → {Base}/yibofile_history.json

    /// <summary>自定义主题目录路径</summary>
    string CustomThemesDirectory { get; }   // → {Base}/CustomThemes/

    /// <summary>备份目录路径（可由用户独立配置）</summary>
    string BackupDirectory { get; }
}
```

### 4.2 需要修复的硬编码路径

以下服务当前存在路径硬编码，需迁移到 `IConfigPathProvider`：

| 服务 | 当前硬编码 | 目标 |
| :--- | :--- | :--- |
| `SearchHistoryService` | `%AppData%/YiboFile/search_history.json` | `pathProvider.HistoryFilePath` |
| `CustomThemeManager` | `%AppData%/YiboFile/CustomThemes/` | `pathProvider.CustomThemesDirectory` |
| `DwgConverter` | `%AppData%/YiboFile/Cache/DWGtoDXF/` | `pathProvider.CacheDirectory` (可扩展) |
| `CadImageCache` | `%AppData%/YiboFile/Cache/CAD/` | `pathProvider.CacheDirectory` (可扩展) |

---

## 5. 配置服务架构 (Configuration Service Architecture)

### 5.1 统一为 IConfigurationService

合并现有的 `ConfigManager`（静态类）和 `ConfigurationService`（单例服务）为统一的 `IConfigurationService` 接口。

```
旧架构:
  ConfigManager (static)  ←→  ConfigurationService (singleton)
      ↓                           ↓
  AppConfig (monolithic)     AppConfig (same instance)

新架构:
  IConfigurationService (interface)
      ↓
  ConfigurationService (singleton, implements IConfigurationService)
      ├── UserSettings (层次1, 来自 yibofile_settings.json)
      ├── AppState     (层次4, 来自 yibofile_state.json)
      └── IConfigPathProvider (路径管理)
```

### 5.2 接口设计要点

```csharp
public interface IConfigurationService
{
    // --- 用户偏好 (Settings) ---
    T GetSetting<T>(Expression<Func<UserSettings, T>> selector);
    void SetSetting<T>(Expression<Func<UserSettings, T>> selector, T value);
    void UpdateSettings(Action<UserSettings> updateAction);
    UserSettings GetSettingsSnapshot();

    // --- 应用状态 (State) ---
    T GetState<T>(Expression<Func<AppState, T>> selector);
    void SetState<T>(Expression<Func<AppState, T>> selector, T value);
    void UpdateState(Action<AppState> updateAction);

    // --- 持久化控制 ---
    void SaveSettingsNow();     // 立即保存用户偏好（带 debounce 的手动触发）
    void SaveStateNow();        // 立即保存应用状态
    void Shutdown();            // 关闭时保存所有未持久化数据

    // --- 路径管理 ---
    IConfigPathProvider PathProvider { get; }
}
```

### 5.3 保存策略

| 数据类型 | 保存触发 | 策略 |
| :--- | :--- | :--- |
| `yibofile_settings.json` | 用户修改设置时 | Debounce（500ms 延迟合并写入） |
| `yibofile_state.json` | 窗口/布局变更时 | Lazy Save（关闭时统一写入 + 异常保护定时写入） |
| `yibofile_history.json` | 每次新增历史项 | 即时写入（量小、频率低） |
| `yibofile_data.db` | 数据变更时 | SQLite 事务（已有机制） |

---

## 6. 导出中心设计 (Export Center Design)

### 6.1 位置

设置页面中的独立面板（与常规/外观/搜索/快捷键等设置面板同级）。

### 6.2 导出模块

| 模块名称 | 图标 | 包含内容 | 导出格式 | 典型体量 |
| :--- | :--: | :--- | :--: | :--- |
| 用户设置 | ⚙️ | `yibofile_settings.json` 内容 | JSON | ~2 KB |
| 组织结构 | 📂 | 库定义 + 收藏夹(含分组) + 标签定义(含分组) | JSON | ~15 KB |
| 文件元数据 | 🏷️ | 文件标签关联 + 文件备注 | JSON | 不确定 |
| 自定义主题 | 🎨 | `CustomThemes/` 目录下所有 JSON | JSON 集合 | ~8 KB |

### 6.3 导出流程

```
用户打开导出中心 → 勾选模块 → 显示预估大小 → 点击"导出为 ZIP" → 选择保存位置 → 生成 ZIP
```

ZIP 内部结构：
```
yibofile_export_{date}.zip
├── manifest.json               ← 导出元数据（版本、时间、包含模块列表）
├── settings.json               ← 用户设置（如勾选）
├── structure.json              ← 组织结构（如勾选）
├── filedata.json               ← 文件元数据（如勾选）
└── themes/                     ← 自定义主题（如勾选）
    ├── {uuid1}.json
    └── {uuid2}.json
```

### 6.4 导入流程

```
用户打开导入中心 → 选择 ZIP 文件 → 解析 manifest.json → 显示包含模块
→ 用户勾选要导入的模块 → 冲突策略选择（覆盖/合并/跳过）→ 执行导入
```

### 6.5 组织结构导入的路径处理

导入完成后弹出结果窗口：

```
┌──────────────────────────────────────────────────────┐
│  📂 导入结果                                    [×]  │
├──────────────────────────────────────────────────────┤
│                                                      │
│  ✅ 成功导入: 24 项                                   │
│     库 (3) + 收藏夹 (15) + 标签组 (2) + 标签 (4)      │
│                                                      │
│  ⚠️ 路径缺失: 5 项                                   │
│                                                      │
│  ┌────────────────────────────┬──────────────────┐   │
│  │ 路径                       │ 操作             │   │
│  ├────────────────────────────┼──────────────────┤   │
│  │ D:\Projects\Old            │ [忽略] [创建] [映射]│  │
│  │ E:\Archive\2024            │ [忽略] [创建] [映射]│  │
│  │ ...                        │                  │   │
│  └────────────────────────────┴──────────────────┘   │
│                                                      │
│  [全部忽略]  [全部创建]           [确定]               │
└──────────────────────────────────────────────────────┘
```

- **忽略**：导入数据但标记路径为不可用
- **创建**：自动创建目录
- **映射**：弹出文件夹选择器，将旧路径映射到新路径

---

## 7. TagTrain 清理 (TagTrain Cleanup)

TagTrain 是旧的标签训练系统，**相关功能将完全删除**（后续会重做）。

### 7.1 需要清理的项目

| 文件/位置 | 清理内容 |
| :--- | :--- |
| `ConfigManager.cs` | 移除 `TagTrainSettingsFileName`、`GetTagTrainSettingsPath()`、相关导出/导入逻辑 |
| `GeneralSettingsPanel.xaml.cs` | 移除 `tt_settings.txt` 相关的导出/导入 UI 行 |
| 现有 ZIP 导出逻辑 | 移除对 `tt_settings.txt`、`tt_training.db`、`tt_model.zip` 的打包 |
| 旧文件 | 不主动删除用户磁盘上的 TagTrain 文件，但不再读取/管理 |

### 7.2 迁移策略

- 迁移代码中**不包含** TagTrain 文件的迁移
- 旧 ZIP 导入时如遇 TagTrain 文件，静默忽略

---

## 8. 迁移策略 (Migration Strategy)

### 8.1 首次启动自动迁移

```
检测 ooi_config.json 是否存在？
  │
  ├─ 是 → 执行迁移：
  │       1. 读取 ooi_config.json
  │       2. 按层次分类拆分字段
  │       3. 写入 yibofile_settings.json（层次1字段）
  │       4. 写入 yibofile_state.json（层次4字段）
  │       5. 将 ooi_config.json 重命名为 ooi_config.json.bak
  │
  └─ 否 → 检测 yibofile_settings.json 是否存在？
          ├─ 是 → 正常加载
          └─ 否 → 创建默认配置
```

### 8.2 数据库迁移

```
检测 ooi_data.db 是否存在且 yibofile_data.db 不存在？
  │
  ├─ 是 → 重命名 ooi_data.db → yibofile_data.db
  │
  └─ 否 → 正常流程
```

### 8.3 搜索历史迁移

```
检测旧路径 %AppData%/YiboFile/search_history.json 是否存在？
  │
  ├─ 是 → 移动到 <BaseDirectory>/yibofile_history.json
  │       （如已存在则合并）
  │
  └─ 否 → 跳过
```

### 8.4 自定义主题迁移

```
检测旧路径 %AppData%/YiboFile/CustomThemes/ 是否存在？
  │
  ├─ 是 → 移动到 <BaseDirectory>/CustomThemes/
  │
  └─ 否 → 跳过
```

---

## 9. 分阶段实施计划 (Phased Implementation Plan)

### 阶段 1：路径集中管理 + 清理 TagTrain

**风险**：低 | **改动范围**：~8 个文件

- [ ] 定义 `IConfigPathProvider` 接口与默认实现
- [ ] 注册到 DI 容器
- [ ] 修复 `SearchHistoryService` 路径硬编码 → 注入 `IConfigPathProvider`
- [ ] 修复 `CustomThemeManager` 路径硬编码 → 注入 `IConfigPathProvider`
- [x] 修复 `DwgConverter` / `CadImageCache` 缓存路径硬编码
- [ ] 清理 `ConfigManager` 中 TagTrain 相关代码
- [ ] 清理 `GeneralSettingsPanel` 中 TagTrain 相关导出 UI

### 阶段 2：拆分 AppConfig + 双文件存储

**风险**：中 | **改动范围**：~12 个文件

- [ ] 定义 `UserSettings` 类（对应 `yibofile_settings.json`）
- [ ] 定义 `AppState` 类（对应 `yibofile_state.json`）
- [ ] 实现 JSON 读写器（支持 schema 版本）
- [ ] 实现 `ooi_config.json` → 双文件自动迁移
- [ ] 更新 `ConfigurationService` 内部实现，对外接口兼容过渡
- [ ] 更新 `WindowStateManager` 使用新的 `AppState` API
- [ ] 更新 9 个 `*SettingsViewModel` 使用新的 `UserSettings` API
- [ ] 数据库文件重命名迁移（`ooi_data.db` → `yibofile_data.db`）

### 阶段 3：统一导入导出中心

**风险**：中 | **改动范围**：~10 个文件（含新增）

- [ ] 实现组织结构的 JSON 序列化/反序列化（库 + 收藏 + 标签定义）
- [ ] 实现文件元数据的 JSON 序列化/反序列化（FileTags + Notes）
- [ ] 实现自定义主题打包/解包
- [ ] 实现 `IExportService` / `IImportService`
- [ ] 实现导入时路径检查与结果弹窗（成功/缺失 + 忽略/创建/映射）
- [ ] 新建「导出中心」设置面板 UI
- [ ] 重构现有 `DataSettingsViewModel` 中的导出逻辑
- [ ] 移除 `LibrarySettingsPanel` / `LibraryManagementPanel` 中的独立导入导出按钮（纳入导出中心）
- [ ] 合并搜索历史服务（`SearchHistoryService`）以统一管理搜索和地址栏历史
- [ ] 搜索历史和自定义主题的路径迁移逻辑

### 阶段 4：合并配置管理器 + 接口化

**风险**：低 | **改动范围**：~5 个文件

- [ ] 提取 `IConfigurationService` 接口
- [ ] 将 `ConfigManager` 的 I/O 逻辑下沉为 `ConfigurationService` 的私有方法
- [ ] 消除 `ConfigManager` 的静态公开 API
- [ ] 更新 DI 注册：`IConfigurationService` → `ConfigurationService`
- [ ] 全项目替换 `ConfigurationService.Instance` → DI 注入

---

## 10. 附录：从旧系统到新系统的映射 (Migration Mapping)

### 10.1 AppConfig 字段归属映射

| 旧字段 (AppConfig) | → 新归属 | 新文件 |
| :--- | :--- | :--- |
| ThemeMode, LayoutMode, WindowOpacity, AnimationsEnabled, IconStyle | `UserSettings.Appearance` | settings.json |
| UIFontSize, TagFontSize, TagBoxWidth, TagWidth | `UserSettings.Fonts` | settings.json |
| ReuseTabTimeWindow, AlwaysReuseTab, NeverReuseTab, ActivateNewTabOnMiddleClick, EnableMultiWindow, TabWidthMode, PinnedTabWidth | `UserSettings.Behavior` | settings.json |
| IsEnableFullTextSearch, FullTextIndexPaths, HistoryMaxCount, AutoExpandHistory | `UserSettings.Search` | settings.json |
| CustomHotkeys | `UserSettings.Hotkeys` | settings.json |
| NavigationSectionsOrder | `UserSettings.Navigation` | settings.json |
| BackupDirectory, BackupRetentionDays | `UserSettings.Backup` | settings.json |
| PinnedTabs, TabTitleOverrides | `UserSettings.Tabs` | settings.json |
| WindowWidth ~ IsMaximized | `AppState.Window` | state.json |
| ColLeftWidth ~ IsDualListMode | `AppState.Layout` | state.json |
| ColNameWidth ~ VisibleColumns_* | `AppState.Columns` | state.json |
| LastPath ~ ActiveTabKeySecondary | `AppState.Session` | state.json |
| FileViewMode, SortColumn, SortDirection | `AppState.View` | state.json |
| SidebarExpanderStates | `AppState.Sidebar` | state.json |
| BackupBrowserWidth, BackupBrowserHeight | `AppState.Misc` | state.json |
| TagTrainDataDirectory | **删除** | — |

### 10.2 文件名映射

| 旧文件 | 新文件 | 迁移方式 |
| :--- | :--- | :--- |
| `ooi_config.json` | `yibofile_settings.json` + `yibofile_state.json` | 拆分 + 重命名 |
| `ooi_data.db` | `yibofile_data.db` | 重命名 |
| `search_history.json` (在 %AppData%) | `yibofile_history.json` (在 BaseDirectory) | 移动 + 重命名 |
| `CustomThemes/` (在 %AppData%) | `CustomThemes/` (在 BaseDirectory) | 移动 |
| `tt_settings.txt` | **删除** | 不迁移 |
| `tt_training.db` | **删除** | 不迁移 |
| `tt_model.zip` | **删除** | 不迁移 |
