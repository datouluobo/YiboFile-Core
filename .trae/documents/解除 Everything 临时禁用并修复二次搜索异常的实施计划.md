## 目标
- 重新启用 Everything 搜索（解除临时禁用），验证 SDK 可用性并恢复优先调用。
- 修复搜索标签页的二次搜索异常（重复创建“搜索: 搜索: …”标签、结果不刷新/空列表）。

## 现象与根因
- 截图显示逐次搜索会生成多个标签标题“搜索: 搜索: win”，且结果区为空；说明：
  - 搜索标题被重复添加“搜索:”前缀，且用于构造标签页键值（`search://{searchText}`）的 `searchText` 不是原始关键词，而是带前缀的可视文本，导致每次搜索创建新标签页（`search://win` → `search://搜索: win` → `search://搜索: 搜索: win`）。
  - 列表不刷新源或刷新为空，怀疑由于搜索文本被污染（带前缀）而查询不到结果，或 ItemsSource 未正确更新。
- 代码位置：
  - 搜索入口：`MainWindow.xaml.cs:FileBrowser_SearchClicked`（读取 `FileBrowser.AddressText` 触发 `PerformSearch`）。
  - 搜索执行：`MainWindow.xaml.cs:PerformSearch`（创建/切换标签页、设置 `FilesItemsSource`、更新面包屑）。
  - 标签切换：`MainWindow.xaml.cs:SwitchToTab`（处理 `search://` 标签页不加载路径）。

## 修复方案
1. 解除临时禁用并恢复 Everything 优先
- 恢复 `PerformSearch` 名称搜索分支使用 Everything：
  - 可用时调用 `EverythingHelper.SearchFiles(keyword, 10000, null)`；不可用回退默认搜索。
  - 保留 `StringBuilder(4096)` 和 `QueryW/A` 函数映射（`Services/EverythingHelper.cs` 已修复）。
  - 在 `App.OnStartup` 保持异步初始化，日志验证版本号与索引状态。

2. 规范搜索关键词（避免“搜索:”污染）
- 读取地址栏搜索词时进行规范化：
  - 去掉所有前缀“搜索:”和周围空白：`NormalizeSearchText(AddressText)`；处理中文/英文/数字。
  - 构造标签页路径与标题使用规范化关键词：`searchTabPath = "search://" + normalized`，`searchTabTitle = "搜索: " + normalized`。
- 进入搜索标签页后：
  - 更新面包屑为“搜索: 关键词”（仅用于显示）；
  - 将地址栏文本设为规范化关键词（保持可编辑的原始词），避免下次搜索读到带前缀字符串。

3. 标签复用与结果刷新
- 标签唯一性：
  - 查找现有 `search://{normalized}` 标签并复用；若存在则只更新 `FilesItemsSource` 与面包屑；不再创建新标签。
- 结果刷新：
  - 每次搜索都覆盖 `_currentFiles` 与 `FileBrowser.FilesItemsSource`；确保缩略图视图优先加载逻辑正常触发。

4. 历史/缓存与界面稳定
- 历史（后续扩展）：将搜索关键词与耗时记录在 SQLite；本次修复不启用历史干预，避免二次搜索读到错误缓存。
- UI 刷新：
  - 强制显示标签区（`FileBrowser.TabsVisible = true`）；
  - 确保在切换到已有搜索标签页时，列表源与面包屑更新立即生效。

## 代码改动点
- `MainWindow.xaml.cs`
  - `FileBrowser_SearchClicked`：对 `FileBrowser.AddressText` 进行规范化（剥离“搜索:”），并传递到 `PerformSearch`。
  - `PerformSearch`：
    - 恢复 Everything 分支：可用则调用 SDK，不可用回退默认搜索；
    - 使用规范化关键词构造 `searchTabTitle` 与 `searchTabPath`；
    - 复用现有标签页（`_pathTabs.FirstOrDefault(t => t.Path == searchTabPath)`）；
    - 刷新 `FilesItemsSource`、面包屑文本，并将地址栏文本重设为规范化关键词。
  - `SwitchToTab`（搜索标签页分支）：
    - 从 `tab.Path`（`search://{keyword}`）解析关键词，进入时同步地址栏文本为关键词，`IsReadOnly=false`；
    - 保持不加载路径，只展示当前结果。

## 验证用例
- 连续搜索：在搜索标签页内多次输入不同关键词（英文/数字/中文），仅存在每个关键词一个标签页，结果正确刷新。
- 关键词稳定：无“搜索: 搜索: …”连带前缀现象；地址栏始终显示原始关键词。
- 性能与回退：Everything 可用时快速返回；不可用时默认搜索仍可正常显示结果。
- 不影响备注搜索与路径导航功能。

## 风险与回滚
- 若 Everything SDK 在目标环境仍部分函数不可用（如 `Query` 装饰名不同），保留回退路径确保功能稳定；必要时增加 IPC WM_COPYDATA 方案（SDK 文档支持）。

请确认该实施计划；确认后我将进行代码修改、构建与运行验证，并回传测试结果与日志。