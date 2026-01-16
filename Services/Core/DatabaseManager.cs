using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Data.Sqlite;

namespace YiboFile
{
    /// <summary>
    /// 数据库管理器
    /// </summary>
    public static class DatabaseManager
    {
        private static string _connectionString;

        /// <summary>
        /// 关闭所有数据库连接并释放池中的连接，以便能够替换数据库文件
        /// </summary>
        public static void Shutdown()
        {
            try
            {
                SqliteConnection.ClearAllPools();
                GC.Collect();
                GC.WaitForPendingFinalizers();
            }
            catch { }
        }

        public static void Initialize()
        {
            var dbPath = ConfigManager.GetDataFilePath();
            var dir = Path.GetDirectoryName(dbPath);
            if (!string.IsNullOrEmpty(dir))
            {
                Directory.CreateDirectory(dir);
            }
            _connectionString = $"Data Source={dbPath}";

            // 确保之前的连接被释放
            SqliteConnection.ClearAllPools();

            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            // 创建文件备注表
            var createFileNotesTable = @"
                CREATE TABLE IF NOT EXISTS FileNotes (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    FilePath TEXT NOT NULL UNIQUE,
                    Notes TEXT NOT NULL DEFAULT '',
                    CreatedAt DATETIME DEFAULT CURRENT_TIMESTAMP,
                    UpdatedAt DATETIME DEFAULT CURRENT_TIMESTAMP
                )";

            // 创建库表（只存储库信息）
            var createLibrariesTable = @"
                CREATE TABLE IF NOT EXISTS Libraries (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Name TEXT NOT NULL UNIQUE,
                    CreatedAt DATETIME DEFAULT CURRENT_TIMESTAMP
                )";

            // 创建库位置表（一个库可以有多个位置）
            var createLibraryPathsTable = @"
                CREATE TABLE IF NOT EXISTS LibraryPaths (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    LibraryId INTEGER NOT NULL,
                    Path TEXT NOT NULL,
                    DisplayName TEXT,
                    FOREIGN KEY (LibraryId) REFERENCES Libraries (Id) ON DELETE CASCADE,
                    UNIQUE(LibraryId, Path)
                )";

            using var command = connection.CreateCommand();
            // 标签相关的本地表已弃用（改为完全使用 TagTrain），不再创建

            command.CommandText = createFileNotesTable;
            command.ExecuteNonQuery();

            command.CommandText = createLibrariesTable;
            command.ExecuteNonQuery();

            command.CommandText = createLibraryPathsTable;
            command.ExecuteNonQuery();

            var createFileNotesFts = @"
                CREATE VIRTUAL TABLE IF NOT EXISTS FileNotesFts USING fts5(
                    FilePath,
                    Notes,
                    tokenize='unicode61 remove_diacritics 0',
                    prefix='1 2 3'
                )";
            command.CommandText = createFileNotesFts;
            command.ExecuteNonQuery();

            // 文件备注 FTS 表同步已移至 Services/FileNotes/FileNotesService.cs
            // 首次搜索时会自动同步

            // 创建收藏表
            var createFavoritesTable = @"
                CREATE TABLE IF NOT EXISTS Favorites (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Path TEXT NOT NULL UNIQUE,
                    DisplayName TEXT,
                    IsDirectory INTEGER NOT NULL DEFAULT 0,
                    CreatedAt DATETIME DEFAULT CURRENT_TIMESTAMP,
                    SortOrder INTEGER DEFAULT 0
                )";

            command.CommandText = createFavoritesTable;
            command.ExecuteNonQuery();

            // 创建文件夹大小缓存表
            var createFolderSizesTable = @"
                CREATE TABLE IF NOT EXISTS FolderSizes (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    FolderPath TEXT NOT NULL UNIQUE,
                    SizeBytes INTEGER NOT NULL,
                    LastModified DATETIME NOT NULL,
                    CalculatedAt DATETIME DEFAULT CURRENT_TIMESTAMP
                )";

            command.CommandText = createFolderSizesTable;
            command.ExecuteNonQuery();

            // 数据库迁移：如果旧的 Libraries 表有 Path 列，迁移到新结构
            try
            {
                var checkOldTable = "SELECT COUNT(*) FROM pragma_table_info('Libraries') WHERE name='Path'";
                command.CommandText = checkOldTable;
                var hasPathColumn = Convert.ToInt32(command.ExecuteScalar()) > 0;

                if (hasPathColumn)
                {
                    // 1. 先迁移旧数据到 LibraryPaths 表
                    var migrateData = @"
                        INSERT OR IGNORE INTO LibraryPaths (LibraryId, Path, DisplayName)
                        SELECT Id, Path, Name FROM Libraries WHERE Path IS NOT NULL AND Path != ''
                    ";
                    command.CommandText = migrateData;
                    command.ExecuteNonQuery();

                    // 2. 重建 Libraries 表以移除 Path 列
                    // 创建临时表
                    command.CommandText = @"
                        CREATE TABLE Libraries_new (
                            Id INTEGER PRIMARY KEY AUTOINCREMENT,
                            Name TEXT NOT NULL UNIQUE,
                            CreatedAt DATETIME DEFAULT CURRENT_TIMESTAMP
                        )";
                    command.ExecuteNonQuery();

                    // 复制数据到新表
                    command.CommandText = "INSERT INTO Libraries_new (Id, Name, CreatedAt) SELECT Id, Name, CreatedAt FROM Libraries";
                    command.ExecuteNonQuery();

                    // 删除旧表
                    command.CommandText = "DROP TABLE Libraries";
                    command.ExecuteNonQuery();

                    // 重命名新表
                    command.CommandText = "ALTER TABLE Libraries_new RENAME TO Libraries";
                    command.ExecuteNonQuery();
                }
            }
            catch
            {
                // 记录错误但不中断初始化
            }

            // 数据库迁移：添加 DisplayOrder 字段
            try
            {
                var checkOrderColumn = "SELECT COUNT(*) FROM pragma_table_info('Libraries') WHERE name='DisplayOrder'";
                command.CommandText = checkOrderColumn;
                var hasOrderColumn = Convert.ToInt32(command.ExecuteScalar()) > 0;

                if (!hasOrderColumn)
                {
                    // 添加 DisplayOrder 列
                    command.CommandText = "ALTER TABLE Libraries ADD COLUMN DisplayOrder INTEGER DEFAULT 0";
                    command.ExecuteNonQuery();

                    // 为现有库设置初始排序值（按Id）
                    command.CommandText = "UPDATE Libraries SET DisplayOrder = Id";
                    command.ExecuteNonQuery();
                }
            }
            catch
            {
                // 记录错误但不中断初始化
            }
        }

