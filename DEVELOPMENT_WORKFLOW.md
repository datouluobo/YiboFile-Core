# 🔄 OoiMRR 开发工作流程

## 📋 目录

1. [日常开发流程](#日常开发流程)
2. [提交代码规范](#提交代码规范)
3. [版本管理](#版本管理)
4. [常用命令](#常用命令)
5. [团队协作](#团队协作)

---

## 🚀 日常开发流程

### 1. 开始工作前

```powershell
# 拉取最新代码
git pull origin main

# 查看当前状态
git status
```

### 2. 进行开发

- 修改代码
- 添加新功能
- 修复 Bug
- 更新文档

### 3. 提交代码

```powershell
# 查看修改的文件
git status

# 添加修改的文件
git add .
# 或者添加特定文件
git add MainWindow.xaml.cs

# 提交修改（使用规范的提交信息）
git commit -m "feat: 添加 Excel 预览功能"

# 推送到 GitHub
git push
```

### 4. 验证推送

访问 GitHub 仓库确认代码已更新：
```
https://github.com/datouluobo/OoiMRR
```

---

## 📝 提交代码规范

### 提交信息格式

```
<类型>: <简短描述>

[可选的详细描述]

[可选的关联 Issue]
```

### 常用类型

| 类型 | 说明 | 示例 |
|------|------|------|
| `feat` | 新功能 | `feat: 添加 PowerPoint 预览支持` |
| `fix` | Bug 修复 | `fix: 修复文件列表刷新问题` |
| `docs` | 文档更新 | `docs: 更新 README 使用说明` |
| `style` | 代码格式调整 | `style: 统一代码缩进格式` |
| `refactor` | 代码重构 | `refactor: 重构预览模块架构` |
| `perf` | 性能优化 | `perf: 优化大文件加载速度` |
| `test` | 测试相关 | `test: 添加文件操作单元测试` |
| `chore` | 构建/工具变动 | `chore: 更新依赖包版本` |

### 提交示例

```powershell
# 添加新功能
git commit -m "feat: 添加 Excel 多工作表切换功能"

# 修复 Bug
git commit -m "fix: 修复视频预览控制按钮不显示的问题"

# 更新文档
git commit -m "docs: 添加 Excel 预览功能说明"

# 性能优化
git commit -m "perf: 优化图片预览加载速度"
```

---

## 🏷️ 版本管理

### 版本号规则

使用语义化版本：`主版本.次版本.修订号`

- **主版本**：重大功能变更或不兼容的 API 修改
- **次版本**：新增功能，向下兼容
- **修订号**：Bug 修复，向下兼容

### 创建版本标签

```powershell
# 创建标签
git tag -a v1.1.0 -m "Release v1.1.0: 添加 Excel 和 PPT 预览"

# 推送标签到 GitHub
git push origin v1.1.0

# 推送所有标签
git push origin --tags
```

### 查看版本历史

```powershell
# 查看所有标签
git tag

# 查看特定标签信息
git show v1.1.0

# 查看提交历史
git log --oneline
```

---

## 💻 常用命令

### 查看状态

```powershell
# 查看工作区状态
git status

# 查看修改内容
git diff

# 查看提交历史
git log --oneline -10

# 查看远程仓库
git remote -v
```

### 撤销操作

```powershell
# 撤销工作区的修改（未 add）
git checkout -- <文件名>

# 撤销暂存区的文件（已 add，未 commit）
git reset HEAD <文件名>

# 撤销最后一次提交（保留修改）
git reset --soft HEAD^

# 撤销最后一次提交（不保留修改）
git reset --hard HEAD^
```

### 分支管理

```powershell
# 查看所有分支
git branch -a

# 创建新分支
git branch feature-excel-preview

# 切换分支
git checkout feature-excel-preview

# 创建并切换到新分支
git checkout -b feature-ppt-preview

# 合并分支
git checkout main
git merge feature-excel-preview

# 删除本地分支
git branch -d feature-excel-preview

# 删除远程分支
git push origin --delete feature-excel-preview
```

---

## 👥 团队协作

### 克隆仓库（新成员）

```powershell
# 克隆仓库
git clone https://github.com/datouluobo/OoiMRR.git

# 进入项目目录
cd OoiMRR

# 安装依赖并构建
dotnet restore
dotnet build
```

### 功能分支开发流程

```powershell
# 1. 更新主分支
git checkout main
git pull origin main

# 2. 创建功能分支
git checkout -b feature-excel-preview

# 3. 开发功能
# ... 编写代码 ...

# 4. 提交代码
git add .
git commit -m "feat: 添加 Excel 预览功能"

# 5. 推送功能分支
git push origin feature-excel-preview

# 6. 在 GitHub 上创建 Pull Request

# 7. 代码审查通过后，合并到主分支
git checkout main
git pull origin main
git merge feature-excel-preview

# 8. 推送主分支
git push origin main

# 9. 删除功能分支
git branch -d feature-excel-preview
git push origin --delete feature-excel-preview
```

### 解决冲突

```powershell
# 1. 拉取最新代码时发现冲突
git pull origin main

# 2. 查看冲突文件
git status

# 3. 手动编辑冲突文件，解决冲突标记
# <<<<<<< HEAD
# 你的修改
# =======
# 其他人的修改
# >>>>>>> branch-name

# 4. 标记冲突已解决
git add <冲突文件>

# 5. 完成合并
git commit -m "merge: 解决与主分支的冲突"

# 6. 推送
git push
```

---

## 🔧 开发环境配置

### 必需软件

- ✅ .NET 8.0 SDK
- ✅ Visual Studio 2022 或 VS Code
- ✅ Git
- ✅ WebView2 Runtime

### 推荐工具

- **GitHub Desktop** - 图形化 Git 工具
- **GitKraken** - 可视化 Git 客户端
- **VS Code Extensions**:
  - GitLens
  - C# Dev Kit
  - XAML Styler

---

## 📊 项目维护

### 定期任务

**每周**：
- 检查并更新依赖包
- 审查待处理的 Issue
- 更新文档

**每月**：
- 发布新版本
- 更新 CHANGELOG.md
- 备份重要数据

**每季度**：
- 代码质量审查
- 性能优化
- 安全检查

### 更新依赖包

```powershell
# 查看过时的包
dotnet list package --outdated

# 更新特定包
dotnet add package Microsoft.Web.WebView2 --version 最新版本

# 更新所有包
dotnet restore
```

---

## 🐛 Bug 修复流程

1. **创建 Issue**（在 GitHub 上）
   - 描述问题
   - 添加复现步骤
   - 附加截图或日志

2. **创建修复分支**
   ```powershell
   git checkout -b fix-issue-123
   ```

3. **修复 Bug**
   - 定位问题
   - 编写修复代码
   - 测试验证

4. **提交修复**
   ```powershell
   git commit -m "fix: 修复文件列表刷新问题 (closes #123)"
   ```

5. **推送并创建 PR**
   ```powershell
   git push origin fix-issue-123
   ```

---

## 📚 参考资源

### Git 学习资源

- [Git 官方文档](https://git-scm.com/doc)
- [GitHub 官方指南](https://docs.github.com/)
- [Git 速查表](https://training.github.com/downloads/zh_CN/github-git-cheat-sheet/)

### 项目文档

- `README.md` - 项目介绍
- `CHANGELOG.md` - 更新日志
- `GITHUB_SETUP.md` - GitHub 设置
- `PUSH_NOW.md` - 推送指南

---

## 💡 最佳实践

### ✅ 推荐做法

- 频繁提交，小步快跑
- 写清晰的提交信息
- 推送前先拉取最新代码
- 使用分支开发新功能
- 定期备份重要数据

### ❌ 避免做法

- 不要提交大文件（>100MB）
- 不要提交敏感信息（密码、密钥）
- 不要直接在 main 分支开发
- 不要使用 `git push --force`（除非确定）
- 不要提交编译产物（bin、obj）

---

## 🎯 快速参考

### 日常开发三步走

```powershell
# 1. 拉取最新代码
git pull

# 2. 提交修改
git add .
git commit -m "feat: 添加新功能"

# 3. 推送到 GitHub
git push
```

### 遇到问题？

```powershell
# 查看帮助
git help

# 查看特定命令帮助
git help commit

# 查看状态
git status

# 查看日志
git log --oneline
```

---

**祝开发顺利！** 🚀

如有问题，请参考项目文档或创建 Issue。

