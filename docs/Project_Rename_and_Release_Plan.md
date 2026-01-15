# 项目正式发布更名方案分析

本方案旨在为项目从 `OoiMRR` 更名为正式发布名称提供技术路径分析与建议。

## 方案对比

### 方案 A：在当前现有仓库上修改
*由于您提到历史记录不重要且希望仓库公开/私有分离，此方案不再作为首选建议。*

### 方案 B：直接重建并实施“双仓库”架构（推荐）
**适用场景**：希望从零开始建立正式的产品发布序列，并实现“免费公开版”与“专业私有版”的代码隔离。

---

## 推荐架构：Open Core (核心公开) 模式

建议将原本单一的 `OoiMRR` 项目重构为以下结构，以保证开发效率与底层隔离。

### 1. 仓库划分
| 仓库名 | 权限 | 内容 |
| :--- | :--- | :--- |
| **`ProductName-Core`** | **公有 (Public)** | 核心逻辑、UI 通用库、底层服务、免费版 App 入口。 |
| **`ProductName-Pro`** | **私有 (Private)** | 高级功能模块、授权验证逻辑、专业版 App 入口。 |

### 2. 本地开发目录结构 (建议使用 Git Submodule)
```text
/Projects/
  ├── ProductName-Core/ (独立克隆，用于维护开源版/免费版)
  │
  ├── ProductName-Pro/ (专业版主仓库 - 引用 Core)
  │     ├── .git/
  │     ├── external/
  │     │     └── core/ (Git Submodule -> Core 仓库)
  │     ├── src/
  │     │     └── ProductName.Pro.Features/ (专业版专有代码)
  │     └── ProductName.Pro.sln (链接 Core 与 Pro 的解决方案)
  │
  └── ProductName-Ultra/ (Ultra 版超级仓库 - 引用 Pro 和 Core)
        ├── .git/
        ├── external/
        │     ├── core/ (Git Submodule -> Core 仓库)
        │     └── pro/ (Git Submodule -> Pro 仓库)
        ├── src/
        │     └── ProductName.Ultra.Features/ (Ultra 专有黑科技代码)
        └── ProductName.Ultra.sln (全功能最终态解决方案)
```

> [!NOTE]
> 在 Ultra 仓库中，建议将 Core 和 Pro 并列作为子模块。这样在 `ProductName.Ultra.sln` 中，你可以自由地组织三方的工程引用关系。

### 3. 代码注入模式 (DI 为主)
利用您项目中已有的 `Microsoft.Extensions.DependencyInjection` 架构：
- **Core 版**：在 `App.xaml.cs` 中注册基础服务。
- **Pro 版**：继承或通过装饰器模式，在 DI 容器中注入额外的 Pro 服务或重写 Core 服务。

---

## 实施路线图 (不实施，仅规划)

### 第一步：代码重构与解耦
1.  **全局更名**：按照之前的分析，将所有 `OoiMRR` 替换为新名称（如 `FastExplorer`）。
2.  **模块拆分**：
    - 将不需要保密的代码移至 `Core` 项目。
    - 将涉及高级算法、商业授权或增值功能的代码移至 `Pro` 项目。

### 第二步：建立公有仓库
1. 在 GitHub 创建新的**公有仓库**。
2. 推送清理后的 `Core` 代码。
3. **发布免费版**：基于此仓库通过 GitHub Actions 或手动构建发布。

### 第三步：建立私有仓库
1. 创建新的**私有仓库**。
2. 将公有仓库添加为 `Submodule`。
3. **开发专业版**：在私有仓库中编写 Pro 功能，并引用 Submodule 中的 Core 工程。
4. **同步机制**：
    - 在 Pro 版中修复了 Core 的 Bug？在 Submodule 目录提交并推送到 Public。
    - 想要 Pro 版包含 Public 的最新更新？在 Submodule 目录执行 `git pull origin main`。

---

## 核心疑问深度解析

