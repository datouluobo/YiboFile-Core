# YiboFile UI 系统彻底重构方案 (v3)

> **文档创建日期**: 2026-02-21
> **最近更新**: 2026-02-21 v3 精确 Token 清单
> **状态**: 待审阅 → 待确认方案 → 实施

---

## 一、设计理念：三维合约驱动架构

当前 UI 系统由**三个独立可切换维度**组成，但建设时间跨度大，方案混杂。本次重构将三个维度统一到**同一套合约驱动架构**中：

```
┌──────────────────────────────────────────────────────────────┐
│                      UI 三维坐标系                            │
│                                                              │
│   颜色主题 (Theme)  ─── 7内置 + 自定义 ─── 37 个颜色 Token   │
│   UI风格 (UIStyle)  ─── 4种风格        ─── 58 个形状 Token   │
│   图标风格 (Icons)  ─── 4种图标集      ─── 50 个图标键       │
│                                                              │
│   三维正交: 任意组合 = 7×4×4 = 112 种视觉方案                 │
│   每个维度: 独立合约 → 独立切换 → 零干扰                      │
└──────────────────────────────────────────────────────────────┘
```

**核心原则**：
1. **合约驱动** — 每个维度有明确的"必需键"清单，新增任何主题/风格/图标集只需填表，不需要理解内部实现
2. **分层解耦** — Token定义 → 别名映射 → 控件样式，严格单向依赖
3. **模板自动化** — 提供模板文件 + 验证脚本，新增流程闭环
4. **细分键、允许共值** — Token 按功能细分到每个控件级，但不同键可以共用同一个值（例如所有按钮圆角相同但通过独立键控制）

### 颜色 vs 形状的边界

UIStyle 文件中**允许包含引用 Theme 颜色的 Brush 映射**，但有严格约束：
- UIStyle 中的颜色值 **只允许引用 Theme 的 Semantic Token** 或 `Transparent`
- **不允许硬编码** `#XXXXXX`
- 这样切换主题时，UIStyle 中的颜色会自动跟随

```
UIStyle 的职责 = "在这种布局氛围下，用哪些颜色角色"
Theme  的职责 = "这些颜色角色的具体色值是什么"

例: Tab 激活时用填充背景还是用顶部指示线? → 由 UIStyle 决定
    填充用什么色? 指示线用什么色?             → 引用 Theme Token
```

---

## 二、现状诊断（三维度全景）

### 2.1 颜色主题 (Theme)

| 问题 | 严重度 | 数据 |
|------|--------|------|
| Light/Dark 有70键, 其余5主题只有63键 | 🔴 | 缺 AccentColor, AccentHoverColor, ButtonPressedBackgroundBrush, DividerBrush, ForegroundPrimaryColor, NavigationRegionBrush, ShadowBrush |
| 13个幽灵画刷被引用但无定义 | 🔴 | AccentSubtleBrush, ForegroundBrush, SurfaceBrush, TextTertiaryBrush, WindowBackgroundBrush 等 |
| 核心键+别名双写 (~35对/文件) | 🟡 | 每个主题120行70键, 同一颜色写两遍 |
| ThemeService(旧) + ThemeManager(新) 并存 | 🟡 | ThemeService 已无调用方 |
| 19个XAML共92处硬编码颜色 | 🟡 | 不跟随主题切换 |
| 缺少焦点/非焦点区域颜色区分 | 🟡 | 双栏模式下焦点栏与非焦点栏无颜色差异 |

### 2.2 UI风格 (UIStyle)

| 问题 | 严重度 | 数据 |
|------|--------|------|
| 4个风格文件键一致 (23键) | ✅ | 无缺失 |
| `UI.TabItem.ActiveBackground` 是颜色, 不是形状 | 🟡 | Dark模式下Tab背景固定白色 |
| 只有1个分割器Token (`VisibleLine`) | � | 无法控制分割器宽度、线粗、圆点等 |
| 无滚动条相关Token | 🟡 | 不同风格滚动条外观相同 |
| 无列头相关Token | 🟡 | 列头样式不跟随风格 |
| Tab指示条只支持顶部线条 | 🟡 | 无法配置底部、背景色等变体 |
| 工具栏按钮无间距Token | � | 按钮间距硬编码 |
| AppStyles中 22+ 处硬编码 CornerRadius | 🟡 | 不跟随风格切换 |
| 缺少元数据 | 🟢 | 设置界面无法展示风格描述 |
| 新增风格门槛高 | 🟡 | 无文档, 无模板 |

### 2.3 图标风格 (Icons)

| 问题 | 严重度 | 数据 |
|------|--------|------|
| 4个图标集键一致 (49键) | ✅ | 无缺失 |
| `Icon_Window_Tasks` 被引用但不在图标字典中 | 🔴 | NavigationRailControl.xaml:96 |
| 缺少元数据 | 🟢 | 设置界面无法展示图标集描述 |
| 新增图标集门槛高 | 🟡 | 49键纯手打码点, 无模板 |

---

## 三、目标架构设计

### 3.1 新目录结构

