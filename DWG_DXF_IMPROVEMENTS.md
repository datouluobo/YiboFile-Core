# DWG/DXF 功能改进完成

## 已完成的三个改进

### 1. ✅ 静默格式转换
**问题**：ODA File Converter 转换时会弹出窗口，影响用户体验

**解决方案**：
```csharp
var processStartInfo = new ProcessStartInfo
{
    FileName = odaExe,
    Arguments = arguments,
    CreateNoWindow = true,              // 不创建窗口
    UseShellExecute = false,            // 不使用系统外壳
    WindowStyle = ProcessWindowStyle.Hidden,  // 隐藏窗口（静默运行）
    RedirectStandardOutput = true,
    RedirectStandardError = true
};
```

**效果**：
- ODA 转换过程完全在后台进行
- 用户界面不会被打断
- 转换过程静默无感

### 2. ✅ 改进错误处理
**问题**：某些 DWG 文件转换失败，显示 "Value does not fall within the expected range"

**解决方案**：
1. **捕获并记录错误**：
   ```csharp
   var output = await process.StandardOutput.ReadToEndAsync();
   var error = await process.StandardError.ReadToEndAsync();
   
   if (!string.IsNullOrEmpty(error))
   {
       Debug.WriteLine($"ODA Converter Error: {error}");
   }
   ```

2. **更友好的错误提示**：
   ```csharp
   throw new InvalidOperationException("DWG转换失败。请确保已正确安装 ODA File Converter。");
   ```

3. **检查进程启动**：
   ```csharp
   using var process = Process.Start(processStartInfo);
   if (process == null)
       return false;
   ```

**效果**：
- 转换失败时提供清晰的错误信息
- 帮助用户诊断问题
- 记录详细的调试信息

### 3. ✅ 添加"DWG转DXF"按钮
**问题**：用户希望将转换后的 DXF 文件保存到当前目录

**解决方案**：
1. **按钮位置**：在"外部程序打开"按钮左边
2. **仅对 DWG 文件显示**：DXF 文件不显示此按钮
3. **命名逻辑**：参考 doc 转 docx 的逻辑

**实现代码**：
```csharp
// 如果是 DWG 文件，添加"DWG转DXF"按钮
if (ext == ".dwg")
{
    var convertButton = new Button
    {
        Content = "🔄 DWG转DXF",
        Padding = new Thickness(12, 6, 12, 6),
        Margin = new Thickness(0, 0, 8, 0),
        Cursor = System.Windows.Input.Cursors.Hand
    };
    convertButton.Click += (s, e) => ConvertDwgToDxf(filePath);
    buttons.Add(convertButton);
}
```

**转换逻辑**：
1. 检查缓存中是否已有转换后的 DXF
2. 如果没有，询问用户是否转换
3. 转换完成后，复制到当前目录
4. 如果文件名冲突，自动添加序号（如 `文件_1.dxf`）
5. 询问用户是否在资源管理器中显示

**命名规则**：
```
原文件: CAD 文件.dwg
目标文件: CAD 文件.dxf

如果已存在:
CAD 文件_1.dxf
CAD 文件_2.dxf
...
```

## 使用说明

### 静默转换
- 打开 DWG 文件时，转换过程自动在后台进行
- 不会弹出任何 ODA 窗口
- 转换完成后直接显示预览

### DWG转DXF 功能
1. 打开一个 DWG 文件
2. 点击标题栏的"🔄 DWG转DXF"按钮
3. 如果缓存中没有，会询问是否转换
4. 转换完成后，DXF 文件保存到 DWG 文件所在目录
5. 可选择在资源管理器中显示文件

### 错误处理
- 如果转换失败，会显示友好的错误提示
- 调试信息会记录到 Debug 输出
- 建议检查 ODA File Converter 是否正确安装

## 技术细节

### 静默运行的关键参数
```csharp
CreateNoWindow = true,              // 不创建控制台窗口
UseShellExecute = false,            // 直接启动进程
WindowStyle = ProcessWindowStyle.Hidden,  // 隐藏所有窗口
```

### 文件命名冲突处理
```csharp
int counter = 1;
while (File.Exists(targetPath))
{
    targetFileName = $"{baseFileName}_{counter}.dxf";
    targetPath = Path.Combine(sourceDir, targetFileName);
    counter++;
}
```

### 缓存机制
- 转换后的 DXF 文件缓存在：
  ```
  %AppData%\OoiMRR\Cache\DWGtoDXF\
  ```
- 缓存文件名包含原文件路径和修改时间的哈希值
- 7天后自动清理

## 测试建议

### 测试静默转换
1. 打开一个 DWG 文件
2. 观察是否有 ODA 窗口弹出
3. 预期：完全静默，直接显示预览

### 测试 DWG转DXF
1. 打开一个 DWG 文件
2. 点击"🔄 DWG转DXF"按钮
3. 确认转换
4. 检查 DXF 文件是否生成在正确位置
5. 再次点击按钮，检查文件名是否正确递增

### 测试错误处理
1. 打开一个损坏的 DWG 文件
2. 观察错误提示是否友好
3. 检查 Debug 输出是否有详细信息

## 已知问题和限制

1. **ODA 版本兼容性**：某些特别旧或特别新的 DWG 文件可能无法转换
2. **文件损坏**：损坏的 DWG 文件会转换失败
3. **大文件**：非常大的 DWG 文件转换可能需要较长时间

## 编译状态

✅ 编译成功（8 个警告，都是原有的）  
✅ 静默转换已实现  
✅ DWG转DXF 按钮已添加  
✅ 错误处理已改进  

所有功能已完成并可以使用！🎉
