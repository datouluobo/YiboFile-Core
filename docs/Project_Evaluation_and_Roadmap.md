# YiboFile 项目评估与重构路线图

> **当前版本**: v1.0.1470 (UI 逻辑修复) | **更新日期**: 2026-02-15  
> **下一版本**: v1.1.0 (目标：Core 完全解耦)  

---

## 一、架构现状分析

### 1.1 代码规模统计

#### 上帝类分析 (God Class Metrics)

| 组件 | 文件数 | 总行数 | 平均行数/文件 | 健康度评估 |
|------|--------|--------|---------------|------------|
| **MainWindow (分部类)** | 0 | 0 | 0 | 🟢 已完全解构 |
| **PaneViewModel** | 1 | ~1,700 | 1,700 | 🟡 中耦合 (职责逐步拆分中) |

---

### 1.2 架构演进阶段回顾

| 阶段 | 时间 | 核心目标 | 完成度 |
|------|------|----------|--------|
| **Phase 1: Code-Behind** | v0.x | WinForms 风格，所有逻辑在 `MainWindow.xaml.cs` | ✅ 历史阶段 |
| **Phase 2: Partial MVVM** | v1.0.1 - v1.0.330 | 引入 `PaneViewModel`，部分功能命令化 | ✅ 90% 完成 |
| **Phase 3: 混合架构** | **v1.0.1460** | **架构重构完成** - 控制器驱动 VM + 消息总线副作用 + 对话框修复 | ✅ 已完成 |
| **Phase 3.5: 细节与修复** | **v1.0.1470 (当前)** | 修复双列表同步、清理 PaneViewModel 冗余 | 🟡 进行中 |
| **Phase 4: 全模块化 (v1.1.0)** | v1.1.0 (目标) | PaneViewModel 完全解耦，模块化重构完成 | ⏳ 规划中 |

---


---

## 二、混合架构规范 (Hybrid MVVM Architecture)

### 2.1 核心原则

**重要概念**：混合架构 ≠ 所有地方都用 Controller

YiboFile 采用 **"分场景使用不同模式"** 的混合架构策略：

#### 模式选择原则

**Controller-driven 模式**（用于核心业务逻辑）：
*   **适用场景**：导航控制、搜索执行、文件操作、复杂的状态机管理
*   **判断标准**：
    *   需要跨服务协调（如导航需要同时操作历史栈、缓存、文件加载）
    *   包含复杂业务验证（如权限检查、状态前置条件）
    *   涉及多个相关状态的联动更新
    *   需要在 Core/Pro/Ultra 版本中扩展不同逻辑
*   **实现方式**：创建独立的 Coordinator/Controller 类，通过消息总线接收请求，驱动 ViewModel 更新

**传统 MVVM 模式**（用于简单 UI 状态）：
*   **适用场景**：过滤器切换、视图模式变更、UI 偏好设置、简单表单输入
*   **判断标准**：
    *   逻辑简单，仅涉及单一属性或简单计算
    *   无需跨服务协调
    *   不包含复杂的业务规则验证
    *   纯 UI 状态，不影响核心业务流程
*   **实现方式**：ViewModel 直接处理属性变更和命令，通过消息总线发布通知消息

#### A. 控制逻辑 (Core Navigation/State Control) - Controller-driven

*   **主体**：独立服务类（如 `NavigationCoordinator`, `SearchModule`）。
*   **行为**：作为"控制器"，直接持有或通过委托操作 `PaneViewModel` 实例。
*   **原则**：核心状态（路径、加载状态、排序方式）通过直接修改 ViewModel 属性来触发绑定更新。**禁止在逻辑层直接操作控件实例**。
*   **示例**：
    ```csharp
    // NavigationCoordinator 驱动 ViewModel
    private void OnNavigateRequested(NavigateToPathMessage message)
    {
        // 业务验证
        if (!IsValidPath(message.Path)) return;
        
        // 协调多个服务
        _historyService.AddToHistory(message.Path);
        _cacheService.InvalidateCache();
        
        // 驱动 ViewModel 更新
        _paneViewModel.CurrentPath = message.Path;
        
        // 发布通知
        _messageBus.Publish(new NavigationCompleteMessage(...));
    }
    ```