```
YiboFile-Core/
├── Styles/
│   ├── Themes/                              ← 颜色主题层
│   │   ├── _ThemeContract.xaml              ← 合约 (37键默认值/Fallback)
│   │   ├── Light.xaml                       ← 瘦身后 ~60行
│   │   ├── Dark.xaml
│   │   ├── Ocean.xaml
│   │   ├── Forest.xaml
│   │   ├── Sunset.xaml
│   │   ├── Purple.xaml
│   │   └── Nordic.xaml
│   │
│   ├── UIStyles/                            ← UI风格层
│   │   ├── _UIStyleContract.xaml            ← 合约 (58键默认值)
│   │   ├── Fluent.xaml
│   │   ├── MacOS.xaml
│   │   ├── Geek.xaml
│   │   └── Original.xaml
│   │
│   ├── Icons/                               ← 图标层 (从 Resources/Icons 移入)
│   │   ├── _IconContract.xaml               ← 合约 (50键默认值)
│   │   ├── Icons.Emoji.xaml
│   │   ├── Icons.Fluent.xaml
│   │   ├── Icons.Material.xaml
│   │   └── Icons.Remix.xaml
│   │
│   ├── Aliases/                             ← 别名映射层
│   │   └── BrushAliases.xaml                ← 颜色别名 (~40个)
│   │
│   ├── Controls/                            ← 按功能拆分的控件样式
│   │   ├── ButtonStyles.xaml
│   │   ├── ScrollBarStyles.xaml
│   │   ├── GridSplitterStyles.xaml
│   │   ├── TabStyles.xaml
│   │   ├── TextBoxStyles.xaml
│   │   ├── DataGridStyles.xaml
│   │   ├── ContextMenuStyles.xaml
│   │   ├── ComboBoxStyles.xaml
│   │   ├── DialogStyles.xaml
│   │   ├── NavigationStyles.xaml
│   │   └── ToastStyles.xaml
│   │
│   └── _GlobalDefaults.xaml                 ← Converters + 固定常量
│
├── Resources/
│   └── Fonts/
│       ├── FluentSystemIcons-Regular.ttf
│       └── MaterialIcons-Regular.ttf
│
├── Services/Theming/
│   ├── IThemeService.cs
│   ├── ThemeService.cs
│   ├── ContractValidator.cs
│   ├── CustomThemeManager.cs
│   └── ThemeMetadata.cs
│
└── scripts/
    ├── Validate-Contracts.ps1
    └── New-Theme.ps1
```

### 3.2 三维 Token 分层架构

```
                    ┌──────────────────────────┐
                    │       合约层 (Contract)    │
                    │   三个 _*Contract.xaml     │
                    │   定义所有必需键 + 默认值   │
                    └─────────┬────────────────┘
                              │ 被覆盖
          ┌───────────────────┼───────────────────┐
          ▼                   ▼                   ▼
┌───────────────────┐ ┌───────────────┐ ┌───────────────────┐
│  颜色主题 (37键)   │ │ UI风格 (58键) │ │ 图标集 (50键)      │
│  Light/Dark/...   │ │ Fluent/...    │ │ Emoji/Fluent/...  │
│  运行时替换       │ │ 运行时替换     │ │ 运行时替换         │
└─────────┬─────────┘ └───────┬───────┘ └───────────────────┘
          │                   │
          ▼                   ▼
┌───────────────────────────────────────┐
│          别名层 (Aliases)              │
│  BrushAliases: 旧键 → 新Semantic键    │
│  一次加载, 永不替换                    │
└───────────────────┬───────────────────┘
                    ▼
┌───────────────────────────────────────┐
│        控件样式层 (Controls/*.xaml)     │
│  所有颜色通过 DynamicResource 引用     │
│  所有形状通过 DynamicResource 引用     │
│  所有图标通过 DynamicResource 引用     │
│  零硬编码                             │
└───────────────────────────────────────┘
```

---

## 四、颜色主题合约 (Theme Contract) — 37 键

### 4.1 Semantic Token 完整清单

```
背景类 ×8:
  BackgroundPrimaryBrush          ← 主内容区背景
  BackgroundSecondaryBrush        ← 次背景/面板
  BackgroundTertiaryBrush         ← 卡片/三级背景
  BackgroundElevatedBrush         ← 对话框/浮层
  TitleBarBackgroundBrush         ← 标题栏背景
  NavigationRegionBrush           ← 导航区/侧栏背景
  SidebarBackgroundBrush          ← 🆕 侧边栏专用背景
  PaneFocusedBackgroundBrush      ← 🆕 焦点栏背景 (焦点所在的文件列表窗格)
  PaneUnfocusedBackgroundBrush    ← 🆕 非焦点栏背景 (非焦点窗格, 微暗以示区分)

文本类 ×6:
  ForegroundPrimaryColor          ← Color 类型 (动画用)
  ForegroundPrimaryBrush          ← 主文本
  ForegroundSecondaryBrush        ← 次要文本
  ForegroundTertiaryBrush         ← 三级文本
  ForegroundDisabledBrush         ← 禁用文本
  ForegroundOnAccentBrush         ← 强调色上的文本

边框类 ×3:
  BorderDefaultBrush              ← 默认边框
  BorderSubtleBrush               ← 淡边框
  BorderFocusBrush                ← 聚焦边框

强调色 ×7:
  AccentColor                     ← Color 类型 (动画用)
  AccentHoverColor                ← Color 类型 (动画用)
  AccentDefaultBrush              ← 主强调色
  AccentHoverBrush                ← 悬停
  AccentPressedBrush              ← 按下
  AccentSelectedBrush             ← 选中
  AccentLightBrush                ← 浅强调色背景

控件状态 ×4:
  ControlDefaultBrush             ← 控件默认
  ControlHoverBrush               ← 控件悬停
  ControlPressedBrush             ← 控件按下
  ControlDisabledBrush            ← 控件禁用

语义状态 ×4:
  StatusSuccessBrush              ← 成功
  StatusWarningBrush              ← 警告
  StatusErrorBrush                ← 错误
  StatusInfoBrush                 ← 信息

特殊用途 ×5:
  TransparentBrush                ← 透明
  OverlayBrush                    ← 遮罩
  OverlayLightBrush               ← 浅遮罩
  DividerBrush                    ← 分隔线
  ShadowBrush                     ← 阴影
```

> **注**: 背景类从原来 6 个增至 9 个, 总计 37 个 Semantic Token。
> `PaneFocusedBackgroundBrush` 和 `PaneUnfocusedBackgroundBrush` 按焦点状态区分,
> 而非按主/副栏区分（因为焦点可切换）。
> 多个键允许共用同一个颜色值 —— 例如在某些主题中 `PaneFocusedBackgroundBrush` 可以等于 `BackgroundPrimaryBrush`,
> 但通过独立键名保留了将来差异化调整的能力。

