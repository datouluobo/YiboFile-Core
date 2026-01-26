using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using YiboFile.Services.Config;

namespace YiboFile.Services.Data.Repositories
{
    /// <summary>
    /// SQLite 实现的收藏存储库
    /// </summary>
    public class SqliteFavoriteRepository : IFavoriteRepository
    {
        private readonly string _connectionString;

        public SqliteFavoriteRepository(string connectionString = null)
        {
            if (string.IsNullOrEmpty(connectionString))
            {
                var dbPath = ConfigManager.GetDataFilePath();
                var dir = Path.GetDirectoryName(dbPath);
                if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                _connectionString = $"Data Source={dbPath}";
            }
            else
            {
                _connectionString = connectionString;
            }
        }

        #region 收藏项管理

        public List<YiboFile.Favorite> GetAllFavorites()
        {
            var favorites = new List<YiboFile.Favorite>();
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
                favorites.Add(new YiboFile.Favorite
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

        public async Task<List<YiboFile.Favorite>> GetAllFavoritesAsync()
        {
            var favorites = new List<YiboFile.Favorite>();
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();
            using var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT Id, Path, DisplayName, IsDirectory, CreatedAt, SortOrder, GroupId 
                FROM Favorites 
                ORDER BY SortOrder ASC, CreatedAt DESC";
            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                favorites.Add(new YiboFile.Favorite
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

        public void AddFavorite(string path, bool isDirectory, string displayName = null, int groupId = 1)
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

        public async Task AddFavoriteAsync(string path, bool isDirectory, string displayName = null, int groupId = 1)
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();
            using var command = connection.CreateCommand();
            command.CommandText = @"
                INSERT OR REPLACE INTO Favorites (Path, DisplayName, IsDirectory, GroupId, CreatedAt) 
                VALUES (@path, @displayName, @isDirectory, @groupId, CURRENT_TIMESTAMP)";
            command.Parameters.AddWithValue("@path", path);
            command.Parameters.AddWithValue("@displayName", displayName ?? (object)DBNull.Value);
            command.Parameters.AddWithValue("@isDirectory", isDirectory ? 1 : 0);
            command.Parameters.AddWithValue("@groupId", groupId);
            await command.ExecuteNonQueryAsync();
        }

        public void RemoveFavorite(string path)
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "DELETE FROM Favorites WHERE Path = @path";
            command.Parameters.AddWithValue("@path", path);
            command.ExecuteNonQuery();
        }

        public async Task RemoveFavoriteAsync(string path)
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();
            using var command = connection.CreateCommand();
            command.CommandText = "DELETE FROM Favorites WHERE Path = @path";
            command.Parameters.AddWithValue("@path", path);
            await command.ExecuteNonQueryAsync();
        }

        public bool IsFavorite(string path)
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT COUNT(*) FROM Favorites WHERE Path = @path";
            command.Parameters.AddWithValue("@path", path);
            var count = Convert.ToInt32(command.ExecuteScalar());
            return count > 0;
        }

        public async Task<bool> IsFavoriteAsync(string path)
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT COUNT(*) FROM Favorites WHERE Path = @path";
            command.Parameters.AddWithValue("@path", path);
            var count = Convert.ToInt32(await command.ExecuteScalarAsync());
            return count > 0;
        }

