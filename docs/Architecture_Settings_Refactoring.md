# SettingsViewModel 重构文档

> **创建日期**: 2026-01-27
> **状态**: 已完成

## 1. 概述

本项目已完成对 `SettingsViewModel` 的重构。原有的 `SettingsViewModel` 是一个庞大的“上帝类” (God Class)，包含所有设置面板的逻辑，导致代码难以维护且耦合度高。

重构的目标是将 `SettingsViewModel` 拆分为多个单一职责的 ViewModel，每个 ViewModel 对应一个具体的设置面板。

## 2. 架构变更

### 2.1 旧架构

*   **SettingsViewModel.cs**: 包含所有设置逻辑 (外观, 常规, 快捷键, 库管理, 搜索, 标签等)。
*   **SettingsViewModel.\*.cs**: 通过 partial class 将逻辑分散在不同文件中，但本质上仍属于同一个类。
*   **SettingsPanel**: 所有的设置 Panel (如 `GeneralSettingsPanel`, `AppearanceSettingsPanel`) 都共享同一个 `SettingsViewModel` 实例。

### 2.2 新架构

我们创建了 `ViewModels/Settings/` 目录，并为每个设置类别创建了独立的 ViewModel：

| 设置面板 | 新 ViewModel | 职责 |
| :--- | :--- | :--- |
| `AppearanceSettingsPanel` | **`AppearanceSettingsViewModel`** | 管理主题、强调色、窗口不透明度、动画开关等。 |
| `GeneralSettingsPanel` | **`GeneralSettingsViewModel`** | 管理窗口行为、多窗口模式、字体大小、标签页宽度等通用设置。 |
| (通用/导入导出) | **`DataSettingsViewModel`** | 管理配置和数据的导入/导出命令。 |
| `PathSettingsPanel` | **`NavigationSettingsViewModel`** | 管理导航栏部分（侧边栏）的显示和排序。 |
| `HotkeySettingsPanel` | **`HotkeySettingsViewModel`** | 管理全局和局部快捷键的设置与重置。 |
| `LibrarySettingsPanel` | **`LibrarySettingsViewModel`** | 管理资料库的添加、移除、导入和导出。 |
| `SearchSettingsPanel` | **`SearchSettingsViewModel`** | 管理全文搜索开关、索引重建、搜索历史记录等。 |
| `TagManagementPanel` | **`TagSettingsViewModel`** | 管理标签分组及标签的增删改查。 |
| `FileListSettingsPanel` | **`FileListSettingsViewModel`** | 管理文件列表的列宽等显示设置。 |

### 2.3 核心基类

所有新的 ViewModel 均继承自 `BaseViewModel`，保留了 `SetProperty` 和 `OnPropertyChanged` 等基础 MVVM 功能。

## 3. 迁移细节

### 3.1 依赖与服务

*   **ConfigurationService**: 大多数设置 ViewModel 仍依赖 `ConfigurationService` 来读写 `AppConfig`。
*   **Services**: 特定的 ViewModel 依赖相应的服务，例如 `LibrarySettingsViewModel` 依赖 `LibraryService`，`TagSettingsViewModel` 依赖 `TagService`。

### 3.2 UI 绑定更新

所有相关的 `.xaml.cs` 文件已更新：
1.  构造函数中实例化对应的具体 ViewModel，而非 `SettingsViewModel`。
2.  `DataContext` 设置为新的 ViewModel 实例。
3.  XAML 绑定路径保持不变（因为属性名在迁移过程中大多保持一致），部分涉及命令绑定的 `nameof` 引用已更新。

### 3.3 废弃代码

以下文件已被**删除**:
*   `ViewModels/SettingsViewModel.cs`
*   `ViewModels/Settings/SettingsViewModel.Appearance.cs`
*   `ViewModels/Settings/SettingsViewModel.Data.cs`
*   `ViewModels/Settings/SettingsViewModel.Hotkeys.cs`
*   `ViewModels/Settings/SettingsViewModel.Libraries.cs`
*   `ViewModels/Settings/SettingsViewModel.Navigation.cs`
*   `ViewModels/Settings/SettingsViewModel.Search.cs`
*   `ViewModels/Settings/SettingsViewModel.Tags.cs`

## 4. 后续维护

*   **新增设置**: 如需新增设置项，请先确定其所属类别。如果是现有类别，直接在对应的 ViewModel 中添加；如果是全新类别，请创建新的 `CreateNewSettingsViewModel`。
*   **性能**: 拆分后的 ViewModel 实例化更轻量，这有助于提高设置窗口打开时的响应速度，因为只有当前显示的 Panel 对应的 ViewModel 会被加载（如果 Panel 是按需加载的话）。

---

**总结**: 此次重构显著降低了代码耦合度，提高了可维护性，符合 MVVM 的最佳实践。
