using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;

namespace YiboFile.Services.Data.Repositories
{
    /// <summary>
    /// SQLite 实现的文件备注存储库
    /// 使用 FTS5 全文搜索支持高效的备注搜索
    /// </summary>
    public class SqliteNotesRepository : INotesRepository
    {
        private readonly string _connectionString;

        /// <summary>
        /// 初始化存储库
        /// </summary>
        /// <param name="connectionString">数据库连接字符串（可选，默认使用配置路径）</param>
        public SqliteNotesRepository(string connectionString = null)
        {
            if (string.IsNullOrEmpty(connectionString))
            {
                var dbPath = ConfigManager.GetDataFilePath();
                Directory.CreateDirectory(Path.GetDirectoryName(dbPath));
                _connectionString = $"Data Source={dbPath}";
            }
            else
            {
                _connectionString = connectionString;
            }

            EnsureTablesCreated();
        }

        /// <summary>
        /// 确保必要的数据表已创建
        /// </summary>
        private void EnsureTablesCreated()
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            // 主表
            using var cmd1 = connection.CreateCommand();
            cmd1.CommandText = @"
                CREATE TABLE IF NOT EXISTS FileNotes (
                    FilePath TEXT PRIMARY KEY,
                    Notes TEXT NOT NULL,
                    UpdatedAt DATETIME DEFAULT CURRENT_TIMESTAMP
                )";
            cmd1.ExecuteNonQuery();

            // FTS 全文搜索表
            using var cmd2 = connection.CreateCommand();
            cmd2.CommandText = @"
                CREATE VIRTUAL TABLE IF NOT EXISTS FileNotesFts 
                USING fts5(FilePath, Notes, content='FileNotes', content_rowid='rowid')";
            try
            {
                cmd2.ExecuteNonQuery();
            }
            catch (SqliteException)
            {
                // FTS 表可能已存在或不支持，忽略
            }
        }

        #region 基本 CRUD 操作

        public string GetNotes(string filePath)
        {
            if (string.IsNullOrEmpty(filePath)) return string.Empty;

            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            using var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT Notes FROM FileNotes WHERE FilePath = @filePath";
            cmd.Parameters.AddWithValue("@filePath", filePath);

            var result = cmd.ExecuteScalar();
            return result?.ToString() ?? string.Empty;
        }

        public async Task<string> GetNotesAsync(string filePath)
        {
            return await Task.Run(() => GetNotes(filePath));
        }

        public void SetNotes(string filePath, string notes)
        {
            if (string.IsNullOrEmpty(filePath)) return;

            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            using var transaction = connection.BeginTransaction();
            try
            {
                // 更新主表
                using var cmd = connection.CreateCommand();
                cmd.CommandText = @"
                    INSERT OR REPLACE INTO FileNotes (FilePath, Notes, UpdatedAt) 
                    VALUES (@filePath, @notes, CURRENT_TIMESTAMP)";
                cmd.Parameters.AddWithValue("@filePath", filePath);
                cmd.Parameters.AddWithValue("@notes", notes ?? string.Empty);
                cmd.ExecuteNonQuery();

                // 更新 FTS 表：先删除，再插入
                UpdateFtsEntry(connection, filePath, notes);

                transaction.Commit();
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
        }

        public async Task SetNotesAsync(string filePath, string notes)
        {
            await Task.Run(() => SetNotes(filePath, notes));
        }

        public void DeleteNotes(string filePath)
        {
            if (string.IsNullOrEmpty(filePath)) return;

            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            using var transaction = connection.BeginTransaction();
            try
            {
                // 删除主表记录
                using var cmd = connection.CreateCommand();
                cmd.CommandText = "DELETE FROM FileNotes WHERE FilePath = @filePath";
                cmd.Parameters.AddWithValue("@filePath", filePath);
                cmd.ExecuteNonQuery();

                // 删除 FTS 表记录
                using var ftsCmd = connection.CreateCommand();
                ftsCmd.CommandText = "DELETE FROM FileNotesFts WHERE FilePath = @filePath";
                ftsCmd.Parameters.AddWithValue("@filePath", filePath);
                ftsCmd.ExecuteNonQuery();

                transaction.Commit();
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
        }

        public async Task DeleteNotesAsync(string filePath)
        {
            await Task.Run(() => DeleteNotes(filePath));
        }

        #endregion

        #region 搜索功能

        public List<string> SearchByNotes(string searchText)
        {
            var results = new List<string>();
            if (string.IsNullOrWhiteSpace(searchText)) return results;

            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            // 同步 FTS 表
            SyncFtsTable(connection);

            // 尝试 FTS 搜索
            try
            {
                var ftsQuery = BuildFtsQuery(searchText.Trim());
                using var cmd = connection.CreateCommand();
                cmd.CommandText = "SELECT FilePath FROM FileNotesFts WHERE Notes MATCH @query";
                cmd.Parameters.AddWithValue("@query", ftsQuery);

                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    var path = reader.GetString(0);
                    if (File.Exists(path) || Directory.Exists(path))
                    {
                        results.Add(path);
                    }
                }
            }
            catch (SqliteException)
            {
                // FTS 搜索失败，回退到 LIKE 查询
            }

            // 如果 FTS 无结果，使用 LIKE 回退
            if (results.Count == 0)
            {
                using var likeCmd = connection.CreateCommand();
                likeCmd.CommandText = "SELECT FilePath FROM FileNotes WHERE Notes LIKE @pattern";
                likeCmd.Parameters.AddWithValue("@pattern", $"%{searchText.Trim()}%");

                using var reader = likeCmd.ExecuteReader();
                while (reader.Read())
                {
                    var path = reader.GetString(0);
                    if (File.Exists(path) || Directory.Exists(path))
                    {
                        results.Add(path);
                    }
                }
            }

            return results.Distinct().ToList();
        }

        public async Task<List<string>> SearchByNotesAsync(string searchText)
        {
            return await Task.Run(() => SearchByNotes(searchText));
        }

        #endregion

        #region 批量操作

        public bool HasNotes(string filePath)
        {
            if (string.IsNullOrEmpty(filePath)) return false;

            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            using var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT 1 FROM FileNotes WHERE FilePath = @filePath AND Notes != '' LIMIT 1";
            cmd.Parameters.AddWithValue("@filePath", filePath);

            return cmd.ExecuteScalar() != null;
        }

        public List<string> GetAllNotedFiles()
        {
            var results = new List<string>();

            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            using var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT FilePath FROM FileNotes WHERE Notes IS NOT NULL AND Notes != ''";

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                results.Add(reader.GetString(0));
            }

            return results;
        }

