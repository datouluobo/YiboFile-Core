# OoiMRR -  文件资源管理器

一个基于 WPF 的现代化文件资源管理器，提供丰富的文件预览和管理功能。

## 主要功能

### 📁 文件管理
- **三栏布局**：导航栏、文件列表、预览/备注区
- **文件操作**：新建、复制、粘贴、剪切、删除、重命名
- **快捷键支持**：完整的 Windows 标准快捷键（Ctrl+C/V/X、Delete、F2、F5 等）
- **智能导航**：方向键导航、回车打开、Backspace 返回上级
- **自动刷新**：文件系统监控，实时更新文件列表

### 🎨 文件预览
- **文本文件**：TXT、LOG、代码文件等
- **图片文件**：PNG、JPG、GIF、BMP、SVG 等常见格式
- **文档文件**：
  - DOCX/DOC（HTML 转换预览 + 转换为 DOCX 功能）
  - PDF（WebView2 渲染）
  - XLSX（表格数据预览）
- **视频文件**：MP4、AVI、MKV 等（带播放控制）
- **音频文件**：MP3、WAV、FLAC 等
- **压缩包**：ZIP、RAR、7Z（文件列表预览，支持中文编码）
- **文件夹**：显示文件夹内容列表

### 📝 新建文件
支持创建多种类型的文件，并生成实际格式内容：
- **Office 文档**：Word、Excel、PowerPoint（使用 COM 创建）
- **图片文件**：PNG、JPG、GIF、BMP（500x500 空白图片）、SVG
- **代码文件**：C#、Python、Java、JavaScript、HTML、CSS 等（带模板代码）
- **配置文件**：JSON、XML、INI、Markdown 等
- **脚本文件**：BAT、PowerShell

### 🏷️ 标签和备注
- 为文件和文件夹添加自定义标签
- 添加备注信息
- 按标签筛选文件
- 搜索功能

### 🤖 TagTrain - AI 图片标签训练系统
- **机器学习标签预测**：基于 ML.NET 的图片标签自动预测
- **智能训练系统**：
  - 支持手动标注训练样本
  - 自动训练分类模型
  - 增量训练支持，持续优化模型
  - 训练进度实时显示
- **标签管理**：
  - 标签自动补全和搜索
  - 标签使用统计
  - 重复标签合并
  - 批量操作支持
- **无缝集成**：
  - 在 OoiMRR 中直接使用标签功能
  - 图片标注自动保存为训练数据
  - 支持独立训练窗口模式（`--tagtrain` 参数）
  - 标签浏览模式，按标签查看文件
- **模型管理**：
  - 模型版本管理
  - 模型验证和诊断
  - 训练历史记录
  - 模型状态指示器

### 📚 库功能
- **Windows 风格库**：类似 Windows 库功能，可以聚合多个文件夹位置
- **多路径支持**：一个库可以包含多个不同路径的文件夹
- **库管理**：创建、重命名、删除库，管理库的位置
- **右键菜单**：重命名、删除、管理位置、在资源管理器中打开
- **文件浏览**：在库模式下查看所有位置的合并文件列表

### ⭐ 收藏功能
- **收藏文件夹和文件**：快速访问常用位置
- **分类显示**：文件夹在上，文件在下
- **拖拽排序**：通过拖拽调整收藏项顺序
- **同名区分**：自动添加父文件夹名称区分同名项
- **整行背景**：清晰的列表显示，鼠标悬停高亮
- **双击打开**：快速导航到收藏的位置

### 🎯 特色功能
- **创建时间着色**：按时间长短用不同颜色显示（秒-绿色、分钟-青色、小时-蓝色、天-紫色、月-棕色、年-灰色）
- **文件夹预览**：右侧预览区显示与左侧一致的文件列表
- **统一对话框风格**：现代化、无边框、圆角设计
- **自动序号命名**：新建文件/文件夹重名时自动添加序号

## 技术栈

- **.NET 8.0** - Windows 桌面应用框架
- **WPF** - UI 框架
- **WebView2** - PDF 和 DOCX 预览
- **ExcelDataReader** - Excel 文件读取
- **System.IO.Compression** - ZIP 文件处理
- **FileSystemWatcher** - 文件系统监控
- **ML.NET** - 机器学习框架（TagTrain）
- **Microsoft.ML.ImageAnalytics** - 图像分析
- **Microsoft.ML.Vision** - 视觉模型支持
- **TensorFlow.NET** - 深度学习后端

## 系统要求

- Windows 10/11
- .NET 8.0 Runtime
- WebView2 Runtime（用于文档预览）

## 构建和运行

```bash
# 克隆仓库
git clone <repository-url>

# 进入项目目录
cd OoiMRR

# 构建项目
dotnet build

# 运行项目
dotnet run
# 或使用脚本
.\scripts\run.ps1
```

