# 拖拽功能集成指南

## 📋 概述

`DragDropManager.cs` 提供了完整的文件拖拽功能，支持：
- 文件列表拖拽
- 拖拽到面包屑导航
- 拖拽到驱动器
- 拖拽到快速访问
- 拖拽到库（待开发）
- 拖拽到标签（待开发）

## 🎯 功能特性

### 支持的拖拽目标

| 目标类型 | 说明 | 默认操作 |
|---------|------|---------|
| 面包屑导航 | 拖拽到路径导航栏 | 移动 |
| 驱动器 | 拖拽到驱动器按钮 | 移动 |
| 快速访问 | 拖拽到快速访问区域 | 添加链接 |
| 库 | 拖拽到库（待开发） | 添加链接 |
| 标签 | 拖拽到标签（待开发） | 添加标签 |
| 文件夹 | 拖拽到文件列表中的文件夹 | 移动 |

### 支持的操作类型

| 操作 | 快捷键 | 说明 |
|------|--------|------|
| 移动 | 默认 | 移动文件/文件夹 |
| 复制 | Ctrl | 复制文件/文件夹 |
| 创建链接 | Ctrl+Shift | 创建快捷方式 |
| 添加到快速访问 | - | 自动（拖到快速访问） |
| 添加到库 | - | 自动（拖到库） |
| 添加标签 | - | 自动（拖到标签） |

## 🔧 在 MainWindow 中集成

### 1. 添加字段

在 `MainWindow.xaml.cs` 中添加：

```csharp
private DragDropManager _dragDropManager;
```

### 2. 初始化拖拽管理器

在 `MainWindow` 构造函数中：

```csharp
public MainWindow()
{
    InitializeComponent();
    
    // 初始化拖拽管理器
    _dragDropManager = new DragDropManager();
    
    // 订阅事件
    _dragDropManager.DragDropCompleted += DragDropManager_DragDropCompleted;
    _dragDropManager.DragDropStarted += DragDropManager_DragDropStarted;
    _dragDropManager.DragDropCancelled += DragDropManager_DragDropCancelled;
    
    // 初始化各个拖拽区域
    InitializeDragDrop();
}
```

### 3. 初始化拖拽区域

```csharp
private void InitializeDragDrop()
{
    // 1. 初始化文件列表拖拽
    _dragDropManager.InitializeFileListDragDrop(FilesListView);
    
    // 2. 初始化面包屑导航拖放
    if (BreadcrumbPanel != null)
    {
        _dragDropManager.InitializeBreadcrumbDrop(BreadcrumbPanel);
        // 设置当前路径到 Tag
        BreadcrumbPanel.Tag = _currentPath;
    }
    
    // 3. 初始化驱动器拖放
    if (DrivesPanel != null)
    {
        _dragDropManager.InitializeDrivesDrop(DrivesPanel);
    }
    
    // 4. 初始化快速访问拖放
    if (QuickAccessPanel != null)
    {
        _dragDropManager.InitializeQuickAccessDrop(QuickAccessPanel);
    }
}
```

### 4. 处理拖拽事件

```csharp
private void DragDropManager_DragDropStarted(object sender, DragDropManager.DragDropData e)
{
    // 拖拽开始时的处理
    // 例如：显示拖拽提示
    StatusText.Text = $"正在拖拽 {e.SourcePaths.Count} 个项目...";
}

private void DragDropManager_DragDropCompleted(object sender, DragDropManager.DragDropData e)
{
    try
    {
        // 执行拖拽操作
        bool success = _dragDropManager.ExecuteDragDropOperation(e);
        
        if (success)
        {
            // 操作成功，刷新界面
            LoadCurrentDirectory();
            
            // 显示成功消息
            string operationText = GetOperationText(e.Operation);
            StatusText.Text = $"{operationText} {e.SourcePaths.Count} 个项目到 {e.TargetPath}";
        }
    }
    catch (Exception ex)
    {
        MessageBox.Show($"拖拽操作失败: {ex.Message}", "错误", 
            MessageBoxButton.OK, MessageBoxImage.Error);
    }
}

private void DragDropManager_DragDropCancelled(object sender, EventArgs e)
{
    // 拖拽取消
    StatusText.Text = "拖拽已取消";
}

private string GetOperationText(DragDropManager.DragDropOperation operation)
{
    return operation switch
    {
        DragDropManager.DragDropOperation.Move => "移动",
        DragDropManager.DragDropOperation.Copy => "复制",
        DragDropManager.DragDropOperation.CreateLink => "创建链接",
        DragDropManager.DragDropOperation.AddToQuickAccess => "添加到快速访问",
        DragDropManager.DragDropOperation.AddToLibrary => "添加到库",
        DragDropManager.DragDropOperation.AddTag => "添加标签",
        _ => "操作"
    };
}
```

### 5. 更新路径时同步

当导航到新路径时，更新面包屑的 Tag：

```csharp
private void NavigateToPath(string path)
{
    _currentPath = path;
    
    // 更新面包屑导航的 Tag（用于拖拽目标）
    if (BreadcrumbPanel != null)
    {
        BreadcrumbPanel.Tag = _currentPath;
    }
    
    LoadFiles();
    UpdateBreadcrumb();
}
```