        public Dictionary<string, string> GetNotesBatch(IEnumerable<string> filePaths)
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var pathList = filePaths?.ToList();
            if (pathList == null || pathList.Count == 0) return result;

            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            // 分批查询，避免参数过多
            const int batchSize = 100;
            for (int i = 0; i < pathList.Count; i += batchSize)
            {
                var batch = pathList.Skip(i).Take(batchSize).ToList();
                var paramNames = batch.Select((_, idx) => $"@p{idx}").ToList();

                using var cmd = connection.CreateCommand();
                cmd.CommandText = $"SELECT FilePath, Notes FROM FileNotes WHERE FilePath IN ({string.Join(",", paramNames)})";

                for (int j = 0; j < batch.Count; j++)
                {
                    cmd.Parameters.AddWithValue($"@p{j}", batch[j]);
                }

                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    var path = reader.GetString(0);
                    var notes = reader.IsDBNull(1) ? string.Empty : reader.GetString(1);
                    result[path] = notes;
                }
            }

            return result;
        }

        public async Task<Dictionary<string, string>> GetNotesBatchAsync(IEnumerable<string> filePaths)
        {
            return await Task.Run(() => GetNotesBatch(filePaths));
        }

        #endregion

        #region 私有辅助方法

        private void UpdateFtsEntry(SqliteConnection connection, string filePath, string notes)
        {
            try
            {
                // 删除旧记录
                using var deleteCmd = connection.CreateCommand();
                deleteCmd.CommandText = "DELETE FROM FileNotesFts WHERE FilePath = @filePath";
                deleteCmd.Parameters.AddWithValue("@filePath", filePath);
                deleteCmd.ExecuteNonQuery();

                // 插入新记录（如果备注非空）
                if (!string.IsNullOrEmpty(notes))
                {
                    using var insertCmd = connection.CreateCommand();
                    insertCmd.CommandText = "INSERT INTO FileNotesFts (FilePath, Notes) VALUES (@filePath, @notes)";
                    insertCmd.Parameters.AddWithValue("@filePath", filePath);
                    insertCmd.Parameters.AddWithValue("@notes", notes);
                    insertCmd.ExecuteNonQuery();
                }
            }
            catch (SqliteException)
            {
                // FTS 更新失败，不影响主表操作
            }
        }

        private void SyncFtsTable(SqliteConnection connection)
        {
            try
            {
                // 获取未同步的记录
                using var selectCmd = connection.CreateCommand();
                selectCmd.CommandText = @"
                    SELECT fn.FilePath, fn.Notes 
                    FROM FileNotes fn 
                    LEFT JOIN FileNotesFts fts ON fn.FilePath = fts.FilePath 
                    WHERE fts.FilePath IS NULL";

                var toSync = new List<(string Path, string Notes)>();
                using (var reader = selectCmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        toSync.Add((reader.GetString(0), reader.IsDBNull(1) ? "" : reader.GetString(1)));
                    }
                }

                foreach (var (path, notes) in toSync)
                {
                    using var insertCmd = connection.CreateCommand();
                    insertCmd.CommandText = "INSERT INTO FileNotesFts (FilePath, Notes) VALUES (@path, @notes)";
                    insertCmd.Parameters.AddWithValue("@path", path);
                    insertCmd.Parameters.AddWithValue("@notes", notes);
                    insertCmd.ExecuteNonQuery();
                }
            }
            catch (SqliteException)
            {
                // 同步失败，不影响主要搜索
            }
        }

        private string BuildFtsQuery(string input)
        {
            var tokens = input.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            if (tokens.Length == 0) return input;

            var parts = new List<string>();
            foreach (var token in tokens)
            {
                if (token.Contains("*") || token.Contains("?"))
                {
                    parts.Add(token);
                    continue;
                }

                // 纯数字需要用引号包裹
                if (token.All(char.IsDigit))
                {
                    parts.Add($"\"{token}\"*");
                    continue;
                }

                // ASCII 单词添加通配符
                bool hasAscii = token.Any(c => c <= 0x007F && (char.IsLetterOrDigit(c) || c == '_' || c == '-'));
                if (hasAscii)
                {
                    parts.Add(token + "*");
                }
                else
                {
                    // 中文等非 ASCII 字符逐字添加
                    foreach (var ch in token)
                    {
                        if (!char.IsWhiteSpace(ch))
                        {
                            parts.Add(char.IsDigit(ch) ? $"\"{ch}\"*" : $"{ch}*");
                        }
                    }
                }
            }

            return string.Join(" AND ", parts);
        }

        #endregion
    }
}
