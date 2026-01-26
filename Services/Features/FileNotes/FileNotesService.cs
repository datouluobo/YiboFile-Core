using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Data.Sqlite;

namespace YiboFile.Services.FileNotes
{
    /// <summary>
    /// 文件备注服务
    /// 负责文件备注的保存、读取和搜索
    /// </summary>
    public static class FileNotesService
    {
        private static string GetConnectionString()
        {
            var dbPath = ConfigManager.GetDataFilePath();
            Directory.CreateDirectory(Path.GetDirectoryName(dbPath));
            return $"Data Source={dbPath}";
        }

        /// <summary>
        /// 设置文件备注
        /// </summary>
        public static void SetFileNotes(string filePath, string notes)
        {
            try
            {
                using var connection = new SqliteConnection(GetConnectionString());
                connection.Open();
                using var command = connection.CreateCommand();
                command.CommandText = @"
                    INSERT OR REPLACE INTO FileNotes (FilePath, Notes, UpdatedAt) 
                    VALUES (@filePath, @notes, CURRENT_TIMESTAMP)";
                command.Parameters.AddWithValue("@filePath", filePath);
                command.Parameters.AddWithValue("@notes", notes);
                command.ExecuteNonQuery();

                // 更新 FTS 表：先删除旧记录（如果存在），再插入新记录
                using var deleteFts = connection.CreateCommand();
                deleteFts.CommandText = "DELETE FROM FileNotesFts WHERE FilePath = @filePath";
                deleteFts.Parameters.AddWithValue("@filePath", filePath);
                deleteFts.ExecuteNonQuery();

                using var insertFts = connection.CreateCommand();
                insertFts.CommandText = @"
                    INSERT INTO FileNotesFts (FilePath, Notes) VALUES (@filePath, @notes)";
                insertFts.Parameters.AddWithValue("@filePath", filePath);
                insertFts.Parameters.AddWithValue("@notes", notes);
                insertFts.ExecuteNonQuery();
            }
            catch (Exception)
            {
                throw;
            }
        }

        /// <summary>
        /// 异步保存备注，提升性能
        /// </summary>
        public static async System.Threading.Tasks.Task SetFileNotesAsync(string filePath, string notes)
        {
            await System.Threading.Tasks.Task.Run(() =>
            {
                SetFileNotes(filePath, notes);
            });
        }

        /// <summary>
        /// 获取文件备注
        /// </summary>
        public static string GetFileNotes(string filePath)
        {
            using var connection = new SqliteConnection(GetConnectionString());
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT Notes FROM FileNotes WHERE FilePath = @filePath";
            command.Parameters.AddWithValue("@filePath", filePath);
            var result = command.ExecuteScalar();
            return result?.ToString() ?? "";
        }

        /// <summary>
        /// 搜索包含指定文本的备注的文件路径
        /// </summary>
        public static List<string> SearchFilesByNotes(string searchText)
        {
            var results = new List<string>();
            try
            {
                // 确保 FTS 表已同步（在搜索前检查）
                try
                {
                    SyncFileNotesFts();
                }
                catch (Exception)
                {
                }

                using var connection = new SqliteConnection(GetConnectionString());
                connection.Open();
                var query = searchText?.Trim() ?? "";
                if (string.IsNullOrEmpty(query))
                {
                    return results;
                }
                // 首先尝试使用 FTS5 全文搜索
                try
                {
                    using var command = connection.CreateCommand();
                    var ftsQuery = BuildFtsWildcardQuery(query);
                    // 仅匹配 Notes 列，避免 FilePath 被误匹配
                    command.CommandText = "SELECT FilePath FROM FileNotesFts WHERE Notes MATCH @q";
                    command.Parameters.AddWithValue("@q", ftsQuery);
                    using var reader = command.ExecuteReader();
                    while (reader.Read())
                    {
                        var filePath = reader.GetString(0);
                        // 验证文件是否存在
                        if (File.Exists(filePath) || Directory.Exists(filePath))
                        {
                            results.Add(filePath);
                        }
                    }
                }
                catch (Exception)
                {
                }

                // 如果 FTS 搜索没有结果，使用 LIKE 查询作为回退
                if (results.Count == 0)
                {
                    using var likeCmd = connection.CreateCommand();
                    likeCmd.CommandText = "SELECT FilePath, Notes FROM FileNotes WHERE Notes LIKE @kw";
                    likeCmd.Parameters.AddWithValue("@kw", $"%{query}%");
                    using var likeReader = likeCmd.ExecuteReader();
                    int likeCount = 0;
                    while (likeReader.Read())
                    {
                        var p = likeReader.GetString(0);
                        var notes = likeReader.IsDBNull(1) ? "" : likeReader.GetString(1);
                        if (File.Exists(p) || Directory.Exists(p))
                        {
                            results.Add(p);
                            likeCount++;
                        }
                        else
                        {
                        }
                    }
                }

                // 去重并返回
                var distinctResults = results.Distinct().ToList();
                return distinctResults;
            }
            catch (Exception)
            {
                // 记录异常但不抛出，返回空列表
            }
            return results.Distinct().ToList();
        }

