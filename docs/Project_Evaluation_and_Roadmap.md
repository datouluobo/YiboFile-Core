# YiboFile 项目评估与行动计划

> **版本**: v1.0.400 | **更新日期**: 2026-02-07

---

## 一、项目评估与架构现状

### 1.1 总体评价

**YiboFile** 核心状态管理已基本完成从 WinForms/Code-behind 到 **MVVM 架构** 的大规模迁移。主副双栏面板已实现逻辑对等与状态独立。

| 维度 | 评价 | 迁移进度 |
|------|------|------|
| ✅ **数据建模 (Models)** | 完善的文件系统、库、标签实体模型 | 100% |
| ✅ **展现逻辑 (ViewModels)** | PaneViewModel 与 FileListViewModel 承载核心业务 | 90% |
| ⚠️ **UI 交互 (View/Events)** | 仍有 Handler 类作为过渡层处理 UI 事件 | 65% |
| ⚠️ **组件解耦** | 全局设置与右键菜单仍偏向单例/Code-behind | 45% |

### 1.2 核心问题与瓶颈

| 领域 | 描述 | 状态 |
|------|------|------|
| **UI 事件驱动** | 部分操作依赖 `FileBrowserEventHandler` 而非 VM 命令 | 🟡 迁移中 |
| **控件属性同步** | 地址栏与面包屑部分同步依赖 Bridge 而非纯数据绑定 | 🟡 迁移中 |
| **路径闪变 (Fix)** | 库/虚拟协议在加载完成后被物理路径覆盖的 Bug | ✅ v1.0.310 已修复 |
| **拖拽稳定性 (Fix)**| 拖拽到文件导致的 Dialog 样式崩溃 | ✅ v1.0.310 已修复 |

---

## 二、本次更新 (v1.0.330) ✅

### 2.1 拖拽与导航深度修复
- [x] **ConfirmDialog 物理隔绝**: 重构确认对话框 XAML，不依赖任何全局样式资源，彻底消除由于 XamlParseException 导致的崩溃。
- [x] **非法拖拽拦截**: 在 `DragDropManager` 的 `DragOver` 阶段增加类型检查，拖拽到非文件夹目标时显示禁用光标，从源头防止崩溃。
- [x] **路径闪变拦截**: 在 `PaneViewModel` 增加协议保护逻辑，禁止虚拟路径（`lib://`, `tag://`）被自动加载出的物理路径覆盖，确保地址栏状态稳定。

### 2.2 导航面板现代化 (v1.0.330)
- [x] **交互修复**: 修复了快速访问、收藏夹和库列表的左键点击导航无效问题，现已支持单击直接切换路径。
- [x] **拖拽支持**: 现在支持将文件直接拖拽到导航面板的任何节点（如库、收藏夹、驱动器）进行复制/移动操作。
- [x] **状态持久化**: 侧边栏的折叠状态（如驱动器、快速访问的展开/折叠）现在会自动保存，并在下次启动时恢复。

---

## 三、待解决问题与行动计划 (MVVM 深度解耦)

### 3.1 方向一：主副栏 MVVM 迁移剩余部分 (当前重点) ⏳

#### 现状分析：右键菜单与工具栏

经过代码审查，**右键菜单（Context Menu）和工具栏按钮（Toolbar）已基本完成MVVM化**，但存在"双轨制"问题：

| 组件 | MVVM进度 | 现状描述 | 问题 |
|------|----------|----------|------|
| **右键菜单** | ✅ 95% | `FileBrowserControl.xaml` 中的右键菜单已全部绑定到 `PaneViewModel` 的 Command | `MenuEventHandler` 仍然存在并被初始化，造成代码冗余 |
| **工具栏按钮** | ✅ 95% | `TitleActionBar.xaml` 中的按钮已全部绑定到 `PaneViewModel` 的 Command | 同上，Handler 依然在处理旧的文件操作逻辑（第528-531行） |
| **Command 定义** | ✅ 100% | `PaneViewModel` 中已定义所有必需的 `ICommand` (DeleteCommand, CopyCommand, PasteCommand等) | 无 |
| **Command 实现** | ✅ 100% | 所有 Command 通过 MessageBus 发布请求消息，由 `FileOperationModule` 统一处理 | 无 |

**详细发现**：
1.  **XAML 层面已完成 MVVM 化**：
    - `FileBrowserControl.xaml` 第116-233行：右键菜单全部通过 `Command="{Binding XXXCommand}"` 绑定。
    - `TitleActionBar.xaml` 第12-158行：所有工具栏按钮全部通过 `Command="{Binding XXXCommand}"` 绑定。
2.  **ViewModel 层面已完成**：
    - `PaneViewModel.cs` 第350-375行：定义了完整的 Command 属性。
    - `PaneViewModel.cs` 第408-437行：在构造函数中初始化了所有 Command。
    - `PaneViewModel.cs` 第542-551行：Command 的实现通过 `_messageBus.Publish()` 发送业务消息。
3.  **冗余代码未清理**：
    - `MenuEventHandler` 类（644行代码）仍被初始化（`MainWindow.Handlers.cs` 第483行），但其核心方法已被 ViewModel Command 替代。
    - `MainWindow.Handlers.cs` 第528-531行仍保留了对 `CopySelectedFilesAsync()` 等方法的引用，但这些方法本应由 `FileOperationModule` 接管。

