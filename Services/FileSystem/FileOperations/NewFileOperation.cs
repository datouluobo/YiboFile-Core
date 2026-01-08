using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using OoiMRR;

namespace OoiMRR.Services.FileOperations
{
    /// <summary>
    /// 新建文件操作
    /// </summary>
    public class NewFileOperation
    {
        private readonly IFileOperationContext _context;
        private readonly System.Windows.Window _ownerWindow;
        private readonly System.Windows.UIElement _placementTarget;
        private readonly FileOperationService _fileOperationService;

        public NewFileOperation(IFileOperationContext context, System.Windows.Window ownerWindow, FileOperationService fileOperationService, System.Windows.UIElement placementTarget = null)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _ownerWindow = ownerWindow ?? throw new ArgumentNullException(nameof(ownerWindow));
            _fileOperationService = fileOperationService;
            _placementTarget = placementTarget;
        }

        /// <summary>
        /// 执行新建文件操作（显示文件类型选择菜单）
        /// </summary>
        public void Execute()
        {
            if (!_context.CanPerformOperation("NewFile"))
            {
                return;
            }

            try
            {
                // 显示文件类型选择菜单
                var contextMenu = new ContextMenu
                {
                    Placement = PlacementMode.Bottom,
                    PlacementTarget = _placementTarget
                };

                // 常用文件类型列表
                var fileTypes = new[]
                {
                    ("📄 文本文件", ".txt"),
                    ("📝 Word 文档", ".docx"),
                    ("📊 Excel 表格", ".xlsx"),
                    ("📽️ PowerPoint", ".pptx"),
                    ("🖼️ PNG 图片", ".png"),
                    ("🖼️ JPEG 图片", ".jpg"),
                    ("🖼️ GIF 图片", ".gif"),
                    ("🖼️ BMP 图片", ".bmp"),
                    ("🖼️ SVG 矢量图", ".svg"),
                    ("🌐 HTML 网页", ".html"),
                    ("🎨 CSS 样式", ".css"),
                    ("⚡ JavaScript", ".js"),
                    ("🐍 Python", ".py"),
                    ("☕ Java", ".java"),
                    ("📋 JSON", ".json"),
                    ("📋 XML", ".xml"),
                    ("📋 Markdown", ".md"),
                    ("⚙️ 配置文件", ".ini"),
                    ("📦 批处理", ".bat"),
                    ("🔧 PowerShell", ".ps1")
                };

                foreach (var (name, extension) in fileTypes)
                {
                    var menuItem = new MenuItem
                    {
                        Header = name,
                        Tag = extension,
                        Padding = new System.Windows.Thickness(10, 5, 10, 5)
                    };
                    menuItem.Click += (s, args) =>
                    {
                        CreateNewFileWithExtension(extension);
                    };
                    contextMenu.Items.Add(menuItem);
                }

                // 添加分隔符和自定义选项
                contextMenu.Items.Add(new Separator());

                var customMenuItem = new MenuItem
                {
                    Header = "✏️ 自定义扩展名...",
                    Padding = new System.Windows.Thickness(10, 5, 10, 5)
                };
                customMenuItem.Click += (s, args) =>
                {
                    var dialog = new PathInputDialog
                    {
                        Title = "新建文件",
                        PromptText = "请输入文件扩展名（如 .txt）：",
                        InputText = ".txt",
                        Owner = _ownerWindow
                    };

                    if (dialog.ShowDialog() == true)
                    {
                        var extension = dialog.InputText.Trim();
                        if (!extension.StartsWith("."))
                        {
                            extension = "." + extension;
                        }
                        CreateNewFileWithExtension(extension);
                    }
                };
                contextMenu.Items.Add(customMenuItem);

                // 显示菜单
                contextMenu.IsOpen = true;
            }
            catch (Exception ex)
            {
                _context.ShowMessage($"创建文件失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public void Execute(string extension)
        {
            if (string.IsNullOrEmpty(extension))
            {
                Execute();
                return;
            }
            CreateNewFileWithExtension(extension);
        }

        private void CreateNewFileWithExtension(string extension)
        {
            try
            {
                string targetPath = _context.GetTargetPath();
                if (string.IsNullOrEmpty(targetPath) || !Directory.Exists(targetPath))
                {
                    _context.ShowMessage("当前没有可用的路径", "错误", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // 根据扩展名生成文件名
                string baseFileName = $"新建文件{extension}";
                string filePath = Path.Combine(targetPath, baseFileName);

                // 如果已存在，自动添加序号
                if (File.Exists(filePath))
                {
                    string fileNameWithoutExt = Path.GetFileNameWithoutExtension(baseFileName);
                    int counter = 2;

                    do
                    {
                        string candidateFileName = $"{fileNameWithoutExt} ({counter}){extension}";
                        filePath = Path.Combine(targetPath, candidateFileName);
                        counter++;
                    }
                    while (File.Exists(filePath));
                }

                // 根据文件类型创建合适的文件内容
                CreateFileWithProperFormat(filePath, extension.ToLower());

                // 注册 Undo
                _fileOperationService?.NotifyFileCreated(filePath);

                // 刷新显示
                _context.RefreshAfterOperation();
            }
            catch (Exception ex)
            {
                _context.ShowMessage($"创建文件失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void CreateFileWithProperFormat(string filePath, string extension)
        {
            switch (extension)
            {
                case ".docx":
                case ".xlsx":
                case ".pptx":
                    // Office 文件需要使用 COM 创建
                    CreateOfficeFile(filePath, extension);
                    break;

                case ".png":
                case ".jpg":
                case ".jpeg":
                case ".bmp":
                case ".gif":
                    // 创建图片文件
                    CreateImageFile(filePath, extension);
                    break;

                case ".html":
                    File.WriteAllText(filePath, @"<!DOCTYPE html>
<html lang=""zh-CN"">
<head>
    <meta charset=""UTF-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
    <title>新建网页</title>
</head>
<body>
    <h1>Hello World</h1>
</body>
</html>");
                    break;

                case ".css":
                    File.WriteAllText(filePath, @"/* CSS Stylesheet */

body {
    margin: 0;
    padding: 0;
}
");
                    break;

                case ".js":
                    File.WriteAllText(filePath, @"// JavaScript File

console.log('Hello World');
");
                    break;

                case ".json":
                    File.WriteAllText(filePath, @"{
  ""key"": ""value""
}");
                    break;

                case ".xml":
                    File.WriteAllText(filePath, @"<?xml version=""1.0"" encoding=""UTF-8""?>
<root>
    <item>Content</item>
</root>");
                    break;

                case ".md":
                    File.WriteAllText(filePath, @"# 标题

内容");
                    break;

                case ".py":
                    File.WriteAllText(filePath, @"#!/usr/bin/env python
# -*- coding: utf-8 -*-

print('Hello World')
");
                    break;

                case ".java":
                    File.WriteAllText(filePath, @"public class Main {
    public static void main(String[] args) {
        System.out.println(""Hello World"");
    }
}");
                    break;

                case ".bat":
                    File.WriteAllText(filePath, @"@echo off
echo Hello World
pause");
                    break;

                case ".ps1":
                    File.WriteAllText(filePath, @"Write-Host ""Hello World""");
                    break;

                default:
                    // 其他文件类型创建空文件
                    File.Create(filePath).Close();
                    break;
            }
        }

        /// <summary>
        /// 创建图片文件
        /// </summary>
        private void CreateImageFile(string filePath, string extension)
        {
            try
            {
                // 创建一个500x500的空白图片
                using (var bitmap = new Bitmap(500, 500))
                {
                    using (var graphics = Graphics.FromImage(bitmap))
                    {
                        // 填充白色背景
                        graphics.Clear(System.Drawing.Color.White);
                    }

                    // 根据扩展名保存为相应格式
                    switch (extension.ToLower())
                    {
                        case ".png":
                            bitmap.Save(filePath, ImageFormat.Png);
                            break;
                        case ".jpg":
                        case ".jpeg":
                            bitmap.Save(filePath, ImageFormat.Jpeg);
                            break;
                        case ".bmp":
                            bitmap.Save(filePath, ImageFormat.Bmp);
                            break;
                        case ".gif":
                            bitmap.Save(filePath, ImageFormat.Gif);
                            break;
                        default:
                            bitmap.Save(filePath, ImageFormat.Png);
                            break;
                    }
                }
            }
            catch (Exception ex)
            {
                _context.ShowMessage($"创建图片文件失败: {ex.Message}\n将创建空文件", "警告",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                File.Create(filePath).Close();
            }
        }

        /// <summary>
        /// 创建 Office 文件
        /// </summary>
        private void CreateOfficeFile(string filePath, string extension)
        {
            try
            {
                dynamic app = null;
                dynamic doc = null;

                switch (extension)
                {
                    case ".docx":
                        try
                        {
                            var wordType = Type.GetTypeFromProgID("Word.Application");
                            if (wordType == null)
                            {
                                _context.ShowMessage("未检测到 Microsoft Word，将创建空文件", "提示",
                                    MessageBoxButton.OK, MessageBoxImage.Information);
                                File.Create(filePath).Close();
                                return;
                            }

                            app = Activator.CreateInstance(wordType);
                            app.Visible = false;
                            app.DisplayAlerts = 0;
                            doc = app.Documents.Add();
                            doc.SaveAs2(filePath);
                            doc.Close(false);
                        }
                        finally
                        {
                            if (app != null)
                            {
                                app.Quit(false);
                                Marshal.ReleaseComObject(app);
                            }
                        }
                        break;

                    case ".xlsx":
                        try
                        {
                            var excelType = Type.GetTypeFromProgID("Excel.Application");
                            if (excelType == null)
                            {
                                _context.ShowMessage("未检测到 Microsoft Excel，将创建空文件", "提示",
                                    MessageBoxButton.OK, MessageBoxImage.Information);
                                File.Create(filePath).Close();
                                return;
                            }

                            app = Activator.CreateInstance(excelType);
                            app.Visible = false;
                            app.DisplayAlerts = false;
                            doc = app.Workbooks.Add();
                            doc.SaveAs(filePath);
                            doc.Close(false);
                        }
                        finally
                        {
                            if (app != null)
                            {
                                app.Quit();
                                Marshal.ReleaseComObject(app);
                            }
                        }
                        break;

                    case ".pptx":
                        try
                        {
                            var pptType = Type.GetTypeFromProgID("PowerPoint.Application");
                            if (pptType == null)
                            {
                                _context.ShowMessage("未检测到 Microsoft PowerPoint，将创建空文件", "提示",
                                    MessageBoxButton.OK, MessageBoxImage.Information);
                                File.Create(filePath).Close();
                                return;
                            }

                            app = Activator.CreateInstance(pptType);
                            doc = app.Presentations.Add();
                            doc.SaveAs(filePath);
                            doc.Close();
                        }
                        finally
                        {
                            if (app != null)
                            {
                                app.Quit();
                                Marshal.ReleaseComObject(app);
                            }
                        }
                        break;
                }
            }
            catch (Exception ex)
            {
                _context.ShowMessage($"创建 Office 文件失败: {ex.Message}\n将创建空文件", "警告",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                File.Create(filePath).Close();
            }
        }
    }
}



