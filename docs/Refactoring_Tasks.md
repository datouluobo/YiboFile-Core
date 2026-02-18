# YiboFile 重构任务清单

> **状态**: 进行中 | **更新日期**: 2026-02-18

## 📊 进度概览

| 优先级 | 任务 | 状态 | 完成度 |
| :--- | :--- | :---: | :---: |
| **P0** | WindowOrchestrator 拆分 | ✅ 已完成 | 100% |
| **P0** | MainWindow.Tabs.cs 逻辑迁移 | ✅ 已完成 | 100% |
| **P1** | Service 层事件迁移 (第一批) | ✅ 已完成 | 100% |
| **P1** | Handler 层解耦 (IShellWindow) | ✅ 已完成 | 100% |
| **P1** | FileListEventHandler 拆分 | ✅ 已完成 | 100% |
| **P1** | 统一主副栏导航架构 | ✅ 已完成 | 100% |
| **P2** | Service 层事件迁移 (第二批) | ✅ 已完成 | 100% |
| **P2** | 配置系统统一重构 | ✅ 已完成 | 100% |
| **P2** | PaneViewModel 逻辑优化 | ✅ 已完成 | 100% |
| **P2** | LibraryManagementWindow MVVM 化 | ✅ 已完成 | 100% |
| **P2** | App.xaml.cs 启动逻辑拆分 | ✅ 已完成 | 100% |
| **P2** | Bug 修复 (BUG-001, BUG-003) | ⬜ 待办 | 0% |

---

## ✅ 已完成任务

> 以下任务仅保留标题，详细内容已归档。

- [x] **P0: WindowOrchestrator 拆分** — 代码行数从 981 降至 313
- [x] **P0: MainWindow.Tabs.cs 逻辑迁移** — 文件已移除
- [x] **P1: Service 层事件迁移 (第一批: 核心导航)** — TabService、NavigationCoordinator 事件已消息化
- [x] **P1: Handler 层解耦 (IShellWindow)** — 关键 Handler 已全部解耦
- [x] **P1: FileListEventHandler 拆分** — 已拆分为 Mouse/Keyboard 子处理器，行数由 760+ 降至 110 (facade)
- [x] **P1: 统一主副栏导航架构** — 消除双轨制，委托回调 → 消息驱动
- [x] **P2: Service 层事件迁移 (第二批: 辅助服务)** — 全部辅助服务已解耦或消息化

---

## 📝 待办任务清单

### 🔴 P2-1: 配置系统统一重构 (NEW)

> **背景**：当前配置管理存在多套并行机制和分散的文件格式，需要集中整理并统一。
### 3. Configuration System Unification (Unified Config Architecture) - **In Progress**

**Goal**: Unify configuration management under `IConfigurationService`, implementing data layering and centralized path management.

#### Phase 1: Path Centralization & Cleanup (Completed) ✅
- [x] Define `IConfigPathProvider` interface and implementation.
- [x] Register `IConfigPathProvider` in DI container.
- [x] Refactor `SearchHistoryService` to use `IConfigPathProvider`.
- [x] Refactor `CustomThemeManager` to use `IConfigPathProvider`.
- [x] Update `DwgConverter` and `CadImageCache` paths.
- [x] Remove TagTrain related code from `ConfigManager` and UI.

#### Phase 2: AppConfig Splitting & Dual Storage (Completed) ✅
- [x] Define `UserSettings` model (Layer 1).
- [x] Define `AppState` model (Layer 4).
- [x] Implement `ConfigMapper` for AppConfig <-> Models mapping.
- [x] Update `ConfigurationService` to support dual-file storage (`settings.json`, `state.json`).
- [x] Implement automatic migration from `ooi_config.json`.
- [x] Update `ConfigManager.Save` to delegate to `ConfigurationService`.

#### Phase 3: Unified Import/Export Center (Completed) ✅
- [x] Implement `IExportService` and `IImportService` interfaces.
- [x] Register services in DI container.
- [x] Update `DataSettingsViewModel` to use `IExportService` and `IImportService`.
- [x] Implement placeholder `ExportService` and `ImportService`.

#### Phase 4: Final Consolidation (Completed) ✅
- [x] Implement full logic for `ExportService` (ZIP generation, JSON serialization).
- [x] Implement full logic for `ImportService` (ZIP extraction, path resolution).
- [x] Deprecate `ConfigManager` static API.
- [x] Replace `AppConfig` usage with `IConfigurationService` interfaces.



### 🟡 P2-2: PaneViewModel 逻辑优化

- [ ] 将 `ExecuteNavigateBack/Forward/Up`、`OnNavigateToPath` 等导航逻辑下沉到 `NavigationModule`
- [ ] PaneViewModel 进一步精简为纯状态容器

### 🟡 P2-3: LibraryManagementWindow MVVM 化

- [ ] 创建 `LibraryManagementViewModel`
- [ ] 绑定视图与 ViewModel
- [ ] 移除 Code-behind 逻辑

### 🟡 P2-4: App.xaml.cs 启动逻辑拆分

- [ ] 提取 `StartupService` (或 `AppBootstrapper`)

### 🟡 P2-5: Bug 修复

- [ ] **BUG-001**: 副地址栏库标识错误
- [ ] **BUG-003**: 副面板库路径识别失败

### 🔵 P3: 文档

- [ ] 更新 `Project_Evaluation_and_Roadmap.md` 中的完成状态