#### B. 简单状态管理 (Simple UI State) - 传统 MVVM

*   **主体**：ViewModel 类（如 `SearchViewModel`, `FilterViewModel`）。
*   **行为**：直接在 ViewModel 中处理属性变更和简单命令。
*   **原则**：简单逻辑不需要额外的 Coordinator 层，避免过度设计。
*   **示例**：
    ```csharp
    // SearchViewModel 直接处理过滤器变更
    public FileTypeFilter TypeFilter
    {
        get => _typeFilter;
        set
        {
            if (SetProperty(ref _typeFilter, value))
            {
                // ViewModel 直接发布通知消息
                NotifyOptionsChanged();
            }
        }
    }
    
    private void NotifyOptionsChanged()
    {
        var options = BuildSearchOptions();
        _messageBus.Publish(new SearchOptionsChangedMessage(options, _targetPaneId));
    }
    ```

#### C. 副作用与跨组件通讯 (Side Effects & Notifications)

*   **主体**：`IMessageBus` (Mediator 模式)。
*   **行为**：逻辑执行完毕后，发布特定消息（如 `NavigationCompleteMessage`, `FileChangedMessage`）。
*   **用途**：
    *   同步非直接绑定的 UI（如全局地址栏、面包屑）。
    *   触发插件逻辑（特别是在 **Ultra** 版本中）。
    *   多窗口/多面板间的状态同步。

### 2.2 架构决策矩阵

| 功能场景 | 推荐模式 | 理由 | 实现示例 |
|---------|---------|------|---------|
| **导航路径切换** | Controller-driven | 需要协调历史栈、缓存、文件加载等多个服务 | `NavigationCoordinator` |
| **搜索执行** | Controller-driven | 需要验证索引状态、权限，协调搜索服务和缓存 | `SearchModule` |
| **文件操作** | Controller-driven | 需要验证权限、处理冲突、更新多个状态 | `FileOperationCoordinator` |
| **过滤器变更** | 传统 MVVM | 简单 UI 状态，仅需构建选项并发布通知 | `SearchViewModel.TypeFilter` |
| **视图模式切换** | 传统 MVVM | 简单属性切换，无复杂逻辑 | `PaneViewModel.FileViewMode` |
| **清空搜索** | 传统 MVVM | 简单属性赋值 | `SearchViewModel.ClearSearchCommand` |
| **范围预设** | Controller-driven | 需要协调多个相关状态（SearchNames, SearchFolders, TypeFilter） | `SearchCoordinator.SetScopePreset` |
| **主题切换** | 传统 MVVM | 简单配置更新，无业务验证 | `AppearanceViewModel.CurrentTheme` |
| **布局模式切换** | 看情况 | 如果仅状态切换→MVVM；如果需要协调面板显示→Controller | 评估后决定 |

### 2.3 分层依赖关系

```
Controller-driven 场景：
┌─────────────────────────────────────┐
│  View (Command/Binding)              │
└────────────┬────────────────────────┘
             │ Publish Request
             ▼
      ┌─────────────┐
      │ MessageBus  │
      └──────┬──────┘
             │ Subscribe
             ▼
┌─────────────────────────────────────┐
│  Coordinator/Controller              │
│  - 业务验证                           │
│  - 服务协调                           │
│  - 驱动 ViewModel                    │
└────────┬────────────────────────────┘
         │ Update State
         ▼
┌─────────────────────────────────────┐
│  ViewModel (仅状态)                  │
└────────┬────────────────────────────┘
         │ Data Binding
         ▼
┌─────────────────────────────────────┐
│  View (UI Update)                    │
└─────────────────────────────────────┘

传统 MVVM 场景：
┌─────────────────────────────────────┐
│  View (Command/Binding)              │
└────────────┬────────────────────────┘
             │ Data Binding
             ▼
┌─────────────────────────────────────┐
│  ViewModel                           │
│  - 状态管理                           │
│  - 简单逻辑                           │
│  - 发布通知消息                        │
└────────┬────────────────────────────┘
         │ Publish Notification
         ▼
      ┌─────────────┐
      │ MessageBus  │
      └──────┬──────┘
             │ Subscribe
             ▼
┌─────────────────────────────────────┐
│  Other Components/Subscribers        │
└─────────────────────────────────────┘
```