        /// <summary>
        /// 构建 FTS 通配符查询
        /// </summary>
        private static string BuildFtsWildcardQuery(string input)
        {
            // 支持空格分词；中文连续字符串按逐字符追加前缀匹配
            var tokens = input.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            if (tokens.Length == 0) return input;
            var parts = new List<string>();
            foreach (var t in tokens)
            {
                if (t.Contains("*") || t.Contains("?"))
                {
                    parts.Add(t);
                    continue;
                }

                // 检查是否为纯数字（可能被误认为是列名，需要转义）
                if (t.All(char.IsDigit))
                {
                    // 纯数字需要用引号包裹，避免被误认为是列名
                    parts.Add($"\"{t}\"*");
                    continue;
                }

                bool isAsciiWord = t.Any(ch => ch <= 0x007F && (char.IsLetterOrDigit(ch) || ch == '_' || ch == '-'));
                if (isAsciiWord)
                {
                    parts.Add(t + "*");
                }
                else
                {
                    foreach (var ch in t)
                    {
                        if (!char.IsWhiteSpace(ch))
                        {
                            // 数字字符也需要用引号包裹
                            if (char.IsDigit(ch))
                            {
                                parts.Add($"\"{ch}\"*");
                            }
                            else
                            {
                                parts.Add(ch + "*");
                            }
                        }
                    }
                }
            }
            return string.Join(" AND ", parts);
        }

        /// <summary>
        /// 同步 FileNotesFts 表，确保所有 FileNotes 表中的数据都在 FTS 表中
        /// </summary>
        private static void SyncFileNotesFts()
        {
            try
            {
                using var connection = new SqliteConnection(GetConnectionString());
                connection.Open();

                // 获取所有 FileNotes 表中的记录
                using var selectCmd = connection.CreateCommand();
                selectCmd.CommandText = "SELECT FilePath, Notes FROM FileNotes";
                var notesToSync = new List<(string FilePath, string Notes)>();
                using (var reader = selectCmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        notesToSync.Add((reader.GetString(0), reader.GetString(1)));
                    }
                }

                if (notesToSync.Count == 0)
                {
                    return;
                }

                // 检查 FTS 表中已存在的记录
                using var checkCmd = connection.CreateCommand();
                checkCmd.CommandText = "SELECT FilePath FROM FileNotesFts";
                var existingPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                using (var reader = checkCmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        existingPaths.Add(reader.GetString(0));
                    }
                }

                // 同步缺失的记录
                int syncedCount = 0;
                foreach (var (filePath, notes) in notesToSync)
                {
                    if (!existingPaths.Contains(filePath))
                    {
                        using var insertCmd = connection.CreateCommand();
                        insertCmd.CommandText = "INSERT INTO FileNotesFts (FilePath, Notes) VALUES (@filePath, @notes)";
                        insertCmd.Parameters.AddWithValue("@filePath", filePath);
                        insertCmd.Parameters.AddWithValue("@notes", notes);
                        insertCmd.ExecuteNonQuery();
                        syncedCount++;
                    }
                }
            }
            catch (Exception)
            {
                throw;
            }
        }
    }
}





















