---
description: 文件列表列宽系统目标规格 — 重构后的最终行为定义
---

# 文件列表列宽系统目标规格

> 本文档定义列宽调整系统在重构完成后的目标行为，作为后续开发、测试和维护的基准参考。

---

## 1. 列定义与角色

### 1.1 列清单

| 序号 | 列名 | Tag | 默认宽度 | 角色 | 可用户拖拽 | XAML 名称 |
|------|------|-----|----------|------|-----------|-----------|
| 1 | 名称 | `Name` | 弹性 | **弹性列 (Fill)** | ✅ 是 | `ColName` |
| 2 | 类型 | `Type` | 80px | 固定列 (Fixed) | ❌ 否 | `ColType` |
| 3 | 大小 | `Size` | 100px | 固定列 (Fixed) | ❌ 否 | `ColSize` |
| 4 | 修改日期 | `ModifiedDate` | 100px | 固定列 (Fixed) | ❌ 否 | `ColModifiedDate` |
| 5 | 创建时间 | `CreatedTime` | 80px | 固定列 (Fixed) | ❌ 否 | (无 x:Name) |
| 6 | 标签 | `Tags` | 150px | 固定列 (Fixed) | ❌ 否 | `ColTags` |
| 7 | 备注 | `Notes` | 200px | 固定列 (Fixed) | ❌ 否 | `ColNotes` |

### 1.2 角色定义

- **弹性列 (Fill)**：宽度 = 可用总宽 − 所有可见固定列宽度之和。自动填充剩余空间。
- **固定列 (Fixed)**：宽度由配置或默认值决定，不随容器尺寸自动变化。用户不可通过 UI 拖拽调整。

### 1.3 弹性列标识方式

```xml
<!-- XAML 声明 (目标方案) -->
<GridViewColumn x:Name="ColName"
    behaviors:AutoColumnWidthBehavior.IsFillColumn="True">
```

> 弃用硬编码 `_targetColumnName = "Name"` 的判定方式，改为基于 `IsFillColumn` 附加属性。

---

## 2. 总宽度约束

### 2.1 水平滚动条策略

```
规则: 文件列表 ListView 永远不显示水平滚动条
实现: ScrollViewer.HorizontalScrollBarVisibility = Disabled
```

此约束通过三层保障：
1. XAML 声明 (`FileListControl.xaml`)
2. 视图模式切换时代码强制 (`FileListViewModeHelper`)
3. 列宽计算引擎主动控制 `Σ(列宽) ≤ usableWidth`

### 2.2 可用宽度计算公式

```
usableWidth = availableWidth − leftCorrection − rightCorrection − safetyPadding

其中:
  availableWidth = ScrollViewer.ViewportWidth (优先) 
                   或 ListView.ActualWidth - scrollBarWidth (fallback)
  leftCorrection  = UI.FileList.RowMargin.Left + UI.FileList.RowPadding.Left
  rightCorrection = UI.FileList.RowMargin.Right + UI.FileList.RowPadding.Right
  safetyPadding   = Overlay模式 ? 20 : 16
```

### 2.3 各主题下的修正量参考

| 主题 | RowMargin (L,R) | RowPadding (L,R) | 总修正量(左+右) | 滚动条模式 | Safety |
|------|-----------------|-------------------|----------------|-----------|--------|
| Original | 0, 0 | 4, 0 | 4px | Overlay | 20 |
| Fluent | 8, 8 | 12, 12 | 40px | Overlay | 20 |
| MacOS | 10, 10 | 16, 16 | 52px | Overlay | 20 |
| Antigravity | 8, 8 | 10, 10 | 36px | Overlay | 20 |
| Geek | 0, 0 | 2, 0 | 2px | Classic | 16 |
| OneCommander | 0, 0 | 2, 0 | 2px | Classic | 16 |

---

## 3. 列宽调整行为规格

### 3.1 唯一计算引擎

所有列宽重算统一由 `AutoColumnWidthBehavior.AdjustTargetColumnWidth()` 执行。

```
触发源:
  ┌─ ListView.SizeChanged (宽度变化)
  ├─ 列可见性切换
  ├─ 用户拖拽 Name 列 Thumb 释放
  ├─ 数据源 / DataContext 变更
  ├─ 控件 Loaded
  └─ WindowLifecycleHandler 通知

全部 → AdjustTargetColumnWidth() (唯一入口)
```

> `ColumnInteractionHandler` 不再包含独立的计算逻辑，仅负责触发信号。

### 3.2 弹性列宽度分配