### 4.2 区域颜色映射示意

```
┌─────────────────────────────────────────────────────────────┐
│  TitleBarBackgroundBrush          ← 标题栏                    │
├────────┬────────────────────────────────────────────────────┤
│ Nav    │  Tab Bar                                            │
│ Region │  PaneFocusedBG / PaneUnfocusedBG ← 按焦点切换       │
│ Brush  ├────────────────────┬───────────────────────────────┤
│        │  焦点栏文件列表     │ 非焦点栏文件列表               │
│ Sidebar│  PaneFocusedBG     │ PaneUnfocusedBG               │
│ BG     │                    │ (微暗/微透, 暗示非活动)        │
│ Brush  ├────────────────────┴───────────────────────────────┤
│        │  预览面板 → BackgroundSecondaryBrush                 │
└────────┴────────────────────────────────────────────────────┘
```

### 4.3 各主题参考值 (新增键)

| 键 | Light | Dark | 说明 |
|---|---|---|---|
| `SidebarBackgroundBrush` | `#F0F0F0` | `#1A1A1A` | 可与 NavigationRegionBrush 共值或微调 |
| `PaneFocusedBackgroundBrush` | `#FFFFFF` | `#202020` | 通常与 BackgroundPrimaryBrush 共值 |
| `PaneUnfocusedBackgroundBrush` | `#FAFAFA` | `#1C1C1C` | 比焦点栏略暗/略灰 |

### 4.4 颜色别名表 (BrushAliases.xaml)

> 所有别名通过 `{DynamicResource SemanticTokenName}` 映射，一次定义，所有主题共享。

| 别名键 (旧代码引用) | → Semantic Token |
|---|---|
| `AppBackgroundBrush` | `BackgroundPrimaryBrush` |
| `PanelBackgroundBrush` | `BackgroundSecondaryBrush` |
| `CardBackgroundBrush` | `BackgroundTertiaryBrush` |
| `DialogBackgroundBrush` | `BackgroundElevatedBrush` |
| `WindowBackgroundBrush` | `BackgroundElevatedBrush` |
| `TextPrimaryBrush` | `ForegroundPrimaryBrush` |
| `TextSecondaryBrush` | `ForegroundSecondaryBrush` |
| `TextTertiaryBrush` | `ForegroundTertiaryBrush` |
| `TextDisabledBrush` | `ForegroundDisabledBrush` |
| `TextInverseBrush` | `ForegroundOnAccentBrush` |
| `ForegroundBrush` | `ForegroundPrimaryBrush` |
| `BorderBrush` | `BorderDefaultBrush` |
| `BorderStrongBrush` | `BorderDefaultBrush` |
| `AccentBrush` | `AccentDefaultBrush` |
| `AccentForegroundBrush` | `ForegroundOnAccentBrush` |
| `AccentSubtleBrush` | `AccentLightBrush` |
| `ButtonBackgroundBrush` | `ControlDefaultBrush` |
| `ButtonBorderBrush` | `BorderDefaultBrush` |
| `ButtonHoverBackgroundBrush` | `ControlHoverBrush` |
| `ButtonPressedBackgroundBrush` | `ControlPressedBrush` |
| `InputBackgroundBrush` | `BackgroundPrimaryBrush` |
| `InputBorderBrush` | `BorderDefaultBrush` |
| `ControlBackgroundBrush` | `ControlDefaultBrush` |
| `ControlDefaultBackgroundBrush` | `ControlDefaultBrush` |
| `ControlHoverBackgroundBrush` | `ControlHoverBrush` |
| `ControlPressedBackgroundBrush` | `ControlPressedBrush` |
| `ControlSampleBackgroundBrush` | `ControlDefaultBrush` |
| `AccountBackgroundBrush` | `BackgroundSecondaryBrush` |
| `SurfaceBrush` | `BackgroundSecondaryBrush` |
| `SelectionBackgroundBrush` | `AccentLightBrush` |
| `DisabledBrush` | `ControlDisabledBrush` |
| `SuccessBrush` | `StatusSuccessBrush` |
| `WarningBrush` | `StatusWarningBrush` |
| `ErrorBrush` | `StatusErrorBrush` |
| `InfoBrush` | `StatusInfoBrush` |
| `PreviewTitleBackgroundBrush` | `BackgroundSecondaryBrush` |
| `PreviewTitleForegroundBrush` | `ForegroundPrimaryBrush` |
| `PreviewPanelBackgroundBrush` | `BackgroundSecondaryBrush` |
| `PreviewTextPrimaryBrush` | `ForegroundPrimaryBrush` |

---

## 五、UI 风格合约 (UIStyle Contract) — 58 键

> **设计原则**: 细分键名到每个控件级别，保证任何单一控件的形状都能独立调整。
> 多个键可以共用同一个值 —— 这只是"当前一样"而非"必须一样"。

### A. Tab 标签页 (14 键)

