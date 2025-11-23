# 标签页统一样式规范（路径 / 库 / 标签）

## 1. 视觉样式

- 字体：
  - 标签页标题：12px，常规体；活动页标题使用白色半粗 `SemiBold`
  - 左侧导航列表项：常规体，非选中保持黑色；匹配/选中时半粗
- 颜色：
  - 活动标签页背景 `#64B5F6`，边框 `#1565C0`，标题前景 `#FFFFFF`
  - 悬停背景 `#5BA5E5`（活动），`#DEE2E6`（非活动）
  - 统一高亮 `HighlightBrush`（橙），边框 `HighlightBorderBrush`，前景 `HighlightForegroundBrush`
  - 非选中标签 chip：背景 `#E3F2FD`，边框 `#BBDEFB`
- 间距：
  - 标签页按钮 `Padding=14,8`，`Height=32`，`CornerRadius=6,6,0,0`
  - 导航列表项 `Padding=5,3`，相邻项 `Margin=0,2`
- 边框：
  - 列表项底部分隔 `BorderThickness=0,0,0,1`，颜色 `#DDDDDD`

## 2. 交互行为

- 标签页按钮：
  - 悬停轻微加深背景并保持阴影一致性（避免高度跳变）
  - 活动页标题与关闭按钮使用白色前景
- 导航列表项（统一样式 `NavListBoxItemStyle`）：
  - 悬停 `HighlightHoverBrush`，选中 `HighlightBrush`
  - 匹配当前路径/库 `Tag="Match"` 优先级最高：始终橙色高亮
- 标签 chip：
  - 非选中浅蓝，悬停略加深，选中橙色统一高亮

## 3. 布局结构

- 三页内容区外层一致边距 `Margin=8`
- 内容对齐采用 `HorizontalContentAlignment=Stretch`，避免左右不齐
- 滚动容器使用 `ScrollViewer`，保持一致的滚动行为与最小高度

## 4. 功能组件统一

- 按钮：统一使用 `ModernButtonStyle`
- 文本框：统一使用 `ModernTextBoxStyle`
- GroupBox：统一使用 `ModernGroupBoxStyle`（减少内部 Padding，避免过多留白）
- 标签输入外框：统一使用 `TagInputBorderStyle`

## 5. 响应式设计

- 顶部 Tab 区域：自动宽度，固定高度 32，窄屏自动隐藏关闭按钮（不改变标题）
- 左侧导航：列表项宽度拉伸，容器滚动；在高 DPI 下视觉一致
- 标签 chip：通过容器宽度自适应项宽，防止出现滚动条时换行抖动

## 6. 资源与样式引用

- `Styles/AppStyles.xaml`
  - `TabButtonStyle` / `ActiveTabButtonStyle`
  - `NavListBoxItemStyle`
  - `ModernButtonStyle` / `ModernTextBoxStyle` / `ModernGroupBoxStyle`
  - `TagInputBorderStyle`
- 颜色资源：`HighlightBrush`、`HighlightBorderBrush`、`HighlightHoverBrush`、`HighlightForegroundBrush`

## 7. 视觉回归测试

- 入口：标题栏右侧系统按钮区的“🧪 视觉测试”
- 快照输出：`VisualTests` 目录（NavPath/NavLibrary/NavTag/Tabs）
- 基准对比：如 `VisualBaseline` 目录存在对应文件，将计算平均差异百分比并弹窗汇总

## 8. 兼容性与平台说明

- 本项目为 WPF 桌面应用，不涉及浏览器兼容；统一测试覆盖：
  - Windows 10/11 不同主题（浅色/深色）
  - 不同 DPI 缩放（100%/125%/150%）
  - 不同窗口尺寸（最小宽度至全屏）

## 9. 开发约定

- 新增列表均使用 `ItemContainerStyle={StaticResource NavListBoxItemStyle}`
- 新增输入框优先使用 `ModernTextBoxStyle`；外包边框统一 `TagInputBorderStyle`
- 代码中创建的标签 chip 背景边框应使用浅蓝配色（`#E3F2FD` / `#BBDEFB`）并与选中橙色高亮逻辑保持一致