```
fillWidth = usableWidth − Σ(可见固定列宽度)
Name.Width = max(40, fillWidth / fillColumnCount)
```

- 隐藏列 (Width=0) 不计入固定列宽度之和
- `fillColumnCount` 目前为 1（仅 Name），未来可扩展

### 3.3 溢出压缩策略（三级瀑布）

当所有可见列的总宽度超出 `usableWidth` 时，按以下优先级依次处理：

```
Level 1: 压缩弹性列 (Name)
  → Name.Width = max(40, usableWidth − Σ(固定列))
  
Level 2: 压缩固定列（不低于硬下限）
  → 等比缩放固定列
  → 约束: Type ≥ 60, Size ≥ 80, ModifiedDate ≥ 90
  →        CreatedTime ≥ 60, Tags ≥ 100, Notes ≥ 100
  
Level 3: 极端情况 — 按优先级隐藏列
  → 隐藏顺序: CreatedTime → ModifiedDate → Size → Type
  → Tags 和 Notes 由用户手动控制，不自动隐藏
```

### 3.4 固定列硬下限

| 列 Tag | 硬下限 | 理由 |
|--------|--------|------|
| `Type` | 60px | 至少显示 ".docx" |
| `Size` | 80px | 至少显示 "999 MB" |
| `ModifiedDate` | 90px | 至少显示 "2025/01/01" |
| `CreatedTime` | 60px | 至少显示时间 Badge |
| `Tags` | 100px | 至少显示 1 个 Tag |
| `Notes` | 100px | 至少显示约 8 个中文字符 |

---

## 4. 列可见性管理

### 4.1 隐藏实现

列隐藏通过 `column.Width = 0` 实现（WPF `GridViewColumn` 无 `Visibility` 属性）。

### 4.2 可见性配置存储

```
AppState.Panes[n].Columns.VisibleColumns = {
    "Path":    "Name,Type,Size,ModifiedDate,Tags",
    "Library": "Name,Tags,Notes",
    "Tag":     "Name,Tags,Notes"
}
```

- 按 `PaneIndex`（0=左栏, 1=右栏）独立存储
- 按导航模式（Path / Library / Tag）独立存储
- 通过 `ConfigurationService.MarkDirty()` 标记延迟保存

### 4.3 隐藏/显示后的链路

```
隐藏列:
  1. RememberColumnWidth(tag, oldWidth)   // 记住旧宽度
  2. column.Width = 0                      // 隐藏
  3. UpdateVisibleColumnsConfig()           // 更新配置
  4. AdjustTargetColumnWidth()              // Name 列获得释放空间

显示列:
  1. ResolveColumnWidth(tag) → 从 ColumnState 恢复
  2. column.Width = max(40, savedWidth)
  3. AdjustTargetColumnWidth()              // Name 列缩窄以腾出空间
```

---

## 5. 列宽持久化

### 5.1 存储结构

```
AppState
  └─ Panes[0].Columns (左栏)
  │    ├─ ColNameWidth: 200
  │    ├─ ColSizeWidth: 100
  │    ├─ ColModifiedDateWidth: 150
  │    ├─ ColCreatedTimeWidth: 80
  │    ├─ ColTypeWidth: 100
  │    ├─ ColTagsWidth: 150
  │    ├─ ColNotesWidth: 200
  │    ├─ ColumnOrder: "Name,Size,Type,ModifiedDate,CreatedTime,Tags,Notes"
  │    └─ VisibleColumns: { "Path": "Name,Size,...", ... }
  └─ Panes[1].Columns (右栏)
       └─ (同上，完全独立)
```

### 5.2 加载时机（仅限以下场景走 LoadColumnWidths）

| 场景 | 说明 |
|------|------|
| 应用启动 / FileListControl.Loaded | 首次从配置恢复 |
| 标签页切换 | 不同路径可能有不同配置 |
| 导航模式变更 (Path ↔ Library ↔ Tag) | 列可见性不同 |
| 主题切换 | 需重新计算修正量 |

### 5.3 窗口 Resize 时不重新加载

```
规则: 窗口 Resize 仅触发 AdjustTargetColumnWidth()
      不调用 LoadColumnWidths()
      不覆盖用户拖拽的列宽内存值
```

### 5.4 列宽保存时机

- 用户拖拽 Name 列 Thumb 释放后，通过 `ColumnService.SaveColumnWidths()` 保存
- 列可见性切换后，通过 `ConfigurationService.MarkDirty()` 标记延迟保存
- 应用退出时全量保存

---

## 6. 双栏模式规格

### 6.1 独立性保证