| # | Token 键 | 类型 | 说明 | Original | Fluent | MacOS | Geek |
|---|---|---|---|---|---|---|---|
| 1 | `UI.TabItem.CornerRadius` | CornerRadius | Tab 整体圆角 | `6,6,0,0` | `8,8,8,8` | `12,12,12,12` | `2,2,0,0` |
| 2 | `UI.TabItem.Margin` | Thickness | Tab 外边距 | `1,0` | `4,5` | `6,6` | `0,0` |
| 3 | `UI.TabItem.Padding` | Thickness | Tab 内边距 | `1,0` | `6,2` | `10,4` | `0,0` |
| 4 | `UI.TabItem.Height` | Double | 🆕 Tab 高度 | `34` | `36` | `38` | `30` |
| 5 | `UI.TabItem.ActiveBackground` | Brush | 激活 Tab 背景色 (引用 Theme) | `Transparent` | `{DR BackgroundElevatedBrush}` | `{DR BackgroundElevatedBrush}` | `Transparent` |
| 6 | `UI.TabItem.ActiveBorderThickness` | Thickness | 🆕 激活 Tab 边框(如底线) | `0` | `0` | `0` | `0` |
| 7 | `UI.TabItem.SeparatorVisibility` | Visibility | Tab 间分隔线 | `Visible` | `Collapsed` | `Collapsed` | `Visible` |
| 8 | `UI.TabItem.SeparatorHeight` | Double | 🆕 分隔线高度 | `14` | `14` | `14` | `14` |
| 9 | `UI.TabItem.ActiveIndicatorVisibility` | Visibility | 指示条是否显示 | `Visible` | `Collapsed` | `Collapsed` | `Visible` |
| 10 | `UI.TabItem.ActiveIndicatorPosition` | VerticalAlignment | 🆕 指示条位置 | `Top` | `Top` | `Top` | `Top` |
| 11 | `UI.TabItem.ActiveIndicatorHeight` | Double | 🆕 指示条高度 | `3` | `3` | `3` | `3` |
| 12 | `UI.TabItem.ActiveIndicatorMargin` | Thickness | 指示条边距 | `6,0,6,0` | `0` | `0` | `0,0,0,0` |
| 13 | `UI.TabItem.ActiveIndicatorRadius` | Double | 指示条圆角 | `1.5` | `0` | `0` | `0` |
| 14 | `UI.TabItem.ActiveIndicatorColor` | Brush | 🆕 指示条颜色 | `{DR AccentDefaultBrush}` | `{DR AccentDefaultBrush}` | `{DR AccentDefaultBrush}` | `{DR AccentDefaultBrush}` |

### B. 工具栏按钮 (5 键)

| # | Token 键 | 类型 | 说明 | Original | Fluent | MacOS | Geek |
|---|---|---|---|---|---|---|---|
| 15 | `UI.ToolbarButton.CornerRadius` | CornerRadius | 按钮圆角 | `4` | `8` | `10` | `2` |
| 16 | `UI.ToolbarButton.BorderThickness` | Thickness | 按钮边框 | `1` | `0` | `0` | `1` |
| 17 | `UI.ToolbarButton.Padding` | Thickness | 按钮内边距 | `10,0` | `14,4` | `12,6` | `6,0` |
| 18 | `UI.ToolbarButton.Margin` | Thickness | 🆕 按钮间距 | `1,0` | `2,0` | `3,0` | `0,0` |
| 19 | `UI.ToolbarButton.Height` | Double | 🆕 按钮高度 | `32` | `34` | `36` | `28` |

### C. 侧边栏/导航列表 (6 键)

| # | Token 键 | 类型 | 说明 | Original | Fluent | MacOS | Geek |
|---|---|---|---|---|---|---|---|
| 20 | `UI.Sidebar.ItemCornerRadius` | CornerRadius | 侧栏项圆角 | `0` | `8` | `10` | `0` |
| 21 | `UI.Sidebar.ItemMargin` | Thickness | 侧栏项外边距 | `0` | `6,3` | `8,4` | `0` |
| 22 | `UI.Sidebar.ActiveIndicatorVisibility` | Visibility | 侧栏激活指示条 | `Collapsed` | `Visible` | `Collapsed` | `Collapsed` |
| 23 | `UI.Sidebar.ItemPadding` | Thickness | 🆕 侧栏项内边距 | `8,6` | `12,8` | `14,10` | `6,4` |
| 24 | `UI.Sidebar.ItemHeight` | Double | 🆕 侧栏项高度 | `30` | `36` | `38` | `26` |
| 25 | `UI.Sidebar.ActiveBackground` | Brush | 🆕 侧栏激活项背景 (引用Theme) | `{DR ControlHoverBrush}` | `{DR AccentLightBrush}` | `{DR AccentLightBrush}` | `{DR ControlHoverBrush}` |

### D. 地址栏 (2 键)

| # | Token 键 | 类型 | 说明 | Original | Fluent | MacOS | Geek |
|---|---|---|---|---|---|---|---|
| 26 | `UI.AddressBar.CornerRadius` | CornerRadius | 地址栏圆角 | `4` | `16` | `10` | `0` |
| 27 | `UI.AddressBar.BorderThickness` | Thickness | 地址栏边框 | `1` | `0` | `0` | `1` |

### E. 文件列表行 (6 键)

| # | Token 键 | 类型 | 说明 | Original | Fluent | MacOS | Geek |
|---|---|---|---|---|---|---|---|
| 28 | `UI.FileList.RowCornerRadius` | CornerRadius | 行圆角 | `0` | `8` | `12` | `0` |
| 29 | `UI.FileList.RowMargin` | Thickness | 行外边距 | `0` | `8,3` | `12,4` | `0` |
| 30 | `UI.FileList.RowBorderThickness` | Thickness | 行边框 | `0,0,0,1` | `0` | `0` | `0,0,0,1` |
| 31 | `UI.FileList.RowMinHeight` | Double | 行最小高度 | `34` | `40` | `44` | `28` |
| 32 | `UI.FileList.RowPadding` | Thickness | 行内边距 | `4,0` | `8,4` | `12,6` | `2,0` |
| 33 | `UI.FileList.SelectedBorderThickness` | Thickness | 选中行强调边框 | `0` | `0` | `2` | `3,0,0,0` |

### F. 文件列表列头 (4 键，🆕)

| # | Token 键 | 类型 | 说明 | Original | Fluent | MacOS | Geek |
|---|---|---|---|---|---|---|---|
| 34 | `UI.ColumnHeader.Padding` | Thickness | 🆕 列头内边距 | `12,8` | `14,10` | `16,10` | `8,6` |
| 35 | `UI.ColumnHeader.BorderThickness` | Thickness | 🆕 列头底边框 | `0,0,0,1` | `0` | `0` | `0,0,0,1` |
| 36 | `UI.ColumnHeader.FontWeight` | FontWeight | 🆕 列头字重 | `Normal` | `Normal` | `SemiBold` | `Bold` |
| 37 | `UI.ColumnHeader.Height` | Double | 🆕 列头高度 | `32` | `36` | `38` | `28` |