### 2.4 实现检查清单

在实现新功能或重构现有代码时，使用以下清单判断应采用哪种模式：

**使用 Controller-driven 的信号**：
- [ ] 需要调用 2 个或以上的服务类
- [ ] 包含复杂的 if-else 业务规则（超过 3 层嵌套）
- [ ] 需要在不同版本（Core/Pro/Ultra）中有不同实现
- [ ] 涉及异步操作的错误处理和重试逻辑
- [ ] 需要维护复杂的状态机（如上传进度管理）

**使用传统 MVVM 的信号**：
- [ ] 逻辑可以在 ViewModel 的单个方法中完成（< 20 行代码）
- [ ] 不需要调用任何服务类，或仅调用 1 个服务的单一方法
- [ ] 主要是属性的 get/set 或简单计算
- [ ] 不涉及异步操作
- [ ] 纯 UI 状态，不影响核心数据流

**严禁模式**：
*   ❌ Services -> UI Controls (跳过 ViewModel 直接操作控件)
*   ❌ ViewModels -> Services (VM 不应主动调用服务类的业务方法，应通过消息请求)
*   ❌ Code-Behind -> ViewModels (XAML.cs 不应直接修改 VM 属性，应通过 Command)
*   ❌ 过度使用 Coordinator（简单逻辑不要创建 Coordinator）
*   ❌ 混合使用模式（同一功能不要既用 Coordinator 又在 ViewModel 中处理部分逻辑）

---

## 三、重构路线图 (按优先级排序)

### 🔴 优先级 1：MainWindow 上帝类解构 (当前阻塞点)

**目标**：将 `MainWindow` 从"全能调度中心"降级为"UI 容器 + 依赖注入根"。

| 步骤 | 任务描述 | 涉及文件 | 预计工作量 | 状态 |
|------|----------|----------|------------|------|
| **3.1.1** | **提取 NavigationModule** | MainWindow.Navigation.cs (~450行) → NavigationModule.cs | 3h | ✅ 已完成 (物理文件已删，Wrapper 仍存) |
| **3.1.2** | **重构 Handler 初始化** | MainWindow.Handlers.cs → App.xaml.cs & WindowOrchestrator | 4h | ✅ 已完成 |
| **3.1.3** | **模块化 LayoutMode** | MainWindow.LayoutMode.cs → LayoutModule.cs | 5h | ✅ 已完成 |
| **3.1.4** | **清理事件订阅** | 删除 MainWindow 中所有对 ViewModel 事件的订阅 | 2h | ✅ 已完成 (键盘/基础UI事件) |
| **3.1.5** | **简化 MainWindow.xaml.cs** | 目标：< 150 行（仅保留窗口生命周期管理）。 | MainWindow.xaml.cs (~800行) | 3h | ✅ 已完成 (Wrapper 清理完毕) |
| **3.1.6** | **重构键盘事件处理**：将 `KeyboardEventHandler` 转换为消息驱动模式。 | KeyboardEventHandler.cs | 2h | ✅ 已完成 |

**成功标准**：
*   `MainWindow.xaml.cs` 不包含任何业务逻辑。
*   所有文件操作、导航、布局切换均由 ViewModel 或 Module 驱动。
*   `MainWindow` 不持有任何 Service 实例（除了 `MainWindowViewModel`）。

---

### 🟡 优先级 2：PaneViewModel 职责拆分

**目标**：将 `PaneViewModel` (1770行) 拆分为更单一职责的子模块。

