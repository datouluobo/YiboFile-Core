using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Data.Sqlite;

namespace YiboFile.Services.FullTextSearch
{
    /// <summary>
    /// 全文搜索索引记录
    /// </summary>
    public class FtsDocument
    {
        public long Id { get; set; }
        public string Path { get; set; }
        public string FileName { get; set; }
        public string Content { get; set; }
        public DateTime ModifiedTime { get; set; }
    }

    /// <summary>
    /// 全文搜索结果
    /// </summary>
    public class FtsSearchResult
    {
        public string Path { get; set; }
        public string FileName { get; set; }
        public string Snippet { get; set; }
        public double Rank { get; set; }
    }

    /// <summary>
    /// SQLite FTS5 全文搜索索引服务
    /// </summary>
    public class FtsIndexService : IDisposable
    {
        private readonly string _dbPath;
        private SqliteConnection _connection;
        private readonly ContentExtractorManager _extractorManager;
        private bool _disposed;

        /// <summary>
        /// 获取当前索引数据库路径
        /// </summary>
        public string IndexDbPath => _dbPath;

        public FtsIndexService(string dbPath = null)
        {
            _dbPath = dbPath ?? GetDefaultDbPath();
            _extractorManager = new ContentExtractorManager();
            EnsureDatabase();
        }

        private string GetDefaultDbPath()
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var dir = Path.Combine(appData, "YiboFile", "Data");
            Directory.CreateDirectory(dir);
            return Path.Combine(dir, "fts_index.db");
        }

        private void EnsureDatabase()
        {
            var connectionString = new SqliteConnectionStringBuilder
            {
                DataSource = _dbPath,
                Mode = SqliteOpenMode.ReadWriteCreate
            }.ToString();

            _connection = new SqliteConnection(connectionString);
            _connection.Open();

            // 创建 FTS5 虚拟表
            var createTableSql = @"
                CREATE VIRTUAL TABLE IF NOT EXISTS file_content USING fts5(
                    path,
                    filename,
                    content,
                    modified_time UNINDEXED,
                    tokenize='unicode61'
                );

                CREATE TABLE IF NOT EXISTS index_meta (
                    path TEXT PRIMARY KEY,
                    modified_time TEXT,
                    indexed_time TEXT
                );
            ";

            using var cmd = new SqliteCommand(createTableSql, _connection);
            cmd.ExecuteNonQuery();

            // Enable WAL mode for better concurrency and performance
            using var walCmd = new SqliteCommand("PRAGMA journal_mode=WAL;", _connection);
            walCmd.ExecuteNonQuery();
        }

        private SqliteTransaction _currentTransaction;

        public void BeginTransaction()
        {
            if (_currentTransaction == null)
            {
                _currentTransaction = _connection.BeginTransaction();
            }
        }

        public void CommitTransaction()
        {
            if (_currentTransaction != null)
            {
                _currentTransaction.Commit();
                _currentTransaction.Dispose();
                _currentTransaction = null;
            }
        }