## 项目结构

```
OoiMRR/
├── Previews/                      # 预览功能模块
│   ├── IPreviewProvider.cs        # 预览提供者接口
│   ├── PreviewFactory.cs          # 预览工厂
│   ├── PreviewHelper.cs           # 预览辅助工具
│   ├── TextPreview.cs             # 文本预览
│   ├── ImagePreview_1.0.2.cs      # 图片预览
│   ├── VideoPreview.cs            # 视频预览
│   ├── AudioPreview.cs            # 音频预览
│   ├── DocumentPreview.cs         # 文档预览
│   ├── PdfPreview_1.0.2.cs        # PDF 预览
│   ├── ExcelPreview.cs            # Excel 预览
│   ├── PowerPointPreview.cs       # PowerPoint 预览
│   ├── CadPreview.cs              # CAD 预览
│   ├── ArchivePreview.cs          # 压缩包预览
│   ├── FolderPreview.cs           # 文件夹预览
│   ├── LnkPreview.cs              # 快捷方式预览
│   ├── HtmlPreview.cs             # HTML 预览
│   ├── XmlPreview.cs              # XML 预览
│   └── DocxToHtmlConverter.cs     # DOCX 转 HTML
├── Rendering/                     # 渲染引擎
│   ├── DxfRenderEngine.cs         # DXF 渲染引擎
│   └── DxfSvgConverter.cs         # DXF 转 SVG
├── Controls/                      # 控件模块
│   ├── ActionButtonsControl.xaml/cs        # 操作按钮控件
│   ├── AddressBarControl.xaml/cs           # 地址栏控件
│   ├── FileBrowserControl.xaml/cs          # 文件浏览器控件
│   ├── FileListControl.xaml/cs             # 文件列表控件
│   ├── NavigationPanelControl.xaml/cs      # 导航面板控件
│   ├── SettingsPanelControl.xaml/cs        # 设置面板控件
│   ├── TabManagerControl.xaml/cs           # 标签页管理控件
│   ├── TitleActionBar.xaml/cs              # 标题栏操作控件
│   ├── WindowControlButtonsControl.xaml/cs # 窗口控制按钮控件
│   ├── RightPanelControl.xaml/cs           # 右侧面板控件
│   ├── FFmpegHelper.cs                     # FFmpeg 辅助工具
│   └── ThumbnailViewManager.cs             # 缩略图视图管理
├── Services/                      # 服务模块
│   ├── FileList/                  # 文件列表服务
│   │   ├── FileListService.cs              # 文件列表服务
│   │   ├── FileSystemWatcherService.cs     # 文件系统监控服务
│   │   ├── FolderSizeCalculationService.cs # 文件夹大小计算服务
│   │   ├── FolderSizeCalculator.cs         # 文件夹大小计算器
│   │   └── FileMetadataEnricher.cs         # 文件元数据增强器
│   ├── FileOperations/            # 文件操作服务
│   │   ├── DeleteOperation.cs              # 删除操作
│   │   ├── FileClipboardManager.cs         # 剪贴板管理
│   │   ├── NewFileOperation.cs             # 新建文件操作
│   │   ├── NewFolderOperation.cs           # 新建文件夹操作
│   │   ├── PasteOperation.cs               # 粘贴操作
│   │   ├── RenameOperation.cs              # 重命名操作
│   │   ├── IFileOperationContext.cs        # 文件操作上下文接口
│   │   ├── PathOperationContext.cs         # 路径操作上下文
│   │   ├── LibraryOperationContext.cs      # 库操作上下文
│   │   └── TagOperationContext.cs          # 标签操作上下文
│   ├── Search/                    # 搜索服务
│   │   ├── SearchService.cs                # 搜索服务
│   │   ├── SearchCacheService.cs           # 搜索缓存服务
│   │   ├── SearchFilterService.cs          # 搜索过滤服务
│   │   ├── SearchResultBuilder.cs          # 搜索结果构建器
│   │   ├── SearchResultGrouper.cs          # 搜索结果分组器
│   │   ├── SearchPaginationService.cs      # 搜索分页服务
│   │   ├── EverythingSearchExecutor.cs     # Everything 搜索执行器
│   │   └── NotesSearchExecutor.cs          # 备注搜索执行器
│   ├── Navigation/                # 导航服务
│   │   ├── NavigationService.cs            # 导航服务
│   │   ├── NavigationCoordinator.cs        # 导航协调器
│   │   ├── NavigationModeService.cs        # 导航模式服务
│   │   └── INavigationUIHelper.cs          # 导航 UI 辅助接口
│   ├── ColumnHeader/              # 列头服务
│   │   └── ColumnHeaderService.cs          # 列头管理服务
│   ├── ColumnManagement/          # 列管理服务
│   │   └── ColumnService.cs                # 列管理服务
│   ├── Favorite/                  # 收藏服务
│   │   └── FavoriteService.cs              # 收藏管理服务
│   ├── QuickAccess/               # 快速访问服务
│   │   └── QuickAccessService.cs           # 快速访问服务
│   ├── Tag/                       # 标签服务
│   │   └── TagService.cs                   # 标签管理服务
│   ├── FileNotes/                 # 文件备注服务
│   │   └── FileNotesService.cs             # 文件备注管理服务
│   ├── Preview/                   # 预览服务
│   │   └── PreviewService.cs               # 预览管理服务
│   ├── Tabs/                      # 标签页服务
│   │   ├── TabService.cs                   # 标签页管理服务
│   │   └── TabModels.cs                    # 标签页模型
│   ├── Settings/                  # 设置服务
│   │   └── SettingsOverlayController_1.0.2.cs  # 设置覆盖层控制器
│   ├── Config/                    # 配置服务
│   │   ├── ConfigService.cs                # 配置管理服务
│   │   └── IConfigUIHelper.cs              # 配置 UI 辅助接口
│   ├── TagTrain/                  # TagTrain 服务
│   │   ├── ImageTagTrainer.cs              # 图片标签训练器
│   │   ├── DataManager.cs                  # 数据管理
│   │   ├── SettingsManager.cs              # 设置管理
│   │   └── TagTrainEventHandler.cs         # 事件处理器
│   ├── Bridges/                   # 桥接服务
│   │   └── FileBrowserBridge.cs            # 文件浏览器桥接
│   ├── Abstractions/              # 抽象定义
│   │   └── NavigationContracts.cs          # 导航契约
│   ├── LibraryService.cs          # 库管理服务
│   ├── NavigationStateManager.cs  # 导航状态管理
│   ├── DragDropManager.cs         # 拖拽管理
│   ├── MainWindowInitializer.cs   # MainWindow 初始化服务
│   ├── OoiMRRIntegration.cs       # TagTrain 集成接口
│   └── ...                        # 其他辅助服务
├── ViewModels/                    # 视图模型
│   ├── BaseViewModel.cs           # 视图模型基类
│   ├── FileListViewModel.cs       # 文件列表视图模型
│   ├── LibraryViewModel.cs        # 库视图模型
│   ├── NavigationViewModel.cs     # 导航视图模型
│   ├── TagViewModel.cs            # 标签视图模型
│   └── SearchResultGroupViewModel.cs  # 搜索结果分组视图模型
├── Handlers/                      # 事件处理器
│   ├── FileBrowserEventHandler.cs # 文件浏览器事件处理器
│   ├── FileListEventHandler.cs    # 文件列表事件处理器
│   ├── KeyboardEventHandler.cs    # 键盘事件处理器
│   ├── MenuEventHandler.cs        # 菜单事件处理器
│   └── MouseEventHandler.cs       # 鼠标事件处理器
├── Models/                        # 数据模型
│   └── TabItemModel.cs            # 标签页项模型
├── Styles/                        # 样式资源
│   └── AppStyles.xaml             # 应用样式
├── Windows/                       # 窗口
│   ├── ColumnChooserWindow.xaml/cs    # 列选择窗口
│   └── DateFilterWindow.xaml/cs       # 日期过滤窗口
├── UI/TagTrain/                   # TagTrain UI 模块
│   ├── TrainingWindow.xaml/cs     # 训练主窗口
│   ├── TagPanel.xaml/cs           # 标签面板
│   ├── ConfigWindow.xaml/cs       # 配置窗口
│   └── TrainingStatusWindow.xaml/cs   # 训练状态窗口
├── MainWindow.xaml/cs             # 主窗口
├── FileTypeManager.cs             # 文件类型管理
├── ConfigManager.cs               # 配置管理
├── DatabaseManager.cs             # 数据库管理
├── DialogService.cs               # 对话框服务
├── PathInputDialog.xaml/cs        # 路径输入对话框
├── ConfirmDialog.xaml/cs          # 确认对话框
└── ...                            # 其他文件
```