| 维度 | 左栏 (Pane A) | 右栏 (Pane B) | 共享？ |
|------|---------------|---------------|--------|
| ColumnState | `Panes[0].Columns` | `Panes[1].Columns` | ❌ 独立 |
| AutoColumnWidthBehavior | 实例 A | 实例 B | ❌ 独立 |
| 列可见性配置 | 独立 per mode | 独立 per mode | ❌ 独立 |
| ColumnService | 单例 | 单例 | ✅ 共享（通过参数区分） |
| 列头样式 | 共享 | 共享 | ✅ 共享 |

### 6.2 模式切换时的列宽行为

```
Single → DualPane:
  1. 右栏容器变可见
  2. 左栏 ListView 缩小 → 左栏 AdjustTargetColumnWidth
  3. 右栏首次加载 → LoadColumnWidths(从 Panes[1] 恢复)
  4. 右栏 AdjustTargetColumnWidth

DualPane → Single:
  1. 右栏容器隐藏
  2. 左栏 ListView 拉宽 → 左栏 AdjustTargetColumnWidth
  3. 右栏不活跃（ActualWidth=0 自动跳过）
```

---

## 7. 表头对齐规则

### 7.1 表头 Margin 同步

表头 (`GridViewHeaderRowPresenter`) 的 Margin 必须与行内容的 Margin+Padding 一致：

```
HeaderRowPresenter.Margin = (leftCorrection, 0, headerRightCorrection, 0)

其中:
  leftCorrection = RowMargin.Left + RowPadding.Left
  headerRightCorrection = rightCorrection + (非Overlay且无ScrollViewer ? scrollBarWidth : 0)
```

### 7.2 缓存失效策略

`_cachedHeader` 在以下情况需重新查找：
- `_cachedHeader.IsLoaded == false`（VisualTree 重建）
- 主题切换后首次重算

---

## 8. 不可调列的语义约束

### 8.1 样式层约束

`NonResizableFileColumnHeaderStyle` 移除了 `PART_HeaderGripper` (Thumb)，用户无法通过 UI 拖拽。

### 8.2 编程层约束

代码仍可修改不可调列的宽度，但必须遵守以下规则：
- **允许**: 从配置恢复宽度 (`LoadColumnWidths`)
- **允许**: 溢出压缩（但不低于硬下限）
- **允许**: 隐藏 (`Width = 0` 当列不在可见集中)
- **禁止**: 任意修改到与配置不一致的值（需通过 `ResolveColumnWidth` 验证）

---

## 9. 边界条件与防御

| 条件 | 行为 |
|------|------|
| `ListView.IsLoaded == false` | 跳过列宽计算 |
| `ActualWidth ≤ 0` | 跳过列宽计算 |
| 所有列均隐藏 | `fillColumns.Count == 0` → 返回 |
| `usableWidth < 100` | 强制 `usableWidth = 100` |
| 分割线正在拖拽 (`IsSplitterDragging`) | 跳过列宽计算 |
| 窗口 PreviousSize 为 0（首次渲染） | 跳过 `AdjustColumnWidths` |

---

## 10. 架构概览图

```
┌──────────────────────────────────────────────────────┐
│                    触发层                              │
│  Window.SizeChanged│ListView.SizeChanged│列可见性切换  │
│  Thumb拖拽释放│数据源变更│Loaded│标签页切换             │
└────────────────────┬─────────────────────────────────┘
                     │ 全部统一到
                     ▼
┌──────────────────────────────────────────────────────┐
│          AutoColumnWidthBehavior (唯一引擎)            │
│  ┌──────────────────────────────────┐                │
│  │ AdjustTargetColumnWidth()        │                │
│  │ 1. 读取主题资源 (RowMargin/Pad) │                │
│  │ 2. 获取 ViewportWidth           │                │
│  │ 3. 计算 usableWidth             │                │
│  │ 4. 同步表头 Margin              │                │
│  │ 5. 分配弹性列宽度               │                │
│  │ 6. 三级瀑布溢出压缩             │                │
│  └──────────────────────────────────┘                │
└──────────────────────┬───────────────────────────────┘
                       │
                       ▼
┌──────────────────────────────────────────────────────┐
│               ColumnService (持久化层)                 │
│  LoadColumnWidths()  ← 仅在特定场景调用               │
│  SaveColumnWidths()  ← 用户操作后保存                 │
│  ResolveColumnWidth() ← 强制硬下限                    │
│  ApplyVisibleColumnsForCurrentMode()                  │
└──────────────────────────────────────────────────────┘
```
