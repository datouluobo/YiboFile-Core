using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Windows;
using YiboFile.Models;
using Microsoft.Extensions.DependencyInjection;
using YiboFile.Services.Features;
using YiboFile.Controls.Dialogs;

namespace YiboFile
{
    /// <summary>
    /// MainWindow 菜单事件处理
    /// </summary>
    public partial class MainWindow
    {
        #region 菜单事件

        // 文件操作事件桥接方法 - 已迁移到 MenuEventHandler
        internal void NewFolder_Click(object sender, RoutedEventArgs e) => _menuEventHandler?.NewFolder_Click(sender, e);
        private void NewFile_Click(object sender, RoutedEventArgs e) => _menuEventHandler?.NewFile_Click(sender, e);

        private void FileAddTag_Click(object sender, RoutedEventArgs e)
        {
            var browser = sender as YiboFile.Controls.FileBrowserControl;
            if (browser == null) return;

            var items = browser.FilesSelectedItems?.Cast<FileSystemItem>().ToList();
            if (items == null || items.Count == 0) return;

            var dialog = new TagSelectionDialog();
            dialog.Owner = this;
            if (dialog.ShowDialog() == true)
            {
                var tagService = App.ServiceProvider.GetService<ITagService>();
                if (tagService != null)
                {
                    int successCount = 0;
                    foreach (var item in items)
                    {
                        try
                        {
                            tagService.AddTagToFile(item.Path, dialog.SelectedTagId);
                            successCount++;
                        }
                        catch { }
                    }

                    if (successCount > 0)
                    {
                        // Refresh the file list to show updated tags
                        if (browser == FileBrowser) RefreshFileList();
                        else if (browser == SecondFileBrowser && !string.IsNullOrEmpty(SecondFileBrowser.AddressText))
                            LoadSecondFileBrowserDirectory(SecondFileBrowser.AddressText);
                    }
                }
            }
        }

