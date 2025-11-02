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

            // 创建库表
            var createLibrariesTable = @"
                CREATE TABLE IF NOT EXISTS Libraries (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Name TEXT NOT NULL UNIQUE,
                    Path TEXT NOT NULL,
                    CreatedAt DATETIME DEFAULT CURRENT_TIMESTAMP
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

        public static void AddLibrary(string name, string path)
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "INSERT OR IGNORE INTO Libraries (Name, Path) VALUES (@name, @path)";
            command.Parameters.AddWithValue("@name", name);
            command.Parameters.AddWithValue("@path", path);
            command.ExecuteNonQuery();
        }

        public static List<Library> GetAllLibraries()
        {
            var libraries = new List<Library>();
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT Id, Name, Path FROM Libraries ORDER BY Name";
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                libraries.Add(new Library
                {
                    Id = reader.GetInt32(0),
                    Name = reader.GetString(1),
                    Path = reader.GetString(2)
                });
            }
            return libraries;
        }
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
        public string Path { get; set; }
    }
}