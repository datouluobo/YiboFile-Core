---
description: 文件列表列宽调整系统重构 — 分阶段执行计划
---

# 列宽调整系统重构执行计划

## 重构目标

消除当前列宽调整系统的三个核心缺陷：
1. **双路径差异**：`AutoFitNameColumn` 和 `AdjustTargetColumnWidth` 两套独立计算，MacOS 主题下差值达 70px
2. **配置频繁覆盖**：窗口 Resize 时 `AdjustListViewColumnWidths` 用配置旧值覆盖用户刚拖拽的列宽
3. **溢出压缩不区分可调/不可调列**：极端窄窗口下不可调列被压缩到 30px，内容不可读

## 总体执行策略

```
Phase 1 (C) → Phase 2 (A) → Phase 3 (B)
低风险先行    统一引擎       智能压缩
~5行改动     ~80行改动      ~40行改动
```

---

## Phase 1：消除配置覆盖（方案 C）

### 问题描述

`ColumnService.AdjustListViewColumnWidths()` 内部调用 `LoadColumnWidths()`，导致每次窗口 Resize 都从持久化配置重新加载全部列宽，覆盖用户刚拖拽但尚未保存的 Name 列宽度。

### 改造内容

**文件**: `YiboFile-Core/Services/UI/ColumnManagement/ColumnService.cs`

**改造前**:
```csharp
public void AdjustListViewColumnWidths(FileBrowserControl fileBrowser)
{
    if (fileBrowser?.FilesGrid == null) return;
    try
    {
        LoadColumnWidths(fileBrowser);  // ← 全量加载，覆盖内存值
    }
    catch { }
}
```

**改造后**:
```csharp
public void AdjustListViewColumnWidths(FileBrowserControl fileBrowser)
{
    if (fileBrowser?.FilesGrid == null) return;
    // 不重新加载配置，仅触发弹性列重算
    fileBrowser.AutoColumnWidthBehavior?.AdjustTargetColumnWidth();
}
```

### 前置条件

- `FileListControl` 需暴露 `AutoColumnWidthBehavior` 实例的公共引用（或提供 `RequestColumnRecalculation()` 桥接方法）

### 验证要点

// turbo
1. 窗口 Resize 后，手动拖拽的 Name 列宽度不被覆盖
2. DualPane 模式下两栏各自保持独立的列宽
3. 初始加载和标签页切换仍正确走 `LoadColumnWidths` 路径

### 不影响的场景

- 初始加载（Loaded 事件中独立调用 `LoadColumnWidths`）
- 标签页切换 / 导航模式变更（各自独立调用 `LoadColumnWidths`）
- 主题切换（需确认现有主题切换链路中有独立的 `LoadColumnWidths` 调用）

---

## Phase 2：统一计算引擎（方案 A）

### 问题描述

`ColumnInteractionHandler.AutoFitNameColumn()` 和 `AutoColumnWidthBehavior.AdjustTargetColumnWidth()` 是两套独立实现，padding 计算方式不同：
- `AdjustTargetColumnWidth` 读取主题资源 `UI.FileList.RowMargin` + `UI.FileList.RowPadding`
- `AutoFitNameColumn` 仅用硬编码 `padding = 2`

### 改造内容

**核心变更**:

| 文件 | 操作 |
|------|------|
| `ColumnInteractionHandler.cs` | 删除 `AutoFitNameColumn()` 方法体（约 80 行），替换为调用 `AutoColumnWidthBehavior` |
| `FileListControl.xaml.cs` | 暴露公共方法 `RequestColumnRecalculation()` → 内部调用 `_autoColumnWidthBehavior.AdjustTargetColumnWidth()` |
| `ColumnInteractionHandler.cs` | 所有原调用 `AutoFitNameColumn()` 的位置替换为 `_fileBrowser.RequestColumnRecalculation()` |

**需要修改调用点**（在 `ColumnInteractionHandler.cs` 中）:

1. `HandleColumnVisibilityChange()` 中的 `Dispatcher.BeginInvoke → AutoFitNameColumn()`
2. `OnColumnPropertyChanged()` 中防抖计时器到期后的 `AutoFitNameColumn()`
3. 双击分隔条自动适应后的 `AutoFitNameColumn()`

**附带修复**: `_cachedHeader` 在主题切换后失效的问题
- 在 `AdjustTargetColumnWidth()` 开头增加 `_cachedHeader.IsLoaded` 校验
- 若失效则重新查找

### 验证要点

1. 各主题下 Name 列宽度在以下场景中一致且无抖动：
   - 窗口 Resize
   - 隐藏/显示某列
   - 拖拽 Name 列 Thumb 后释放
2. MacOS 主题下不再出现 Name 列宽度在两个值之间跳动的现象
3. 主题切换后表头 Margin 正确更新

### 主题差异消除预期

| 主题 | 改造前差值 | 改造后差值 |
|------|-----------|-----------|
| MacOS | 70px | 0px |
| Fluent | ~38px | 0px |
| Antigravity | ~36px | 0px |
| Original | ~22px | 0px |
| Geek | ~16px | 0px |
| OneCommander | ~16px | 0px |

---

## Phase 3：智能溢出压缩（方案 B）

### 前置条件

Phase 2 已完成（统一引擎），确保只需在一处实现压缩逻辑。

### 问题描述

当前溢出压缩对所有 `otherColumns` 等比缩放（最小 30px），不区分可调/不可调列。在 DualPane 模式下（每栏约一半宽度），不可调列可能被压到完全不可读。

### 改造内容

**文件**: `AutoColumnWidthBehavior.cs` 的 `AdjustTargetColumnWidth()` 第二阶段

**改造前**:
```csharp
// 等比压缩，最小 30px
double scale = targetOtherWidth / otherColumnsWidth;
double minOtherCol = 30;
foreach (var col in otherColumns)
{
    double w = ...;
    col.Width = Math.Max(minOtherCol, w * scale);
}
```

**改造后**: 三级瀑布压缩策略

```csharp
// 三级瀑布压缩：
// 1️⃣ 先压缩 Name 列到 minFillWidth
// 2️⃣ 仍溢出 → 压缩不可调列，遵守各列硬下限
// 3️⃣ 极端情况 → 按优先级隐藏列 (CreatedTime → ModifiedDate → Size → Type)
```

**新增硬下限映射**:
```csharp
private static readonly Dictionary<string, double> MinColumnWidths = new()
{
    { "Type",         60 },
    { "Size",         80 },
    { "ModifiedDate", 90 },
    { "CreatedTime",  60 },
    { "Tags",        100 },
    { "Notes",       100 },
};
```

### 验证要点

1. DualPane + MacOS 主题（最严苛场景）：
   - 窄窗口时 Name 列先压缩
   - Type/Size 等列不会小于硬下限
   - 极端窄窗口时按优先级隐藏列，而非无限压缩
2. 切回单栏后，被隐藏的列自动恢复

---

## 回滚策略

每个 Phase 独立提交，回归测试验证后再进入下一阶段。若某阶段出现问题：
- Phase 1: 回退 `AdjustListViewColumnWidths` 为调用 `LoadColumnWidths`
- Phase 2: 恢复 `AutoFitNameColumn` 方法
- Phase 3: 恢复等比压缩逻辑

## 测试矩阵

| 场景 | Original | Fluent | MacOS | Geek | DualPane |
|------|----------|--------|-------|------|----------|
| 窗口 Resize | ✓ | ✓ | ✓ | ✓ | ✓ |
| 拖拽 Name 列 | ✓ | ✓ | ✓ | ✓ | ✓ |
| 隐藏/显示列 | ✓ | ✓ | ✓ | ✓ | ✓ |
| 极端窄窗口 | ✓ | ✓ | ✓ | ✓ | ✓ |
| 主题切换 | ✓ | ✓ | ✓ | ✓ | ✓ |
| Single↔DualPane | ✓ | ✓ | ✓ | ✓ | — |
