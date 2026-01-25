using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using YiboFile.Services.Config;
using YiboFile.Services.Features;

namespace YiboFile.Services.Data.Repositories
{
    /// <summary>
    /// SQLite 实现的标签存储库
    /// </summary>
    public class SqliteTagsRepository : ITagsRepository
    {
        private readonly string _connectionString;

        public SqliteTagsRepository(string connectionString = null)
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
        }

        #region 同步方法实现 (从 DatabaseManager 迁移)

        public int AddTagGroup(string name, string color = null)
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "INSERT INTO TagGroups (Name, Color) VALUES (@name, @color); SELECT last_insert_rowid();";
            command.Parameters.AddWithValue("@name", name);
            command.Parameters.AddWithValue("@color", color ?? (object)DBNull.Value);
            return Convert.ToInt32(command.ExecuteScalar());
        }

        public void RenameTagGroup(int groupId, string newName)
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "UPDATE TagGroups SET Name = @name WHERE Id = @id";
            command.Parameters.AddWithValue("@name", newName);
            command.Parameters.AddWithValue("@id", groupId);
            command.ExecuteNonQuery();
        }

        public void DeleteTagGroup(int groupId)
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "DELETE FROM TagGroups WHERE Id = @id";
            command.Parameters.AddWithValue("@id", groupId);
            command.ExecuteNonQuery();
        }

        public List<ITagGroup> GetTagGroups()
        {
            var result = new List<ITagGroup>();
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

        public int AddTag(int groupId, string name, string color = null)
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

        public void RenameTag(int tagId, string newName)
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "UPDATE Tags SET Name = @name WHERE Id = @id";
            command.Parameters.AddWithValue("@name", newName);
            command.Parameters.AddWithValue("@id", tagId);
            command.ExecuteNonQuery();
        }

        public void UpdateTagColor(int tagId, string color)
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "UPDATE Tags SET Color = @color WHERE Id = @id";
            command.Parameters.AddWithValue("@color", color ?? "");
            command.Parameters.AddWithValue("@id", tagId);
            command.ExecuteNonQuery();
        }

        public void DeleteTag(int tagId)
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "DELETE FROM Tags WHERE Id = @id";
            command.Parameters.AddWithValue("@id", tagId);
            command.ExecuteNonQuery();
        }

        public void UpdateTagGroup(int tagId, int newGroupId)
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "UPDATE Tags SET GroupId = @groupId WHERE Id = @id";
            command.Parameters.AddWithValue("@groupId", newGroupId);
            command.Parameters.AddWithValue("@id", tagId);
            command.ExecuteNonQuery();
        }

        public ITag GetTag(int tagId)
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT Id, Name, Color, IFNULL(GroupId, 0) FROM Tags WHERE Id = @id";
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

        public List<ITag> GetTagsByGroup(int groupId)
        {
            var result = new List<ITag>();
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

        public List<ITag> GetAllTags()
        {
            var result = new List<ITag>();
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT Id, Name, Color, IFNULL(GroupId, 0) FROM Tags ORDER BY Name";
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

        public List<ITag> GetUngroupedTags()
        {
            var result = new List<ITag>();
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

        public void AddTagToFile(string filePath, int tagId)
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "INSERT OR IGNORE INTO FileTags (FilePath, TagId) VALUES (@path, @tagId)";
            command.Parameters.AddWithValue("@path", filePath);
            command.Parameters.AddWithValue("@tagId", tagId);
            command.ExecuteNonQuery();
        }

        public void RemoveTagFromFile(string filePath, int tagId)
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "DELETE FROM FileTags WHERE FilePath = @path AND TagId = @tagId";
            command.Parameters.AddWithValue("@path", filePath);
            command.Parameters.AddWithValue("@tagId", tagId);
            command.ExecuteNonQuery();
        }

        public List<ITag> GetFileTags(string filePath)
        {
            var result = new List<ITag>();
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT t.Id, t.Name, t.Color, IFNULL(t.GroupId, 0) 
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

        public List<string> GetFilesByTag(int tagId)
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

        public List<string> GetFilesByTagName(string tagName)
        {
            var result = new List<string>();
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            // 首先查找 TagId
            int tagId = -1;
            using (var findCommand = connection.CreateCommand())
            {
                findCommand.CommandText = "SELECT Id FROM Tags WHERE Name = @name";
                findCommand.Parameters.AddWithValue("@name", tagName);
                var idObj = findCommand.ExecuteScalar();
                if (idObj == null || idObj == DBNull.Value) return result;
                tagId = Convert.ToInt32(idObj);
            }

            // 然后查找文件
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

        #endregion

        #region 异步方法包装

        public Task<int> AddTagGroupAsync(string name, string color = null) => Task.Run(() => AddTagGroup(name, color));
        public Task RenameTagGroupAsync(int groupId, string newName) => Task.Run(() => RenameTagGroup(groupId, newName));
        public Task DeleteTagGroupAsync(int groupId) => Task.Run(() => DeleteTagGroup(groupId));
        public Task<List<ITagGroup>> GetTagGroupsAsync() => Task.Run(() => GetTagGroups());

        public Task<int> AddTagAsync(int groupId, string name, string color = null) => Task.Run(() => AddTag(groupId, name, color));
        public Task RenameTagAsync(int tagId, string newName) => Task.Run(() => RenameTag(tagId, newName));
        public Task UpdateTagColorAsync(int tagId, string color) => Task.Run(() => UpdateTagColor(tagId, color));
        public Task DeleteTagAsync(int tagId) => Task.Run(() => DeleteTag(tagId));
        public Task UpdateTagGroupAsync(int tagId, int newGroupId) => Task.Run(() => UpdateTagGroup(tagId, newGroupId));
        public Task<ITag> GetTagAsync(int tagId) => Task.Run(() => GetTag(tagId));
        public Task<List<ITag>> GetTagsByGroupAsync(int groupId) => Task.Run(() => GetTagsByGroup(groupId));
        public Task<List<ITag>> GetAllTagsAsync() => Task.Run(() => GetAllTags());
        public Task<List<ITag>> GetUngroupedTagsAsync() => Task.Run(() => GetUngroupedTags());

        public Task AddTagToFileAsync(string filePath, int tagId) => Task.Run(() => AddTagToFile(filePath, tagId));
        public Task RemoveTagFromFileAsync(string filePath, int tagId) => Task.Run(() => RemoveTagFromFile(filePath, tagId));
        public Task<List<ITag>> GetFileTagsAsync(string filePath) => Task.Run(() => GetFileTags(filePath));
        public Task<List<string>> GetFilesByTagAsync(int tagId) => Task.Run(() => GetFilesByTag(tagId));
        public Task<List<string>> GetFilesByTagNameAsync(string tagName) => Task.Run(() => GetFilesByTagName(tagName));

        #endregion

        #region 模型类

        private class TagGroupModel : ITagGroup
        {
            public int Id { get; set; }
            public string Name { get; set; }
            public string Color { get; set; }
            public int Count { get; set; } // ITagGroup 需要 Count，虽然数据库不存，但作为视图模型需要
        }

        private class TagModel : ITag
        {
            public int Id { get; set; }
            public string Name { get; set; }
            public string Color { get; set; }
            public int GroupId { get; set; }
        }

        #endregion
    }
}