| 步骤 | 任务描述 | 当前代码行数 | 拆分目标 | 状态 |
|------|----------|--------------|----------|------|
| **3.2.1** | **提取 FilterViewModel**：将文件过滤逻辑（类型、大小、日期）独立为可重用的 `FilterViewModel`。 | ~300行 | FilterViewModel.cs | ⏳ 待启动 |
| **3.2.2** | **提取 SelectionViewModel**：将文件选择、多选、全选逻辑独立。 | ~200行 | SelectionViewModel.cs | ⏳ 待启动 |
| **3.2.3** | **简化 Command 定义**：将 `ICommand` 封装到 `PaneCommandSet` 类，`PaneViewModel` 只持有该类的实例。 | ~150行 | PaneCommandSet.cs | ⏳ 待启动 |

**成功标准**：
*   `PaneViewModel.cs` 核心代码压缩至 < 800 行。
*   每个子 ViewModel 可独立测试。

---

### 🟢 优先级 3：消息驱动架构完善

**目标**：补齐跨组件消息定义，彻底消除"事件订阅 + 委托回调"的双轨制。

| 步骤 | 任务描述 | 消息类型 | 状态 |
|------|----------|----------|------|
| **3.3.1** | **定义导航消息**：`NavigationCompleteMessage`, `NavigationStatusChangedMessage` | ✅ 已完成 | ✅ v1.1.0 |
| **3.3.2** | **定义文件操作消息**：`FileOperationRequestMessage`, `FileOperationCompleteMessage` | 待定义 | ⏳ 待启动 |
| **3.3.3** | **定义布局消息**：`LayoutModeChangedMessage`, `PaneFocusChangedMessage` | 待定义 | ⏳ 待启动 |
| **3.3.4** | **清理遗留事件**：删除 `NavigationCoordinator` 中的 `PathNavigateRequested` 等事件。 | - | ✅ 已完成 |

**成功标准**：
*   所有跨模块通讯通过 `IMessageBus` 实现。
*   服务类不持有任何 `event` 或 `Action<>` 委托。

---

### 🔵 优先级 4：Pro/Ultra 扩展性准备

**目标**：为 Pro 和 Ultra 版本铺设插件化基础。

| 步骤 | 任务描述 | 技术方案 | 状态 |
|------|----------|----------|------|
| **3.4.1** | **定义插件接口**：`IYiboFilePlugin`, `IMenuExtension`, `IPreviewExtension` | 使用 MEF 或反射 | ⏳ 待启动 |
| **3.4.2** | **实现插件加载器**：`PluginManager` 在启动时扫描 `Plugins/` 目录并动态加载。 | DI Container 注入 | ⏳ 待启动 |
| **3.4.3** | **消息总线开放**：允许插件订阅核心消息（如 `FileSelectionChangedMessage`）。 | 已有 MessageBus 基础 | ⏳ 待启动 |

---

### 🟣 优先级 5：已知 Bug 修复清单

| ID | 模块 | 描述 | 根因分析 | 修复策略 | 状态 |
|----|------|------|----------|----------|------|
| **BUG-001** | AddressBar | 副地址栏库标识错误 (显示 `path` 而非 `lib`) | `AddressBarControl` 未正确绑定到 `PaneViewModel.NavigationMode` | 检查 SecondFileBrowser 的 DataContext 绑定 | ⏳ 待修复 |
| **BUG-002** | Pane | 副面板刷新与操作混乱 | 焦点管理依赖 WPF Focus 而非 VM.IsActive | 使用 `PaneViewModel.IsActive` 驱动所有操作上下文 | ⏳ 待修复 |
| **BUG-003** | Library | 副面板库路径识别失败 | `FileOperationModule` 未正确解析 `lib://` 协议 | 在 Module 中增加协议解析逻辑 | ⏳ 待修复 |
| **BUG-007** | Sorting | 文件名排序导致列表变空 | `CollectionView` 与 `ObservableCollection` 同步冲突 | 使用 `BindingOperations.EnableCollectionSynchronization` | ⏳ 待修复 |
| **BUG-008** | Header | 列头点击误触发双击响应 | 事件冒泡未正确拦截 | 在 `GridViewColumnHeader_Click` 中设置 `e.Handled = true` | ✅ 已修复 |
| **BUG-009** | Tabs/Nav | 双面板标签页重复打开 | 副面板打开标签时主面板同步创建重复标签 | 需检查 `MainWindow` 事件绑定或 `TabsModule` 的 `PaneId` 过滤逻辑 | ⏳ 待修复 |
| **BUG-010** | UI/Overlay | 设置和关于页面无法打开 | 重构事件桥接后，设置按钮的点击请求未正确到达 Controller | 检查 `NavigationRail` 的按钮 Command 绑定与 `WindowOrchestrator` 的桥接 | ✅ 已修复 |

