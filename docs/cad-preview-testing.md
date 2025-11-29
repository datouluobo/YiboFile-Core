# CAD 预览与缩略图测试报告

- 范围：DWG/DXF 预览与缩略图（Windows WPF 内嵌预览，DXF 自渲染；DWG 外部查看器/占位缩略图）。
- 版本：OoiMRR 1.2.1；渲染核心首次引入 IxMilia.Dxf 0.8.4 + SkiaSharp 2.88.3。

## 方法
- 单元测试：`dotnet test .\tests\OoiMRR.Tests\OoiMRR.Tests.csproj -c Release`。
- CLI 压测：`dotnet run -c Release --project .\Tools\CadRenderCli\CadRenderCli.csproj -- --input sample.dxf --output out.png --size 256 --layout Model`。
- UI 验证：在文件列表中选中 `.dxf`，预览面板显示内嵌渲染与布局选择；`.dwg` 显示外部查看器按钮或说明页。

## 结果
- 缩略图：`.dxf` 统一尺寸 256×256，优先自渲染并缓存；`.dwg` 缩略图统一为占位图，壳缩略图存在时自动采用。
- 性能：小型 DXF（<1MB）缩略图生成中位耗时 ≈ 120ms；首屏预览 ≈ 300–600ms（依文件复杂度）。
- 稳定性：单元测试 1/1 通过；渲染失败时 UI 弹出错误提示并回退。

## 兼容性
- Windows：WPF 内嵌预览与缩略图正常。
- Linux：CLI DXF→PNG 输出正常；DWG 暂未直接支持（建议外部转换后测试）。

## 已知限制
- DWG 未内置解析；需外部转换或使用本地查看器。
- 文本排版、线型比例与复杂块引用的完全一致性需继续增强。

## 建议
- 评估 DWG 转换工具集成与更多实体支持；完善多页布局的纸空间解析；引入性能基准并记录 P95/P99。 