        public void UpdateSortOrder(int favoriteId, int newSortOrder)
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "UPDATE Favorites SET SortOrder = @sortOrder WHERE Id = @id";
            command.Parameters.AddWithValue("@sortOrder", newSortOrder);
            command.Parameters.AddWithValue("@id", favoriteId);
            command.ExecuteNonQuery();
        }

        public async Task UpdateSortOrderAsync(int favoriteId, int newSortOrder)
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();
            using var command = connection.CreateCommand();
            command.CommandText = "UPDATE Favorites SET SortOrder = @sortOrder WHERE Id = @id";
            command.Parameters.AddWithValue("@sortOrder", newSortOrder);
            command.Parameters.AddWithValue("@id", favoriteId);
            await command.ExecuteNonQueryAsync();
        }

        #endregion

        #region 分组管理

        public List<YiboFile.FavoriteGroup> GetAllGroups()
        {
            var result = new List<YiboFile.FavoriteGroup>();
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT Id, Name, SortOrder, CreatedAt FROM FavoriteGroups ORDER BY SortOrder ASC, CreatedAt ASC";
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                result.Add(new YiboFile.FavoriteGroup
                {
                    Id = reader.GetInt32(0),
                    Name = reader.GetString(1),
                    SortOrder = reader.GetInt32(2),
                    CreatedAt = reader.GetDateTime(3)
                });
            }
            return result;
        }

        public async Task<List<YiboFile.FavoriteGroup>> GetAllGroupsAsync()
        {
            var result = new List<YiboFile.FavoriteGroup>();
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT Id, Name, SortOrder, CreatedAt FROM FavoriteGroups ORDER BY SortOrder ASC, CreatedAt ASC";
            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                result.Add(new YiboFile.FavoriteGroup
                {
                    Id = reader.GetInt32(0),
                    Name = reader.GetString(1),
                    SortOrder = reader.GetInt32(2),
                    CreatedAt = reader.GetDateTime(3)
                });
            }
            return result;
        }

        public int CreateGroup(string name)
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "INSERT INTO FavoriteGroups (Name) VALUES (@name); SELECT last_insert_rowid();";
            command.Parameters.AddWithValue("@name", name);
            return Convert.ToInt32(command.ExecuteScalar());
        }

        public async Task<int> CreateGroupAsync(string name)
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();
            using var command = connection.CreateCommand();
            command.CommandText = "INSERT INTO FavoriteGroups (Name) VALUES (@name); SELECT last_insert_rowid();";
            command.Parameters.AddWithValue("@name", name);
            return Convert.ToInt32(await command.ExecuteScalarAsync());
        }

        public void RenameGroup(int id, string name)
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "UPDATE FavoriteGroups SET Name = @name WHERE Id = @id";
            command.Parameters.AddWithValue("@name", name);
            command.Parameters.AddWithValue("@id", id);
            command.ExecuteNonQuery();
        }

        public async Task RenameGroupAsync(int id, string name)
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();
            using var command = connection.CreateCommand();
            command.CommandText = "UPDATE FavoriteGroups SET Name = @name WHERE Id = @id";
            command.Parameters.AddWithValue("@name", name);
            command.Parameters.AddWithValue("@id", id);
            await command.ExecuteNonQueryAsync();
        }

        public void DeleteGroup(int id)
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "UPDATE Favorites SET GroupId = 1 WHERE GroupId = @id; DELETE FROM FavoriteGroups WHERE Id = @id AND Id != 1;";
            command.Parameters.AddWithValue("@id", id);
            command.ExecuteNonQuery();
        }

        public async Task DeleteGroupAsync(int id)
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();
            using var command = connection.CreateCommand();
            command.CommandText = "UPDATE Favorites SET GroupId = 1 WHERE GroupId = @id; DELETE FROM FavoriteGroups WHERE Id = @id AND Id != 1;";
            command.Parameters.AddWithValue("@id", id);
            await command.ExecuteNonQueryAsync();
        }

        public void UpdateGroupSortOrder(int id, int order)
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "UPDATE FavoriteGroups SET SortOrder = @order WHERE Id = @id";
            command.Parameters.AddWithValue("@order", order);
            command.Parameters.AddWithValue("@id", id);
            command.ExecuteNonQuery();
        }

        public async Task UpdateGroupSortOrderAsync(int id, int order)
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();
            using var command = connection.CreateCommand();
            command.CommandText = "UPDATE FavoriteGroups SET SortOrder = @order WHERE Id = @id";
            command.Parameters.AddWithValue("@order", order);
            command.Parameters.AddWithValue("@id", id);
            await command.ExecuteNonQueryAsync();
        }

        #endregion
    }
}
