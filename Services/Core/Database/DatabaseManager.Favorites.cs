using System;
using System.Collections.Generic;
using Microsoft.Data.Sqlite;
using YiboFile.Models;

namespace YiboFile
{
    public static partial class DatabaseManager
    {
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
    }
}
