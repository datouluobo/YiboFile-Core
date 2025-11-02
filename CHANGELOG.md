# 更新日志

所有重要的项目更改都将记录在此文件中。

## [1.0.0] - 2025-01-02

### 新增功能 ✨

#### 核心功能
- 三栏式文件管理器布局（导航栏、文件列表、预览/备注区）
- 完整的文件操作功能（新建、复制、粘贴、剪切、删除、重命名）
- Windows 标准快捷键支持（Ctrl+C/V/X、Delete、F2、F5、Alt+Enter 等）
- 智能键盘导航（方向键、Home/End、Enter、Backspace）
- 文件系统实时监控和自动刷新（300ms 防抖）

#### 预览功能 🎨
- **文本文件**：支持多种编码（UTF-8、GBK、GB2312 等）
- **图片文件**：PNG、JPG、GIF、BMP、SVG、ICO、TIFF、WebP
- **视频文件**：MP4、AVI、MKV、WMV、MOV、FLV（带播放控制）
- **音频文件**：MP3、WAV、FLAC、AAC、OGG、WMA
- **文档文件**：
  - DOCX/DOC：HTML 转换预览 + 转换为 DOCX 功能
  - PDF：WebView2 渲染
  - XLSX：表格数据预览（支持多工作表）
- **压缩包**：ZIP、RAR、7Z 文件列表预览（支持中文编码自动检测）
- **文件夹**：显示文件夹内容列表（与左侧列表一致的界面）

#### 新建文件功能 📝
- **Office 文档**：Word、Excel、PowerPoint（使用 COM 自动化创建实际格式）
- **图片文件**：
  - PNG、JPG、GIF、BMP：500x500 像素空白图片
  - SVG：矢量图模板
- **代码文件**：C#、Python、Java、JavaScript、HTML、CSS（带模板代码）
- **配置文件**：JSON、XML、INI、Markdown
- **脚本文件**：BAT、PowerShell
- 支持自定义扩展名
- 重名自动添加序号

#### UI/UX 改进 🎯
- **创建时间列**：
  - 简洁格式显示（s/m/h/d/mo/y）
  - 彩虹色渐变着色：
    - 秒（s）- 绿色 + 粗体
    - 分钟（m）- 青色 + 半粗体
    - 小时（h）- 蓝色
    - 天（d）- 紫色
    - 月（mo）- 棕色
    - 年（y）- 深灰色
- **文件夹预览**：右侧预览区显示与左侧一致的文件列表界面
- **统一对话框风格**：
  - 现代化设计（圆角、无边框、阴影）
  - 支持 Enter/Esc 快捷键
  - 一致的视觉风格
- **布局优化**：
  - 列3与列2水平对齐
  - 响应式布局
  - 可调整列宽

#### 标签和备注 🏷️
- 为文件和文件夹添加自定义标签
- 添加和编辑备注信息
- 按标签筛选文件
- 搜索功能

### 技术实现 🔧

#### 依赖包
- Microsoft.Web.WebView2 (1.0.3537.50) - PDF 和 DOCX 预览
- ExcelDataReader (3.7.0) - Excel 文件读取
- ExcelDataReader.DataSet (3.7.0) - Excel 数据集支持
- System.Text.Encoding.CodePages (9.0.10) - 扩展编码支持

#### 架构设计
- **模块化预览系统**：
  - `IPreviewProvider` 接口
  - `PreviewFactory` 工厂模式
  - 按文件类型分离的预览类
- **文件类型管理**：`FileTypeManager` 统一管理文件类型信息
- **配置管理**：`ConfigManager` 处理应用配置
- **数据库管理**：`DatabaseManager` 管理标签和备注数据

#### 代码组织
```
Previews/
├── IPreviewProvider.cs      # 预览提供者接口
├── PreviewFactory.cs        # 预览工厂
├── PreviewHelper.cs         # 预览辅助工具
├── TextPreview.cs           # 文本预览
├── ImagePreview.cs          # 图片预览
├── VideoPreview.cs          # 视频预览
├── AudioPreview.cs          # 音频预览
├── DocumentPreview.cs       # 文档预览
├── ArchivePreview.cs        # 压缩包预览
├── FolderPreview.cs         # 文件夹预览
└── DocxToHtmlConverter.cs   # DOCX 转 HTML
```

### 已知问题 ⚠️
- CAD 文件（DWG/DXF）预览功能已实现但需要外部查看器
- RAR 和 7Z 文件预览依赖于系统安装的解压工具

### 待开发功能 📋
- [ ] Excel 文件预览增强（图表、公式显示）
- [ ] PowerPoint 文件预览
- [ ] 文件拖拽支持
- [ ] 批量操作
- [ ] 文件搜索增强（内容搜索）
- [ ] 主题切换（深色模式）
- [ ] 文件收藏夹
- [ ] 历史记录

---

## 版本说明

版本号格式：`主版本.次版本.修订号`

- **主版本**：重大功能变更或架构调整
- **次版本**：新增功能或重要改进
- **修订号**：Bug 修复或小改进

---

**最后更新**: 2025-01-02