        internal void CreateNewFileWithExtension(string extension)
        {
            try
            {
                string targetPath = null;

                // 判断当前模式：库模式还是路径模式
                if (_currentLibrary != null)
                {
                    // 库模式：使用库的第一个位置
                    if (_currentLibrary.Paths == null || _currentLibrary.Paths.Count == 0)
                    {
                        MessageBox.Show("当前库没有添加任何位置，请先在管理库中添加位置", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                        return;
                    }

                    // 如果有多个位置，让用户选择
                    if (_currentLibrary.Paths.Count > 1)
                    {
                        var paths = string.Join("\n", _currentLibrary.Paths.Select((p, i) => $"{i + 1}. {p}"));
                        var result = MessageBox.Show(
                            $"当前库有多个位置，将在第一个位置创建文件：\n\n{_currentLibrary.Paths[0]}\n\n是否继续？\n\n所有位置：\n{paths}",
                            "选择位置",
                            MessageBoxButton.YesNo,
                            MessageBoxImage.Question);

                        if (result != MessageBoxResult.Yes)
                        {
                            return;
                        }
                    }

                    targetPath = _currentLibrary.Paths[0];
                    if (!Directory.Exists(targetPath))
                    {
                        MessageBox.Show($"库位置不存在: {targetPath}", "错误", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }
                }
                else if (!string.IsNullOrEmpty(_currentPath) && Directory.Exists(_currentPath))
                {
                    // 路径模式：使用当前路径
                    targetPath = _currentPath;
                }
                else
                {
                    MessageBox.Show("当前没有可用的路径", "错误", MessageBoxButton.OK, MessageBoxImage.Warning);
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

                // 刷新显示
                RefreshFileList();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"创建文件失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        private void CreateFileWithProperFormat(string filePath, string extension)
        {
            switch (extension)
            {
                case ".docx":
                case ".xlsx":
                case ".pptx":
                    CreateOfficeFile(filePath, extension);
                    break;

                case ".html":
                    var htmlLines = new[]
                    {
                        "<!DOCTYPE html>",
                        "<html lang=\"zh-CN\">",
                        "<head>",
                        "    <meta charset=\"UTF-8\">",
                        "    <meta name=\"viewport\" content=\"width=device-width, initial-scale=1.0\">",
                        "    <title>新建网页</title>",
                        "</head>",
                        "<body>",
                        "    <h1>Hello World</h1>",
                        "</body>",
                        "</html>",
                        string.Empty
                    };
                    File.WriteAllText(filePath, string.Join(Environment.NewLine, htmlLines));
                    break;

                case ".css":
                    var cssLines = new[]
                    {
                        "/* CSS Stylesheet */",
                        string.Empty,
                        "body {",
                        "    margin: 0;",
                        "    padding: 0;",
                        "}",
                        string.Empty
                    };
                    File.WriteAllText(filePath, string.Join(Environment.NewLine, cssLines));
                    break;

                case ".js":
                    var jsLines = new[]
                    {
                        "// JavaScript",
                        string.Empty,
                        "console.log('Hello World');",
                        string.Empty
                    };
                    File.WriteAllText(filePath, string.Join(Environment.NewLine, jsLines));
                    break;

                case ".cs":
                    var csLines = new[]
                    {
                        "using System;",
                        string.Empty,
                        "namespace MyNamespace",
                        "{",
                        "    class Program",
                        "    {",
                        "        static void Main(string[] args)",
                        "        {",
                        "            Console.WriteLine(\"Hello World\");",
                        "        }",
                        "    }",
                        "}",
                        string.Empty
                    };
                    File.WriteAllText(filePath, string.Join(Environment.NewLine, csLines));
                    break;

                case ".py":
                    var pyLines = new[]
                    {
                        "# Python Script",
                        string.Empty,
                        "def main():",
                        "    print('Hello World')",
                        string.Empty,
                        "if __name__ == '__main__':",
                        "    main()",
                        string.Empty
                    };
                    File.WriteAllText(filePath, string.Join(Environment.NewLine, pyLines));
                    break;

                case ".java":
                    var className = Path.GetFileNameWithoutExtension(filePath).Replace(" ", "_");
                    var javaLines = new[]
                    {
                        $"public class {className} {{",
                        "    public static void main(String[] args) {",
                        "        System.out.println(\"Hello World\");",
                        "    }",
                        "}",
                        string.Empty
                    };
                    File.WriteAllText(filePath, string.Join(Environment.NewLine, javaLines));
                    break;

                case ".json":
                    var jsonLines = new[]
                    {
                        "{",
                        "    \"name\": \"example\",",
                        "    \"version\": \"1.0.0\"",
                        "}",
                        string.Empty
                    };
                    File.WriteAllText(filePath, string.Join(Environment.NewLine, jsonLines));
                    break;

                case ".xml":
                    var xmlLines = new[]
                    {
                        "<?xml version=\"1.0\" encoding=\"UTF-8\"?>",
                        "<root>",
                        "    <item>Example</item>",
                        "</root>",
                        string.Empty
                    };
                    File.WriteAllText(filePath, string.Join(Environment.NewLine, xmlLines));
                    break;

                case ".md":
                    var mdLines = new[]
                    {
                        "# 标题",
                        string.Empty,
                        "这是一个 Markdown 文档。",
                        string.Empty,
                        "## 二级标题",
                        string.Empty,
                        "- 列表项 1",
                        "- 列表项 2",
                        string.Empty
                    };
                    File.WriteAllText(filePath, string.Join(Environment.NewLine, mdLines));
                    break;

                case ".ini":
                    var iniLines = new[]
                    {
                        "[Settings]",
                        "Key=Value",
                        string.Empty
                    };
                    File.WriteAllText(filePath, string.Join(Environment.NewLine, iniLines));
                    break;

                case ".bat":
                    var batLines = new[]
                    {
                        "@echo off",
                        "echo Hello World",
                        "pause",
                        string.Empty
                    };
                    File.WriteAllText(filePath, string.Join(Environment.NewLine, batLines));
                    break;

                case ".ps1":
                    var psLines = new[]
                    {
                        "# PowerShell Script",
                        string.Empty,
                        "Write-Host \"Hello World\"",
                        string.Empty
                    };
                    File.WriteAllText(filePath, string.Join(Environment.NewLine, psLines));
                    break;

                case ".png":
                case ".jpg":
                case ".jpeg":
                case ".bmp":
                case ".gif":
                    CreateImageFile(filePath, extension);
                    break;

                case ".svg":
                    var svgLines = new[]
                    {
                        "<?xml version=\"1.0\" encoding=\"UTF-8\"?>",
                        "<svg width=\"500\" height=\"500\" xmlns=\"http://www.w3.org/2000/svg\">",
                        "    <rect width=\"500\" height=\"500\" fill=\"#FFFFFF\"/>",
                        "</svg>",
                        string.Empty
                    };
                    File.WriteAllText(filePath, string.Join(Environment.NewLine, svgLines));
                    break;

                default:
                    File.WriteAllText(filePath, string.Empty);
                    break;
            }
        }

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
                            bitmap.Save(filePath, System.Drawing.Imaging.ImageFormat.Png);
                            break;
                        case ".jpg":
                        case ".jpeg":
                            bitmap.Save(filePath, System.Drawing.Imaging.ImageFormat.Jpeg);
                            break;
                        case ".bmp":
                            bitmap.Save(filePath, System.Drawing.Imaging.ImageFormat.Bmp);
                            break;
                        case ".gif":
                            bitmap.Save(filePath, System.Drawing.Imaging.ImageFormat.Gif);
                            break;
                        default:
                            bitmap.Save(filePath, System.Drawing.Imaging.ImageFormat.Png);
                            break;
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"创建图片文件失败: {ex.Message}\n将创建空文件", "警告", MessageBoxButton.OK, MessageBoxImage.Warning);
                File.WriteAllText(filePath, string.Empty);
            }
        }

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
                                CreateBasicDocx(filePath);
                                MessageBox.Show("未检测到 Microsoft Word，已创建基本 DOCX 模板。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                                return;
                            }

                            app = Activator.CreateInstance(wordType);
                            app.Visible = false;
                            app.DisplayAlerts = 0;
                            doc = app.Documents.Add();
                            doc.SaveAs2(filePath);
                            doc.Close(false);
                        }
                        catch (Exception ex)
                        {
                            CreateBasicDocx(filePath);
                            MessageBox.Show($"创建文件失败，已回退为基本 DOCX: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Warning);
                        }
                        finally
                        {
                            if (app != null)
                            {
                                app.Quit(false);
                                System.Runtime.InteropServices.Marshal.ReleaseComObject(app);
                            }
                        }
                        break;

                    case ".xlsx":
                        try
                        {
                            var excelType = Type.GetTypeFromProgID("Excel.Application");
                            if (excelType == null)
                            {
                                MessageBox.Show("未检测到 Microsoft Excel，将创建空文件", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                                File.WriteAllText(filePath, string.Empty);
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
                                System.Runtime.InteropServices.Marshal.ReleaseComObject(app);
                            }
                        }
                        break;

                    case ".pptx":
                        try
                        {
                            var pptType = Type.GetTypeFromProgID("PowerPoint.Application");
                            if (pptType == null)
                            {
                                MessageBox.Show("未检测到 Microsoft PowerPoint，将创建空文件", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                                File.WriteAllText(filePath, string.Empty);
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
                                System.Runtime.InteropServices.Marshal.ReleaseComObject(app);
                            }
                        }
                        break;
                }
            }
            catch (Exception ex)
            {
                if (extension.Equals(".docx", StringComparison.OrdinalIgnoreCase))
                {
                    CreateBasicDocx(filePath);
                }
                else
                {
                    File.WriteAllText(filePath, string.Empty);
                }
                MessageBox.Show($"创建 Office 文件失败: {ex.Message}\n已写入占位文件", "警告", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void CreateBasicDocx(string filePath)
        {
            using (var fs = new FileStream(filePath, FileMode.Create, FileAccess.Write))
            using (var archive = new ZipArchive(fs, ZipArchiveMode.Create))
            {
                void AddEntry(string entryName, string content)
                {
                    var entry = archive.CreateEntry(entryName);
                    using var writer = new StreamWriter(entry.Open(), Encoding.UTF8);
                    writer.Write(content);
                }

                AddEntry("[Content_Types].xml",
                    @"<?xml version=""1.0"" encoding=""UTF-8""?>
<Types xmlns=""http://schemas.openxmlformats.org/package/2006/content-types"">
  <Default Extension=""rels"" ContentType=""application/vnd.openxmlformats-package.relationships+xml""/>
  <Default Extension=""xml"" ContentType=""application/xml""/>
  <Override PartName=""/word/document.xml"" ContentType=""application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml""/>
  <Override PartName=""/docProps/core.xml"" ContentType=""application/vnd.openxmlformats-package.core-properties+xml""/>
  <Override PartName=""/docProps/app.xml"" ContentType=""application/vnd.openxmlformats-officedocument.extended-properties+xml""/>
</Types>");

                AddEntry("_rels/.rels",
                    @"<?xml version=""1.0"" encoding=""UTF-8""?>
<Relationships xmlns=""http://schemas.openxmlformats.org/package/2006/relationships"">
  <Relationship Id=""rId1"" Type=""http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument"" Target=""word/document.xml""/>
  <Relationship Id=""rId2"" Type=""http://schemas.openxmlformats.org/package/2006/relationships/metadata/core-properties"" Target=""docProps/core.xml""/>
  <Relationship Id=""rId3"" Type=""http://schemas.openxmlformats.org/officeDocument/2006/relationships/extended-properties"" Target=""docProps/app.xml""/>
</Relationships>");

                AddEntry("word/_rels/document.xml.rels",
                    @"<?xml version=""1.0"" encoding=""UTF-8""?>
<Relationships xmlns=""http://schemas.openxmlformats.org/package/2006/relationships"">
</Relationships>");

                AddEntry("word/document.xml",
                    @"<?xml version=""1.0"" encoding=""UTF-8""?>
<w:document xmlns:wpc=""http://schemas.microsoft.com/office/word/2010/wordprocessingCanvas""
 xmlns:mc=""http://schemas.openxmlformats.org/markup-compatibility/2006""
 xmlns:o=""urn:schemas-microsoft-com:office:office""
 xmlns:r=""http://schemas.openxmlformats.org/officeDocument/2006/relationships""
 xmlns:m=""http://schemas.openxmlformats.org/officeDocument/2006/math""
 xmlns:v=""urn:schemas-microsoft-com:vml""
 xmlns:wp14=""http://schemas.microsoft.com/office/word/2010/wordprocessingDrawing""
 xmlns:wp=""http://schemas.openxmlformats.org/drawingml/2006/wordprocessingDrawing""
 xmlns:w10=""urn:schemas-microsoft-com:office:word""
 xmlns:w=""http://schemas.openxmlformats.org/wordprocessingml/2006/main""
 xmlns:w14=""http://schemas.microsoft.com/office/word/2010/wordml""
 xmlns:w15=""http://schemas.microsoft.com/office/word/2012/wordml""
 mc:Ignorable=""w14 w15 wp14"">
  <w:body>
    <w:p>
      <w:r>
        <w:t>Hello World</w:t>
      </w:r>
    </w:p>
  </w:body>
</w:document>");

                AddEntry("docProps/core.xml",
                    @"<?xml version=""1.0"" encoding=""UTF-8""?>
<cp:coreProperties xmlns:cp=""http://schemas.openxmlformats.org/package/2006/core-properties""
 xmlns:dc=""http://purl.org/dc/elements/1.1/""
 xmlns:dcterms=""http://purl.org/dc/terms/""
 xmlns:dcmitype=""http://purl.org/dc/dcmitype/""
 xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance"">
  <dc:title>New Document</dc:title>
  <dc:creator>YiboFile</dc:creator>
  <cp:lastModifiedBy>YiboFile</cp:lastModifiedBy>
  <dcterms:created xsi:type=""dcterms:W3CDTF"">{DateTime.UtcNow:yyyy-MM-ddTHH:mm:ssZ}</dcterms:created>
  <dcterms:modified xsi:type=""dcterms:W3CDTF"">{DateTime.UtcNow:yyyy-MM-ddTHH:mm:ssZ}</dcterms:modified>
</cp:coreProperties>");

                AddEntry("docProps/app.xml",
                    @"<?xml version=""1.0"" encoding=""UTF-8""?>
<Properties xmlns=""http://schemas.openxmlformats.org/officeDocument/2006/extended-properties""
 xmlns:vt=""http://schemas.openxmlformats.org/officeDocument/2006/docPropsVTypes"">
  <Application>YiboFile</Application>
</Properties>");
            }
        }




        #endregion

    }
}