## 更新日志

> ⚠️ **重要提示**: v1.5.2 和 v1.5.3 版本存在问题，不推荐使用。请升级到 [v1.5.6](../../releases/tag/v1.5.6) 或更高版本。

### v1.7.8 (2026-01-01)

**5种美观内置主题 + UI优化**
- 🎨 **5种新主题**：Ocean（海洋蓝）、Forest（森林绿）、Sunset（日落橙）、Purple（紫色梦幻）、Nordic（北欧灰）
- 🔄 **自动主题发现**：ThemeManager自动扫描Themes目录，轻松扩展新主题
- 📦 **ComboBox选择器**：用下拉列表替代平铺RadioButton，更紧凑易扩展
- 😊 **Emoji图标**：每个主题配有专属emoji，视觉识别更直观
- 📏 **控件宽度优化**：设置面板所有控件添加MaxWidth限制，避免过度拉伸

### v1.7.7 (2026-01-01)

**自定义主题颜色功能 - 打造个性化配色方案**
- 🎨 **自定义主题创建**：基于Light/Dark主题创建个性化配色方案
- 🖌️ **颜色选择器**：完整的RGB/HSV/Hex颜色选择工具
- 📝 **28个核心颜色编辑**：可自定义所有主题颜色（强调色、背景、文本、边框等7大类）
- 💾 **主题管理**：查看、编辑、删除和应用自定义主题
- 👁️ **实时预览**：编辑时实时查看颜色效果
- 🔄 **主题持久化**：自定义主题保存为JSON文件，重启后自动加载

