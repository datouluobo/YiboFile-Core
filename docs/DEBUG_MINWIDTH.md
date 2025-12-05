# 列2和列3最小宽度问题调试说明

## 调试日志位置

调试日志会输出到Visual Studio的"输出"窗口中的"调试"面板。

## 如何查看调试日志

1. 在Visual Studio中运行程序（F5或Ctrl+F5）
2. 打开"输出"窗口：视图 -> 输出，或按 `Ctrl+Alt+O`
3. 在输出窗口的下拉菜单中选择"调试"
4. 拖动GridSplitter时，日志会实时显示

## 日志标签说明

- `[DragStarted]` - GridSplitter拖拽开始时的状态
- `[DragDelta]` - GridSplitter拖拽过程中的状态（每次移动都会触发）
- `[DragCompleted]` - GridSplitter拖拽结束时的状态
- `[LayoutUpdated]` - 布局更新时的状态（带防抖，50ms间隔）
- `[ForceColumnWidthsToFixed]` - 强制修复列宽时的状态

## 需要关注的信息

1. **IsStar** - 列是否使用Star模式（`Width="*"`）
   - 如果为`True`，说明列仍然是Star模式，需要改为固定宽度
   
2. **ActualWidth** - 列的实际宽度（像素）
   - 如果小于`MinWidth`，说明最小宽度约束没有生效
   
3. **MinWidth** - 列的最小宽度设置（列2=250px, 列3=360px）
   - 这是XAML中设置的最小宽度值
   
4. **Width.Value** - 列宽度对象的值（仅当不是Star模式时有意义）

## 测试步骤

1. 启动程序
2. 打开Visual Studio输出窗口的调试面板
3. 尝试拖动左侧和中间列之间的分割器（第一个分割器）
4. 尝试拖动中间和右侧列之间的分割器（第二个分割器）
5. 观察日志输出：
   - 查看`[DragStarted]`时的初始状态
   - 查看`[DragDelta]`中的列宽变化
   - 查看是否出现"修复列2"或"修复列3"的日志
   - 查看`[DragCompleted]`的最终状态

## 预期行为

- 列2（中间列）不应小于250px
- 列3（右侧列）不应小于360px
- 如果尝试拖到小于最小宽度，应该立即被修复
- 列2和列3不应该保持Star模式，应该改为固定宽度模式

## 问题排查

如果最小宽度仍然不生效，请检查日志中的：

1. **列是否一直是Star模式？**
   - 如果`IsStar`一直为`True`，说明转换为固定宽度失败
   - 可能是GridSplitter在拖拽时重新设置了Star模式

2. **ActualWidth是否小于MinWidth？**
   - 如果`ActualWidth < MinWidth`，说明最小宽度约束失效
   - 需要检查为什么修复代码没有执行

3. **修复代码是否执行？**
   - 查看是否有"修复列2"或"修复列3"的日志
   - 如果没有，说明检测条件有问题

## 将日志保存到文件（可选）

如果需要将日志保存到文件进行分析，可以添加文件输出：

```csharp
// 在ForceColumnWidthsToFixed方法开头添加
var logFile = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "minwidth_debug.log");
File.AppendAllText(logFile, $"[{DateTime.Now:HH:mm:ss.fff}] [ForceColumnWidthsToFixed] ...\n");
```

