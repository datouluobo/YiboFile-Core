using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Net;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Web.WebView2.Wpf;
using System.Xml.Linq;
using System.Runtime.InteropServices;
using YiboFile.Controls;

namespace YiboFile.Previews
{
    public class ExcelPreview : IPreviewProvider
    {
        public UIElement CreatePreview(string filePath)
        {
            try
            {
                var ext = Path.GetExtension(filePath).ToLower();

                // Common Toolbar Setup
                var toolbar = new TextPreviewToolbar
                {
                    FileName = Path.GetFileName(filePath),
                    FileIcon = "📊",
                    ShowSearch = false,
                    ShowWordWrap = false,
                    ShowEncoding = false,
                    ShowViewToggle = false,
                    ShowFormat = false
                };

                toolbar.OpenExternalRequested += (s, e) => PreviewHelper.OpenInDefaultApp(filePath);

                if (ext == ".xls")
                {
                    var panel = new StackPanel
                    {
                        Orientation = Orientation.Vertical,
                        Background = Brushes.White
                    };

                    // Has Excel Check
                    bool hasExcel = IsExcelInstalled();

                    // Convert Button
                    var convertButton = PreviewHelper.CreateConvertButton(
                        "🔄 转换为XLSX格式",
                        async (s, e) =>
                        {
                            var btn = s as Button;
                            try
                            {
                                btn.IsEnabled = false;
                                btn.Content = "⏳ 转换中...";

                                await System.Threading.Tasks.Task.Run(() =>
                                {
                                    try
                                    {
                                        string directory = Path.GetDirectoryName(filePath);
                                        string baseName = Path.GetFileNameWithoutExtension(filePath);
                                        string outputPath = Path.Combine(directory, baseName + ".xlsx");
                                        outputPath = GetUniqueFilePath(outputPath);

                                        string errorMsg = null;
                                        if (ConvertXlsToXlsx(filePath, outputPath, out errorMsg))
                                        {
                                            Application.Current.Dispatcher.Invoke(() =>
                                            {
                                                btn.Content = "✅ 转换成功";
                                                MessageBox.Show($"文件已成功转换为XLSX格式：\n{outputPath}", "转换成功", MessageBoxButton.OK, MessageBoxImage.Information);
                                            });
                                        }
                                        else
                                        {
                                            Application.Current.Dispatcher.Invoke(() =>
                                            {
                                                string errorTitle = errorMsg?.Contains("未检测到") == true ? "需要 Microsoft Excel" : "转换错误";
                                                MessageBox.Show(errorMsg ?? "转换失败", errorTitle, MessageBoxButton.OK, MessageBoxImage.Error);
                                                btn.IsEnabled = true;
                                                btn.Content = "🔄 转换为XLSX格式";
                                            });
                                        }
                                    }
                                    catch (Exception ex)
                                    {
                                        Application.Current.Dispatcher.Invoke(() =>
                                        {
                                            MessageBox.Show($"转换失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                                            btn.IsEnabled = true;
                                            btn.Content = "🔄 转换为XLSX格式";
                                        });
                                    }
                                });
                            }
                            catch (Exception ex)
                            {
                                MessageBox.Show($"转换失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                                btn.IsEnabled = true;
                                btn.Content = "🔄 转换为XLSX格式";
                            }
                        }
                    );

                    // If no Excel, disable
                    if (!hasExcel)
                    {
                        convertButton.IsEnabled = false;
                        convertButton.ToolTip = "未检测到 Microsoft Excel，无法使用自动转换功能";
                    }

                    // Add Custom Button to Toolbar
                    toolbar.CustomActionContent = convertButton;

                    panel.Children.Add(toolbar);

                    // Legacy Info Panel
                    var infoPanel = PreviewHelper.CreateLegacyFormatPanel(
                        "Excel",
                        "该文件为旧的 Excel 格式（Microsoft Excel 97-2003）\n" +
                        "由于 XLS 使用二进制格式，无法直接预览内容。",
                        hasExcel,
                        "转换为XLSX格式"
                    );
                    panel.Children.Add(infoPanel);

                    return panel;
                }
                else
                {
                    // .xlsx preview with WebView2
                    var grid = new Grid();
                    grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Toolbar
                    grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Sheets
                    grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // Content

                    Grid.SetRow(toolbar, 0);
                    grid.Children.Add(toolbar);

                    // Get Sheets
                    var sheets = GetAllSheetNames(filePath);
                    if (sheets == null || sheets.Count == 0)
                    {
                        sheets = new List<(string Name, string Id)> { ("Sheet1", "sheet1") };
                    }

                    // Tab bar for sheets
                    var tabBorder = new Border
                    {
                        Background = new SolidColorBrush(Color.FromRgb(245, 245, 245)),
                        BorderBrush = new SolidColorBrush(Color.FromRgb(230, 230, 230)),
                        BorderThickness = new Thickness(0, 0, 0, 1),
                        Padding = new Thickness(10, 5, 10, 5)
                    };

                    var tabPanel = new StackPanel { Orientation = Orientation.Horizontal };
                    tabBorder.Child = tabPanel;

                    var webView = new WebView2 { VerticalAlignment = VerticalAlignment.Stretch, HorizontalAlignment = HorizontalAlignment.Stretch };
                    Grid.SetRow(webView, 2);
                    grid.Children.Add(webView);

                    string currentSheetId = sheets[0].Id;

                    foreach (var (sheetName, sheetId) in sheets)
                    {
                        var tabButton = new Button
                        {
                            Content = sheetName,
                            Padding = new Thickness(12, 6, 12, 6),
                            Margin = new Thickness(0, 0, 5, 0),
                            FontSize = 13,
                            Cursor = Cursors.Hand
                        };

                        if (sheetId == currentSheetId)
                        {
                            tabButton.Background = new SolidColorBrush(Color.FromRgb(33, 150, 243));
                            tabButton.Foreground = Brushes.White;
                        }
                        else
                        {
                            tabButton.Background = new SolidColorBrush(Color.FromRgb(250, 250, 250));
                            tabButton.Foreground = Brushes.Black;
                        }

                        tabButton.Click += async (s, e) =>
                        {
                            foreach (Button btn in tabPanel.Children)
                            {
                                btn.Background = new SolidColorBrush(Color.FromRgb(250, 250, 250));
                                btn.Foreground = Brushes.Black;
                            }
                            tabButton.Background = new SolidColorBrush(Color.FromRgb(33, 150, 243));
                            tabButton.Foreground = Brushes.White;

                            currentSheetId = sheetId;
                            try
                            {
                                await webView.EnsureCoreWebView2Async();
                                var html = GenerateHtmlFromXlsx(filePath, sheetId);
                                webView.NavigateToString(html);
                            }
                            catch (Exception ex)
                            {
                                webView.NavigateToString($"<html><body style='font-family:Segoe UI;color:#c00;padding:16px'>预览失败: {WebUtility.HtmlEncode(ex.Message)}</body></html>");
                            }
                        };

                        tabPanel.Children.Add(tabButton);
                    }

                    Grid.SetRow(tabBorder, 1);
                    grid.Children.Add(tabBorder);

                    webView.Loaded += async (s, e) =>
                    {
                        try
                        {
                            await webView.EnsureCoreWebView2Async();
                            var html = GenerateHtmlFromXlsx(filePath, currentSheetId);
                            webView.NavigateToString(html);
                        }
                        catch (Exception ex)
                        {
                            webView.NavigateToString($"<html><body style='font-family:Segoe UI;color:#c00;padding:16px'>预览失败: {WebUtility.HtmlEncode(ex.Message)}</body></html>");
                        }
                    };

                    return grid;
                }
            }
            catch (Exception ex)
            {
                return PreviewHelper.CreateErrorPreview($"无法加载Excel: {ex.Message}");
            }
        }

        // ... Keep existing helper methods (GetAllSheetNames, GenerateHtmlFromXlsx, FindWorksheetById, GetSheetNameById, ParseSharedStrings, ParseSheetRows, ColRefToIndex, IndexToCol, GetUniqueFilePath, ConvertXlsToXlsx, IsExcelInstalled) ...

        /// <summary>
        /// 获取所有工作表名称和ID
        /// </summary>
        private List<(string Name, string Id)> GetAllSheetNames(string path)
        {
            try
            {
                using var fs = File.OpenRead(path);
                using var zip = new ZipArchive(fs, ZipArchiveMode.Read, true);

                var e = zip.GetEntry("xl/workbook.xml");
                if (e == null) return null;

                using var s = e.Open();
                var doc = XDocument.Load(s);
                var ns = doc.Root?.Name.Namespace;
                var sheets = new List<(string Name, string Id)>();

                foreach (var sheet in doc.Descendants(ns + "sheet"))
                {
                    var name = sheet.Attribute("name")?.Value;
                    var sheetId = sheet.Attribute("sheetId")?.Value;
                    var rId = sheet.Attribute(System.Xml.Linq.XName.Get("id", "http://schemas.openxmlformats.org/officeDocument/2006/relationships"))?.Value;

                    if (!string.IsNullOrEmpty(rId))
                    {
                        var relationships = zip.GetEntry("xl/_rels/workbook.xml.rels");
                        if (relationships != null)
                        {
                            using var relStream = relationships.Open();
                            var relDoc = XDocument.Load(relStream);
                            var relNs = relDoc.Root?.Name.Namespace;
                            var rel = relDoc.Descendants(relNs + "Relationship")
                                .FirstOrDefault(x => x.Attribute("Id")?.Value == rId);
                            if (rel != null)
                            {
                                var target = rel.Attribute("Target")?.Value;
                                if (!string.IsNullOrEmpty(target))
                                {
                                    var fileName = Path.GetFileNameWithoutExtension(target);
                                    sheets.Add((name ?? "Sheet1", fileName));
                                    continue;
                                }
                            }
                        }
                    }

                    if (!string.IsNullOrEmpty(sheetId) && int.TryParse(sheetId, out var id))
                    {
                        sheets.Add((name ?? "Sheet1", $"sheet{id}"));
                    }
                }

                return sheets;
            }
            catch
            {
                return null;
            }
        }

        private string GenerateHtmlFromXlsx(string path, string sheetId = "sheet1")
        {
            using var fs = File.OpenRead(path);
            using var zip = new ZipArchive(fs, ZipArchiveMode.Read, true);

            var sharedStrings = ParseSharedStrings(zip);
            var sheetEntry = FindWorksheetById(zip, sheetId);
            var sheetName = GetSheetNameById(zip, sheetId) ?? sheetId;
            var rows = ParseSheetRows(zip, sheetEntry, sharedStrings, 100, 50);

            var sb = new StringBuilder();
            sb.Append("<html><head><meta charset='utf-8'><style>");
            sb.Append("body{font-family:Segoe UI,Arial,Helvetica,sans-serif;margin:0}");
            sb.Append(".hdr{background:#f5f5f5;border-bottom:1px solid #e6e6e6;padding:10px 15px;font-weight:600}");
            sb.Append("table{border-collapse:collapse;width:100%}");
            sb.Append("th,td{border:1px solid #ddd;padding:6px;font-size:13px}");
            sb.Append("th{background:#fafafa}");
            sb.Append(".meta{padding:8px 15px;color:#666;font-size:12px}");
            sb.Append("</style></head><body>");
            sb.Append($"<div class='hdr'>工作表: {WebUtility.HtmlEncode(sheetName)}</div>");
            sb.Append($"<div class='meta'>行数预览: {rows.Count}</div>");
            sb.Append("<table>");
            if (rows.Count > 0)
            {
                sb.Append("<thead><tr>");
                var maxCols = rows.Max(r => r.Count);
                for (int c = 0; c < maxCols; c++) sb.Append($"<th>{IndexToCol(c)}</th>");
                sb.Append("</tr></thead>");
            }
            sb.Append("<tbody>");
            foreach (var row in rows)
            {
                sb.Append("<tr>");
                foreach (var cell in row)
                {
                    var text = WebUtility.HtmlEncode(cell ?? "");
                    sb.Append($"<td>{text}</td>");
                }
                sb.Append("</tr>");
            }
            sb.Append("</tbody></table></body></html>");
            return sb.ToString();
        }

        private static ZipArchiveEntry FindWorksheetById(ZipArchive zip, string sheetId)
        {
            var e = zip.GetEntry($"xl/worksheets/{sheetId}.xml");
            if (e != null) return e;

            var any = zip.Entries.FirstOrDefault(x => x.FullName.StartsWith("xl/worksheets/") && x.FullName.EndsWith(".xml"));
            if (any == null) throw new InvalidOperationException("未找到工作表");
            return any;
        }

        private static string GetSheetNameById(ZipArchive zip, string sheetId)
        {
            var e = zip.GetEntry("xl/workbook.xml");
            if (e == null) return null;
            using var s = e.Open();
            var doc = XDocument.Load(s);
            var ns = doc.Root?.Name.Namespace;

            var relEntry = zip.GetEntry("xl/_rels/workbook.xml.rels");
            if (relEntry != null)
            {
                using var relStream = relEntry.Open();
                var relDoc = XDocument.Load(relStream);
                var relNs = relDoc.Root?.Name.Namespace;
                var rel = relDoc.Descendants(relNs + "Relationship")
                    .FirstOrDefault(x => x.Attribute("Target")?.Value?.Contains(sheetId) == true);
                if (rel != null)
                {
                    var rId = rel.Attribute("Id")?.Value;
                    var sheet = doc.Descendants(ns + "sheet")
                        .FirstOrDefault(x => x.Attribute(System.Xml.Linq.XName.Get("id", "http://schemas.openxmlformats.org/officeDocument/2006/relationships"))?.Value == rId);
                    if (sheet != null)
                    {
                        return sheet.Attribute("name")?.Value;
                    }
                }
            }

            var firstSheet = doc.Descendants(ns + "sheet").FirstOrDefault();
            return firstSheet?.Attribute("name")?.Value;
        }

        private static List<string> ParseSharedStrings(ZipArchive zip)
        {
            var list = new List<string>();
            var e = zip.GetEntry("xl/sharedStrings.xml");
            if (e == null) return list;
            using var s = e.Open();
            var doc = XDocument.Load(s);
            var ns = doc.Root?.Name.Namespace;
            foreach (var si in doc.Descendants(ns + "si"))
            {
                var t = si.Descendants(ns + "t").FirstOrDefault();
                list.Add(t?.Value ?? string.Empty);
            }
            return list;
        }

        private static List<List<string>> ParseSheetRows(ZipArchive zip, ZipArchiveEntry sheetEntry, List<string> shared, int maxRows, int maxCols)
        {
            using var s = sheetEntry.Open();
            var doc = XDocument.Load(s);
            var ns = doc.Root?.Name.Namespace;
            var rows = new List<List<string>>();
            foreach (var row in doc.Descendants(ns + "row").Take(maxRows))
            {
                var cols = new List<string>();
                int currentCol = 0;
                foreach (var c in row.Descendants(ns + "c"))
                {
                    var r = c.Attribute("r")?.Value;
                    int targetIndex = r != null ? ColRefToIndex(r) : currentCol;
                    while (cols.Count < targetIndex) cols.Add(string.Empty);
                    var t = c.Attribute("t")?.Value;
                    var v = c.Descendants(ns + "v").FirstOrDefault()?.Value;
                    string val = string.Empty;
                    if (t == "s")
                    {
                        if (int.TryParse(v, out var idx) && idx >= 0 && idx < shared.Count) val = shared[idx]; else val = v;
                    }
                    else if (t == "b")
                    {
                        val = v == "1" ? "TRUE" : "FALSE";
                    }
                    else
                    {
                        val = v ?? string.Empty;
                    }
                    cols.Add(val);
                    currentCol = targetIndex + 1;
                    if (cols.Count >= maxCols) break;
                }
                rows.Add(cols);
            }
            return rows;
        }

        private static int ColRefToIndex(string cellRef)
        {
            int i = 0;
            while (i < cellRef.Length && char.IsLetter(cellRef[i])) i++;
            var letters = cellRef.Substring(0, i).ToUpperInvariant();
            int index = 0;
            foreach (var ch in letters)
            {
                index = index * 26 + (ch - 'A' + 1);
            }
            return Math.Max(0, index - 1);
        }

        private static string IndexToCol(int index)
        {
            var sb = new StringBuilder();
            index++;
            while (index > 0)
            {
                int rem = (index - 1) % 26;
                sb.Insert(0, (char)('A' + rem));
                index = (index - 1) / 26;
            }
            return sb.ToString();
        }

        private string GetUniqueFilePath(string filePath)
        {
            if (!File.Exists(filePath))
            {
                return filePath;
            }

            string directory = Path.GetDirectoryName(filePath);
            string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(filePath);
            string extension = Path.GetExtension(filePath);

            int counter = 1;
            string newFilePath;
            do
            {
                newFilePath = Path.Combine(directory, $"{fileNameWithoutExtension}({counter}){extension}");
                counter++;
            }
            while (File.Exists(newFilePath));

            return newFilePath;
        }

        private bool ConvertXlsToXlsx(string xlsPath, string xlsxPath, out string errorMessage)
        {
            errorMessage = null;

            try
            {
                Type excelType = Type.GetTypeFromProgID("Excel.Application");
                if (excelType == null)
                {
                    errorMessage = "未检测到 Microsoft Excel。\n\n转换 XLS 到 XLSX 需要安装 Microsoft Excel。";
                    return false;
                }

                dynamic excelApp = Activator.CreateInstance(excelType);
                try
                {
                    try
                    {
                        excelApp.Visible = false;
                    }
                    catch
                    {
                    }

                    excelApp.DisplayAlerts = false;

                    dynamic workbook = excelApp.Workbooks.Open(xlsPath, ReadOnly: true);

                    workbook.SaveAs(xlsxPath, 51);
                    workbook.Close(false);

                    return true;
                }
                finally
                {
                    try
                    {
                        excelApp.Quit();
                    }
                    catch (COMException)
                    {
                    }
                    catch
                    {
                    }
                    try
                    {
                        System.Runtime.InteropServices.Marshal.ReleaseComObject(excelApp);
                    }
                    catch (COMException)
                    {
                    }
                    catch
                    {
                    }
                }
            }
            catch (Exception ex)
            {
                errorMessage = $"转换失败: {ex.Message}\n\n请确保：\n1. 已安装 Microsoft Excel\n2. 文件未被其他程序占用\n3. 有足够的磁盘空间";
                return false;
            }
        }

        private bool IsExcelInstalled()
        {
            try
            {
                Type excelType = Type.GetTypeFromProgID("Excel.Application");
                return excelType != null;
            }
            catch
            {
                return false;
            }
        }
    }
}