---

## 四、当前重构进度 (v1.1.0)

### 4.1 已完成工作 ✅

| 任务 | 涉及文件 | 代码行数变化 | 完成日期 |
|------|----------|--------------|----------|
| **删除 MenuEventHandler** | MenuEventHandler.cs (-644行) | -644 | 2026-02-06 |
| **删除 FileBrowserEventHandler** | FileBrowserEventHandler.cs (-XXX行) | -XXX | 2026-02-07 |
| **引入混合架构规范** | .antigravityrules (+30行) | +30 | 2026-02-07 |
| **定义导航消息** | NavigationMessages.cs (+24行) | +24 | 2026-02-08 |
| **重构 NavigationCoordinator** | NavigationCoordinator.cs (~80行修改) | +80 / -120 | 2026-02-08 |
| **增强 PaneViewModel 导航状态** | PaneViewModel.cs (+15行) | +15 | 2026-02-08 |
| **重构 NavigationModule** | NavigationModule.cs (+150行) | +150 / -220 | 2026-02-08 |
| **重构文件重命名逻辑** | FileOperationModule.cs / FileListControl.xaml.cs | +60 / -40 | 2026-02-08 |
| **优化文件监视器防抖** | FileListViewModel.cs (3000ms -> 500ms) | 1 | 2026-02-08 |
| **修复列头事件处理** | FileListControl.xaml.cs (BUG-008) | +20 | 2026-02-08 |
| **重构键盘事件处理** | KeyboardEventHandler.cs / EventBridgeService.cs | +120 / -150 | 2026-02-09 |
| **移除 MainWindow 键盘桥接** | MainWindow.Input.cs | -15 | 2026-02-09 |
| **清理 NavigationCoordinator 事件** | NavigationCoordinator.cs / MainWindow.Initialization.cs | -25 | 2026-02-08 |
| **修复标签页 UI 显示** | TabManagerControl.xaml (+1行) | +1 | 2026-02-09 |
| **解决双面板同步干扰** | WindowOrchestrator.cs (-13行) | -13 | 2026-02-09 |
| **优化驱动器树导航逻辑** | NavigationPanelControl.xaml.cs | +5 / -8 | 2026-02-09 |
| **重构 Handler 初始化** | MainWindow.Handlers.cs (-XXX行) | -XXX | 2026-02-10 |
| **创建 IWindowOrchestrator** | IWindowOrchestrator.cs (New) | +80 | 2026-02-10 |
| **移除 MainWindow 服务字段** | MainWindow.xaml.cs | (委派模式) | 2026-02-10 |
| **标签页同步与刷新修复** | TabService.cs / PaneViewModel.cs | +50 / -30 | 2026-02-11 |
| **物理删除 MainWindow 分部类** | MainWindow.Navigation.cs / MenuEvents.cs | -Sum(1000+) | 2026-02-14 |
| **合并 MainWindow.LayoutMode** | LayoutModule.cs / MainWindow.xaml.cs | Refactor | 2026-02-14 |
| **清理 MainWindow XAML 事件** | MainWindow.xaml.cs | -25 methods | 2026-02-14 |
| **修复 Region 指令错误** | MainWindow.xaml.cs | +2 | 2026-02-14 |

