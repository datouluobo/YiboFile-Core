、# MainWindow 上帝类解构方案 (方案 B：激进式重构)

| 版本 | 日期 | 作者 | 状态 | 目标版本 |
|------|------|------|------|---------|
| v1.1 | 2026-02-10 | Antigravity | 执行中 (95% 完成) | v1.1.0 (Core 解耦) |

## 1. 背景与目标

### 1.1 现状分析 (God Class)
目前 `MainWindow.xaml.cs` 规模已超过 **1100 行**，且包含：
*   复杂的 UI 初始化序列
*   40+ 个 `internal` 字段暴露给外部服务
*   直接处理 20+ 个 XAML 事件逻辑
*   承担了服务编排、状态管理、事件桥接等多重职责

### 1.2 重构目标
根据 `.antigravityrules` 的混合 MVVM 规范，将 `MainWindow` 彻底脱离业务逻辑：
*   **代码规模**：从 1147 行缩减至 **< 100 行**。
*   **职责重塑**：从“上帝类”退化为纯粹的“View 容器”。
*   **解耦策略**：利用 `WindowOrchestrator`、`UIAdapters` 和 `MessageBus` 实现完全解耦。

---

## 2. 核心架构设计

### 2.1 依赖关系演进
**重构前**：`MainWindow` ↔ `Services` (强耦合，双向依赖)
**重构后**：`MainWindow` ← `Adapters` ← `Orchestrator` → `ViewModels/Messages` (单向流，松耦合)

### 2.2 核心组件职责
*   **WindowOrchestrator**: 负责整个窗口生命周期的初始化编排。
*   **UI Adapters**: 封装对 XAML 控件的直接访问，实现 `IConfigUIHelper` 等接口。
*   **EventBridge**: 将原始 XAML 事件转化为强类型消息。
*   **MessageBus**: 作为中枢，同步所有组件的状态。

---

## 3. 六阶段实施计划

### 阶段 1：消息基础设施 (Infrastructure) [✅ 100% 完成]
*   **目标**：建立规范的消息总线通讯协议。
*   **已完成交付物**：
    *   ✅ `NavigationMessages.cs`: 路径变更、导航完成、导航状态消息
    *   ✅ `LayoutMessages.cs`: 布局模式、双列表、焦点面板变更消息
    *   ✅ `FileOperationMessages.cs`: 选择变更、刷新请求消息
    *   ✅ `KeyboardMessages.cs`: 键盘快捷键消息
    *   ✅ `UIEventMessages.cs`: UI 事件消息定义

### 阶段 2：WindowOrchestrator 创建 (Orchestration) [✅ 100% 完成]
*   **目标**：接管初始化编排职责。
*   **已完成任务**：
    *   ✅ 创建 `WindowOrchestrator.cs` 并接管服务初始化
    *   ✅ 迁移所有 MVVM 模块的初始化逻辑
    *   ✅ 实现 ViewModel 创建和关联
    *   ✅ 迁移消息总线订阅逻辑
    *   ✅ 删除了全局 `LibrarySelectedMessage` 订阅，实现面板级解耦
    *   ✅ TabService 双实例机制已建立 (主/副面板各一个)
    *   ✅ 将 `MainWindow.Handlers.cs` 中剩余的服务初始化迁移到 DI 容器
    *   ✅ 优化初始化顺序，消除潜在的依赖循环
    *   ✅ 建立 `IWindowOrchestrator` 接口，实现 `MainWindow` 服务完全委派

### 阶段 3：UI 适配器层 (Interface Isolation) [✅ 100% 完成]
*   **目标**：移除 MainWindow 实现的接口。适配器已在 `Services/UI/Adapters` 实现。
*   **已完成任务**：
    *   ✅ 创建 `ConfigUIAdapter` 和 `NavigationModeUIAdapter`
    *   ✅ 将所有依赖 `IConfigUIHelper` 的服务重定向到适配器
    *   ✅ 确保 MainWindow 仅暴露必要的 `internal` 控件引用
    *   ✅ MainWindow 已不再直接实现任何业务接口
