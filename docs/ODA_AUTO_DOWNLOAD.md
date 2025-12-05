# ODA File Converter 自动下载功能说明

## 功能概述

当用户尝试预览 DWG 文件时，如果系统未安装 ODA File Converter，程序会自动显示一个精美的引导界面，帮助用户完成安装。

## 工作流程

### 1. 检测阶段
- 用户打开 DWG 文件
- 程序检查 `Dependencies/ODAFileConverter/ODAFileConverter.exe` 是否存在
- 如果不存在，显示下载引导界面

### 2. 下载引导界面
界面包含以下内容：
- **文件信息**：显示当前 DWG 文件的名称、大小和类型
- **说明信息**：解释为什么需要 ODA File Converter
- **安装步骤**：详细的 5 步安装指南
- **操作按钮**：
  - 🌐 前往 ODA 官网下载
  - 🔄 我已安装，刷新预览

### 3. 安装步骤

用户需要按照以下步骤操作：

1. **访问 ODA 官网**
   - 点击"前往 ODA 官网下载"按钮
   - 网址：https://www.opendesign.com/guestfiles/oda_file_converter

2. **注册并下载**
   - 填写简单的注册信息（邮箱）
   - 下载 ODA File Converter for Windows

3. **解压安装**
   - 将下载的 ZIP 文件解压
   - 复制到程序目录：`Dependencies\ODAFileConverter\`
   - 确保 `ODAFileConverter.exe` 在该目录下

4. **刷新预览**
   - 点击"我已安装，刷新预览"按钮
   - 或重新打开 DWG 文件

## 技术实现

### 新增文件

#### `Services/OdaDownloader.cs`
提供 ODA File Converter 的管理功能：
- `IsInstalled()` - 检查是否已安装
- `GetInstallPath()` - 获取安装路径
- `OpenDownloadPage()` - 打开官网下载页面
- `InstallFromLocalFile()` - 从本地 ZIP 安装（预留功能）
- `Uninstall()` - 卸载功能

#### `Previews/CadPreview.cs` 更新
- 添加 `ShowOdaDownloadUI()` 方法
- 在 `InitializeAndRender()` 中检查 ODA 是否安装
- 使用精美的 HTML 界面展示引导信息

### 界面设计特点

1. **现代化设计**
   - 渐变背景（紫色系）
   - 圆角卡片设计
   - 阴影效果

2. **动画效果**
   - 图标弹跳动画
   - 按钮悬停效果

3. **清晰的信息层次**
   - 文件信息卡片
   - 黄色提示框
   - 蓝色步骤指南

4. **友好的交互**
   - 大按钮设计
   - 清晰的操作指引
   - 一键跳转官网

## 为什么不能直接下载？

ODA File Converter 的许可证要求：
1. 用户必须在 ODA 官网注册
2. 不允许第三方软件直接分发
3. 需要用户同意使用条款

因此，我们采用"引导式"安装，既合法合规，又提供良好的用户体验。

## 未来改进方向

1. **自动检测系统安装**
   - 检查用户是否已在系统其他位置安装 ODA
   - 自动创建符号链接

2. **离线安装包**
   - 提供预下载的 ODA 安装包（需要用户手动放置）
   - 一键解压安装

3. **进度显示**
   - 如果实现自动下载，显示下载进度
   - 解压进度提示

## 使用说明

### 对于用户
1. 打开任意 DWG 文件
2. 如果看到引导界面，点击"前往 ODA 官网下载"
3. 按照步骤完成安装
4. 刷新预览即可

### 对于开发者
```csharp
// 检查是否安装
if (OdaDownloader.IsInstalled())
{
    // 可以使用 DWG 转换功能
}
else
{
    // 显示下载引导
    ShowOdaDownloadUI(...);
}
```

## 相关文件

- `Services/OdaDownloader.cs` - ODA 管理服务
- `Services/DwgConverter.cs` - DWG 转 DXF 转换器
- `Previews/CadPreview.cs` - CAD 预览控件
- `Rendering/DxfSvgConverter.cs` - DXF 转 SVG 渲染器
