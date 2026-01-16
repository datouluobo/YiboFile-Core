using System;
using System.IO;
using System.Linq;
using System.Text;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;

namespace YiboFile.Services.FullTextSearch.Extractors
{
    /// <summary>
    /// XLSX 文件文本提取器 (使用 OpenXML)
    /// </summary>
    public class XlsxExtractor : IContentExtractor
    {
        public string[] SupportedExtensions => new[] { ".xlsx" };

        public bool CanExtract(string extension)
        {
            if (string.IsNullOrEmpty(extension)) return false;
            var ext = extension.ToLowerInvariant();
            if (!ext.StartsWith(".")) ext = "." + ext;
            return ext == ".xlsx";
        }

        public string ExtractText(string filePath)
        {
            try
            {
                if (!File.Exists(filePath)) return string.Empty;

                var sb = new StringBuilder();

                using (var doc = SpreadsheetDocument.Open(filePath, false))
                {
                    var workbookPart = doc.WorkbookPart;
                    if (workbookPart == null) return string.Empty;

                    var sharedStrings = workbookPart.SharedStringTablePart?.SharedStringTable;

                    foreach (var worksheetPart in workbookPart.WorksheetParts)
                    {
                        var sheetData = worksheetPart.Worksheet?.GetFirstChild<SheetData>();
                        if (sheetData == null) continue;

                        foreach (var row in sheetData.Elements<Row>())
                        {
                            foreach (var cell in row.Elements<Cell>())
                            {
                                var value = GetCellValue(cell, sharedStrings);
                                if (!string.IsNullOrWhiteSpace(value))
                                {
                                    sb.Append(value).Append(" ");
                                }
                            }
                            sb.AppendLine();
                        }
                    }
                }

                return sb.ToString();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[XlsxExtractor] Error extracting {filePath}: {ex.Message}");
                return string.Empty;
            }
        }

        private string GetCellValue(Cell cell, SharedStringTable sharedStrings)
        {
            if (cell.CellValue == null) return string.Empty;

            var value = cell.CellValue.InnerText;

            // 如果是共享字符串引用
            if (cell.DataType != null && cell.DataType.Value == CellValues.SharedString)
            {
                if (int.TryParse(value, out int index) && sharedStrings != null)
                {
                    var item = sharedStrings.ElementAt(index);
                    return item?.InnerText ?? string.Empty;
                }
            }

            return value;
        }
    }
}

