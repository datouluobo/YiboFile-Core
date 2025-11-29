# ODA File Converter 集成完成

## 已完成的改进

### 1. ✅ 自动创建 Dependencies\ODAFileConverter\ 目录
- 在项目文件中添加了构建后任务
- 编译后自动创建空目录
- 如果用户安装了 ODA，文件会自动复制到输出目录

### 2. ✅ 修复 ODA File Converter 调用问题
根据 ODA 的命令行格式要求，完全重写了调用逻辑。

## ODA File Converter 命令行格式

根据您提供的截图，ODA File Converter 的正确格式为：

```
ODAFileConverter.exe "输入文件夹" "输出文件夹" "输出版本" "输出格式" "递归" "审计"
```

### 参数说明

| 参数 | 说明 | 可选值 |
|------|------|--------|
| 输入文件夹 | 包含 DWG 文件的文件夹路径（带引号） | 任意有效路径 |
| 输出文件夹 | 转换后文件的输出路径（带引号） | 任意有效路径 |
| 输出版本 | AutoCAD 版本 | ACAD9, ACAD10, ACAD12, ACAD13, ACAD14, ACAD2000, ACAD2004, ACAD2007, ACAD2010, ACAD2013, ACAD2018 |
| 输出格式 | 文件格式 | DWG, DXF, DXB |
| 递归 | 是否递归处理子文件夹 | 0 (否), 1 (是) |
| 审计 | 是否审计文件 | 0 (否), 1 (是) |

### 我们使用的参数

```
"临时输入目录" "临时输出目录" ACAD2018 DXF 0 1
```

- **ACAD2018**: 使用较新的 AutoCAD 2018 格式
- **DXF**: 输出为 DXF 格式（用于 SVG 渲染）
- **0**: 不递归（只转换指定文件）
- **1**: 启用审计（确保文件完整性）

## 实现细节

### 使用临时目录的原因

ODA File Converter 要求：
1. 输入必须是**文件夹**，而不是单个文件
2. 输出也必须是**文件夹**
3. 它会转换输入文件夹中的所有 DWG 文件

因此我们的实现：
```csharp
// 1. 创建临时输入目录
var tempInputDir = Path.Combine(Path.GetTempPath(), "ODA_Input_" + Guid.NewGuid());
Directory.CreateDirectory(tempInputDir);

// 2. 复制 DWG 文件到临时输入目录
var tempDwgPath = Path.Combine(tempInputDir, Path.GetFileName(dwgFilePath));
File.Copy(dwgFilePath, tempDwgPath, true);

// 3. 创建临时输出目录
var tempOutputDir = Path.Combine(Path.GetTempPath(), "ODA_Output_" + Guid.NewGuid());
Directory.CreateDirectory(tempOutputDir);

// 4. 调用 ODA
var arguments = $"\"{tempInputDir}\" \"{tempOutputDir}\" ACAD2018 DXF 0 1";
Process.Start(odaExe, arguments);

// 5. 从输出目录复制 DXF 文件到缓存
var outputDxfPath = Path.Combine(tempOutputDir, Path.GetFileNameWithoutExtension(dwgFilePath) + ".dxf");
File.Copy(outputDxfPath, dxfPath, true);

// 6. 清理临时目录
Directory.Delete(tempInputDir, true);
Directory.Delete(tempOutputDir, true);
```

## 安装 ODA File Converter

### 方法 1：从官网下载（推荐）

1. 访问：https://www.opendesign.com/guestfiles/oda_file_converter
2. 下载 Windows 版本（EXE 安装程序）
3. 运行安装程序，安装到默认位置或自定义位置
4. 将安装目录中的所有文件复制到：
   ```
   <程序目录>\Dependencies\ODAFileConverter\
   ```

### 方法 2：直接安装到程序目录

1. 下载 ODA File Converter
2. 运行安装程序时，选择安装路径为：
   ```
   <程序目录>\Dependencies\ODAFileConverter\
   ```

### 需要复制的文件

确保以下文件在 `Dependencies\ODAFileConverter\` 目录中：
- `ODAFileConverter.exe` (主程序)
- 所有 DLL 文件
- 配置文件

## 测试步骤

1. **安装 ODA File Converter**（按上述方法）

2. **验证安装**：
   ```
   检查文件是否存在：
   <程序目录>\Dependencies\ODAFileConverter\ODAFileConverter.exe
   ```

3. **测试转换**：
   - 打开一个 DWG 文件
   - 应该能看到正常的 SVG 预览
   - 不再显示下载引导页面

4. **检查缓存**：
   ```
   转换后的 DXF 文件会缓存在：
   %AppData%\OoiMRR\Cache\DWGtoDXF\
   ```

## 故障排除

### 问题：仍然显示下载页面
**原因**：ODAFileConverter.exe 不在正确位置
**解决**：检查文件路径，确保 exe 文件存在

### 问题：转换失败
**原因**：ODA 缺少依赖文件
**解决**：复制完整的 ODA 安装目录，不要只复制 exe

### 问题：弹出命令行格式对话框
**原因**：命令行参数不正确（已修复）
**解决**：更新到最新代码

## 项目文件更新

### OoiMRR.csproj

添加了以下内容：

```xml
<!-- 自动复制 Dependencies\ODAFileConverter 文件夹到输出目录（如果存在） -->
<ItemGroup>
  <None Include="Dependencies\ODAFileConverter\**\*" CopyToOutputDirectory="PreserveNewest" Condition="Exists('Dependencies\ODAFileConverter')" />
</ItemGroup>

<!-- 创建空的 ODAFileConverter 目录 -->
<Target Name="CreateODADirectory" AfterTargets="Build">
  <MakeDir Directories="$(OutputPath)Dependencies\ODAFileConverter" Condition="!Exists('$(OutputPath)Dependencies\ODAFileConverter')" />
</Target>
```

这确保：
- 如果源代码中有 ODA 文件，会自动复制到输出目录
- 如果没有，会创建空目录供用户安装

## 完成状态

✅ 编译成功  
✅ 自动创建目录  
✅ 修复命令行参数  
✅ 使用临时目录处理  
✅ 自动清理临时文件  
✅ 缓存转换结果  

现在可以正常使用 DWG 预览功能了！
