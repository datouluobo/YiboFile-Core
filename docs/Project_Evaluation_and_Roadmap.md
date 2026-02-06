# YiboFile 项目评估与行动计划

> **版本**: v1.0.320 | **更新日期**: 2026-02-05

---

## 一、项目评估与架构现状

### 1.1 总体评价

**YiboFile** 核心状态管理已基本完成从 WinForms/Code-behind 到 **MVVM 架构** 的大规模迁移。主副双栏面板已实现逻辑对等与状态独立。

| 维度 | 评价 | 迁移进度 |
22: |------|------|------|
23: | ✅ **数据建模 (Models)** | 完善的文件系统、库、标签实体模型 | 100% |
24: | ✅ **展现逻辑 (ViewModels)** | PaneViewModel 与 FileListViewModel 承载核心业务 | 90% |
25: | ⚠️ **UI 交互 (View/Events)** | 仍有 Handler 类作为过渡层处理 UI 事件 | 60% |
26: | ⚠️ **组件解耦** | 全局设置与右键菜单仍偏向单例/Code-behind | 40% |

### 1.2 核心问题与瓶颈

| 领域 | 描述 | 状态 |
|------|------|------|
| **UI 事件驱动** | 部分操作依赖 `FileBrowserEventHandler` 而非 VM 命令 | 🟡 迁移中 |
| **控件属性同步** | 地址栏与面包屑部分同步依赖 Bridge 而非纯数据绑定 | 🟡 迁移中 |
| **路径闪变 (Fix)** | 库/虚拟协议在加载完成后被物理路径覆盖的 Bug | ✅ v1.0.310 已修复 |
| **拖拽稳定性 (Fix)**| 拖拽到文件导致的 Dialog 样式崩溃 | ✅ v1.0.310 已修复 |

---

## 二、本次更新 (v1.0.320) ✅

### 2.1 拖拽与导航深度修复
- [x] **ConfirmDialog 物理隔绝**: 重构确认对话框 XAML，不依赖任何全局样式资源，彻底消除由于 XamlParseException 导致的崩溃。
- [x] **非法拖拽拦截**: 在 `DragDropManager` 的 `DragOver` 阶段增加类型检查，拖拽到非文件夹目标时显示禁用光标，从源头防止崩溃。
- [x] **路径闪变拦截**: 在 `PaneViewModel` 增加协议保护逻辑，禁止虚拟路径（`lib://`, `tag://`）被自动加载出的物理路径覆盖，确保地址栏状态稳定。

---

## 三、待解决问题与行动计划 (MVVM 深度解耦)

### 3.1 方向一：主副栏 MVVM 迁移剩余部分 (当前重点) ⏳
彻底消除 `FileBrowserEventHandler` 和 `FileBrowserBridge` 的依赖。

| 步骤 | 任务描述 | 验证方法 | 状态 |
|------|----------|----------|------|
| **1.1** | **纯绑定式地址栏** | 将 `AddressText` 与 `CurrentPath` 双向绑定，引入 Converter 处理 `lib://` 显示 | 移除手动 SetText 代码，修复 BUG-001 | ✅ 已完成 |
| **1.2** | **Tab 系统 UI 解耦** | 重构 `TabService.UI`，使用 ObservableCollection // DataTemplate 驱动 Tab 渲染 | 移除 TabManager 引用，Tab 创建完全数据驱动 | ✅ 已完成 |
| **1.3** | **右键菜单 Command 化** | 将文件/库右键菜单绑定到 VM Command | 移除 `MenuEventHandler` 和 `ContextMenuBuilder` 中的 Click 事件 | ⚪ 计划中 |
| **1.4** | **工具栏按钮 Command 化** | 将刷新、删除等按钮 Click 事件改为绑定 VM 命令 | 移除 `ButtonEvents.cs` 相关代码 | ⚪ 计划中 |

### 3.2 方向二：全局功能模块迁移 🟡

| 模块 | 迁移目标 | 技术难点 |
|------|----------|----------|
| **MenuModule** | 将右键菜单（Context Menu）与快捷键逻辑命令化 | 动态菜单项的 Command 绑定 |
| **ConfigModule** | 实现 SettingsViewModel 与配置文件的响应式关联 | 配置实时生效机制 |
| **TagManagement** | 标签增删改查完全由 TagManagementViewModel 驱动 | 颜色选择器的 VM 集成 |
| **DragDrop** | 实现声明式拖拽反馈逻辑 | 跨进程拖拽状态在 VM 中的实时反映 |

### 3.3 迁移过程中的已知 Bug (暂缓修复) 🐛

由于系统处于 MVVM 混合状态，以下 Bug 预期将在后续完全迁移后自然解决或统一修复：

| ID | 模块 | 描述 | 状态 |
|----|------|------|------|
| **BUG-001** | **UI/AddressBar** | **副地址栏库标识错误**: 在库模式下，主地址栏正确显示 `lib 库名`，但副地址栏仍显示为 `path 库名`。这可能是由于副面板的 ViewModel 或 UI 绑定未能像主面板一样完全更新协议头导致的。 | ⏳ 待迁移后修复 |

---

## 四、总结与展望

**YiboFile** 1.0.320 版本通过“釜底抽薪”式的重构解决了长期困扰的 UI 稳定性问题。下一阶段的目标是“削藩”，即削减 `MainWindow` 及其关联 Handler 的权利，让 ViewModel 成为真正的业务指挥中心。

