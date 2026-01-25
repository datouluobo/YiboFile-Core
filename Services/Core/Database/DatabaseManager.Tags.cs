using System;
using System.Collections.Generic;
using Microsoft.Data.Sqlite;
using YiboFile.Services.Features;

namespace YiboFile
{
    public static partial class DatabaseManager
    {
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
            command.CommandText = "DELETE FROM TagGroups WHERE Id = @id";
            command.Parameters.AddWithValue("@id", groupId);
            command.ExecuteNonQuery();
        }

        public static List<ITagGroup> GetTagGroups()
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

        public static List<ITag> GetTagsByGroup(int groupId)
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

        public static List<ITag> GetFileTags(string filePath)
        {
            var result = new List<ITag>();
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

            int tagId = -1;
            using (var findCommand = connection.CreateCommand())
            {
                findCommand.CommandText = "SELECT Id FROM Tags WHERE Name = @name";
                findCommand.Parameters.AddWithValue("@name", tagName);
                var idObj = findCommand.ExecuteScalar();
                if (idObj == null || idObj == DBNull.Value) return result;
                tagId = Convert.ToInt32(idObj);
            }

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

        public static ITag GetTag(int tagId)
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

        public static List<ITag> GetAllTags()
        {
            var result = new List<ITag>();
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

        public static List<ITag> GetUngroupedTags()
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

        private class TagGroupModel : ITagGroup
        {
            public int Id { get; set; }
            public string Name { get; set; }
            public string Color { get; set; }
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
