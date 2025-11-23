## 目标
- 完善“搜索: 关键词”标签页，确保稳定打开与完整搜索能力。
- 在修复 Everything 之前，提供高性能本地索引与搜索，达到接近 Everything 的体验。

## 架构概览
- **UI层**：复用 `FileBrowserControl`，在其地址栏右侧增加“搜索工具栏”，在标签区使用 `Path=search://{keyword}` 呈现。
- **搜索服务层**：新增 `SearchService`（后台托管），包含：
  - **Indexer**：文件系统索引构建与增量更新（Windows 优先）。
  - **QueryEngine**：解析查询、执行过滤、排名与结果分页。
- **存储层**：SQLite 数据库（已有 `DatabaseManager`），新增索引库 `SearchIndex.db`，启用 FTS5 表支持 BM25 与高亮。

## UI与交互
- 在 `FileBrowserControl` 顶部增加搜索工具栏：
  - 关键词输入（沿用地址栏），右侧新增选项：`大小写`/`整词`/`正则`/`路径限定`/`类型筛选`/`时间范围`/`大小范围`。
  - 视图上方显示：`结果计数`、`耗时`、`索引状态（已更新/正在更新）`。
- 标签页逻辑（已具备）：
  - 创建或切换 `search://{关键词}` 标签页，面包屑显示“搜索: 关键词”。参考 `MainWindow.xaml.cs:7399-7435`。

## 索引设计（接近 Everything 性能）
- **数据模型**（SQLite）：
  - `files(path PRIMARY KEY, name, ext, parent, is_dir, size, mtime, ctime, atime, volume, inode)`。
  - `files_fts(name, path, ext, content='') USING FTS5`（用于快速模糊/前缀/整词搜索，启用 `tokenize=unicode61`）。
  - `meta(k PRIMARY KEY, v)`（索引状态、版本、上次全量时间、USN 日志位置等）。
- **初次构建**：多线程扫描已就绪固定盘符；按卷分片并行，批量写入（事务+批量提交）。
- **增量更新（Windows/NTFS）**：
  - 使用 USN Journal 读取变更（`FSCTL_QUERY_USN_JOURNAL` / `FSCTL_READ_USN_JOURNAL`），在可用时实现近实时更新。
  - 非 NTFS/网络盘回退为轮询变更（`FileSystemWatcher` + 批量重扫目录）。
- **性能策略**：
  - 批量插入（每批 1000-5000），关闭同步写延迟（`PRAGMA synchronous=NORMAL`），构建阶段可用 `wal` 模式。
  - 轻量字段索引（`name`, `ext`, `parent`）+ FTS5；避免在大字段上加多余索引。
  - 统一路径规范化（大小写、分隔符、尾部反斜杠），长路径支持（`\?\` 前缀）。

## 搜索算法
- **查询解析**：
  - 模式：`简单`（默认，关键字分词）、`整词`、`大小写`、`正则`（限定在 `name` 与 `path`）。
  - 路径限定：`path:xxx` 前缀或 UI 路径范围选择（卷、目录）。
- **执行**：
  - `name` 走 FTS5（支持 BM25 排序、高亮），`path` 限定通过前缀匹配（索引：`parent`/`path` 前缀列）。
  - 过滤器：类型（文件/文件夹/扩展名）、日期范围（`mtime`）、大小范围。
  - 排序：名称、路径、修改时间、大小、BM25 分数；支持二级排序。
- **并集与交集**：
  - 名称与备注（`DatabaseManager.FileNotes`）结果并集；切换模式时可选交集。
- **分页**：默认返回前 1000 条，支持翻页；UI 显示总数（快速计数）。

## 结果展示与筛选
- 列表展示沿用 `FileBrowserControl.FilesListView`：
  - 列：名称、大小、类型、修改日期、创建、标签、备注。
  - 顶部筛选条：扩展名下拉、多选标签、日期/大小范围、是否仅文件/仅目录。
  - 快速操作：打开文件/所在文件夹、复制路径、添加到库/收藏。

## 搜索历史与建议
- 历史表 `search_history(id, keyword, options_json, created_at, count, duration_ms)`（SQLite）。
- 最近搜索下拉；常用建议基于出现频次与最近度；支持固定常用过滤器（如 `ext:pdf`、`path:Downloads`）。

## 兼容性
- **Windows（首要）**：WPF UI，NTFS 优先 USN 增量；非 NTFS 使用轮询/Watcher。
- **未来扩展**：抽象 `IIndexer` 与 `IQueryEngine` 接口；Linux/macOS 可替换实现（文件系统监听+递归扫描），UI 保持一致。

## 测试与基准
- **准确性**：
  - 中英文、Unicode、Emoji、长路径（>260）、大小写/整词/正则、扩展名、路径限定。
- **性能**：
  - 首次索引时长、增量更新延迟、查询耗时（目标：常见关键字 <50ms，分页 <100ms）。
- **边界**：
  - 大量小文件、超大目录、权限受限、网络盘、磁盘满、数据库锁。
- **体验**：
  - 进度反馈、取消、占用控制（后台节流）、无结果提示与建议。

## 与现有代码衔接
- 标签页创建/切换：`MainWindow.xaml.cs:7399-7435` 保持当前协议 `search://{keyword}`。
- 备注搜索复用：`DatabaseManager.SearchFilesByNotes(...)`。
- 新增服务：`Services/SearchService`（后台线程、取消令牌），`Services/IndexBuilder`（首建与增量），`Services/QueryEngine`（SQL/FTS查询封装）。

## 里程碑
1. UI工具栏与标签页稳定打开（结果渲染、计数/耗时）
2. 首次索引（多盘并行，FTS5启用）
3. 查询引擎（过滤器、排序、分页、高亮）
4. 增量更新（USN/回退方案）
5. 历史与建议（SQLite表+UI下拉）
6. 测试与基准（准确/性能/边界/体验）

## 风险与权衡
- USN 集成需要管理员权限与 NTFS 限制；提供无 USN 的回退方案。
- 目标“与 Everything 相当”的性能需逐步迭代：先达成“可用与稳定”，再优化到“高速”。

请确认该计划；确认后我将开始实现第1与第2里程碑（UI工具栏、索引库与首次索引），并持续交付测试结果与性能数据。