#### 下一步行动计划

| 步骤 | 任务描述 | 预计工作量 | 状态 |
|------|----------|------------|------|
| **1.3.1** | **审核并移除 MenuEventHandler 的初始化调用** | 0.5h | ✅ 已完成 |
| **1.3.2** | **确认所有右键菜单项均已绑定 VM Command，无遗漏** | 0.5h | ✅ 已完成 |
| **1.3.3** | **移除 MainWindow 中冗余的文件操作桥接方法** (CopySelectedFilesAsync, DeleteSelectedFilesAsync等) | 1h | ✅ 已完成 |
| **1.3.4** | **删除 MenuEventHandler.cs 文件** | 0.2h | ✅ 已完成 |
| **1.3.5** | **回归测试：验证所有右键菜单和工具栏功能正常** | 1h | ⏳ 已部分测试（副面板问题已确认暂缓） |
| **1.4** | **工具栏按钮 Command 化** | 将刷新、删除等按钮 Click 事件改为绑定 VM 命令 | 移除 `ButtonEvents.cs` 相关代码 | ✅ 已完成 |

**已完成工作摘要（v1.0.330后）**：
- ✅ 删除了 `MenuEventHandler.cs` 文件（644行代码清理）
- ✅ 移除了 `_menuEventHandler` 字段声明及其110行的初始化代码块
- ✅ 删除了所有文件操作相关的桥接方法（Copy/Cut/Paste/Delete/Rename等）
- ✅ 将 `FileListEventHandler` 和 `KeyboardEventHandler` 中对 `_menuEventHandler` 的调用改为直接调用 `PaneViewModel` 的 Command
- ✅ 为库管理功能添加了临时实现（待后续迁移到LibraryManagementViewModel）

**遗留工作（非阻塞）**：
- ⚠️ `MainWindow.Menu.cs` 中的顶部菜单栏事件（Settings、About、View模式等）仍有部分代码待迁移，属于全局菜单功能，不影响核心文件操作
- ⚠️ 其他Handler（FileBrowserEventHandler、FileListEventHandler等）仍作为过渡层存在，后续将逐步迁移

### 3.2 方向二：全局功能模块迁移 🟡

| 模块 | 迁移目标 | 技术难点 |
|------|----------|----------|
| **MenuModule** | 将右键菜单（Context Menu）与快捷键逻辑命令化 | 动态菜单项的 Command 绑定 |
| **ConfigModule** | 实现 SettingsViewModel 与配置文件的响应式关联 | 配置实时生效机制 |
| **TagManagement** | 标签增删改查完全由 TagManagementViewModel 驱动 | 颜色选择器的 VM 集成 |
| **DragDrop** | 实现声明式拖拽反馈逻辑 | 跨进程拖拽状态在 VM 中的实时反映 |

### 3.3 迁移过程中的已知 Bug 🐛

| ID | 模块 | 描述 | 状态 |
|----|------|------|------|
| **BUG-001** | **UI/AddressBar** | **副地址栏库标识错误**: 在库模式下，主地址栏正确显示 `lib 库名`，但副地址栏仍显示为 `path 库名`。这可能是由于副面板的 ViewModel 或 UI 绑定未能像主面板一样完全更新协议头导致的。 | ⏳ 待迁移后修复 |
| **BUG-002** | **Logic/Pane** | **副面板刷新与操作混乱** ✅确认: 副面板进行删除/粘贴操作时常导致主面板刷新，或副面板自身不刷新；F5 刷新对副面板无效。根源在于操作上下文（Context）仍重度依赖 WPF 焦点而非 VM 激活状态。 | ⏳ 待 MVVM 彻底解耦后修复 |
| **BUG-003** | **Logic/Library** | **副面板库路径识别失败** ✅确认: 在库模式下，副面板的文件操作无法正确解析 `lib://` 协议，导致路径指向错误。 | ⏳ 待 MVVM 彻底解耦后修复 |
| **BUG-004** | **UI/FileList** | **副列表库加载失效** ✅ v1.0.400 已修复 | ✅ 已修复 |
| **BUG-005** | **Filter/Secondary** | **副列表过滤器不可用** ✅ v1.0.400 已修复 | ✅ 已修复 |
| **BUG-006** | **Filter/FileSize** | **尺寸过滤器未排除文件夹** ✅ v1.0.400 已修复 | ✅ 已修复 |
| **BUG-007** | **UI/Sorting** | **文件名排序导致列表变空**: 当按文件名排序时，有时列表会变空。需要检查排序后的 `CollectionView` 刷新逻辑及 `FileListService` 的排序冲突。 | ⏳ 待修复 |
| **BUG-008** | **UI/Header** | **列头点击误触发双击响应**: 点击列头（用于排序）不应该触发或响应双击事件（双击通常用于返回上层目录或打开文件）。需要优化 `FileListEventHandler` 的事件冒泡拦截。 | ⏳ 待修复 |

---

## 四、总结与展望

**YiboFile** 1.0.400 版本通过 MVVM 深度重构解决了过滤器的稳定性问题，并修复了多处初始化崩溃。下一阶段的目标是进一步增强数据绑定的健壮性，解决排序导致的列表显示异常。


