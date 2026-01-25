using System;
using System.IO;
using Microsoft.Data.Sqlite;

namespace YiboFile
{
    /// <summary>
    /// 数据库管理器 - 核心基础设施
    /// </summary>
    public static partial class DatabaseManager
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

            // 标签系统表
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

            // 数据库迁移逻辑
            PerformMigrations(command);

            // 初始化默认标签
            InitializeDefaultTags(command);
        }

        private static void PerformMigrations(SqliteCommand command)
        {
            // 库结构迁移: Path列 -> LibraryPaths表
            try
            {
                var checkOldTable = "SELECT COUNT(*) FROM pragma_table_info('Libraries') WHERE name='Path'";
                command.CommandText = checkOldTable;
                var hasPathColumn = Convert.ToInt32(command.ExecuteScalar()) > 0;

                if (hasPathColumn)
                {
                    command.CommandText = @"
                        INSERT OR IGNORE INTO LibraryPaths (LibraryId, Path, DisplayName)
                        SELECT Id, Path, Name FROM Libraries WHERE Path IS NOT NULL AND Path != ''
                    ";
                    command.ExecuteNonQuery();

                    command.CommandText = @"
                        CREATE TABLE Libraries_new (
                            Id INTEGER PRIMARY KEY AUTOINCREMENT,
                            Name TEXT NOT NULL UNIQUE,
                            CreatedAt DATETIME DEFAULT CURRENT_TIMESTAMP
                        )";
                    command.ExecuteNonQuery();

                    command.CommandText = "INSERT INTO Libraries_new (Id, Name, CreatedAt) SELECT Id, Name, CreatedAt FROM Libraries";
                    command.ExecuteNonQuery();

                    command.CommandText = "DROP TABLE Libraries";
                    command.ExecuteNonQuery();

                    command.CommandText = "ALTER TABLE Libraries_new RENAME TO Libraries";
                    command.ExecuteNonQuery();
                }
            }
            catch { }

            // 添加 DisplayOrder
            try
            {
                var checkOrderColumn = "SELECT COUNT(*) FROM pragma_table_info('Libraries') WHERE name='DisplayOrder'";
                command.CommandText = checkOrderColumn;
                var hasOrderColumn = Convert.ToInt32(command.ExecuteScalar()) > 0;

                if (!hasOrderColumn)
                {
                    command.CommandText = "ALTER TABLE Libraries ADD COLUMN DisplayOrder INTEGER DEFAULT 0";
                    command.ExecuteNonQuery();
                    command.CommandText = "UPDATE Libraries SET DisplayOrder = Id";
                    command.ExecuteNonQuery();
                }
            }
            catch { }

            // Tags 表 GroupId
            try
            {
                command.CommandText = "SELECT name FROM sqlite_master WHERE type='table' AND name='Tags'";
                if (command.ExecuteScalar() != null)
                {
                    command.CommandText = "SELECT COUNT(*) FROM pragma_table_info('Tags') WHERE name='GroupId'";
                    if (Convert.ToInt32(command.ExecuteScalar()) == 0)
                    {
                        command.CommandText = "ALTER TABLE Tags ADD COLUMN GroupId INTEGER DEFAULT 0";
                        command.ExecuteNonQuery();
                    }
                }
            }
            catch { }

            // 收藏分组支持
            try
            {
                var createFavoriteGroupsTable = @"
                    CREATE TABLE IF NOT EXISTS FavoriteGroups (
                        Id INTEGER PRIMARY KEY AUTOINCREMENT,
                        Name TEXT NOT NULL UNIQUE,
                        SortOrder INTEGER DEFAULT 0,
                        CreatedAt DATETIME DEFAULT CURRENT_TIMESTAMP
                    )";
                command.CommandText = createFavoriteGroupsTable;
                command.ExecuteNonQuery();

                command.CommandText = "SELECT COUNT(*) FROM pragma_table_info('Favorites') WHERE name='GroupId'";
                if (Convert.ToInt32(command.ExecuteScalar()) == 0)
                {
                    command.CommandText = "ALTER TABLE Favorites ADD COLUMN GroupId INTEGER DEFAULT 1";
                    command.ExecuteNonQuery();

                    command.CommandText = "SELECT COUNT(*) FROM FavoriteGroups WHERE Id IN (1, 2)";
                    if (Convert.ToInt32(command.ExecuteScalar()) == 0)
                    {
                        command.CommandText = "INSERT OR IGNORE INTO FavoriteGroups (Id, Name, SortOrder) VALUES (1, '文件夹', 0)";
                        command.ExecuteNonQuery();
                        command.CommandText = "INSERT OR IGNORE INTO FavoriteGroups (Id, Name, SortOrder) VALUES (2, '文件', 1)";
                        command.ExecuteNonQuery();
                    }

                    command.CommandText = "UPDATE Favorites SET GroupId = 1 WHERE IsDirectory = 1";
                    command.ExecuteNonQuery();
                    command.CommandText = "UPDATE Favorites SET GroupId = 2 WHERE IsDirectory = 0";
                    command.ExecuteNonQuery();
                }
            }
            catch { }
        }

        private static void InitializeDefaultTags(SqliteCommand command)
        {
            try
            {
                command.CommandText = "SELECT COUNT(*) FROM TagGroups";
                if (Convert.ToInt32(command.ExecuteScalar()) == 0)
                {
                    command.CommandText = "INSERT INTO TagGroups (Name, Color) VALUES ('核心', '#FFB3BA')";
                    command.ExecuteNonQuery();
                    command.CommandText = "SELECT last_insert_rowid()";
                    int coreGroupId = Convert.ToInt32(command.ExecuteScalar());

                    command.CommandText = "INSERT INTO TagGroups (Name, Color) VALUES ('工作', '#BAE1FF')";
                    command.ExecuteNonQuery();
                    command.CommandText = "SELECT last_insert_rowid()";
                    int workGroupId = Convert.ToInt32(command.ExecuteScalar());

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
    }
}