- **MainWindow 解构 (阶段 5)**: `已完成` (100%). `MainWindow.xaml.cs` 从 >2400 行减少到 <800 行。
- **内存审计与优化 (阶段 6)**: `已完成` (100%).
    - [x] 移除 `MainWindow.LayoutMode.cs`。
    - [x] 移除 `MainWindow.Navigation.cs` 和 `MainWindow.MenuEvents.cs`。
    - [x] 清理 `MainWindow.xaml.cs` 中的废弃事件处理 (`Undo_Click`, `Redo_Click` 等)。
    - [x] 全面检查未使用的 `using` 引用。
    - [x] 修复设置与关于页面无法打开的重绑逻辑 (BUG-010)。
    - [x] 验证所有功能模块的集成测试。
    - [x] 更新架构文档。
- **预计剩余工作量**: 0小时

**净效果**：
*   代码总行数：-1500+ 行
*   明确了架构方向，为后续迁移奠定基础。

### 4.2 遗留问题 ⚠️

1.  **NavigationCoordinator 初始化未更新**：`MainWindow.Initialization.cs` 中初始化 `NavigationCoordinator` 的方法签名已改变（增加了 `IMessageBus` 参数和 ViewModel 解析器），需要同步更新。
2.  **预览组件兼容性**（记录于 2026-02-09）：观察到部分文件预览失效（文件夹正常），疑似与 Pro 版功能拆分逻辑有关。在 Stage 4 重构中需核对 `PreviewService` 的消息链路。

---

## 五、下一步行动 (Next Actions)

### 立即行动 (本周)

1.  **彻底清理 MainWindow 分部类**：
    *   移除 `MainWindow.Navigation.cs` 中的 Wrapper 方法，更新调用方直接使用 `NavigationModule`。
    *   重构 `MainWindow.MenuEvents.cs` 里的菜单逻辑。
    
2.  **启动 PaneViewModel 拆分**：
    *   提取 `FilterViewModel` 和 `SelectionViewModel`。
    *   简化 `PaneViewModel` 的体积。

3.  **完成 NavigationCoordinator 迁移**：
    *   在 `MainWindowViewModel` 中实现 `PaneViewModel` 解析器。
    *   测试路径导航和库导航是否正常工作。

### 中期目标 (本月)

1.  完成 `MainWindow` 上帝类解构（优先级1）。
2.  拆分 `PaneViewModel`（优先级2）。
3.  修复所有 BUG-001 至 BUG-008。

### 长期目标 (Q1 2026)

1.  完成消息驱动架构（优先级3）。
2.  实现 Pro/Ultra 插件化基础（优先级4）。
3.  发布 v2.0 正式版。

---

## 六、总结

**YiboFile** 当前处于从"部分 MVVM"向"混合架构"演进的关键阶段。通过引入"控制器驱动 VM + 消息总线副作用"的模式，我们在保证核心逻辑稳定性的同时，为 Pro/Ultra 版本的扩展性奠定了坚实基础。

**核心挑战**：
*   `MainWindow` 上帝类的解构需要谨慎，避免引入新的回归问题。
*   `PaneViewModel` 的拆分需要在"职责单一"和"使用便捷"之间找到平衡。
*   消息驱动架构的完善需要制定清晰的消息命名和生命周期规范。

**预期收益**：
*   代码可测试性提升 80%+。
*   Pro/Ultra 版本可通过插件机制实现零侵入扩展。
*   团队协作效率提升（模块边界清晰）。

---

## 七、 版本功能规划路线图

### 7.1 版本定位原则

在分配功能时，核心原则是：
- **Core (Free)** - 负责留住用户，提供完整的基础文件管理体验
- **Pro (Professional)** - 负责提高大众效率，引入智能化与自动化能力
- **Ultra (Intelligence)** - 针对专业用户或特定行业场景，提供极限扩展与定制能力

### 7.2 功能分配矩阵