        // 本地标签相关 API 已弃用（改为完全使用 TagTrain），故移除

        // 文件备注功能已移至 Services/FileNotes/FileNotesService.cs

        public static int AddLibrary(string name)
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            // 先检查库是否已存在
            using var checkCommand = connection.CreateCommand();
            checkCommand.CommandText = "SELECT Id FROM Libraries WHERE Name = @name";
            checkCommand.Parameters.AddWithValue("@name", name);
            var existingId = checkCommand.ExecuteScalar();

            if (existingId != null && existingId != DBNull.Value)
            {
                // 库已存在，返回其ID（但返回负数表示已存在）
                return -Convert.ToInt32(existingId);
            }

            // 库不存在，创建新库
            // 获取当前最大的 DisplayOrder 值
            using var maxOrderCommand = connection.CreateCommand();
            maxOrderCommand.CommandText = "SELECT COALESCE(MAX(DisplayOrder), 0) FROM Libraries";
            var maxOrder = Convert.ToInt32(maxOrderCommand.ExecuteScalar());

            using var insertCommand = connection.CreateCommand();
            insertCommand.CommandText = "INSERT INTO Libraries (Name, DisplayOrder) VALUES (@name, @displayOrder); SELECT last_insert_rowid();";
            insertCommand.Parameters.AddWithValue("@name", name);
            insertCommand.Parameters.AddWithValue("@displayOrder", maxOrder + 1);
            var newId = insertCommand.ExecuteScalar();