### 1. 纯数字版本号区分策略
如果发布渠道不支持字母（如 Windows 应用商店），建议使用四段式版本号（Major.Minor.Build.Revision）：
- **规则**：利用第四位（Revision）或第三位（Build）的**奇偶性**或**特定起始数字**。
- **方案示例**：
    - **Free 版**：`1.2.100.0`
    - **Pro 版**： `1.2.100.1` （末位 1 代表 Pro，0 代表 Free）
    - 或者：Free 版使用 `1.2.x`，Pro 版使用 `1.3.x`（次版本号奇偶区分）。

### 2. AIDE 在双仓库中的角色与协作
AIDE 本质上是根据您**当前打开的文件上下文**进行工作的：
- **自动区分**：只要代码在物理文件夹上是分开的（如 `external/core` 与 `src/pro`），AIDE 编写代码时会自动引用正确的路径。
- **推送逻辑**：AIDE 无法“自动”决定推送到哪个仓库。您需要在 Git 终端中切换到对应的子模块目录或主仓库目录进行推送。
- **如何告诉 AIDE**：您只需在提示词中提到“在 Pro 模块中实现这个功能”或“修改核心底层逻辑”，AIDE 就会定位到对应的物理文件。

### 3. 工作区配置建议
**建议：同时加入工作区（推荐使用 Pro 仓库作为根路径）。**
- 如果您使用了 Submodule 模式，打开 `ProductName-Pro` 文件夹时，由于 `Core` 在其 `external/` 目录下，您实际上已经同时打开了两个仓库。
- 在 AIDE 中，这样它可以跨仓库进行联想（比如 Pro 的类继承 Core 的基类），跨仓库重构也会非常方便。

### 4. 商店发布策略建议
考虑到您已经决定“代码物理隔离”，这通常对应**两个独立的 App** 发布方案：
- **方案 A：独立双 App（目前偏向的选择）**
    - 商店里有两个条目：`FastExplorer` (Free) 和 `FastExplorer Pro` (收费)。
    - **优点**：逻辑简单，无需在代码里写复杂的“购买/验证解锁”逻辑。
- **方案 B：单 App + 重量级插件（更高级）**
    - 只发布一个 App。Pro 版的功能代码编译成独立 DLL，用户购买后，程序自动从云端下载该 DLL 并加载。
    - **建议**：初期建议走**方案 A**，维护成本最低。

### 5. 初始迁移操作
**是的，直接复制即可。**
- **操作步骤**：
    1. 新建 `ProductName-Core` 文件夹。
    2. 将现有 `OoiMRR` 代码全部复制进去。
    3. 执行全局重命名（`OoiMRR` -> `NewName`）。
    4. 删掉任何您想保留在 Pro 版里的“未成品”或“高级构思”代码。
    5. 这个干净的版本即作为您的第一个公有仓库提交。

---

## 未来扩展：层级化版本堆栈 (Ultra 版本)

这套系统采用了**“洋葱模型”**设计，具有极强的扩展性。如果以后要增加比 Pro 更高级的 **Ultra 版**，操作非常丝滑：

### 1. 递归依赖结构
您可以建立一个全新的 `ProductName-Ultra` 私有仓库，其结构如下：
- **Ultra 仓库**（主控）
    - 📁 `external/pro/` （作为子模块引入 **Pro 仓库**）
    - 📁 `external/core/` （由于 Pro 引用了 Core，这里会自动带入）
    - 📁 `src/ultra/` （存放 Ultra 专有的黑科技代码）

### 2. 核心优势
- **代码重用**：Ultra 自动继承 Pro 的所有改良，Pro 自动继承 Core 的所有更新。
- **DI 深度注入**：在 Ultra 的启动代码中，您可以轻松地用 `UltraSearchService` 替换掉 `ProSearchService`。
- **发布隔离**：商店里只需增加一个 `Ultra` 条目，而对于 Free 或 Pro 用户的包体积完全没有负担。

### 3. 操作总结
这种系统就像积木，您可以无限向上堆叠。
- **Free**：只有底座（Core）。
- **Pro**：底座 + 第一层插件（Pro）。
- **Ultra**：底座 + 第一层插件 + 第二层高级插件（Ultra）。

这也是您现在的规划不仅解决了当下的问题，还为未来 3-5 年的商业化路径打好了地基。
