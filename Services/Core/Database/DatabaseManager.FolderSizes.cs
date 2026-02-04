using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Data.Sqlite;

namespace YiboFile
{
    public static partial class DatabaseManager
    {
        #region 文件夹大小缓存

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

                    var currentLastModified = Directory.GetLastWriteTime(folderPath);
                    if (currentLastModified <= lastModified)
                    {
                        return sizeBytes;
                    }
                }
            }
            catch { }

            return null;
        }

        public static void SetFolderSize(string folderPath, long sizeBytes)
        {
            if (string.IsNullOrEmpty(folderPath))
                return;

            try
            {
                using var connection = new SqliteConnection(_connectionString);
                connection.Open();
                using var command = connection.CreateCommand();

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
            catch { }
        }

        public static Dictionary<string, long> GetFolderSizesBatch(List<string> folderPaths)
        {
            var result = new Dictionary<string, long>();
            if (folderPaths == null || folderPaths.Count == 0)
                return result;

            try
            {
                using var connection = new SqliteConnection(_connectionString);
                connection.Open();

                var existingPaths = folderPaths.Where(p => !string.IsNullOrEmpty(p) && Directory.Exists(p)).ToList();
                if (existingPaths.Count == 0)
                    return result;

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

                    try
                    {
                        var currentLastModified = Directory.GetLastWriteTime(folderPath);
                        if (currentLastModified <= lastModified)
                        {
                            result[folderPath] = sizeBytes;
                        }
                    }
                    catch { }
                }
            }
            catch { }

            return result;
        }

        public static Dictionary<string, (long SizeBytes, DateTime LastModified)> GetAllSubFolderSizes(string rootPath)
        {
            var result = new Dictionary<string, (long SizeBytes, DateTime LastModified)>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrEmpty(rootPath))
                return result;

            try
            {
                using var connection = new SqliteConnection(_connectionString);
                connection.Open();
                using var command = connection.CreateCommand();

                string searchPattern = rootPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar + "%";

                command.CommandText = @"
                    SELECT FolderPath, SizeBytes, LastModified 
                    FROM FolderSizes 
                    WHERE FolderPath LIKE @searchPattern";
                command.Parameters.AddWithValue("@searchPattern", searchPattern);

                using var reader = command.ExecuteReader();
                while (reader.Read())
                {
                    try
                    {
                        var folderPath = reader.GetString(0);
                        var sizeBytes = reader.GetInt64(1);
                        var lastModified = reader.GetDateTime(2);
                        result[folderPath] = (sizeBytes, lastModified);
                    }
                    catch { }
                }
            }
            catch { }

            return result;
        }

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
            catch { }
        }

        public static int CleanupNonExistentFolderSizes(int batchSize = 100, int maxProcessed = 1000)
        {
            int cleanedCount = 0;
            try
            {
                using var connection = new SqliteConnection(_connectionString);
                connection.Open();

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
                        break;

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
                            pathsToDelete.Add(path);
                        }
                    }

                    if (pathsToDelete.Count > 0)
                    {
                        using var deleteCommand = connection.CreateCommand();
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

                    if (pathsToDelete.Count == 0 && processed >= maxProcessed / 2)
                    {
                        break;
                    }
                }
            }
            catch { }

            return cleanedCount;
        }

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
}
