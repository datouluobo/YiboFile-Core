using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Xml.Linq;
using System.Net;
using YiboFile.Previews;

namespace YiboFile.ViewModels.Previews
{
    public class SheetInfo
    {
        public string Name { get; set; }
        public string Id { get; set; }
    }

    public class ExcelPreviewViewModel : BasePreviewViewModel
    {
        private bool _isLegacyFormat;
        public bool IsLegacyFormat
        {
            get => _isLegacyFormat;
            set => SetProperty(ref _isLegacyFormat, value);
        }

        private bool _hasExcelInstalled;
        public bool HasExcelInstalled
        {
            get => _hasExcelInstalled;
            set => SetProperty(ref _hasExcelInstalled, value);
        }

        private bool _isConverting;
        public bool IsConverting
        {
            get => _isConverting;
            set => SetProperty(ref _isConverting, value);
        }

        private string _convertStatusText = "🔄 转换为XLSX格式";
        public string ConvertStatusText
        {
            get => _convertStatusText;
            set => SetProperty(ref _convertStatusText, value);
        }

        private List<SheetInfo> _sheets;
        public List<SheetInfo> Sheets
        {
            get => _sheets;
            set => SetProperty(ref _sheets, value);
        }

        private SheetInfo _selectedSheet;
        public SheetInfo SelectedSheet
        {
            get => _selectedSheet;
            set
            {
                if (SetProperty(ref _selectedSheet, value))
                {
                    GenerateHtmlForSelectedSheet();
                }
            }
        }

        private string _generatedHtml;
        public string GeneratedHtml
        {
            get => _generatedHtml;
            set => SetProperty(ref _generatedHtml, value);
        }

        public ICommand ConvertCommand { get; }

        public event EventHandler ReloadRequested;

        public ExcelPreviewViewModel()
        {
            ConvertCommand = new RelayCommand(ConvertXlsToXlsxAsync);
            OpenExternalCommand = new RelayCommand(() => PreviewHelper.OpenInDefaultApp(FilePath));
        }

        public override async Task LoadAsync(string filePath, System.Threading.CancellationToken token = default)
        {
            FilePath = filePath;
            Title = Path.GetFileName(filePath);
            Icon = "📊";
            IsLoading = true;
            IsLegacyFormat = false;

            try
            {
                await Task.Run(() =>
                {
                    var ext = Path.GetExtension(filePath).ToLower();
                    if (ext == ".xls")
                    {
                        // Try auto-conversion to temp file for preview
                        string tempXlsx = Path.Combine(Path.GetTempPath(), $"excel_preview_{Guid.NewGuid()}.xlsx");
                        string error;
                        if (ExcelParser.ConvertXlsToXlsx(filePath, tempXlsx, out error))
                        {
                            LoadXlsx(tempXlsx);
                            // The temp file will be deleted later or overwritten
                        }
                        else
                        {
                            IsLegacyFormat = true;
                            HasExcelInstalled = IsExcelInstalled();
                        }
                    }
                    else
                    {
                        LoadXlsx(filePath);
                    }
                });
            }
            catch (Exception ex)
            {
                GeneratedHtml = $"<html><body>Error: {ex.Message}</body></html>";
            }
            finally
            {
                IsLoading = false;
            }
        }

        private void LoadXlsx(string path)
        {
            try
            {
                var sheets = ExcelParser.GetAllSheetNames(path);
                if (sheets == null || sheets.Count == 0)
                {
                    Sheets = new List<SheetInfo> { new SheetInfo { Name = "Sheet1", Id = "sheet1" } };
                }
                else
                {
                    Sheets = sheets.Select(x => new SheetInfo { Name = x.Name, Id = x.Id }).ToList();
                }

                if (Sheets.Count > 0)
                {
                    SelectedSheet = Sheets[0];
                }
            }
            catch (Exception ex)
            {
                GeneratedHtml = $"<html><body>Error loading Excel file: {ex.Message}</body></html>";
            }
        }