### G. 分割器 (5 键)

| # | Token 键 | 类型 | 说明 | Original | Fluent | MacOS | Geek |
|---|---|---|---|---|---|---|---|
| 38 | `UI.Splitter.VisibleLine` | Visibility | 分割线可见性 | `Visible` | `Collapsed` | `Collapsed` | `Visible` |
| 39 | `UI.Splitter.Width` | Double | 🆕 分割器热区宽度 | `6` | `8` | `8` | `4` |
| 40 | `UI.Splitter.LineThickness` | Double | 🆕 分割线粗细 | `1` | `1` | `1` | `1` |
| 41 | `UI.Splitter.HoverShowGripDot` | Visibility | 🆕 悬停时是否显示抓手圆点 | `Visible` | `Collapsed` | `Collapsed` | `Visible` |
| 42 | `UI.Splitter.CollapseButtonOpacity` | Double | 🆕 折叠按钮默认透明度 | `0.3` | `0.2` | `0.2` | `0.5` |

### H. 滚动条 (6 键，🆕)

| # | Token 键 | 类型 | 说明 | Original | Fluent | MacOS | Geek |
|---|---|---|---|---|---|---|---|
| 43 | `UI.ScrollBar.ThumbCornerRadius` | CornerRadius | 🆕 滑块圆角 | `3` | `4` | `6` | `2` |
| 44 | `UI.ScrollBar.ThumbWidth` | Double | 🆕 默认滑块宽度 | `4` | `4` | `6` | `4` |
| 45 | `UI.ScrollBar.ThumbHoverWidth` | Double | 🆕 悬停时滑块宽度 | `8` | `8` | `10` | `6` |
| 46 | `UI.ScrollBar.ThumbOpacity` | Double | 🆕 默认透明度 | `0.3` | `0.3` | `0.4` | `0.5` |
| 47 | `UI.ScrollBar.ThumbHoverOpacity` | Double | 🆕 悬停透明度 | `0.6` | `0.6` | `0.8` | `0.8` |
| 48 | `UI.ScrollBar.Mode` | String | 🆕 滚动条模式: `Overlay`/`Classic` | `Overlay` | `Overlay` | `Overlay` | `Classic` |

### I. 通用控件圆角 (6 键，🆕)

| # | Token 键 | 类型 | 说明 | Original | Fluent | MacOS | Geek |
|---|---|---|---|---|---|---|---|
| 49 | `UI.Button.CornerRadius` | CornerRadius | 🆕 通用按钮圆角 | `4` | `8` | `10` | `2` |
| 50 | `UI.TextBox.CornerRadius` | CornerRadius | 🆕 文本框圆角 | `4` | `8` | `10` | `2` |
| 51 | `UI.ComboBox.CornerRadius` | CornerRadius | 🆕 下拉框圆角 | `4` | `8` | `10` | `2` |
| 52 | `UI.GroupBox.CornerRadius` | CornerRadius | 🆕 分组框圆角 | `4` | `6` | `8` | `0` |
| 53 | `UI.ContextMenu.CornerRadius` | CornerRadius | 🆕 右键菜单圆角 | `6` | `8` | `10` | `2` |
| 54 | `UI.MenuItem.CornerRadius` | CornerRadius | 🆕 菜单项圆角 | `2` | `4` | `6` | `0` |

### J. 对话框 (2 键，🆕)

| # | Token 键 | 类型 | 说明 | Original | Fluent | MacOS | Geek |
|---|---|---|---|---|---|---|---|
| 55 | `UI.Dialog.CornerRadius` | CornerRadius | 🆕 对话框窗体圆角 | `8` | `12` | `16` | `4` |
| 56 | `UI.Dialog.ButtonCornerRadius` | CornerRadius | 🆕 对话框按钮圆角 | `4` | `6` | `8` | `2` |

### K. 元数据 (3 键)

| # | Token 键 | 类型 | 说明 |
|---|---|---|---|
| 57 | `UIStyleId` | String | 风格唯一标识 |
| 58 | `UIStyleDisplayName` | String | 显示名称 (如 "流畅设计") |
| 59 | `UIStyleDescription` | String | 描述文本 |

### Token 统计

| 类别 | 数量 | 其中现有 | 其中新增 |
|---|---|---|---|
| A. Tab 标签页 | 14 | 8 | 6 |
| B. 工具栏按钮 | 5 | 3 | 2 |
| C. 侧边栏 | 6 | 3 | 3 |
| D. 地址栏 | 2 | 2 | 0 |
| E. 文件列表行 | 6 | 6 | 0 |
| F. 列头 | 4 | 0 | 4 |
| G. 分割器 | 5 | 1 | 4 |
| H. 滚动条 | 6 | 0 | 6 |
| I. 通用控件 | 6 | 0 | 6 |
| J. 对话框 | 2 | 0 | 2 |
| K. 元数据 | 3 | 0 | 3 |
| **合计** | **59** | **23** | **36** |

> 注: 原有的 23 键中，`UI.TabItem.ActiveBackground` 保留在 UIStyle 中但改为引用 Theme Token 的方式（不再硬编码颜色值）。新增 `UI.TabItem.ActiveIndicatorColor`, `UI.TabItem.ActiveIndicatorPosition`, `UI.Sidebar.ActiveBackground` 也是同样的"引用 Theme"方式。

---

## 六、图标合约 (Icon Contract) — 50 键

### 6.1 图标键清单

