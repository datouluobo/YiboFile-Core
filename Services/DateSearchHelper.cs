using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace OoiMRR.Services
{
    /// <summary>
    /// 日期搜索辅助类
    /// 支持从文件名中提取和匹配日期格式
    /// </summary>
    public static class DateSearchHelper
    {
        // 支持的日期格式模式（完整年份和短年份）
        private static readonly string[] DatePatterns = new[]
        {
            // 完整年份格式
            @"(\d{4})(\d{2})(\d{2})",                    // 20251201
            @"(\d{4})\.(\d{1,2})\.(\d{1,2})",          // 2025.12.01 或 2025.12.1
            @"(\d{4})-(\d{1,2})-(\d{1,2})",            // 2025-12-01 或 2025-12-1
            @"(\d{4})/(\d{1,2})/(\d{1,2})",             // 2025/12/01 或 2025/12/1
            
            // 短年份格式（2位年份，自动推断为20xx）
            @"(\d{2})(\d{2})(\d{2})",                    // 251201 (25年12月01日)
            @"(\d{2})\.(\d{1,2})\.(\d{1,2})",          // 25.12.01 或 25.12.1
            @"(\d{2})-(\d{1,2})-(\d{1,2})",            // 25-12-01 或 25-12-1
            @"(\d{2})/(\d{1,2})/(\d{1,2})"              // 25/12/01 或 25/12/1
        };

        /// <summary>
        /// 解析日期字符串，支持多种格式（完整年份和短年份）
        /// </summary>
        public static DateTime? ParseDate(string dateStr)
        {
            if (string.IsNullOrWhiteSpace(dateStr))
                return null;

            // 尝试直接解析标准格式
            if (DateTime.TryParseExact(dateStr, "yyyyMMdd", null, DateTimeStyles.None, out var dt1))
                return dt1;

            // 尝试解析带分隔符的格式
            foreach (var pattern in DatePatterns)
            {
                var match = Regex.Match(dateStr, pattern);
                if (match.Success && match.Groups.Count >= 4)
                {
                    if (int.TryParse(match.Groups[1].Value, out int year) &&
                        int.TryParse(match.Groups[2].Value, out int month) &&
                        int.TryParse(match.Groups[3].Value, out int day))
                    {
                        // 如果是2位年份，推断为20xx年
                        if (year < 100)
                        {
                            year += 2000;
                        }

                        try
                        {
                            return new DateTime(year, month, day);
                        }
                        catch { }
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// 解析简化的日期范围格式（如 20251201-1210 表示 20251201-20251210）
        /// </summary>
        private static (DateTime? start, DateTime? end) ParseSimplifiedRange(string startStr, string endStr)
        {
            var startDate = ParseDate(startStr);
            if (!startDate.HasValue)
                return (null, null);

            var endDate = ParseDate(endStr);
            if (endDate.HasValue)
                return (startDate, endDate);

            // 如果 endStr 无法解析为完整日期，尝试作为简化格式
            // 例如：20251201-1210 可能是 20251201-20251210 的简写
            if (startDate.HasValue && !string.IsNullOrWhiteSpace(endStr))
            {
                // 移除所有非数字字符，只保留数字
                string endDigits = Regex.Replace(endStr, @"[^\d]", "");
                
                if (endDigits.Length >= 4 && Regex.IsMatch(endDigits, @"^\d{4}$"))
                {
                    // 4位数字，可能是 MMDD（如 1210 表示 12月10日）
                    if (endDigits.Length >= 4 &&
                        int.TryParse(endDigits.Substring(0, 2), out int month) &&
                        int.TryParse(endDigits.Substring(2, 2), out int day))
                    {
                        try
                        {
                            var year = startDate.Value.Year;
                            var simplifiedEnd = new DateTime(year, month, day);
                            // 如果结束日期小于开始日期，可能是跨年了
                            if (simplifiedEnd < startDate.Value)
                            {
                                simplifiedEnd = simplifiedEnd.AddYears(1);
                            }
                            return (startDate, simplifiedEnd);
                        }
                        catch { }
                    }
                }
                else if (endDigits.Length >= 6 && Regex.IsMatch(endDigits, @"^\d{6}$"))
                {
                    // 6位数字，可能是 YYMMDD（如 251210 表示 2025年12月10日）
                    if (endDigits.Length >= 6 &&
                        int.TryParse(endDigits.Substring(0, 2), out int year) &&
                        int.TryParse(endDigits.Substring(2, 2), out int month) &&
                        int.TryParse(endDigits.Substring(4, 2), out int day))
                    {
                        try
                        {
                            // 如果是2位年份，推断为20xx年
                            if (year < 100)
                            {
                                year += 2000;
                            }
                            var simplifiedEnd = new DateTime(year, month, day);
                            return (startDate, simplifiedEnd);
                        }
                        catch { }
                    }
                }
                else if (Regex.IsMatch(endDigits, @"^\d{2}$"))
                {
                    // 2位数字，可能是 DD（同月）
                    if (int.TryParse(endDigits, out int day))
                    {
                        try
                        {
                            var simplifiedEnd = new DateTime(startDate.Value.Year, startDate.Value.Month, day);
                            return (startDate, simplifiedEnd);
                        }
                        catch { }
                    }
                }
            }

            return (startDate, null);
        }

        /// <summary>
        /// 从搜索文本中提取日期信息
        /// </summary>
        public static DateSearchInfo ExtractDateInfo(string searchText)
        {
            if (string.IsNullOrWhiteSpace(searchText))
                return null;

            // 检查是否包含日期范围（使用 - 或 ~ 分隔）
            // 支持完整格式：20251201-20251231 或简化格式：20251201-1210
            var rangeMatch = Regex.Match(searchText, @"(\d{4,8}[\d\.\-/]{0,6})\s*[-~]\s*(\d{2,8}[\d\.\-/]{0,6})");
            if (rangeMatch.Success)
            {
                var startStr = rangeMatch.Groups[1].Value.Trim();
                var endStr = rangeMatch.Groups[2].Value.Trim();

                // 尝试解析为完整日期范围
                var startDate = ParseDate(startStr);
                var endDate = ParseDate(endStr);

                if (startDate.HasValue && endDate.HasValue)
                {
                    return new DateSearchInfo
                    {
                        IsDateSearch = true,
                        IsRange = true,
                        StartDate = startDate.Value,
                        EndDate = endDate.Value,
                        OriginalText = searchText,
                        RemainingKeyword = searchText.Replace(rangeMatch.Value, "").Trim()
                    };
                }

                // 尝试解析为简化格式（如 20251201-1210）
                var (simplifiedStart, simplifiedEnd) = ParseSimplifiedRange(startStr, endStr);
                if (simplifiedStart.HasValue && simplifiedEnd.HasValue)
                {
                    return new DateSearchInfo
                    {
                        IsDateSearch = true,
                        IsRange = true,
                        StartDate = simplifiedStart.Value,
                        EndDate = simplifiedEnd.Value,
                        OriginalText = searchText,
                        RemainingKeyword = searchText.Replace(rangeMatch.Value, "").Trim()
                    };
                }
            }

            // 检查是否包含单个日期
            // 优先匹配完整年份格式（8位数字），避免误匹配
            foreach (var pattern in DatePatterns)
            {
                var match = Regex.Match(searchText, pattern);
                if (match.Success)
                {
                    var date = ParseDate(match.Value);
                    if (date.HasValue)
                    {
                        // 验证日期是否合理（年份在1900-2100之间）
                        if (date.Value.Year >= 1900 && date.Value.Year <= 2100)
                        {
                            return new DateSearchInfo
                            {
                                IsDateSearch = true,
                                IsRange = false,
                                StartDate = date.Value,
                                EndDate = date.Value,
                                OriginalText = searchText,
                                RemainingKeyword = searchText.Replace(match.Value, "").Trim()
                            };
                        }
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// 检查文件名是否包含指定日期范围内的日期
        /// </summary>
        public static bool MatchesDateRange(string fileName, DateTime startDate, DateTime endDate)
        {
            if (string.IsNullOrWhiteSpace(fileName))
                return false;

            // 从文件名中提取所有可能的日期
            var datesInFileName = ExtractDatesFromFileName(fileName);
            
            foreach (var date in datesInFileName)
            {
                if (date >= startDate.Date && date <= endDate.Date)
                    return true;
            }

            return false;
        }

        /// <summary>
        /// 从文件名中提取所有日期
        /// </summary>
        private static List<DateTime> ExtractDatesFromFileName(string fileName)
        {
            var dates = new List<DateTime>();

            foreach (var pattern in DatePatterns)
            {
                var matches = Regex.Matches(fileName, pattern);
                foreach (Match match in matches)
                {
                    if (match.Groups.Count >= 4)
                    {
                        if (int.TryParse(match.Groups[1].Value, out int year) &&
                            int.TryParse(match.Groups[2].Value, out int month) &&
                            int.TryParse(match.Groups[3].Value, out int day))
                        {
                            // 如果是2位年份，推断为20xx年
                            if (year < 100)
                            {
                                year += 2000;
                            }

                            try
                            {
                                var date = new DateTime(year, month, day);
                                dates.Add(date);
                            }
                            catch { }
                        }
                    }
                }
            }

            return dates;
        }

        /// <summary>
        /// 生成日期匹配的正则表达式模式（用于 Everything 搜索优化）
        /// </summary>
        public static string GenerateDatePattern(DateTime date)
        {
            // 生成多种可能的日期格式模式
            var patterns = new List<string>
            {
                date.ToString("yyyyMMdd"),           // 20251201
                date.ToString("yyMMdd"),            // 251201
                date.ToString("yyyy.MM.dd"),        // 2025.12.01
                date.ToString("yy.MM.dd"),          // 25.12.01
                date.ToString("yyyy.M.dd"),          // 2025.12.1
                date.ToString("yy.M.dd"),           // 25.12.1
                date.ToString("yyyy-MM-dd"),         // 2025-12-01
                date.ToString("yy-MM-dd"),          // 25-12-01
                date.ToString("yyyy-M-dd"),          // 2025-12-1
                date.ToString("yy-M-dd"),           // 25-12-1
                date.ToString("yyyy/MM/dd"),         // 2025/12/01
                date.ToString("yy/MM/dd"),          // 25/12/01
                date.ToString("yyyy/M/dd")           // 2025/12/1
            };

            return string.Join("|", patterns.Distinct());
        }
    }

    /// <summary>
    /// 日期搜索信息
    /// </summary>
    public class DateSearchInfo
    {
        public bool IsDateSearch { get; set; }
        public bool IsRange { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public string OriginalText { get; set; }
        public string RemainingKeyword { get; set; }
    }
}