        /// <summary>
        /// 索引单个文件
        /// </summary>
        public bool IndexFile(string filePath)
        {
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
                return false;

            if (!_extractorManager.CanExtract(filePath))
                return false;

            try
            {
                var fileInfo = new System.IO.FileInfo(filePath);
                var modifiedTime = fileInfo.LastWriteTimeUtc;

                // 检查是否需要重新索引
                if (!NeedsReindex(filePath, modifiedTime))
                    return true;

                // 提取文本
                var content = _extractorManager.ExtractText(filePath);
                if (string.IsNullOrWhiteSpace(content))
                    return false;

                // 删除旧记录 (DeleteDocument handles transaction internally if we update it)
                DeleteDocument(filePath);

                // 插入新记录
                var insertSql = @"
                    INSERT INTO file_content (path, filename, content, modified_time)
                    VALUES (@path, @filename, @content, @modifiedTime);

                    INSERT OR REPLACE INTO index_meta (path, modified_time, indexed_time)
                    VALUES (@path, @modifiedTime, @indexedTime);
                ";

                using var cmd = new SqliteCommand(insertSql, _connection);
                cmd.Transaction = _currentTransaction;
                cmd.Parameters.AddWithValue("@path", filePath);
                cmd.Parameters.AddWithValue("@filename", Path.GetFileName(filePath));
                cmd.Parameters.AddWithValue("@content", content);
                cmd.Parameters.AddWithValue("@modifiedTime", modifiedTime.ToString("o"));
                cmd.Parameters.AddWithValue("@indexedTime", DateTime.UtcNow.ToString("o"));
                cmd.ExecuteNonQuery();

                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[FtsIndexService] Error indexing {filePath}: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 检查文件是否需要重新索引
        /// </summary>
        private bool NeedsReindex(string filePath, DateTime modifiedTime)
        {
            var sql = "SELECT modified_time FROM index_meta WHERE path = @path";
            using var cmd = new SqliteCommand(sql, _connection);
            cmd.Transaction = _currentTransaction;
            cmd.Parameters.AddWithValue("@path", filePath);

            var result = cmd.ExecuteScalar();
            if (result == null || result == DBNull.Value)
                return true;

            if (DateTime.TryParse(result.ToString(), out var indexedModTime))
            {
                return modifiedTime > indexedModTime;
            }
            return true;
        }

        /// <summary>
        /// 删除文档索引
        /// </summary>
        public void DeleteDocument(string filePath)
        {
            var sql = @"
                DELETE FROM file_content WHERE path = @path;
                DELETE FROM index_meta WHERE path = @path;
            ";
            using var cmd = new SqliteCommand(sql, _connection);
            cmd.Transaction = _currentTransaction;
            cmd.Parameters.AddWithValue("@path", filePath);
            cmd.ExecuteNonQuery();
        }

        /// <summary>
        /// 搜索文件内容
        /// </summary>
        public List<FtsSearchResult> Search(string keyword, int maxResults = 100)
        {
            var results = new List<FtsSearchResult>();

            if (string.IsNullOrWhiteSpace(keyword))
                return results;

            try
            {
                // 转义 FTS5 特殊字符
                var escapedKeyword = EscapeFts5Query(keyword);

                var sql = @"
                    SELECT path, filename, snippet(file_content, 2, '<b>', '</b>', '...', 32) as snippet,
                           bm25(file_content) as rank
                    FROM file_content
                    WHERE file_content MATCH @keyword
                    ORDER BY rank
                    LIMIT @maxResults
                ";

                using var cmd = new SqliteCommand(sql, _connection);
                cmd.Parameters.AddWithValue("@keyword", escapedKeyword);
                cmd.Parameters.AddWithValue("@maxResults", maxResults);

                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    results.Add(new FtsSearchResult
                    {
                        Path = reader.GetString(0),
                        FileName = reader.GetString(1),
                        Snippet = reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
                        Rank = reader.GetDouble(3)
                    });
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[FtsIndexService] Search error: {ex.Message}");
            }

            return results;
        }

        /// <summary>
        /// 转义 FTS5 查询中的特殊字符
        /// </summary>
        private string EscapeFts5Query(string query)
        {
            // 对于简单查询，用引号包裹
            if (!query.Contains("\""))
            {
                return $"\"{query}\"";
            }
            // 如果已经包含引号，转义内部引号
            return $"\"{query.Replace("\"", "\"\"")}\"";
        }

        /// <summary>
        /// 获取已索引文件数量
        /// </summary>
        public int GetIndexedCount()
        {
            var sql = "SELECT COUNT(*) FROM index_meta";
            using var cmd = new SqliteCommand(sql, _connection);
            cmd.Transaction = _currentTransaction;
            var result = cmd.ExecuteScalar();
            return Convert.ToInt32(result);
        }

        /// <summary>
        /// 清空索引
        /// </summary>
        public void ClearIndex()
        {
            var sql = @"
                DELETE FROM file_content;
                DELETE FROM index_meta;
            ";
            using var cmd = new SqliteCommand(sql, _connection);
            cmd.Transaction = _currentTransaction;
            cmd.ExecuteNonQuery();
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _connection?.Close();
                _connection?.Dispose();
                _disposed = true;
            }
        }
    }
}