```
-- 元数据 (5键) --
IconFontFamily                         ← 图标字体
WindowControlIconSize                  ← 窗口控制图标尺寸
IconStyleId                            ← 🆕 图标集标识
IconStyleDisplayName                   ← 🆕 显示名称
IconStyleDescription                   ← 🆕 描述

-- 通用操作 (18键) --
Icon_Copy, Icon_Edit, Icon_Format, Icon_Render, Icon_Wrap
Icon_OpenExternal, Icon_ChevronUp, Icon_ChevronDown
Icon_Search, Icon_Settings, Icon_Filter, Icon_Add
Icon_Delete_Outline, Icon_NewFolder, Icon_NewFile
Icon_Refresh, Icon_Desktop, Icon_User

-- 导航/工具栏 (5键) --
Icon_Back, Icon_Forward, Icon_Up, Icon_ViewList, Icon_ViewThumb

-- 文件类型 (7键) --
Icon_Drive, Icon_Folder, Icon_File
Icon_Music, Icon_Video, Icon_Image, Icon_Document

-- 导航面板 (3键) --
Icon_Nav_Path, Icon_Nav_Library, Icon_Nav_Tag

-- 窗口控制 (7键) --
Icon_Window_Settings, Icon_Window_About
Icon_Window_Minimize, Icon_Window_Maximize
Icon_Window_Restore, Icon_Window_Close
Icon_Window_Tasks                      ← 🆕 补齐幽灵键

-- 布局 (3键) --
Icon_Layout_Focus, Icon_Layout_Work, Icon_Layout_Full

-- 状态/Toast (5键) --
Icon_Success, Icon_Error, Icon_Warning, Icon_Info, Icon_Close
```

**总计**: 5 元数据 + 45 图标映射 = **50 键**

### 6.2 `_IconContract.xaml` 的作用

提供所有 50 个键的默认值（使用 Emoji 字符），保证即使图标集文件不完整，也不会有空白图标：

```xml
<!-- _IconContract.xaml 示例 -->
<sys:String x:Key="Icon_Copy">📋</sys:String>          <!-- Fallback: emoji -->
<sys:String x:Key="Icon_Window_Tasks">📋</sys:String>   <!-- 补齐幽灵键 -->
```

---

## 七、App.xaml 加载顺序

```xml
<Application.Resources>
    <ResourceDictionary>
        <ResourceDictionary.MergedDictionaries>
            <!-- ① 合约层 (三维Fallback, 永不替换) -->
            <ResourceDictionary Source="Styles/Themes/_ThemeContract.xaml"/>
            <ResourceDictionary Source="Styles/UIStyles/_UIStyleContract.xaml"/>
            <ResourceDictionary Source="Styles/Icons/_IconContract.xaml"/>

            <!-- ② 当前主题 (运行时替换) -->
            <ResourceDictionary Source="Styles/Themes/Light.xaml"/>

            <!-- ③ 别名层 (一次加载, 永不替换) -->
            <ResourceDictionary Source="Styles/Aliases/BrushAliases.xaml"/>

            <!-- ④ 全局常量 -->
            <ResourceDictionary Source="Styles/_GlobalDefaults.xaml"/>

            <!-- ⑤ 当前UI风格 (运行时替换) -->
            <ResourceDictionary Source="Styles/UIStyles/Original.xaml"/>

            <!-- ⑥ 当前图标集 (运行时替换) -->
            <ResourceDictionary Source="Styles/Icons/Icons.Emoji.xaml"/>

            <!-- ⑦ 控件样式 (一次加载, 永不替换) -->
            <ResourceDictionary Source="Styles/Controls/ButtonStyles.xaml"/>
            <ResourceDictionary Source="Styles/Controls/TabStyles.xaml"/>
            <ResourceDictionary Source="Styles/Controls/ScrollBarStyles.xaml"/>
            <ResourceDictionary Source="Styles/Controls/GridSplitterStyles.xaml"/>
            <ResourceDictionary Source="Styles/Controls/TextBoxStyles.xaml"/>
            <ResourceDictionary Source="Styles/Controls/ComboBoxStyles.xaml"/>
            <ResourceDictionary Source="Styles/Controls/DataGridStyles.xaml"/>
            <ResourceDictionary Source="Styles/Controls/ContextMenuStyles.xaml"/>
            <ResourceDictionary Source="Styles/Controls/DialogStyles.xaml"/>
            <ResourceDictionary Source="Styles/Controls/NavigationStyles.xaml"/>
            <ResourceDictionary Source="Styles/Controls/ToastStyles.xaml"/>
        </ResourceDictionary.MergedDictionaries>
    </ResourceDictionary>
</Application.Resources>
```

**运行时切换逻辑**:
- 切换主题 → 替换 ② 位置的字典 → 别名层自动重新解析
- 切换UI风格 → 替换 ⑤ 位置的字典 → 控件样式自动适应
- 切换图标 → 替换 ⑥ 位置的字典 → 所有图标自动更新

---

## 八、统一服务接口

```csharp
public interface IThemeService
{
    // ── 颜色主题 ──
    ThemeMetadata CurrentTheme { get; }
    IReadOnlyList<ThemeMetadata> AvailableThemes { get; }
    void SetTheme(string themeId, bool animate = true);
    void ToggleTheme();

    // ── UI风格 ──
    UIStyleMetadata CurrentUIStyle { get; }
    IReadOnlyList<UIStyleMetadata> AvailableUIStyles { get; }
    void SetUIStyle(string styleId);

    // ── 图标风格 ──
    IconStyleMetadata CurrentIconStyle { get; }
    IReadOnlyList<IconStyleMetadata> AvailableIconStyles { get; }
    void SetIconStyle(string styleName);

    // ── 系统主题跟随 ──
    bool IsFollowingSystemTheme { get; }
    void EnableSystemThemeFollowing();
    void DisableSystemThemeFollowing();

    // ── 自定义主题 ──
    IReadOnlyList<CustomTheme> CustomThemes { get; }
    CustomTheme CreateCustomTheme(string name, string baseTheme);
    void SaveCustomTheme(CustomTheme theme);
    void DeleteCustomTheme(string themeId);
    void ApplyCustomTheme(CustomTheme theme);

    // ── 验证 ──
    ContractValidationResult ValidateAll();

    // ── 事件 ──
    event EventHandler<ThemeChangedEventArgs> ThemeChanged;
}
```