        private void GenerateHtmlForSelectedSheet()
        {
            if (SelectedSheet == null) return;

            Task.Run(() =>
            {
                try
                {
                    var html = ExcelParser.GenerateHtmlFromXlsx(FilePath, SelectedSheet.Id, GetThemeCss());
                    Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                    {
                        GeneratedHtml = html;
                        ReloadRequested?.Invoke(this, EventArgs.Empty);
                    }));
                }
                catch (Exception ex)
                {
                    Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                    {
                        GeneratedHtml = $"<html><body>Error generating preview: {ex.Message}</body></html>";
                        ReloadRequested?.Invoke(this, EventArgs.Empty);
                    }));
                }
            });
        }

        private async void ConvertXlsToXlsxAsync()
        {
            if (IsConverting) return;
            IsConverting = true;
            ConvertStatusText = "⏳ 转换中...";

            await Task.Run(() =>
            {
                try
                {
                    string directory = Path.GetDirectoryName(FilePath);
                    string baseName = Path.GetFileNameWithoutExtension(FilePath);
                    string outputPath = Path.Combine(directory, baseName + ".xlsx");
                    outputPath = ExcelParser.GetUniqueFilePath(outputPath);

                    string errorMsg;
                    if (ExcelParser.ConvertXlsToXlsx(FilePath, outputPath, out errorMsg))
                    {
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            ConvertStatusText = "✅ 转换成功";
                            Services.Core.NotificationService.ShowSuccess($"文件已成功转换为XLSX格式：\n{outputPath}");
                        });
                    }
                    else
                    {
                        Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                        {
                            ConvertStatusText = "🔄 转换为XLSX格式";
                            Services.Core.NotificationService.ShowError(errorMsg ?? "转换失败");
                        }));
                    }
                }
                catch (Exception ex)
                {
                    Application.Current.Dispatcher.Invoke(() =>
                   {
                       ConvertStatusText = "🔄 转换为XLSX格式";
                       Services.Core.NotificationService.ShowError($"转换失败: {ex.Message}");
                   });
                }
            });

            IsConverting = false;
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

    public static class ExcelParser
    {
        public static List<(string Name, string Id)> GetAllSheetNames(string path)
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
                    var rId = sheet.Attribute(XName.Get("id", "http://schemas.openxmlformats.org/officeDocument/2006/relationships"))?.Value;

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

        public static string GenerateHtmlFromXlsx(string path, string sheetId, string themeCss = "")
        {
            using var fs = File.OpenRead(path);
            using var zip = new ZipArchive(fs, ZipArchiveMode.Read, true);

            var sharedStrings = ParseSharedStrings(zip);
            var sheetEntry = FindWorksheetById(zip, sheetId);
            var sheetName = GetSheetNameById(zip, sheetId) ?? sheetId;
            var rows = ParseSheetRows(zip, sheetEntry, sharedStrings, 100, 50);

            var sb = new StringBuilder();
            sb.Append("<html><head><meta charset='utf-8'><style>");
            sb.Append("body{font-family:'Segoe UI', Tahoma, Geneva, Verdana, sans-serif;margin:0;background:#fff;color:#333}");
            sb.Append(".hdr{background:#f8f9fa;border-bottom:1px solid #dee2e6;padding:12px 20px;font-weight:600;font-size:14px;display:flex;justify-content:space-between;align-items:center}");
            sb.Append("table{border-collapse:collapse;width:100%;table-layout:fixed;border-spacing:0;background:#fff}");
            sb.Append("th,td{border:1px solid #e2e4e7;padding:4px 8px;font-size:12px;overflow:hidden;text-overflow:ellipsis;white-space:nowrap}");
            sb.Append("th{background:#f1f3f4;color:#5f6368;font-weight:normal;text-align:center;position:sticky;top:0;z-index:10}");
            sb.Append(".row-idx{background:#f1f3f4;color:#5f6368;text-align:center;width:40px;position:sticky;left:0;z-index:5}");
            sb.Append("tr:hover td{background:#f8f9fa}");
            sb.Append(".meta{color:#70757a;font-size:11px;font-weight:normal}");
            sb.Append(themeCss);
            sb.Append("[data-theme='dark'] body{background:#1e1e1e;color:#e8eaed}");
            sb.Append("[data-theme='dark'] th, [data-theme='dark'] .row-idx{background:#2d2e30;color:#9aa0a6;border-color:#3c4043}");
            sb.Append("[data-theme='dark'] td{border-color:#3c4043;background:#1e1e1e}");
            sb.Append("[data-theme='dark'] .hdr{background:#202124;border-color:#3c4043}");
            sb.Append("</style></head><body>");
            sb.Append("<div class='hdr'>");
            sb.Append($"<span>工作表: {WebUtility.HtmlEncode(sheetName)}</span>");
            sb.Append($"<span class='meta'>行数: {rows.Count} | 列数: {(rows.Count > 0 ? rows.Max(r => r.Count) : 0)}</span>");
            sb.Append("</div>");
            sb.Append("<div style='overflow:auto; height:calc(100vh - 45px)'>");
            sb.Append("<table>");
            if (rows.Count > 0)
            {
                sb.Append("<thead><tr>");
                sb.Append("<th class='row-idx'>#</th>"); // Corner cell
                var maxCols = rows.Max(r => r.Count);
                for (int c = 0; c < maxCols; c++) sb.Append($"<th style='width:100px'>{IndexToCol(c)}</th>");
                sb.Append("</tr></thead>");
            }
            sb.Append("<tbody>");
            for (int i = 0; i < rows.Count; i++)
            {
                var row = rows[i];
                sb.Append("<tr>");
                sb.Append($"<td class='row-idx'>{i + 1}</td>");
                foreach (var cell in row)
                {
                    var text = WebUtility.HtmlEncode(cell ?? "");
                    sb.Append($"<td title='{text}'>{text}</td>");
                }
                sb.Append("</tr>");
            }
            sb.Append("</tbody></table></div></body></html>");
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
                        .FirstOrDefault(x => x.Attribute(XName.Get("id", "http://schemas.openxmlformats.org/officeDocument/2006/relationships"))?.Value == rId);
                    if (sheet != null) return sheet.Attribute("name")?.Value;
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
                    if (t == "s") { if (int.TryParse(v, out var idx) && idx >= 0 && idx < shared.Count) val = shared[idx]; else val = v; }
                    else if (t == "b") val = v == "1" ? "TRUE" : "FALSE";
                    else val = v ?? string.Empty;
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
            foreach (var ch in letters) index = index * 26 + (ch - 'A' + 1);
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

        public static string GetUniqueFilePath(string filePath)
        {
            if (!File.Exists(filePath)) return filePath;
            string directory = Path.GetDirectoryName(filePath);
            string baseName = Path.GetFileNameWithoutExtension(filePath);
            string ext = Path.GetExtension(filePath);
            int counter = 1;
            string newPath;
            do { newPath = Path.Combine(directory, $"{baseName}({counter}){ext}"); counter++; } while (File.Exists(newPath));
            return newPath;
        }

        public static bool ConvertXlsToXlsx(string xlsPath, string xlsxPath, out string errorMessage)
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
                    try { excelApp.Visible = false; } catch { }
                    excelApp.DisplayAlerts = false;
                    dynamic workbook = excelApp.Workbooks.Open(xlsPath, ReadOnly: true);
                    workbook.SaveAs(xlsxPath, 51);
                    workbook.Close(false);
                    return true;
                }
                finally
                {
                    try { excelApp.Quit(); } catch { }
                    try { Marshal.ReleaseComObject(excelApp); } catch { }
                }
            }
            catch (Exception ex)
            {
                errorMessage = $"转换失败: {ex.Message}";
                return false;
            }
        }
    }
}

