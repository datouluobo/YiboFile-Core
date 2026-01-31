# 调试记录：底部文件信息栏不可见问题修复

> **修复日期**: 2026-01-31
> **涉及模块**: RightPanelControl, FileBrowserControl, FileInfoService, MainWindow.Handlers

## 一、问题描述

用户反馈在选中文件时，文件列表下方的“信息栏”（Info Area）不显示任何内容（如文件名、大小、修改日期等）。无论是在左侧主面板还是右侧副面板，选中文件后底部区域均为空白。

## 二、排查过程

### 2.1 初始假设与误区
最初我们假设问题出在全局的“右侧面板”（RightPanelControl），并尝试对其进行了一系列的调试：
- 在 `RightPanelViewModel` 中添加日志。
- 强制设置 `RightPanelControl` 中信息区的高度和背景色（粉色）。
- 添加显眼的调试文本。

**结果**：即使强制设置了背景色，用户仍然反馈“什么都看不到”。这让我们意识到我们可能调试错了对象。在双栏模式下，全局的右侧面板通常是折叠的，用户所指的“信息栏”实际上是位于每个文件列表底部的 `FileInfoPanel`（属于 `FileBrowserControl` 的一部分），而不是全局的 `RightPanelControl`。

### 2.2 深入代码审查
在通过截图确认是列表底部的面板后，我们专注于审查 `FileInfoService.cs` 和其调用逻辑。

#### 发现问题 1：样式资源过时
在 `FileInfoService.cs` 中，`CreateInfoPanel` 方法使用了旧的样式资源键：
```csharp
// 错误（旧代码）
labelText.SetResourceReference(TextBlock.ForegroundProperty, "ForegroundPrimaryBrush");
valueText.SetResourceReference(TextBlock.ForegroundProperty, "ForegroundSecondaryBrush");
```
由于 `App.xaml` 中已经移除了这些键并替换为新的 Theme System 键（如 `TextPrimaryBrush`），导致 TextBlock 虽然被创建了，但前景色无法解析（可能默认为透明或与背景同色），从而导致“不可见”。

#### 发现问题 2：服务初始化逻辑缺失
在最新的 MVVM 重构过程中，`MainWindow.Handlers.cs` 中的 `InitializeHandlers` 方法被大幅修改。检查发现：
- `FileInfoService` 虽然被设计为处理信息展示，但在 `SelectionEventHandler` 被创建时，并没有将 `FileInfoService` 的更新方法传递给它。
- `SelectionEventHandler` 的构造函数中 `updateInfoPanel` 参数被留空（null），导致当文件选中事件触发时，只会发送消息总线消息，但没有任何消费者去直接更新底部的 UI 面板。

## 三、解决方案

### 3.1 修复样式资源
将 `FileInfoService.cs` 中所有过时的资源键引用更新为当前主题系统的标准键：
- `ForegroundPrimaryBrush` -> `TextPrimaryBrush`
- `ForegroundSecondaryBrush` -> `TextSecondaryBrush`

### 3.2 重新接线业务逻辑
在 `MainWindow.Handlers.cs` 中：
1. **实例化服务**：在初始化 Handler 之前，手动实例化了两个 `FileInfoService`（分别对应主浏览器和副浏览器）。
2. **注入回调**：在创建 `SelectionEventHandler` 和 `FileListEventHandler` 时，将 `localFileInfoService.ShowFileInfo(item)` 作为回调函数注入。
3. **处理清除逻辑**：确保在取消选择或清空列表时，调用 `ShowFileInfo(null)` 来清空面板。

```csharp
// 修复后的关键代码片段 (MainWindow.Handlers.cs)
var localFileInfoService = new Services.FileInfo.FileInfoService(
    FileBrowser, 
    _fileListService, 
    _navigationCoordinator, 
    tagService
);

_selectionEventHandler = new SelectionEventHandler(
    // ... 其他参数
    (item) => localFileInfoService.ShowFileInfo(item), // 注入更新回调
    // ...
);
```

### 3.3 清理调试代码
移除了之前在 `RightPanelControl.xaml` 和 `RightPanelViewModel.cs` 中添加的所有临时调试代码（红色文字、粉色背景、测试日志项），恢复代码库的整洁。

## 四、验证结果

- **功能恢复**：现在选中文件列表中的项目时，底部的 `FileInfoPanel` 应能正确显示文件的详细信息（名称、大小、日期等）。
- **双向支持**：主面板和副面板（如果有）均能独立显示各自选中文件的信息。
- **视觉正常**：文本颜色正确适配当前主题（亮色/暗色）。