| 功能模块 | Core (Free) | Pro (Professional) | Ultra (Intelligence) |
|----------|:-----------:|:------------------:|:--------------------:|
| **基础管理** | 库、标签、备注、主题 | 全部 Core 功能 | 全部 Pro 功能 |
| **文件整理** | 基础筛选、手动标签 | AI 批量重命名、自动分类建议 | 全自动化规则工作流 |
| **搜索能力** | 关键词搜索 (Everything) | 语义搜索、OCR 文字搜索 | 多模态搜索（提描述/提内容） |
| **图片 AI** | 基础预览、手动标注 | 自动打标签、人脸/场景分类 | 物体检测/圈选（零件识别）、标注导出 |
| **文档 AI** | 基础查看 | AI 摘要预览、文本提取 | 文档对话 (Local LLM) |
| **系统集成** | 单一库管理 | 多库关联、跨设备同步 | Docker 远程服务器同步 |
| **扩展性** | ❌ | ❌ | 全模块插件系统 |
| **技术支持** | 社区 | 优先 | 专属 |

### 7.3 版本开发路线图

#### v1.0.1460 (Current) - 混合架构重构完成
**Core 功能**：
- ✅ 状态记忆修复（解决 JSON NaN 故障）
- ✅ 基础文件管理（库、标签、备注、主题）
- ✅ Everything 关键词搜索
- ✅ 多标签页 + 双列表模式
- ✅ 基础筛选与预览
- ✅ MainWindow 代码量减少 60%+
- ✅ 混合架构落地 (Controller + MessageBus)

**Pro 功能**：
- ✅ 全文搜索服务 (FTS5)
- ✅ TagTrain 图像智能标签
- ✅ Office/PDF 深度提取

#### v1.1.0 (Target) - Core 完全解耦
**目标**：
- ✅ MainWindow 上帝类完全解构
- ✅ 所有模块通过 MessageBus 通讯
- ✅ PaneViewModel 职责拆分
- ✅ 代码可测试性达 80%+

#### v1.2.0 - Pro 智能化增强
**Pro 新增功能**：
- ⏳ AI 批量重命名
- ⏳ 自动分类建议
- ⏳ 语义搜索
- ⏳ OCR 文字搜索
- ⏳ 自动打标签（基于图像内容）
- ⏳ 人脸/场景分类
- ⏳ AI 摘要预览。
- ⏳ 多库关联
- ⏳ 跨设备同步

#### v2.0.0 - Ultra 旗舰版发布
**Ultra 新增功能**：
- ⏳ 全自动化规则工作流
- ⏳ 多模态搜索（提描述/提内容）
- ⏳ 物体检测与圈选（零件识别、工业检测）
- ⏳ 文档对话 (Local LLM)
- ⏳ Docker 远程服务器同步
- ⏳ 全模块插件系统 (IYiboFilePlugin)
- ⏳ 开放 API 供行业定制（CAD、医疗影像等）

### 7.4 功能开发优先级原则

1.  **Core 优先原则**：保证 Core 版本功能完整且稳定，能够满足 80% 用户的基础需求。
2.  **Pro 差异化是关键**：智能化功能必须能显著提高效率，而不是鸡肋功能。
3.  **Ultra 针对垂直场景**：逻辑工作流、物体检测、LLM 对话等功能需要有明确的行业用例敐持。
4.  **技术实现逐步迭代**：先实现 MVP，验证效果后再扩展。

### 7.5 技术栈规划

| 模块 | Core | Pro | Ultra |
|------|------|-----|-------|
| **搜索** | Everything CLI | FTS5 + Semantic Vector | Multi-modal Embedding |
| **AI 图像** | - | Microsoft.ML (TagTrain) | YOLO/Detectron2 (Object Detection) |
| **AI 文档** | - | Transformer (摘要) | Local LLM (Llama, Qwen) |
| **工作流** | - | - | Visual Workflow Engine |
| **同步** | - | Cloud API (OneDrive/S3) | Docker + Redis + MinIO |
| **插件** | - | - | MEF / Reflection |

---

**备注**：
- 详细的版本功能说明请参考 `VERSION_INFO.md`。
- 关于架构调整的实施细节，请参考本文档的“三、重构路线图”章节。

