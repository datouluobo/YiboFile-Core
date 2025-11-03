using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Data.Sqlite;

namespace OoiMRR
{
    /// <summary>
    /// 数据库管理器
    /// </summary>
    public static class DatabaseManager
    {
        private static string _connectionString;

        public static void Initialize()
        {
            var dbPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "OoiMRR", "data.db");
            Directory.CreateDirectory(Path.GetDirectoryName(dbPath));
            _connectionString = $"Data Source={dbPath}";

            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            // 创建标签表
            var createTagsTable = @"
                CREATE TABLE IF NOT EXISTS Tags (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Name TEXT NOT NULL UNIQUE,
                    Color TEXT NOT NULL DEFAULT '#FF0000'
                )";

            // 创建文件标签关联表
            var createFileTagsTable = @"
                CREATE TABLE IF NOT EXISTS FileTags (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    FilePath TEXT NOT NULL,
                    TagId INTEGER NOT NULL,
                    FOREIGN KEY (TagId) REFERENCES Tags (Id),
                    UNIQUE(FilePath, TagId)
                )";

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
            command.CommandText = createTagsTable;
            command.ExecuteNonQuery();

            command.CommandText = createFileTagsTable;
            command.ExecuteNonQuery();

            command.CommandText = createFileNotesTable;
            command.ExecuteNonQuery();

            command.CommandText = createLibrariesTable;
            command.ExecuteNonQuery();

            command.CommandText = createLibraryPathsTable;
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
            catch (Exception ex)
            {
                // 记录错误但不中断初始化
                System.Diagnostics.Debug.WriteLine($"数据库迁移失败: {ex.Message}");
            }
        }

        public static void AddTag(string name, string color = "#FF0000")
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "INSERT OR IGNORE INTO Tags (Name, Color) VALUES (@name, @color)";
            command.Parameters.AddWithValue("@name", name);
            command.Parameters.AddWithValue("@color", color);
            command.ExecuteNonQuery();
        }

        public static List<Tag> GetAllTags()
        {
            var tags = new List<Tag>();
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT Id, Name, Color FROM Tags ORDER BY Name";
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                tags.Add(new Tag
                {
                    Id = reader.GetInt32(0),
                    Name = reader.GetString(1),
                    Color = reader.GetString(2)
                });
            }
            return tags;
        }

        public static void AddFileTag(string filePath, int tagId)
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "INSERT OR IGNORE INTO FileTags (FilePath, TagId) VALUES (@filePath, @tagId)";
            command.Parameters.AddWithValue("@filePath", filePath);
            command.Parameters.AddWithValue("@tagId", tagId);
            command.ExecuteNonQuery();
        }

        public static void RemoveFileTag(string filePath, int tagId)
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "DELETE FROM FileTags WHERE FilePath = @filePath AND TagId = @tagId";
            command.Parameters.AddWithValue("@filePath", filePath);
            command.Parameters.AddWithValue("@tagId", tagId);
            command.ExecuteNonQuery();
        }

        public static List<Tag> GetFileTags(string filePath)
        {
            var tags = new List<Tag>();
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT t.Id, t.Name, t.Color 
                FROM Tags t 
                INNER JOIN FileTags ft ON t.Id = ft.TagId 
                WHERE ft.FilePath = @filePath";
            command.Parameters.AddWithValue("@filePath", filePath);
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                tags.Add(new Tag
                {
                    Id = reader.GetInt32(0),
                    Name = reader.GetString(1),
                    Color = reader.GetString(2)
                });
            }
            return tags;
        }

        public static void SetFileNotes(string filePath, string notes)
        {
            try
            {
                using var connection = new SqliteConnection(_connectionString);
                connection.Open();
                using var command = connection.CreateCommand();
                command.CommandText = @"
                    INSERT OR REPLACE INTO FileNotes (FilePath, Notes, UpdatedAt) 
                    VALUES (@filePath, @notes, CURRENT_TIMESTAMP)";
                command.Parameters.AddWithValue("@filePath", filePath);
                command.Parameters.AddWithValue("@notes", notes);
                command.ExecuteNonQuery();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"保存备注失败: {ex.Message}");
                throw;
            }
        }
        
        // 异步保存备注，提升性能
        public static async System.Threading.Tasks.Task SetFileNotesAsync(string filePath, string notes)
        {
            await System.Threading.Tasks.Task.Run(() =>
            {
                SetFileNotes(filePath, notes);
            });
        }

        public static string GetFileNotes(string filePath)
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT Notes FROM FileNotes WHERE FilePath = @filePath";
            command.Parameters.AddWithValue("@filePath", filePath);
            var result = command.ExecuteScalar();
            return result?.ToString() ?? "";
        }

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
            using var insertCommand = connection.CreateCommand();
            insertCommand.CommandText = "INSERT INTO Libraries (Name) VALUES (@name); SELECT last_insert_rowid();";
            insertCommand.Parameters.AddWithValue("@name", name);
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
            command.CommandText = "SELECT Id, Name FROM Libraries ORDER BY Name";
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                var libraryId = reader.GetInt32(0);
                var libraryName = reader.GetString(1);
                
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
                    Paths = paths
                });
            }
            
            return libraries;
        }

        public static Library GetLibrary(int libraryId)
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();
            
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT Name FROM Libraries WHERE Id = @libraryId";
            command.Parameters.AddWithValue("@libraryId", libraryId);
            var name = command.ExecuteScalar()?.ToString();
            
            if (name == null) return null;
            
            var paths = new List<string>();
            command.CommandText = "SELECT Path FROM LibraryPaths WHERE LibraryId = @libraryId ORDER BY Id";
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                paths.Add(reader.GetString(0));
            }
            
            return new Library
            {
                Id = libraryId,
                Name = name,
                Paths = paths
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
    }

    public class Tag
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Color { get; set; }
    }

    public class Library
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public List<string> Paths { get; set; } = new List<string>();
        
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