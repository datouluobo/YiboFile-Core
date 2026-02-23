# 导航模块重构日志

**日期**: 2026-01-27
**目标**: 将 `MainWindow` 后置代码中的核心导航逻辑（后退、前进、向上）迁移至 MVVM 架构下的 `NavigationModule`。

## 1. 变更概述

本次重构主要关注将导航操作从 UI 事件驱动转变为 ViewModel 命令驱动，实现了 UI 与业务逻辑的进一步解耦。

## 2. 详细变更

### 2.1 基础架构更新 (`ModuleBase.cs`)
- **修改**: `ModuleBase` 类改为继承自 `BaseViewModel`。
- **目的**: 使所有模块能够支持 `INotifyPropertyChanged` 接口，从而能够通知 UI 属性的变化（如 `CanNavigateBack` 状态）。

### 2.2 导航模块增强 (`NavigationModule.cs`)
- **新增命令**:
    - `NavigateBackCommand`: 处理后退操作。
    - `NavigateForwardCommand`: 处理前进操作。
    - `NavigateUpCommand`: 处理向上导航操作。
    - `NavigateToCommand`: 通用导航命令。
    - `RefreshCommand`: 刷新命令。
- **状态管理**: 实现了 `UpdateCommandStates` 方法，使用 `CommandManager.InvalidateRequerySuggested()` 自动刷新命令的可用状态（CanExecute）。
- **逻辑封装**: 命令直接调用模块内的导航方法，并通过消息总线 (`IMessageBus`) 发布导航事件。

### 2.3 主窗口重构 (`MainWindow.xaml.cs`)
- **初始化顺序调整**: 将 `InitializeMvvmModules()` 的调用移动到 `InitializeEvents()` 之前。
    - **原因**: 确保在绑定 UI 事件到 ViewModel 命令时，ViewModel 和模块已经完全初始化。
- **移除旧代码**: 注释/移除了 `NavigateBack_Click`, `NavigateForward_Click`, `NavigateUp_Click` 等传统的事件处理方法。

### 2.4 事件绑定更新 (`MainWindow.Initialization.cs`)
- **修改**: `FileBrowser` 的导航事件（`NavigationBack`, `NavigationForward`, `NavigationUp`）不再调用后置代码方法，而是直接执行 `_viewModel.Navigation` 中的对应命令。
- **代码示例**:
  ```csharp
  FileBrowser.NavigationBack += (s, e) => _viewModel?.Navigation?.NavigateBackCommand?.Execute(null);
  ```

### 2.5 输入处理更新 (`MainWindow.Input.cs`)
- **修改**: 键盘快捷键（如 `Backspace` 返回上一级）的处理逻辑改为直接执行 `NavigateBackCommand`。

## 3. 结果与收益

- **解耦**: `MainWindow` 不再包含具体的导航历史管理和状态判断逻辑，所有逻辑内聚在 `NavigationModule` 中。
- **可测试性**: 导航逻辑现在位于 ViewModel 层，更容易进行单元测试。
- **一致性**: 统一了导航行为的入口（无论是点击按钮还是键盘快捷键，最终都执行同一个 Command）。
