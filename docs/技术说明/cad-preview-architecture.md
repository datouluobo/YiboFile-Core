# CAD 预览与缩略图技术说明

## 路由与入口
- 路由：`Previews/PreviewFactory.cs:86-99` 将 `.dwg`/`.dxf` 路由到 `CadPreview`。
- 预览：`Previews/CadPreview.cs:18` 入口；`.dxf` 走内嵌渲染分支，`.dwg` 打开本地查看器或说明页。

## 渲染与缩略图
- DXF 渲染：`Rendering/DxfRenderEngine.cs` 读取实体并用 Skia 绘制，输出 `BitmapSource`。
- 缩略图：`Controls/Converters/ThumbnailConverter.cs:231` 优先 DXF 自渲染与 `Services/CadImageCache` 缓存；Shell 缩略图与系统图标为回退。

## 多页与错误反馈
- 布局选择：`CadPreview.cs` 组合框切换布局，实时渲染。
- 错误反馈：DXF 渲染异常时弹窗提示并清空画面；DWG 无查看器时显示说明页。

## 兼容与回退
- 外部查看器：`CadPreview.cs:154` 检测路径；提供“使用本地查看器打开”和“使用系统默认程序打开”。
- DWG 支持：占位缩略图与外部打开；建议后续引入转换工具或商用库。

## 缓存策略
- 路径+尺寸+最后修改时间组成键，磁盘 PNG 存储于 `%AppData%/OoiMRR/Cache/CAD/`。
- 缓存命中优先返回，渲染后入库。
