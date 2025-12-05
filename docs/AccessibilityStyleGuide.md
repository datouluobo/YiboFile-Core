# 视频预览可访问性与视觉规范（WCAG 2.1 AA）

## 标题样式
- 字体大小：≥ 18px（当前为 18px）
- 字重：Bold
- 颜色：`#FFFFFF`（前景）/ `#212529`（背景），对比度约 12.6:1
- 内边距：上下 8px、左右 12px

## 控件与面板背景
- 面板背景：`#F8F9FA`
- 主文字：`#212529`（纯色）
- 对比度：约 7.3:1（满足 AA）
- 避免复杂纹理与图案；半透明仅用于非文本容器

## 文字颜色与状态
- 主文字：`#212529` 纯色
- 强调信息：`#FF5722`（Emphasis）
- 禁用状态：`#6C757D`（Disabled），不小于 0.6 透明度下降

## 组件样式键（XAML 资源）
- `PreviewTitleBackgroundBrush`：标题背景
- `PreviewTitleForegroundBrush`：标题文字
- `PreviewPanelBackgroundBrush`：面板背景
- `PreviewTextPrimaryBrush`：主文字颜色
- `EmphasisBrush`：强调色
- `DisabledForegroundBrush`：禁用文字颜色
- `ModernButtonStyle`：浅色按钮通用样式（含禁用态）
- `ModernComboBoxStyle`：浅色下拉样式

## 明暗模式适配
- 当前默认浅色模式。暗色模式可通过替换上述资源为深色配色实现：
  - 面板背景：`#1E1E1E`
  - 主文字：`#FFFFFF`
  - 对比度控制 ≥ 4.5:1

## WCAG 验证要点
- 文本与背景对比度 ≥ 4.5:1
- 禁用态与可交互态外观明确区分
- 焦点可见（边框或背景变化）

## 对比截图（待补充）
- 旧版：`docs/screenshots/before-video-preview.png`
- 新版：`docs/screenshots/after-video-preview.png`

## 变更摘要
- 标题：增大字体、加粗、高对比度背景与内边距
- 控件：面板改浅灰背景、主文字深色纯色
- 速度选择：统一下拉样式与可读文本项
- 点击画面播放：提升交互直觉

## 测试建议（多光照条件）
- 在亮屏与暗屏环境观察标题与时间文本清晰度
- 按下禁用按钮查看禁用态可识别性
- 检查焦点状态（Tab 导航）是否明显
