# YiboFile Core - 第三方组件声明 / Third-Party Notices

本应用包含以下第三方组件。感谢所有开源和免费软件的贡献者。

This application includes the following third-party components. We thank all open-source and freeware contributors.

---

## 捆绑工具 / Bundled Tools

### 1. 7-Zip
- **用途**: 压缩包浏览与解压（zip, 7z, rar, tar, gz 等格式）
- **版本**: 24.09
- **许可证**: GNU LGPL v2.1 / BSD 3-Clause（unRAR 代码）
- **官网**: https://www.7-zip.org/
- **源码**: https://sourceforge.net/projects/sevenzip/files/7-Zip/
- **说明**: 7-Zip 为开源软件，按 LGPL 许可证分发。本应用仅使用其命令行版本（7z.exe, 7z.dll）。

### 2. Everything SDK
- **用途**: 文件名即时搜索（基于 Voidtools Everything 索引引擎）
- **组件**: Everything64.dll (SDK), Everything.exe (可选)
- **许可证**: Freeware（SDK 免费使用）
- **官网**: https://www.voidtools.com/
- **SDK 文档**: https://www.voidtools.com/support/everything/sdk/
- **说明**: Everything 是免费的 Windows 桌面搜索工具。本应用通过其 SDK DLL 实现文件名搜索功能，不修改 Everything 引擎本身。

---

## NuGet 依赖 / NuGet Dependencies

### 3. AvalonEdit
- **用途**: 代码/文本编辑器控件
- **许可证**: MIT License
- **源码**: https://github.com/icsharpcode/AvalonEdit

### 4. DocumentFormat.OpenXml
- **用途**: Office 文档（Word, Excel, PPT）格式解析与预览
- **许可证**: MIT License
- **源码**: https://github.com/OfficeDev/Open-XML-SDK

### 5. LibVLCSharp.WPF + VideoLAN.LibVLC
- **用途**: 视频与音频播放（基于 VLC 媒体引擎）
- **许可证**: LGPL v2.1 (LibVLCSharp), LGPL v2.1 / GPL v2 (VLC)
- **源码**: https://github.com/videolan/libvlcsharp, https://github.com/videolan/vlc
- **说明**: VLC 按原样分发，未做任何修改。

### 6. Magick.NET
- **用途**: 高级图片处理与格式转换（基于 ImageMagick）
- **许可证**: Apache-2.0
- **源码**: https://github.com/dlemstra/Magick.NET

### 7. Markdig
- **用途**: Markdown 文档解析与渲染
- **许可证**: BSD-2-Clause
- **源码**: https://github.com/xoofx/markdig

### 8. Microsoft.Data.Sqlite
- **用途**: SQLite 数据库访问（收藏夹、标签、备注等本地数据）
- **许可证**: MIT License
- **源码**: https://github.com/dotnet/efcore

### 9. Microsoft.Extensions.DependencyInjection
- **用途**: 依赖注入容器
- **许可证**: MIT License
- **源码**: https://github.com/dotnet/runtime

### 10. Microsoft.Web.WebView2
- **用途**: 基于 Chromium 的内嵌浏览器控件（PDF/HTML/Markdown 预览）
- **许可证**: BSD-3-Clause
- **源码**: https://github.com/MicrosoftEdge/WebView2Feedback
- **运行时要求**: 需要 WebView2 Runtime（Windows 10/11 通常已预装）

### 11. SkiaSharp
- **用途**: 2D 图形渲染（图标、缩略图等）
- **许可证**: MIT License
- **源码**: https://github.com/mono/SkiaSharp

### 12. IxMilia.Dxf
- **用途**: DXF (CAD) 文件解析与预览
- **许可证**: BSD-3-Clause
- **源码**: https://github.com/ixmilia/dxf

### 13. VirtualizingWrapPanel
- **用途**: 高性能虚拟化 WrapPanel 控件
- **许可证**: MS-PL
- **源码**: https://github.com/sbaeumlisberger/VirtualizingWrapPanel

### 14. WpfAnimatedGif
- **用途**: WPF GIF 动画播放
- **许可证**: Apache-2.0
- **源码**: https://github.com/XamlAnimatedGif/WpfAnimatedGif

### 15. System.IO.Packaging
- **用途**: ZIP/OpenXML 包格式处理
- **许可证**: MIT License

### 16. System.Drawing.Common
- **用途**: 图像处理辅助（Shell 图标提取等）
- **许可证**: MIT License

---

## 运行时 / Runtime

### .NET 8 Runtime
- **许可证**: MIT License
- **源码**: https://github.com/dotnet/runtime
- **说明**: 本应用为自包含部署（self-contained），内置 .NET 8 运行时，用户无需额外安装。

---

*最后更新: 2026-04-23 | YiboFile Core v1.0.1920.0*