### 6. 为驱动器按钮设置路径

在创建驱动器按钮时：

```csharp
private void LoadDrives()
{
    DrivesPanel.Children.Clear();
    
    foreach (var drive in DriveInfo.GetDrives())
    {
        var button = new Button
        {
            Content = $"{drive.Name} {drive.VolumeLabel}",
            Tag = drive.Name, // 设置驱动器路径
            // ... 其他属性
        };
        
        button.Click += (s, e) => NavigateToPath(drive.Name);
        DrivesPanel.Children.Add(button);
    }
    
    // 重新初始化驱动器拖放
    _dragDropManager.InitializeDrivesDrop(DrivesPanel);
}
```

## 📝 XAML 修改

确保相关控件有正确的名称：

```xml
<!-- 面包屑导航 -->
<StackPanel Name="BreadcrumbPanel" Orientation="Horizontal" Margin="10,5">
    <!-- 面包屑内容 -->
</StackPanel>

<!-- 驱动器列表 -->
<StackPanel Name="DrivesPanel" Margin="4">
    <!-- 驱动器按钮 -->
</StackPanel>

<!-- 快速访问 -->
<StackPanel Name="QuickAccessPanel" Margin="2">
    <!-- 快速访问项 -->
</StackPanel>

<!-- 文件列表 -->
<ListView Name="FilesListView" 
          SelectionChanged="FilesListView_SelectionChanged"
          MouseDoubleClick="FilesListView_MouseDoubleClick"
          PreviewKeyDown="FilesListView_PreviewKeyDown">
    <!-- 列定义 -->
</ListView>
```

## 🎨 视觉反馈

拖拽管理器会自动提供视觉反馈：
- 拖拽进入目标区域时，目标透明度变为 0.7
- 拖拽离开或完成时，恢复透明度为 1.0

你可以自定义视觉反馈：

```csharp
_dragDropManager.DragDropStarted += (s, e) =>
{
    // 自定义拖拽开始的视觉效果
    FilesListView.Opacity = 0.5;
};

_dragDropManager.DragDropCompleted += (s, e) =>
{
    // 恢复视觉效果
    FilesListView.Opacity = 1.0;
};
```

## 🔄 扩展功能

### 添加新的拖拽目标类型

1. 在 `DropTargetType` 枚举中添加新类型：

```csharp
public enum DropTargetType
{
    // ... 现有类型
    CustomTarget  // 新类型
}
```

2. 在 `GetDragDropEffects` 中定义默认行为：

```csharp
return targetType switch
{
    // ... 现有类型
    DropTargetType.CustomTarget => DragDropEffects.Copy,
    _ => DragDropEffects.Move
};
```

3. 在 `GetOperationType` 中定义操作类型：

```csharp
if (targetType == DropTargetType.CustomTarget)
    return DragDropOperation.CustomOperation;
```

4. 实现操作逻辑：

```csharp
private bool ExecuteCustomOperation(DragDropData data)
{
    // 实现自定义操作
    return true;
}
```

### 添加新的操作类型

1. 在 `DragDropOperation` 枚举中添加：

```csharp
public enum DragDropOperation
{
    // ... 现有操作
    CustomOperation  // 新操作
}
```

2. 在 `ExecuteDragDropOperation` 中处理：

```csharp
case DragDropOperation.CustomOperation:
    return ExecuteCustomOperation(data);
```

## 💡 使用技巧

### 1. 拖拽时的按键组合

- **默认拖拽**：移动文件
- **按住 Ctrl**：复制文件
- **按住 Shift**：强制移动
- **按住 Ctrl+Shift**：创建链接

### 2. 特殊目标的自动行为

- 拖到**快速访问**：自动添加链接
- 拖到**库**：自动添加到库
- 拖到**标签**：自动添加标签

### 3. 多文件拖拽

- 支持同时拖拽多个文件/文件夹
- 在文件列表中选中多项后拖拽

### 4. 跨驱动器拖拽

- 同驱动器内：默认移动
- 跨驱动器：默认复制（按住 Shift 强制移动）

## ⚠️ 注意事项

1. **权限检查**：某些系统文件夹可能需要管理员权限
2. **文件占用**：确保文件未被其他程序占用
3. **路径长度**：Windows 路径长度限制（260 字符）
4. **磁盘空间**：复制大文件前检查目标磁盘空间

## 🐛 故障排除

### 拖拽无响应

检查：
1. 是否调用了 `InitializeFileListDragDrop`
2. ListView 是否有选中项
3. 是否有异常被静默捕获

### 拖放目标无效果

检查：
1. 目标控件的 `AllowDrop` 是否为 `true`
2. 目标控件的 `Tag` 是否设置了正确的路径
3. 是否调用了对应的初始化方法

### 操作失败

检查：
1. 目标路径是否存在
2. 是否有足够的权限
3. 文件是否被占用

## 📚 完整示例

参考 `MainWindow.xaml.cs` 中的完整集成示例。

---

**拖拽功能已准备就绪！** 🎉