### 元数据类

```csharp
public class UIStyleMetadata
{
    public string Id { get; set; }
    public string DisplayName { get; set; }
    public string Description { get; set; }
}

public class IconStyleMetadata
{
    public string Id { get; set; }
    public string DisplayName { get; set; }
    public string Description { get; set; }
}
```

### 三维合约验证器

```csharp
public static class ContractValidator
{
    public static readonly IReadOnlyList<string> ThemeRequiredKeys = new[] { /* 37键 */ };
    public static readonly IReadOnlyList<string> UIStyleRequiredKeys = new[] { /* 59键 */ };
    public static readonly IReadOnlyList<string> IconRequiredKeys = new[] { /* 50键 */ };

    public static ContractValidationResult Validate(
        ResourceDictionary dict, IReadOnlyList<string> contract)
    {
        var missing = contract.Where(k => !dict.Contains(k)).ToList();
        return new ContractValidationResult
        {
            IsValid = missing.Count == 0,
            MissingKeys = missing
        };
    }
}
```

---

## 九、实施步骤 (分7个阶段)

### 阶段 1: 基础设施搭建 (零风险, 不影响运行)

| 步骤 | 内容 |
|------|------|
| 1.1 | 创建目录: `Styles/Themes/`, `Styles/UIStyles/`, `Styles/Icons/`, `Styles/Aliases/`, `Styles/Controls/` |
| 1.2 | 创建 `_ThemeContract.xaml` — 37 个 Semantic Token 默认值 |
| 1.3 | 创建 `_UIStyleContract.xaml` — 59 个 UI 形状 Token 默认值 |
| 1.4 | 创建 `_IconContract.xaml` — 50 个图标键默认值 (Emoji Fallback) |
| 1.5 | 创建 `BrushAliases.xaml` — 颜色别名映射 (~40个) |
| 1.6 | 创建 `_GlobalDefaults.xaml` — 提取 Converters, CloseButton 画刷 |
| 1.7 | 创建 `IThemeService.cs` + `ContractValidator.cs` |

### 阶段 2: 颜色主题迁移

| 步骤 | 内容 |
|------|------|
| 2.1 | 瘦身 Light.xaml → 37 Semantic Token + 元数据, 移到 `Styles/Themes/` |
| 2.2 | 瘦身 Dark.xaml → 同上 |
| 2.3 | 补齐+瘦身 Ocean/Forest/Sunset/Purple/Nordic (补缺失键+新增3键, 移除别名) |
| 2.4 | 修改 App.xaml 加载链 (新路径) |
| 2.5 | 修改 ThemeManager.ApplyTheme() 中路径 |
| 2.6 | ✅ 编译 + 切换7个主题验证 |

### 阶段 3: UI 风格迁移 + 新增 Token

| 步骤 | 内容 |
|------|------|
| 3.1 | 扩展 Fluent.xaml — 添加 36 个新 Token + 元数据, 移到 `Styles/UIStyles/` |
| 3.2 | 扩展 MacOS.xaml — 同上 |
| 3.3 | 扩展 Geek.xaml — 同上 |
| 3.4 | 扩展 Original.xaml — 同上 |
| 3.5 | 修改 `UI.TabItem.ActiveBackground` 为引用 Theme Token 方式 |
| 3.6 | 修改 ThemeManager.SetUIStyle() 中路径 |
| 3.7 | ✅ 编译 + 切换4个风格验证 |

### 阶段 4: 图标系统迁移

| 步骤 | 内容 |
|------|------|
| 4.1 | 为 Icons.Emoji.xaml 添加元数据, 补 `Icon_Window_Tasks`, 移到 `Styles/Icons/` |
| 4.2 | 为 Icons.Fluent.xaml 同上处理 |
| 4.3 | 为 Icons.Material.xaml 同上处理 |
| 4.4 | 为 Icons.Remix.xaml 同上处理 |
| 4.5 | 修改 ThemeManager.ChangeIconStyle() 中路径 |
| 4.6 | ✅ 编译 + 切换4个图标集验证 |

### 阶段 5: AppStyles.xaml 拆分 + 消除硬编码

| 步骤 | 内容 | 估算行数 |
|------|------|----------|
| 5.1 | 拆出 ScrollBarStyles.xaml (Overlay + Modern + Floating) | ~240行 |
| 5.2 | 拆出 GridSplitterStyles.xaml (Modern + V/H + Collapsible) | ~300行 |
| 5.3 | 拆出 ButtonStyles.xaml (Modern + Frameless + Accent + Toolbar) | ~200行 |
| 5.4 | 拆出 TabStyles.xaml (TabButton + ActiveTab + Template) | ~120行 |
| 5.5 | 拆出 TextBoxStyles.xaml | ~60行 |
| 5.6 | 拆出 DataGridStyles.xaml (ColumnHeader + FileListItem + DriveItem) | ~250行 |
| 5.7 | 拆出 ContextMenuStyles.xaml | ~150行 |
| 5.8 | 拆出 ComboBoxStyles.xaml | ~80行 |
| 5.9 | 拆出 NavigationStyles.xaml (Drive + QuickAccess + NavItem + Breadcrumb) | ~100行 |
| 5.10 | 拆出 DialogStyles.xaml (合并 BaseDialogStyle.xaml) | ~120行 |
| 5.11 | **同步替换**: 硬编码颜色 `#XXXXXX` → `{DynamicResource XxxBrush}` |
| 5.12 | **同步替换**: 硬编码 `CornerRadius="N"` → `{DynamicResource UI.*.CornerRadius}` |
| 5.13 | 更新 App.xaml 加载拆分后的文件 |
| 5.14 | 删除旧 `Styles/AppStyles.xaml` + 旧 `Dialogs/Styles/BaseDialogStyle.xaml` |
| 5.15 | ✅ 编译 + 全面视觉验证 |