*   **交付成果**：
    *   📦 `Services/UI/Adapters/ConfigUIAdapter.cs`
    *   📦 `Services/UI/Adapters/NavigationModeUIAdapter.cs`

### 阶段 4：事件桥接服务 (Event Bridging) [✅ 100% 完成]
*   **目标**：事件逻辑从 Code-Behind 剥离。
*   **已完成任务**：
    *   ✅ 创建 `EventBridgeService` 并挂载核心 XAML 事件
    *   ✅ 键盘事件完全转换为 `KeyboardShortcutMessage`
    *   ✅ 文件列表选择变更转换为 `FileSelectionChangedMessage`
    *   ✅ 移除 `MainWindow.Input.cs` 中的键盘桥接逻辑
    *   ✅ 清理 `NavigationRailControl.NavigationModeChanged` 未使用事件
    *   ✅ 修复标签页 UI 显示 (恢复 `PathTabDataTemplate` 引用)
    *   ✅ 优化侧边栏导航逻辑 (精简事件拦截范围)
    *   ✅ 清理 XAML 中剩余的直接事件绑定 (`NavPathBtn_Click`, `Undo_Click` 等)

### 阶段 5：简化 MainWindow.xaml.cs (已完成)
- **目标**: 移除所有业务逻辑代码，只保留 UI 框架代码。
- **状态**: ✅ **100% 完成**
- **已完成任务**:
    1.  ✅ 移除 `MainWindow.Initialization.cs` 中的业务初始化逻辑（移至 `WindowOrchestrator`）。
    2.  ✅ 移除 `MainWindow.Navigate` 等包装方法（调用方改为 `NavigationCoordinator`）。
    3.  ✅ 清理 `FileBrowserControl.xaml.cs` 中的桥接属性 (`NavBackEnabled`, `TabsVisible` 等)。
    4.  ✅ 解决因移除属性导致的编译错误 (更新 `NavigationModeService`, `LibraryEventHandler`)。
    5.  ✅ 移除未使用的桥接类 (`FileBrowserBridge`, `UiLayoutMcp`)。

### 阶段 6：最终清理与优化 (进行中)
- **目标**: 移除废弃文件，优化引用，文档更新。
- **状态**: 🔄 **进行中 (80%)**
- **待执行任务**:
    1.  [x] 移除 `MainWindow.LayoutMode.cs`。
    2.  [x] 移除 `MainWindow.Navigation.cs` 和 `MainWindow.MenuEvents.cs`。
    3.  [x] 清理 `MainWindow.xaml.cs` 中的废弃事件处理 (`Undo_Click`, `Redo_Click` 等)。
    4.  [x] 全面检查未使用的 `using` 引用。
    5.  [ ] 验证所有功能模块的集成测试。
    6.  [ ] 更新架构文档。
- **预计剩余工作量**: ~1小时

---

## 4. 风险评估与缓解

| 风险项 | 影响等级 | 缓解措施 |
|--------|---------|---------|
| 初始化依赖循环 | 🔴 高 | 使用 Lazy 注入或分两步初始化 (Construct -> Attach) |
| 跨线程 UI 访问 | 🟡 中 | 强制在适配器层使用 `Dispatcher.Invoke` |
| 性能开销 (消息总线) | 🟢 低 | 使用高性能消息队列，避免在高频事件（如 MouseMove）中使用消息 |
| 现有功能回归 | 🔴 高 | 建立 E2E 自动化测试，配合分支开发模式 |

## 5. 验收标准

1.  **架构指标**：`MainWindow` 零业务逻辑，无直接数据库/服务调用。
2.  **代码指标**：`MainWindow.xaml.cs` 行数 < 100 行。
3.  **合规指标**：完全通过 `.antigravityrules` 的架构审计。
4.  **性能指标**：应用启动时间波动率 < 10%。
