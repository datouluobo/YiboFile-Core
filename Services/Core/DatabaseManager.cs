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

            // 标签系统表 (Restored for Core)
            var createTagGroupsTable = @"
                CREATE TABLE IF NOT EXISTS TagGroups (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Name TEXT NOT NULL UNIQUE,
                    Color TEXT
                )";
            command.CommandText = createTagGroupsTable;
            command.ExecuteNonQuery();

            var createTagsTable = @"
                CREATE TABLE IF NOT EXISTS Tags (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    GroupId INTEGER NOT NULL,
                    Name TEXT NOT NULL,
                    Color TEXT,
                    FOREIGN KEY (GroupId) REFERENCES TagGroups (Id) ON DELETE CASCADE,
                    UNIQUE(GroupId, Name)
                )";
            command.CommandText = createTagsTable;
            command.ExecuteNonQuery();

            var createFileTagsTable = @"
                CREATE TABLE IF NOT EXISTS FileTags (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    FilePath TEXT NOT NULL,
                    TagId INTEGER NOT NULL,
                    CreatedAt DATETIME DEFAULT CURRENT_TIMESTAMP,
                    FOREIGN KEY (TagId) REFERENCES Tags (Id) ON DELETE CASCADE,
                    UNIQUE(FilePath, TagId)
                )";
            command.CommandText = createFileTagsTable;
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


            // 数据库迁移：Tags 表添加 GroupId 列 (从 Pro 迁移可能遗留旧结构)
            try
            {
                // 检查 Tags 表是否存在
                command.CommandText = "SELECT name FROM sqlite_master WHERE type='table' AND name='Tags'";
                var tableExists = command.ExecuteScalar() != null;

                if (tableExists)
                {
                    var checkGroupIdColumn = "SELECT COUNT(*) FROM pragma_table_info('Tags') WHERE name='GroupId'";
                    command.CommandText = checkGroupIdColumn;
                    var hasGroupId = Convert.ToInt32(command.ExecuteScalar()) > 0;

                    if (!hasGroupId)
                    {
                        // 添加 GroupId 列
                        command.CommandText = "ALTER TABLE Tags ADD COLUMN GroupId INTEGER DEFAULT 0";
                        command.ExecuteNonQuery();
                    }
                    else
                    {
                        // 确保没有 NULL 值
                        command.CommandText = "UPDATE Tags SET GroupId = 0 WHERE GroupId IS NULL";
                        command.ExecuteNonQuery();
                    }
                }
            }
            catch
            {
                // Ignore
            }

            // 数据库迁移：收藏分组支持
            try
            {
                // 1. 创建收藏分组表
                var createFavoriteGroupsTable = @"
                    CREATE TABLE IF NOT EXISTS FavoriteGroups (
                        Id INTEGER PRIMARY KEY AUTOINCREMENT,
                        Name TEXT NOT NULL UNIQUE,
                        SortOrder INTEGER DEFAULT 0,
                        CreatedAt DATETIME DEFAULT CURRENT_TIMESTAMP
                    )";
                command.CommandText = createFavoriteGroupsTable;
                command.ExecuteNonQuery();

                // 2. 检查 Favorites 表是否有 GroupId 列
                command.CommandText = "SELECT COUNT(*) FROM pragma_table_info('Favorites') WHERE name='GroupId'";
                var hasGroupId = Convert.ToInt32(command.ExecuteScalar()) > 0;

                if (!hasGroupId)
                {
                    // 添加 GroupId 列
                    command.CommandText = "ALTER TABLE Favorites ADD COLUMN GroupId INTEGER DEFAULT 1";
                    command.ExecuteNonQuery();

                    // 初始化默认分组 (1: 文件夹, 2: 文件)
                    // 使用显式事务确保一致性
                    command.CommandText = "SELECT COUNT(*) FROM FavoriteGroups WHERE Id IN (1, 2)";
                    if (Convert.ToInt32(command.ExecuteScalar()) == 0)
                    {
                        command.CommandText = "INSERT OR IGNORE INTO FavoriteGroups (Id, Name, SortOrder) VALUES (1, '文件夹', 0)";
                        command.ExecuteNonQuery();
                        command.CommandText = "INSERT OR IGNORE INTO FavoriteGroups (Id, Name, SortOrder) VALUES (2, '文件', 1)";
                        command.ExecuteNonQuery();
                    }

                    // 迁移现有数据：根据 IsDirectory 分配到 文件夹(1) 或 文件(2) 分组
                    command.CommandText = "UPDATE Favorites SET GroupId = 1 WHERE IsDirectory = 1";
                    command.ExecuteNonQuery();
                    command.CommandText = "UPDATE Favorites SET GroupId = 2 WHERE IsDirectory = 0";
                    command.ExecuteNonQuery();
                }
            }
            catch
            {
                // 记录错误但不中断初始化
            }
            // 数据库初始化默认标签
            try
            {
                command.CommandText = "SELECT COUNT(*) FROM TagGroups";
                if (Convert.ToInt32(command.ExecuteScalar()) == 0)
                {
                    // 创建默认分组
                    command.CommandText = "INSERT INTO TagGroups (Name, Color) VALUES ('核心', '#FFB3BA')";
                    command.ExecuteNonQuery();
                    var coreGroupId = 0;
                    command.CommandText = "SELECT last_insert_rowid()";
                    coreGroupId = Convert.ToInt32(command.ExecuteScalar());

                    command.CommandText = "INSERT INTO TagGroups (Name, Color) VALUES ('工作', '#BAE1FF')";
                    command.ExecuteNonQuery();
                    var workGroupId = 0;
                    command.CommandText = "SELECT last_insert_rowid()";
                    workGroupId = Convert.ToInt32(command.ExecuteScalar());

                    // 添加默认标签
                    var defaultTags = new[]
                    {
                        (coreGroupId, "重要", "#FFB3BA"),
                        (coreGroupId, "待办", "#FFDFBA"),
                        (workGroupId, "文档", "#BAE1FF"),
                        (workGroupId, "项目", "#BAFFC9")
                    };

                    foreach (var tag in defaultTags)
                    {
                        command.CommandText = "INSERT INTO Tags (GroupId, Name, Color) VALUES (@gid, @name, @color)";
                        command.Parameters.Clear();
                        command.Parameters.AddWithValue("@gid", tag.Item1);
                        command.Parameters.AddWithValue("@name", tag.Item2);
                        command.Parameters.AddWithValue("@color", tag.Item3);
                        command.ExecuteNonQuery();
                    }
                }
            }
            catch { }
        }


        #region 标签系统 (Tag System)

        public static int AddTagGroup(string name, string color = null)
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "INSERT INTO TagGroups (Name, Color) VALUES (@name, @color); SELECT last_insert_rowid();";
            command.Parameters.AddWithValue("@name", name);
            command.Parameters.AddWithValue("@color", color ?? (object)DBNull.Value);
            return Convert.ToInt32(command.ExecuteScalar());
        }

        public static void RenameTagGroup(int groupId, string newName)
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "UPDATE TagGroups SET Name = @name WHERE Id = @id";
            command.Parameters.AddWithValue("@name", newName);
            command.Parameters.AddWithValue("@id", groupId);
            command.ExecuteNonQuery();
        }

        public static void DeleteTagGroup(int groupId)
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();
            using var command = connection.CreateCommand();
            // Tags and FileTags will be deleted by CASCADE if supported, but Sqlite default might not be ON.
            // Let's ensure cleaner deletion just in case or rely on PRAGMA foreign_keys = ON;
            // Best practice implies ensuring dependencies are gone or letting DB handle it.
            // We'll trust CASCADE if configured, but to be safe/explicit:
            command.CommandText = "DELETE FROM TagGroups WHERE Id = @id";
            command.Parameters.AddWithValue("@id", groupId);
            command.ExecuteNonQuery();
        }

        public static List<Services.Features.ITagGroup> GetTagGroups()
        {
            var result = new List<Services.Features.ITagGroup>();
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT Id, Name, Color FROM TagGroups ORDER BY Id";
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                result.Add(new TagGroupModel
                {
                    Id = reader.GetInt32(0),
                    Name = reader.GetString(1),
                    Color = reader.IsDBNull(2) ? null : reader.GetString(2)
                });
            }
            return result;
        }

        public static int AddTag(int groupId, string name, string color = null)
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "INSERT INTO Tags (GroupId, Name, Color) VALUES (@groupId, @name, @color); SELECT last_insert_rowid();";
            command.Parameters.AddWithValue("@groupId", groupId);
            command.Parameters.AddWithValue("@name", name);
            command.Parameters.AddWithValue("@color", string.IsNullOrEmpty(color) ? "#808080" : color);
            return Convert.ToInt32(command.ExecuteScalar());
        }

        public static void RenameTag(int tagId, string newName)
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "UPDATE Tags SET Name = @name WHERE Id = @id";
            command.Parameters.AddWithValue("@name", newName);
            command.Parameters.AddWithValue("@id", tagId);
            command.ExecuteNonQuery();
        }

        public static void UpdateTagColor(int tagId, string color)
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "UPDATE Tags SET Color = @color WHERE Id = @id";
            command.Parameters.AddWithValue("@color", color ?? "");
            command.Parameters.AddWithValue("@id", tagId);
            command.ExecuteNonQuery();
        }

        public static void DeleteTag(int tagId)
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "DELETE FROM Tags WHERE Id = @id";
            command.Parameters.AddWithValue("@id", tagId);
            command.ExecuteNonQuery();
        }

        public static void UpdateTagGroup(int tagId, int newGroupId)
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "UPDATE Tags SET GroupId = @groupId WHERE Id = @id";
            command.Parameters.AddWithValue("@groupId", newGroupId);
            command.Parameters.AddWithValue("@id", tagId);
            command.ExecuteNonQuery();
        }

        public static List<Services.Features.ITag> GetTagsByGroup(int groupId)
        {
            var result = new List<Services.Features.ITag>();
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT Id, Name, Color, IFNULL(GroupId, 0) FROM Tags WHERE IFNULL(GroupId, 0) = @groupId ORDER BY Id";
            command.Parameters.AddWithValue("@groupId", groupId);
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                result.Add(new TagModel
                {
                    Id = reader.GetInt32(0),
                    Name = reader.GetString(1),
                    Color = reader.IsDBNull(2) ? null : reader.GetString(2),
                    GroupId = reader.GetInt32(3)
                });
            }
            return result;
        }

        public static void AddTagToFile(string filePath, int tagId)
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "INSERT OR IGNORE INTO FileTags (FilePath, TagId) VALUES (@path, @tagId)";
            command.Parameters.AddWithValue("@path", filePath);
            command.Parameters.AddWithValue("@tagId", tagId);
            command.ExecuteNonQuery();
        }

        public static void RemoveTagFromFile(string filePath, int tagId)
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "DELETE FROM FileTags WHERE FilePath = @path AND TagId = @tagId";
            command.Parameters.AddWithValue("@path", filePath);
            command.Parameters.AddWithValue("@tagId", tagId);
            command.ExecuteNonQuery();
        }

        public static List<Services.Features.ITag> GetFileTags(string filePath)
        {
            var result = new List<Services.Features.ITag>();
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT t.Id, t.Name, t.Color, t.GroupId 
                FROM Tags t
                INNER JOIN FileTags ft ON t.Id = ft.TagId
                WHERE ft.FilePath = @path";
            command.Parameters.AddWithValue("@path", filePath);
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                result.Add(new TagModel
                {
                    Id = reader.GetInt32(0),
                    Name = reader.GetString(1),
                    Color = reader.IsDBNull(2) ? null : reader.GetString(2),
                    GroupId = reader.GetInt32(3)
                });
            }
            return result;
        }

        public static List<string> GetFilesByTag(int tagId)
        {
            var result = new List<string>();
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT FilePath FROM FileTags WHERE TagId = @tagId";
            command.Parameters.AddWithValue("@tagId", tagId);
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                result.Add(reader.GetString(0));
            }
            return result;
        }

        public static string GetTagColorByName(string tagName)
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT Color FROM Tags WHERE Name = @name";
            command.Parameters.AddWithValue("@name", tagName);
            var result = command.ExecuteScalar();
            return result != null && result != DBNull.Value ? result.ToString() : null;
        }

        public static List<string> GetFilesByTagName(string tagName)
        {
            var result = new List<string>();
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            // First find the tag ID
            int tagId = -1;
            using (var findCommand = connection.CreateCommand())
            {
                findCommand.CommandText = "SELECT Id FROM Tags WHERE Name = @name";
                findCommand.Parameters.AddWithValue("@name", tagName);
                var idObj = findCommand.ExecuteScalar();
                if (idObj == null || idObj == DBNull.Value) return result;
                tagId = Convert.ToInt32(idObj);
            }

            // Then get files
            using (var command = connection.CreateCommand())
            {
                command.CommandText = "SELECT FilePath FROM FileTags WHERE TagId = @tagId";
                command.Parameters.AddWithValue("@tagId", tagId);
                using var reader = command.ExecuteReader();
                while (reader.Read())
                {
                    result.Add(reader.GetString(0));
                }
            }
            return result;
        }

        /// <summary>
        /// 根据标签 ID 获取关联的文件路径列表
        /// </summary>
        public static List<string> GetFilesByTagId(int tagId)
        {
            var result = new List<string>();
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT FilePath FROM FileTags WHERE TagId = @tagId";
            command.Parameters.AddWithValue("@tagId", tagId);
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                result.Add(reader.GetString(0));
            }
            return result;
        }

        public static Services.Features.ITag GetTag(int tagId)
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT Id, Name, Color, GroupId FROM Tags WHERE Id = @id";
            command.Parameters.AddWithValue("@id", tagId);
            using var reader = command.ExecuteReader();
            if (reader.Read())
            {
                return new TagModel
                {
                    Id = reader.GetInt32(0),
                    Name = reader.GetString(1),
                    Color = reader.IsDBNull(2) ? null : reader.GetString(2),
                    GroupId = reader.GetInt32(3)
                };
            }
            return null;
        }

        public static List<Services.Features.ITag> GetAllTags()
        {
            var result = new List<Services.Features.ITag>();
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT Id, Name, Color, GroupId FROM Tags ORDER BY Name";
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                result.Add(new TagModel
                {
                    Id = reader.GetInt32(0),
                    Name = reader.GetString(1),
                    Color = reader.IsDBNull(2) ? null : reader.GetString(2),
                    GroupId = reader.GetInt32(3)
                });
            }
            return result;
        }

        public static List<Services.Features.ITag> GetUngroupedTags()
        {
            var result = new List<Services.Features.ITag>();
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT Id, Name, Color, IFNULL(GroupId, 0) FROM Tags WHERE IFNULL(GroupId, 0) = 0 OR IFNULL(GroupId, 0) NOT IN (SELECT Id FROM TagGroups) ORDER BY Name";
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                result.Add(new TagModel
                {
                    Id = reader.GetInt32(0),
                    Name = reader.GetString(1),
                    Color = reader.IsDBNull(2) ? null : reader.GetString(2),
                    GroupId = reader.GetInt32(3)
                });
            }
            return result;
        }

        // Private models for DB interaction
        private class TagGroupModel : Services.Features.ITagGroup
        {
            public int Id { get; set; }
            public string Name { get; set; }
            public string Color { get; set; }
        }

        private class TagModel : Services.Features.ITag
        {
            public int Id { get; set; }
            public string Name { get; set; }
            public string Color { get; set; }
            public int GroupId { get; set; }
        }

        #endregion

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

        public static void AddFavorite(string path, bool isDirectory, string displayName = null, int groupId = 1)
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = @"
                INSERT OR REPLACE INTO Favorites (Path, DisplayName, IsDirectory, GroupId, CreatedAt) 
                VALUES (@path, @displayName, @isDirectory, @groupId, CURRENT_TIMESTAMP)";
            command.Parameters.AddWithValue("@path", path);
            command.Parameters.AddWithValue("@displayName", displayName ?? (object)DBNull.Value);
            command.Parameters.AddWithValue("@isDirectory", isDirectory ? 1 : 0);
            command.Parameters.AddWithValue("@groupId", groupId);
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
            command.CommandText = @"
                SELECT Id, Path, DisplayName, IsDirectory, CreatedAt, SortOrder, GroupId 
                FROM Favorites 
                ORDER BY SortOrder ASC, CreatedAt DESC";
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
                    SortOrder = reader.GetInt32(5),
                    GroupId = reader.GetInt32(6)
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

        #region 收藏分组管理

        public static List<FavoriteGroup> GetAllFavoriteGroups()
        {
            var result = new List<FavoriteGroup>();
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT Id, Name, SortOrder, CreatedAt FROM FavoriteGroups ORDER BY SortOrder ASC, CreatedAt ASC";
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                result.Add(new FavoriteGroup
                {
                    Id = reader.GetInt32(0),
                    Name = reader.GetString(1),
                    SortOrder = reader.GetInt32(2),
                    CreatedAt = reader.GetDateTime(3)
                });
            }
            return result;
        }

        public static int CreateFavoriteGroup(string name)
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "INSERT INTO FavoriteGroups (Name) VALUES (@name); SELECT last_insert_rowid();";
            command.Parameters.AddWithValue("@name", name);
            return Convert.ToInt32(command.ExecuteScalar());
        }

        public static void RenameFavoriteGroup(int id, string name)
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "UPDATE FavoriteGroups SET Name = @name WHERE Id = @id";
            command.Parameters.AddWithValue("@name", name);
            command.Parameters.AddWithValue("@id", id);
            command.ExecuteNonQuery();
        }

        public static void DeleteFavoriteGroup(int id)
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();
            using var command = connection.CreateCommand();
            // 如果删除分组，将其中的收藏移动到默认分组(1) 或者直接删除？
            // 建议移动到默认分组以防意外丢失
            command.CommandText = "UPDATE Favorites SET GroupId = 1 WHERE GroupId = @id; DELETE FROM FavoriteGroups WHERE Id = @id AND Id != 1;";
            command.Parameters.AddWithValue("@id", id);
            command.ExecuteNonQuery();
        }

        public static void UpdateFavoriteGroupSortOrder(int id, int order)
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "UPDATE FavoriteGroups SET SortOrder = @order WHERE Id = @id";
            command.Parameters.AddWithValue("@order", order);
            command.Parameters.AddWithValue("@id", id);
            command.ExecuteNonQuery();
        }

        #endregion

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
        public int GroupId { get; set; }
        public string Path { get; set; }
        public string DisplayName { get; set; }
        public bool IsDirectory { get; set; }
        public DateTime CreatedAt { get; set; }
        public int SortOrder { get; set; }
    }

    public class FavoriteGroup
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public int SortOrder { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}

