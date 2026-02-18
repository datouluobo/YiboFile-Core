# YiboFile 已知 Bug 清单

> **更新日期**: 2026-02-19 | **版本**: v1.0.1530

---

## 📌 待修复 (Open)

| ID | 模块 | 描述 | 根因分析 | 修复策略 |
|----|------|------|----------|----------|
| **BUG-001** | AddressBar | 副地址栏库标识错误（显示 `path` 而非 `lib`） | `AddressBarControl` 未正确绑定到 `PaneViewModel.NavigationMode` | 检查 SecondFileBrowser 的 DataContext 绑定 |
| **BUG-003** | Library | 副面板库路径识别失败 | `FileOperationModule` 未正确解析 `lib://` 协议 | 在 Module 中增加协议解析逻辑 |

---

## ✅ 已修复 (Closed)

| ID | 模块 | 描述 | 修复版本 | 修复说明 |
|----|------|------|----------|----------|
| **BUG-002** | Pane | 副面板刷新与操作混乱 | v1.0.1509 | 更新 FileOperationHandler 使用 ActivePane |
| **BUG-007** | Sorting | 文件名排序导致列表变空 | v1.0.1509 | 启用 CollectionSynchronization |
| **BUG-008** | Header | 列头点击误触发双击响应 | — | 在 `GridViewColumnHeader_Click` 中设置 `e.Handled = true` |
| **BUG-011** | Clipboard | 剪切板管理器体验差 | v1.0.1509 | 重新设计 UI，现代列表 + 搜索 + 预览 |
| **BUG-012** | FileOps | 工具栏按钮与快捷键部分失效 | v1.0.1503 | 检查 `PaneCommandSet` 与 `InputBindings` 连接 |
| **BUG-013** | Performance | 双栏模式选中文件卡顿 | v1.0.1504 | 确保 `PreviewService` 异步执行并增加防抖 |
| **BUG-015** | FileList | 文件操作后列表不自动刷新 | v1.0.1502 | 检查消息发布与订阅链路 |
| **BUG-018** | UI/Init | 启动时主副文件信息区空白，或切换文件夹后显示错误信息 | v1.1.0 | 在 PaneViewModel 中清理库/标签上下文；回滚路径回退逻辑 |
| **BUG-019** | Navigation | "快速访问"双栏同时切换 | v1.1.0 | 统一使用 `PreviewMouseDown` 并修复事件路由 |
| **BUG-021** | Library | 程序启动时死循环刷屏 (LibraryListChangedMessage) | v1.0.1506 | 改为 GetAllLibraries 并不再发布消息 |
| **BUG-022** | FileList | 文件列表显示视图模式切换有问题 | v1.0.1530 | 完善 PaneViewModel 持久化与 ViewModeHelper 刷新逻辑 |
| **BUG-014** | Window | 窗口状态持久化问题：标签页无法记忆 | v1.0.1530+ | 重构 `SaveAllState` 为事务性更新，增加空列表防护机制 |
| **BUG-023** | Window | 标签页持久化系列的自赋值与引用问题 | v1.0.1530+ | 重写 `WindowStateManager`，使用 `ConfigurationService.Update` 和 `Save...To` 模式消除不安全访问 |