            if (newId != null && newId != DBNull.Value)
            {
                return Convert.ToInt32(newId);
            }

            return 0;
        }

        public static void AddLibraryPath(int libraryId, string path, string displayName = null)
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "INSERT OR IGNORE INTO LibraryPaths (LibraryId, Path, DisplayName) VALUES (@libraryId, @path, @displayName)";
            command.Parameters.AddWithValue("@libraryId", libraryId);
            command.Parameters.AddWithValue("@path", path);
            command.Parameters.AddWithValue("@displayName", displayName ?? (object)DBNull.Value);
            command.ExecuteNonQuery();
        }

        public static void RemoveLibraryPath(int libraryId, string path)
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "DELETE FROM LibraryPaths WHERE LibraryId = @libraryId AND Path = @path";
            command.Parameters.AddWithValue("@libraryId", libraryId);
            command.Parameters.AddWithValue("@path", path);
            command.ExecuteNonQuery();
        }

        public static void UpdateLibraryPathDisplayName(int libraryId, string path, string displayName)
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "UPDATE LibraryPaths SET DisplayName = @displayName WHERE LibraryId = @libraryId AND Path = @path";
            command.Parameters.AddWithValue("@displayName", string.IsNullOrWhiteSpace(displayName) ? (object)DBNull.Value : displayName);
            command.Parameters.AddWithValue("@libraryId", libraryId);
            command.Parameters.AddWithValue("@path", path);
            command.ExecuteNonQuery();
        }

        public static void UpdateLibraryName(int libraryId, string newName)
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "UPDATE Libraries SET Name = @newName WHERE Id = @libraryId";
            command.Parameters.AddWithValue("@newName", newName);
            command.Parameters.AddWithValue("@libraryId", libraryId);
            command.ExecuteNonQuery();
        }

        public static void DeleteLibrary(int libraryId)
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "DELETE FROM Libraries WHERE Id = @libraryId";
            command.Parameters.AddWithValue("@libraryId", libraryId);
            command.ExecuteNonQuery();
        }

        public static List<Library> GetAllLibraries()
        {
            var libraries = new List<Library>();
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            // 获取所有库
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT Id, Name, DisplayOrder FROM Libraries ORDER BY DisplayOrder, Id";
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                var libraryId = reader.GetInt32(0);
                var libraryName = reader.GetString(1);
                var displayOrder = reader.IsDBNull(2) ? 0 : reader.GetInt32(2);

                // 获取该库的所有位置
                var paths = new List<string>();
                using var pathCommand = connection.CreateCommand();
                pathCommand.CommandText = "SELECT Path FROM LibraryPaths WHERE LibraryId = @libraryId ORDER BY Id";
                pathCommand.Parameters.AddWithValue("@libraryId", libraryId);
                using var pathReader = pathCommand.ExecuteReader();
                while (pathReader.Read())
                {
                    paths.Add(pathReader.GetString(0));
                }

                libraries.Add(new Library
                {
                    Id = libraryId,
                    Name = libraryName,
                    Paths = paths,
                    DisplayOrder = displayOrder
                });
            }

            return libraries;
        }

        public static void MoveLibraryUp(int libraryId)
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            // 获取当前库的DisplayOrder
            using var getCurrentCommand = connection.CreateCommand();
            getCurrentCommand.CommandText = "SELECT DisplayOrder FROM Libraries WHERE Id = @id";
            getCurrentCommand.Parameters.AddWithValue("@id", libraryId);
            var currentOrder = getCurrentCommand.ExecuteScalar();

            if (currentOrder == null || currentOrder == DBNull.Value) return;
            var currentOrderValue = Convert.ToInt32(currentOrder);

            // 查找上一个库（DisplayOrder小于当前值的最大DisplayOrder）
            using var getPrevCommand = connection.CreateCommand();
            getPrevCommand.CommandText = "SELECT Id, DisplayOrder FROM Libraries WHERE DisplayOrder < @order ORDER BY DisplayOrder DESC LIMIT 1";
            getPrevCommand.Parameters.AddWithValue("@order", currentOrderValue);
            using var prevReader = getPrevCommand.ExecuteReader();

            if (prevReader.Read())
            {
                var prevId = prevReader.GetInt32(0);
                var prevOrder = prevReader.GetInt32(1);

                // 交换两个库的DisplayOrder
                using var updateCommand = connection.CreateCommand();
                updateCommand.CommandText = "UPDATE Libraries SET DisplayOrder = @newOrder WHERE Id = @id";

                updateCommand.Parameters.AddWithValue("@newOrder", prevOrder);
                updateCommand.Parameters.AddWithValue("@id", libraryId);
                updateCommand.ExecuteNonQuery();

                updateCommand.Parameters.Clear();
                updateCommand.Parameters.AddWithValue("@newOrder", currentOrderValue);
                updateCommand.Parameters.AddWithValue("@id", prevId);
                updateCommand.ExecuteNonQuery();
            }
        }

        public static void MoveLibraryDown(int libraryId)
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            // 获取当前库的DisplayOrder
            using var getCurrentCommand = connection.CreateCommand();
            getCurrentCommand.CommandText = "SELECT DisplayOrder FROM Libraries WHERE Id = @id";
            getCurrentCommand.Parameters.AddWithValue("@id", libraryId);
            var currentOrder = getCurrentCommand.ExecuteScalar();

            if (currentOrder == null || currentOrder == DBNull.Value) return;
            var currentOrderValue = Convert.ToInt32(currentOrder);

            // 查找下一个库（DisplayOrder大于当前值的最小DisplayOrder）
            using var getNextCommand = connection.CreateCommand();
            getNextCommand.CommandText = "SELECT Id, DisplayOrder FROM Libraries WHERE DisplayOrder > @order ORDER BY DisplayOrder ASC LIMIT 1";
            getNextCommand.Parameters.AddWithValue("@order", currentOrderValue);
            using var nextReader = getNextCommand.ExecuteReader();

            if (nextReader.Read())
            {
                var nextId = nextReader.GetInt32(0);
                var nextOrder = nextReader.GetInt32(1);

                // 交换两个库的DisplayOrder
                using var updateCommand = connection.CreateCommand();
                updateCommand.CommandText = "UPDATE Libraries SET DisplayOrder = @newOrder WHERE Id = @id";

                updateCommand.Parameters.AddWithValue("@newOrder", nextOrder);
                updateCommand.Parameters.AddWithValue("@id", libraryId);
                updateCommand.ExecuteNonQuery();

                updateCommand.Parameters.Clear();
                updateCommand.Parameters.AddWithValue("@newOrder", currentOrderValue);
                updateCommand.Parameters.AddWithValue("@id", nextId);
                updateCommand.ExecuteNonQuery();
            }
        }

        public static Library GetLibrary(int libraryId)
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            string name;
            int displayOrder;

            // 获取库的基本信息
            using (var command = connection.CreateCommand())
            {
                command.CommandText = "SELECT Name, DisplayOrder FROM Libraries WHERE Id = @libraryId";
                command.Parameters.AddWithValue("@libraryId", libraryId);
                using var reader = command.ExecuteReader();

                if (!reader.Read()) return null;

                name = reader.GetString(0);
                displayOrder = reader.IsDBNull(1) ? 0 : reader.GetInt32(1);
            }

            // 获取库的所有路径（使用新的command对象）
            var paths = new List<string>();
            using (var pathCommand = connection.CreateCommand())
            {
                pathCommand.CommandText = "SELECT Path FROM LibraryPaths WHERE LibraryId = @libraryId ORDER BY Id";
                pathCommand.Parameters.AddWithValue("@libraryId", libraryId);
                using var pathReader = pathCommand.ExecuteReader();
                while (pathReader.Read())
                {
                    paths.Add(pathReader.GetString(0));
                }
            }

            return new Library
            {
                Id = libraryId,
                Name = name,
                Paths = paths,
                DisplayOrder = displayOrder
            };
        }

        public static List<LibraryPath> GetLibraryPaths(int libraryId)
        {
            var paths = new List<LibraryPath>();
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT Id, Path, DisplayName FROM LibraryPaths WHERE LibraryId = @libraryId ORDER BY Id";
            command.Parameters.AddWithValue("@libraryId", libraryId);
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                paths.Add(new LibraryPath
                {
                    Id = reader.GetInt32(0),
                    LibraryId = libraryId,
                    Path = reader.GetString(1),
                    DisplayName = reader.IsDBNull(2) ? null : reader.GetString(2)
                });
            }
            return paths;
        }

        #region 收藏功能

        public static void AddFavorite(string path, bool isDirectory, string displayName = null)
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = @"
                INSERT OR REPLACE INTO Favorites (Path, DisplayName, IsDirectory, CreatedAt) 
                VALUES (@path, @displayName, @isDirectory, CURRENT_TIMESTAMP)";
            command.Parameters.AddWithValue("@path", path);
            command.Parameters.AddWithValue("@displayName", displayName ?? (object)DBNull.Value);
            command.Parameters.AddWithValue("@isDirectory", isDirectory ? 1 : 0);
            command.ExecuteNonQuery();
        }

        public static void RemoveFavorite(string path)
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "DELETE FROM Favorites WHERE Path = @path";
            command.Parameters.AddWithValue("@path", path);
            command.ExecuteNonQuery();
        }

        public static bool IsFavorite(string path)
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT COUNT(*) FROM Favorites WHERE Path = @path";
            command.Parameters.AddWithValue("@path", path);
            var count = Convert.ToInt32(command.ExecuteScalar());
            return count > 0;
        }

        public static List<Favorite> GetAllFavorites()
        {
            var favorites = new List<Favorite>();
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();
            using var command = connection.CreateCommand();
            // 文件夹在上（IsDirectory=1），文件在下（IsDirectory=0），按SortOrder和创建时间排序
            command.CommandText = @"
                SELECT Id, Path, DisplayName, IsDirectory, CreatedAt, SortOrder 
                FROM Favorites 
                ORDER BY IsDirectory DESC, SortOrder ASC, CreatedAt DESC";
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                favorites.Add(new Favorite
                {
                    Id = reader.GetInt32(0),
                    Path = reader.GetString(1),
                    DisplayName = reader.IsDBNull(2) ? null : reader.GetString(2),
                    IsDirectory = reader.GetInt32(3) == 1,
                    CreatedAt = reader.GetDateTime(4),
                    SortOrder = reader.GetInt32(5)
                });
            }
            return favorites;
        }

        public static void UpdateFavoriteSortOrder(int favoriteId, int newSortOrder)
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "UPDATE Favorites SET SortOrder = @sortOrder WHERE Id = @id";
            command.Parameters.AddWithValue("@sortOrder", newSortOrder);
            command.Parameters.AddWithValue("@id", favoriteId);
            command.ExecuteNonQuery();
        }

        #endregion

        #region 文件夹大小缓存

        /// <summary>
        /// 获取文件夹的缓存大小（如果存在）
        /// </summary>
        public static long? GetFolderSize(string folderPath)
        {
            if (string.IsNullOrEmpty(folderPath) || !Directory.Exists(folderPath))
                return null;

            try
            {
                using var connection = new SqliteConnection(_connectionString);
                connection.Open();
                using var command = connection.CreateCommand();
                command.CommandText = @"
                    SELECT SizeBytes, LastModified 
                    FROM FolderSizes 
                    WHERE FolderPath = @folderPath";
                command.Parameters.AddWithValue("@folderPath", folderPath);

                using var reader = command.ExecuteReader();
                if (reader.Read())
                {
                    var sizeBytes = reader.GetInt64(0);
                    var lastModified = reader.GetDateTime(1);

                    // 检查文件夹的最后修改时间是否与缓存一致
                    var currentLastModified = Directory.GetLastWriteTime(folderPath);
                    if (currentLastModified <= lastModified)
                    {
                        // 文件夹未修改，返回缓存的大小
                        return sizeBytes;
                    }
                }
            }
            catch
            {
            }

            return null;
        }

        /// <summary>
        /// 设置文件夹的大小缓存
        /// </summary>
        public static void SetFolderSize(string folderPath, long sizeBytes)
        {
            if (string.IsNullOrEmpty(folderPath))
                return;

            try
            {
                using var connection = new SqliteConnection(_connectionString);
                connection.Open();
                using var command = connection.CreateCommand();

                // 获取文件夹的最后修改时间
                DateTime lastModified = DateTime.MinValue;
                try
                {
                    if (Directory.Exists(folderPath))
                    {
                        lastModified = Directory.GetLastWriteTime(folderPath);
                    }
                }
                catch { }

                command.CommandText = @"
                    INSERT OR REPLACE INTO FolderSizes (FolderPath, SizeBytes, LastModified, CalculatedAt) 
                    VALUES (@folderPath, @sizeBytes, @lastModified, CURRENT_TIMESTAMP)";
                command.Parameters.AddWithValue("@folderPath", folderPath);
                command.Parameters.AddWithValue("@sizeBytes", sizeBytes);
                command.Parameters.AddWithValue("@lastModified", lastModified);
                command.ExecuteNonQuery();
            }
            catch
            {
            }
        }

        /// <summary>
        /// 批量获取多个文件夹的缓存大小
        /// </summary>
        public static Dictionary<string, long> GetFolderSizesBatch(List<string> folderPaths)
        {
            var result = new Dictionary<string, long>();
            if (folderPaths == null || folderPaths.Count == 0)
                return result;

            try
            {
                using var connection = new SqliteConnection(_connectionString);
                connection.Open();

                // 过滤出存在的文件夹路径
                var existingPaths = folderPaths.Where(p => !string.IsNullOrEmpty(p) && Directory.Exists(p)).ToList();
                if (existingPaths.Count == 0)
                    return result;

                // 构建查询参数
                var placeholders = string.Join(",", existingPaths.Select((_, i) => $"@path{i}"));
                using var command = connection.CreateCommand();
                command.CommandText = $@"
                    SELECT FolderPath, SizeBytes, LastModified 
                    FROM FolderSizes 
                    WHERE FolderPath IN ({placeholders})";

                for (int i = 0; i < existingPaths.Count; i++)
                {
                    command.Parameters.AddWithValue($"@path{i}", existingPaths[i]);
                }

                using var reader = command.ExecuteReader();
                while (reader.Read())
                {
                    var folderPath = reader.GetString(0);
                    var sizeBytes = reader.GetInt64(1);
                    var lastModified = reader.GetDateTime(2);

                    // 检查文件夹的最后修改时间是否与缓存一致
                    try
                    {
                        var currentLastModified = Directory.GetLastWriteTime(folderPath);
                        if (currentLastModified <= lastModified)
                        {
                            // 文件夹未修改，使用缓存的大小
                            result[folderPath] = sizeBytes;
                        }
                    }
                    catch { }
                }
            }
            catch
            {
            }

            return result;
        }

        /// <summary>
        /// 删除文件夹大小缓存（当文件夹被删除时）
        /// </summary>
        public static void RemoveFolderSize(string folderPath)
        {
            if (string.IsNullOrEmpty(folderPath))
                return;

            try
            {
                using var connection = new SqliteConnection(_connectionString);
                connection.Open();
                using var command = connection.CreateCommand();
                command.CommandText = "DELETE FROM FolderSizes WHERE FolderPath = @folderPath";
                command.Parameters.AddWithValue("@folderPath", folderPath);
                command.ExecuteNonQuery();
            }
            catch
            {
            }
        }

        /// <summary>
        /// 清理不存在的文件夹大小缓存（防止数据库无限增长）
        /// </summary>
        /// <param name="batchSize">每批处理的记录数，默认100</param>
        /// <param name="maxProcessed">最多处理的记录数，默认1000，0表示不限制</param>
        /// <returns>清理的记录数</returns>
        public static int CleanupNonExistentFolderSizes(int batchSize = 100, int maxProcessed = 1000)
        {
            int cleanedCount = 0;
            try
            {
                using var connection = new SqliteConnection(_connectionString);
                connection.Open();

                // 获取所有文件夹路径（分批处理）
                using var selectCommand = connection.CreateCommand();
                selectCommand.CommandText = @"
                    SELECT FolderPath 
                    FROM FolderSizes 
                    ORDER BY CalculatedAt ASC
                    LIMIT @limit";

                int processed = 0;
                while (maxProcessed == 0 || processed < maxProcessed)
                {
                    selectCommand.Parameters.Clear();
                    int currentBatchSize = Math.Min(batchSize, maxProcessed == 0 ? batchSize : maxProcessed - processed);
                    selectCommand.Parameters.AddWithValue("@limit", currentBatchSize);

                    var pathsToCheck = new List<string>();
                    using (var reader = selectCommand.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            pathsToCheck.Add(reader.GetString(0));
                        }
                    }

                    if (pathsToCheck.Count == 0)
                        break; // 没有更多记录了

                    // 检查每个路径是否存在
                    var pathsToDelete = new List<string>();
                    foreach (var path in pathsToCheck)
                    {
                        try
                        {
                            if (!Directory.Exists(path))
                            {
                                pathsToDelete.Add(path);
                            }
                        }
                        catch
                        {
                            // 如果检查时出错，也认为不存在
                            pathsToDelete.Add(path);
                        }
                    }

                    // 批量删除不存在的路径
                    if (pathsToDelete.Count > 0)
                    {
                        using var deleteCommand = connection.CreateCommand();
                        // 构建批量删除SQL
                        var placeholders = string.Join(",", pathsToDelete.Select((_, i) => $"@path{i}"));
                        deleteCommand.CommandText = $"DELETE FROM FolderSizes WHERE FolderPath IN ({placeholders})";

                        for (int i = 0; i < pathsToDelete.Count; i++)
                        {
                            deleteCommand.Parameters.AddWithValue($"@path{i}", pathsToDelete[i]);
                        }

                        int deleted = deleteCommand.ExecuteNonQuery();
                        cleanedCount += deleted;
                    }

                    processed += pathsToCheck.Count;

                    // 如果这批没有需要删除的，且已处理足够多，可以提前结束
                    if (pathsToDelete.Count == 0 && processed >= maxProcessed / 2)
                    {
                        break;
                    }
                }
            }
            catch
            {
            }

            return cleanedCount;
        }

        /// <summary>
        /// 获取文件夹大小缓存的总记录数
        /// </summary>
        public static int GetFolderSizeCacheCount()
        {
            try
            {
                using var connection = new SqliteConnection(_connectionString);
                connection.Open();
                using var command = connection.CreateCommand();
                command.CommandText = "SELECT COUNT(*) FROM FolderSizes";
                var result = command.ExecuteScalar();
                return result != null ? Convert.ToInt32(result) : 0;
            }
            catch
            {
                return 0;
            }
        }

        #endregion
    }

    public class Library
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public List<string> Paths { get; set; } = new List<string>();
        public int DisplayOrder { get; set; }

        // 兼容旧代码
        [Obsolete("Use Paths property instead")]
        public string Path
        {
            get => Paths?.FirstOrDefault() ?? "";
            set
            {
                if (Paths == null) Paths = new List<string>();
                if (!Paths.Contains(value)) Paths.Add(value);
            }
        }
    }

    public class LibraryPath
    {
        public int Id { get; set; }
        public int LibraryId { get; set; }
        public string Path { get; set; }
        public string DisplayName { get; set; }
    }

    public class Favorite
    {
        public int Id { get; set; }
        public string Path { get; set; }
        public string DisplayName { get; set; }
        public bool IsDirectory { get; set; }
        public DateTime CreatedAt { get; set; }
        public int SortOrder { get; set; }
    }
}