### 阶段 6: 服务层统一 + 散落清理

| 步骤 | 内容 |
|------|------|
| 6.1 | 实现 `ThemeService.cs` (基于 ThemeManager 重构, 实现 IThemeService) |
| 6.2 | 添加 UIStyleMetadata/IconStyleMetadata 的自动发现逻辑 |
| 6.3 | 更新 CustomThemeManager.GetCoreColorKeysList() → 37 键 |
| 6.4 | 更新 Bootstrapper.cs → 使用 IThemeService |
| 6.5 | 更新 AppearanceSettingsViewModel.cs → 使用 IThemeService |
| 6.6 | 更新 AppearanceSettingsPanel.xaml.cs → 使用 IThemeService |
| 6.7 | 删除旧 `Services/ThemeService.cs` |
| 6.8 | 注册 DI: IThemeService → ThemeService |
| 6.9 | 清理 19 个 XAML 文件中的 92 处散落硬编码颜色 |
| 6.10 | ✅ 编译 + 全面验证 |

### 阶段 7: 自动化 + 收尾

| 步骤 | 内容 |
|------|------|
| 7.1 | 创建 `scripts/Validate-Contracts.ps1` — 一键检查三维合约完整性 |
| 7.2 | 创建 `scripts/New-Theme.ps1` — 交互式脚手架 (选择类型→复制模板→填入值) |
| 7.3 | 创建 `scripts/Detect-HardcodedColors.ps1` — 扫描残留硬编码 |
| 7.4 | 删除旧目录: `Themes/`, `Resources/UIStyles/`, `Resources/Icons/` |
| 7.5 | 更新 `.antigravityignore` |
| 7.6 | 更新 Refactoring_Tasks.md + Project_Evaluation_and_Roadmap.md |
| 7.7 | ✅ 全量回归: 7主题 × 4风格 × 4图标集 交叉验证 |

---

## 十、新增流程 (重构后)

### 10.1 新增颜色主题

```
1. 复制 _ThemeContract.xaml → MyTheme.xaml
2. 修改元数据 (Id, DisplayName, Description, Author, Version, 4个预览色)
3. 修改 37 个颜色值 (多个键允许共用同一个值)
4. 放入 Styles/Themes/
5. 运行 Validate-Contracts.ps1 确认 ✅
6. (自动发现, 无需修改代码)
```

### 10.2 新增 UI 风格

```
1. 复制 _UIStyleContract.xaml → MyStyle.xaml
2. 修改 3 个元数据键 (Id, DisplayName, Description)
3. 修改 56 个形状 Token 值 (CornerRadius, Margin, Padding, Height 等)
   — 多个键可以填同样的值, 例如所有圆角都设为 4
   — 引用 Theme 的 Brush 键只需写 {DynamicResource XxxBrush}
4. 放入 Styles/UIStyles/
5. 运行 Validate-Contracts.ps1 确认 ✅
6. (自动发现, 无需修改代码)
```

### 10.3 新增图标集

```
1. 复制 _IconContract.xaml → Icons.MySet.xaml
2. 修改 3 个元数据键 (Id, DisplayName, Description)
3. 修改 IconFontFamily 指向你的字体文件
4. 修改 45 个图标码点/字符
5. 放入 Styles/Icons/ + 字体文件放入 Resources/Fonts/
6. 运行 Validate-Contracts.ps1 确认 ✅
7. (自动发现, 无需修改代码)
```

---

## 十一、收益预期

| 指标 | 当前 | 重构后 |
|---|---|---|
| **颜色主题文件** | 120行 / 70键 (不一致) | ~65行 / 37键 (**100%一致**) |
| **风格文件** | 44行 / 23键 (缺元数据) | ~90行 / 59键 (**完整控件覆盖**) |
| **图标文件** | 73行 / 49键 (缺元数据+幽灵键) | ~85行 / 50键 (**含元数据+补齐**) |
| **最大单文件** | 2371行 | ≤300行 (**-87%**) |
| **硬编码颜色** | 92处 / 19个文件 | **0处** |
| **硬编码圆角** | 22+处 | **0处** |
| **幽灵资源** | 13画刷 + 1图标 = 14处 | **0处** |
| **新增主题** | 需懂70键+35别名 | 填37个颜色 + 运行脚本 |
| **新增风格** | 无模板, 不知改什么 | 填59个值(大量可共值) + 运行脚本 |
| **新增图标集** | 纯手工49键+字体 | 复制模板 + 填码点 + 运行脚本 |
| **焦点/非焦点区分** | 无 | ✅ PaneFocused/UnfocusedBrush |

---

## 十二、风险控制

| 风险 | 缓解措施 |
|------|----------|
| 合约默认值导致视觉不对 | 默认值使用 Light 主题 + Original 风格 + Emoji 图标, 确保 Fallback 本身可用 |
| DynamicResource 链断裂 | 三个 Contract.xaml 作为第一层加载, 保证所有键都有值 |
| 文件拆分后引用丢失 | 每拆一个模块立即编译+运行; 不跳步 |
| 自定义主题 JSON 兼容 | 保持 CustomTheme.Colors 的 key-value 格式不变 |
| ThemeManager 路径变更 | 旧路径也保留兼容扫描, 新路径优先 |
| UI Token 新增导致旧风格缺键 | _UIStyleContract.xaml 提供默认值兜底 |
| UIStyle 中引用 Theme Token 的 Brush 链 | 合约层提供 Fallback 色, 确保 ThemeContract+UIStyleContract 配合完整 |
