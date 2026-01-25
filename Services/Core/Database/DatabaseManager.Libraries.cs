using System;
using System.Collections.Generic;
using Microsoft.Data.Sqlite;
using YiboFile.Models;

namespace YiboFile
{
    public static partial class DatabaseManager
    {
        public static int AddLibrary(string name)
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            using var checkCommand = connection.CreateCommand();
            checkCommand.CommandText = "SELECT Id FROM Libraries WHERE Name = @name";
            checkCommand.Parameters.AddWithValue("@name", name);
            var existingId = checkCommand.ExecuteScalar();

            if (existingId != null && existingId != DBNull.Value)
            {
                return -Convert.ToInt32(existingId);
            }

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

            using var command = connection.CreateCommand();
            command.CommandText = "SELECT Id, Name, DisplayOrder FROM Libraries ORDER BY DisplayOrder, Id";
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                var libraryId = reader.GetInt32(0);
                var libraryName = reader.GetString(1);
                var displayOrder = reader.IsDBNull(2) ? 0 : reader.GetInt32(2);

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

            using var getCurrentCommand = connection.CreateCommand();
            getCurrentCommand.CommandText = "SELECT DisplayOrder FROM Libraries WHERE Id = @id";
            getCurrentCommand.Parameters.AddWithValue("@id", libraryId);
            var currentOrder = getCurrentCommand.ExecuteScalar();

            if (currentOrder == null || currentOrder == DBNull.Value) return;
            var currentOrderValue = Convert.ToInt32(currentOrder);

            using var getPrevCommand = connection.CreateCommand();
            getPrevCommand.CommandText = "SELECT Id, DisplayOrder FROM Libraries WHERE DisplayOrder < @order ORDER BY DisplayOrder DESC LIMIT 1";
            getPrevCommand.Parameters.AddWithValue("@order", currentOrderValue);
            using var prevReader = getPrevCommand.ExecuteReader();

            if (prevReader.Read())
            {
                var prevId = prevReader.GetInt32(0);
                var prevOrder = prevReader.GetInt32(1);

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

            using var getCurrentCommand = connection.CreateCommand();
            getCurrentCommand.CommandText = "SELECT DisplayOrder FROM Libraries WHERE Id = @id";
            getCurrentCommand.Parameters.AddWithValue("@id", libraryId);
            var currentOrder = getCurrentCommand.ExecuteScalar();

            if (currentOrder == null || currentOrder == DBNull.Value) return;
            var currentOrderValue = Convert.ToInt32(currentOrder);

            using var getNextCommand = connection.CreateCommand();
            getNextCommand.CommandText = "SELECT Id, DisplayOrder FROM Libraries WHERE DisplayOrder > @order ORDER BY DisplayOrder ASC LIMIT 1";
            getNextCommand.Parameters.AddWithValue("@order", currentOrderValue);
            using var nextReader = getNextCommand.ExecuteReader();

            if (nextReader.Read())
            {
                var nextId = nextReader.GetInt32(0);
                var nextOrder = nextReader.GetInt32(1);

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

            using (var command = connection.CreateCommand())
            {
                command.CommandText = "SELECT Name, DisplayOrder FROM Libraries WHERE Id = @libraryId";
                command.Parameters.AddWithValue("@libraryId", libraryId);
                using var reader = command.ExecuteReader();

                if (!reader.Read()) return null;

                name = reader.GetString(0);
                displayOrder = reader.IsDBNull(1) ? 0 : reader.GetInt32(1);
            }

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
    }
}
