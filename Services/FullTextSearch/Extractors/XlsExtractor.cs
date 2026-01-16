using System;
using System.IO;
using System.Text;
using NPOI.HSSF.UserModel;
using NPOI.SS.UserModel;

namespace YiboFile.Services.FullTextSearch.Extractors
{
    /// <summary>
    /// XLS (旧版 Excel) 文件文本提取器 (使用 NPOI)
    /// </summary>
    public class XlsExtractor : IContentExtractor
    {
        public string[] SupportedExtensions => new[] { ".xls" };

        public bool CanExtract(string extension)
        {
            if (string.IsNullOrEmpty(extension)) return false;
            var ext = extension.ToLowerInvariant();
            if (!ext.StartsWith(".")) ext = "." + ext;
            return ext == ".xls";
        }

        public string ExtractText(string filePath)
        {
            try
            {
                if (!File.Exists(filePath)) return string.Empty;

                var sb = new StringBuilder();

                using (var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    var workbook = new HSSFWorkbook(fs);

                    for (int i = 0; i < workbook.NumberOfSheets; i++)
                    {
                        var sheet = workbook.GetSheetAt(i);
                        if (sheet == null) continue;

                        for (int rowIndex = sheet.FirstRowNum; rowIndex <= sheet.LastRowNum; rowIndex++)
                        {
                            var row = sheet.GetRow(rowIndex);
                            if (row == null) continue;

                            for (int colIndex = row.FirstCellNum; colIndex < row.LastCellNum; colIndex++)
                            {
                                var cell = row.GetCell(colIndex);
                                if (cell == null) continue;

                                var value = GetCellValue(cell);
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
                System.Diagnostics.Debug.WriteLine($"[XlsExtractor] Error extracting {filePath}: {ex.Message}");
                return string.Empty;
            }
        }

        private string GetCellValue(ICell cell)
        {
            switch (cell.CellType)
            {
                case CellType.String:
                    return cell.StringCellValue;
                case CellType.Numeric:
                    return cell.NumericCellValue.ToString();
                case CellType.Boolean:
                    return cell.BooleanCellValue.ToString();
                case CellType.Formula:
                    try { return cell.StringCellValue; }
                    catch { return cell.NumericCellValue.ToString(); }
                default:
                    return string.Empty;
            }
        }
    }
}

