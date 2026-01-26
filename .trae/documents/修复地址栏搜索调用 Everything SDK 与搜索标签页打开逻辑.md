## 现象与证据
- SDK 加载日志显示 `Everything_Query` 未找到，导致初始化失败并回退传统搜索：Services/EverythingHelper.cs:116-151；运行日志显示 “函数 Everything_Query 地址: 0 … 关键函数加载失败”。
- 点击地址栏搜索按钮后未打开新标签页，调试日志出现多次 `TaskCanceledException`，且备注搜索失败时直接 `return`，中断后续创建标签页：MainWindow.xaml.cs:7307-7324。

## 根因
1. SDK 绑定函数名不完整：字符串相关函数已修复为 W/A 变体，但 `Everything_Query` 仍只尝试无后缀名，未尝试 `Everything_QueryW/Everything_QueryA`，导致关键函数缺失，搜索无法走 SDK。
2. 搜索流程健壮性不足：备注搜索异常会提前 `return`，即使文件名搜索已成功也不会创建搜索标签页。
3. 标签区可见性与提示：搜索时未显式确保标签区可见，面包屑/地址栏未显示“搜索: 关键词”，体验上看似没有打开新标签页。

## 修复方案
- SDK 函数解析：
  - 为 `Everything_Query` 增加候选导出名：依次尝试 `"Everything_QueryW"`、`"Everything_QueryA"`、`"Everything_Query"`。
  - 保持现有 SetSearch/GetResultFullPathName 的 W/A 变体解析；关键函数缺失时返回 `false`，不继续运行。
  - `IsEverythingRunning` 若发现未加载则尝试重新加载 DLL；缓冲区从 `260` 提升至 `4096` 防止长路径截断（EverythingHelper.cs:399-401）。
- 搜索流程健壮性：
  - 备注搜索失败时不再 `return`，仅记录错误并继续用名称搜索结果创建标签页（MainWindow.xaml.cs:7319-7324）。
  - 合并结果集保持并集逻辑；即使结果为空也创建标签页。
- 标签页与提示：
  - 执行搜索前强制显示标签区（`FileBrowser.TabsVisible = true`），并在面包屑显示 `搜索: 关键词`（MainWindow.xaml.cs:7399-7435）。

## 具体改动
- Services/EverythingHelper.cs：
  - `functions` 数组为 `Everything_Query` 增加 W/A 变体；绑定委托时根据实际命中名称分派（116-199）。
  - 关键函数校验包含 `_GetResultFullPathName` 与 `_Query`，失败返回 `false`（210-218）。
  - `IsEverythingRunning` 在未加载时触发重新加载（338-358）；`StringBuilder(4096)` 防止路径截断（399-401）。
- MainWindow.xaml.cs：
  - `PerformSearch` 中备注搜索异常改为仅提示，不提前返回；始终进入标签页创建逻辑（7307-7435）。
  - 搜索标签页创建/切换后更新文件列表与面包屑文本；确保标签区可见（7399-7435）。

## 验证步骤
- 构建并运行，观察 SDK 日志应出现 `Everything_QueryW` 或 `Everything_QueryA` 委托创建成功；`GetVersion()` 正常返回版本号。
- 在地址栏输入非路径关键词，点击搜索：
  - 无论备注搜索是否异常，均创建或切换到 `search://{关键词}` 标签页；标签区可见，面包屑显示 `搜索: 关键词`，文件列表显示结果或空状态。
- 手动停止 Everything 进程再搜索，确认回退搜索仍创建标签页且不崩溃。

## 兼容性与风险
- SDK 解析按 W/A/无后缀三重回退，兼容不同版本导出名；仅在关键函数缺失时失败，避免半功能状态。
- 备注搜索异常不拦截流程，提升鲁棒性；不影响原有并集语义。

请确认上述方案，我将按计划实施修复并提交变更。