### v1.7.6.1 (2026-01-01)

**列头视觉改进：**
- ✨ 优化列头悬停效果（明显的浅蓝色背景）
- 📏 添加列分隔线视觉反馈（悬停和拖动时的蓝色竖条）
- 🔼🔽 实现排序指示器（升序/降序箭头图标）
- 🎨 排序箭头使用主题色，自动适配Light/Dark主题
- 🐛 修复因排序箭头导致的横向滚动条问题

### v1.7.6 (2026-01-01)
**外观设置增强 - 系统主题跟随与UI美化**
- 🔄 **系统主题自动跟随**：监听Windows主题变化并自动切换，提供更智能的体验
- 💎 **启动透明度应用**：窗口透明度设置在应用启动时自动生效
- 🎨 **外观面板UI美化**：颜色预览区域采用卡片式设计，更大的颜色块（80px），圆润的圆角（6px），悬停动画效果
- ⚡ **动画效果控制**：新增AnimationsEnabled全局开关，可在低配设备上禁用动画提升性能
- 🔧 **事件管理优化**：正确处理SystemEvents的注册和注销，避免内存泄漏

### v1.7.5 (2026-01-01)
**外观设置面板 - 主题管理功能**
- ✨ **新增外观设置面板**：统一管理主题、颜色和外观设置
- 🎨 **主题选择**：支持浅色/深色/跟随系统三种模式
- 🔍 **主题颜色预览**：显示当前主题的核心颜色（主色调、背景、表面、文本）
- 💎 **窗口透明度**：可调节50%-100%透明度，实时生效
- ⚡ **动画效果开关**：可关闭动画以提升低配设备性能
- 🔧 **配置扩展**：AppConfig新增ThemeMode、WindowOpacity、AnimationsEnabled属性
- 📋 **功能优化**：从通用设置移除重复的主题选择，避免功能冗余

### v1.7.2 (2025-12-28)
**标题栏 UI 细节打磨**
- 📐 **完美对齐**：右上角控制按钮现在严格垂直居中，且与预览区分界线完美对齐，消除视觉误差。
- 🖥️ **最大化优化**：修复窗口最大化时按钮位置偏移问题，确保在不同窗口状态下按钮相对于内容的视觉位置始终如一。
- 🎨 **全高交互**：按钮背景色现在垂直填满整个标题栏区域（51px），提供更大的点击热区和更饱满的视觉反馈。

### v1.7.1 (2025-12-27)
**分割器交互体验升级**
- 🎨 **视觉优化**：回归实心色块设计，采用高对比度配色（深灰/鲜绿），显著提升可见性。
- 📐 **布局改良**：实施“方向感知”间距策略，大幅增加按钮间距（12px）且不破坏布局，防止误触。
- 🚀 **动画流畅度**：升级折叠动画算法为 `CubicEase` 并强制 60FPS 渲染，体验更加丝滑跟手。

### v1.7.0 (2025-12-26)
**Legacy 格式 UI 统一与重构**
... (此处省略 v1.7.0 详情)

### v1.5.6 (2025-12-21)
**文件选择逻辑重构**
- ♻️ 抽离 `FileSelectionLogic` 到独立处理器 `SelectionEventHandler`
- 🧠 优化 AI 标签预测逻辑（仅在 Tag 导航 + 编辑模式下触发）
- 📁 迁移文件夹大小计算逻辑，支持取消操作
- 🔧 修复 `MainWindow` 依赖注入和事件处理
- ✅ 解决所有编译错误，代码结构更加模块化

### v1.5.5 (2025-12-21)
...

---

**当前版本**: v1.7.8  
**最后更新**: 2026-01-01  
**版本说明**: 5种美观内置主题 + UI优化
