## 目标
- 在修复 Everything 集成前，临时禁用 Everything 搜索路径，保证地址栏搜索能稳定创建“搜索: 关键词”标签页并展示结果。

## 变更点
1. 搜索逻辑降级
- 在 `MainWindow.xaml.cs:PerformSearch(...)` 中，搜索名称分支强制走默认磁盘搜索（跳过 `EverythingHelper` 的可用性判断与调用）。
- 默认搜索：遍历已就绪的固定盘符，使用 `Directory.GetFiles(root, "*{keyword}*", SearchOption.TopDirectoryOnly)`（每盘最多1000条，合并去重），避免卡顿与异常。

2. 搜索流程健壮性
- 备注搜索失败时仅记录错误，不再 `return`，确保后续创建搜索标签页与展示结果仍然进行。
- 即使结果为空也创建标签页，面包屑显示 `搜索: 关键词`，标签区强制可见。

3. 标签页呈现
- 搜索后：
  - 若已有 `search://{关键词}` 标签，切换并刷新列表；
  - 否则创建新标签页 `Path = "search://{关键词}"，Title = "搜索: 关键词"`；
  - 调用 `FileBrowser.FilesItemsSource = results` 与 `FileBrowser.UpdateBreadcrumbText("搜索: 关键词")`。

## 验证
- 构建并运行，地址栏输入非路径关键词点击搜索：
  - 总是打开或切换到搜索标签页；
  - 文件列表展示符合关键词的顶层文件或空状态；
  - 面包屑显示“搜索: 关键词”，标签区可见。
- 断开 Everything 进程，搜索行为不受影响。

## 说明
- 不新增控件或窗体，复用现有 `FileBrowserControl` 与标签页机制。
- 这是临时降级，后续可在修复 Everything 后再恢复 SDK 路径（或加配置开关）。

请确认，我将据此实施变更并完成构建与运行验证。