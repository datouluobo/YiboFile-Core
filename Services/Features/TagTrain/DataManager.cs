using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Data.Sqlite;

namespace TagTrain.Services
{
    /// <summary>
    /// 训练数据管理器
    /// </summary>
    public static class DataManager
    {
        private static string _customDbPath = null;

        private static string GetDbPath()
        {
            if (!string.IsNullOrEmpty(_customDbPath))
            {
                return _customDbPath;
            }

            // 从统一配置管理器读取
            return SettingsManager.GetDatabasePath();
        }

        private static string GetConnectionString()
        {
            var dbPath = GetDbPath();
            var dbDir = Path.GetDirectoryName(dbPath);
            if (!string.IsNullOrEmpty(dbDir))
            {
                Directory.CreateDirectory(dbDir);
            }
            return $"Data Source={dbPath}";
        }

        /// <summary>
        /// 设置数据库路径（已废弃，请使用SettingsManager.SetDataStorageDirectory）
        /// </summary>
        [Obsolete("请使用 SettingsManager.SetDataStorageDirectory 来设置数据保存目录")]
        public static void SetDatabasePath(string folderPath)
        {
            // 兼容旧代码，直接设置数据保存目录
            if (!string.IsNullOrEmpty(folderPath))
            {
                SettingsManager.SetDataStorageDirectory(folderPath);
                SettingsManager.ClearCache();
            }
        }

        /// <summary>
        /// 获取当前数据库路径
        /// </summary>
        public static string GetDatabasePath()
        {
            return GetDbPath();
        }

        /// <summary>
        /// 清除数据库路径缓存（用于路径更改后重新加载）
        /// </summary>
        public static void ClearDatabasePathCache()
        {
            _customDbPath = null;
        }

        /// <summary>
        /// 关闭所有数据库连接以释放文件锁
        /// </summary>
        public static void Shutdown()
        {
            try
            {
                SqliteConnection.ClearAllPools();
            }
            catch { }
        }

        // ========== 模型版本/增量训练辅助 ==========
        public static DateTime? GetLatestModelTrainedAt()
        {
            using var connection = new SqliteConnection(GetConnectionString());
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT MAX(CreatedAt) FROM ModelVersions";
            var val = command.ExecuteScalar();
            if (val == null || val == DBNull.Value) return null;
            try { return Convert.ToDateTime(val); } catch { return null; }
        }

        public static void AddModelVersion(string version, string modelPath, int trainingSamples, double? accuracy = null)
        {
            using var connection = new SqliteConnection(GetConnectionString());
            connection.Open();
            using var cmd = connection.CreateCommand();
            cmd.CommandText = @"
                INSERT INTO ModelVersions(Version, ModelPath, Accuracy, TrainingSamples, CreatedAt)
                VALUES(@v, @p, @a, @s, CURRENT_TIMESTAMP)";
            cmd.Parameters.AddWithValue("@v", version ?? "unknown");
            cmd.Parameters.AddWithValue("@p", modelPath ?? "");
            if (accuracy.HasValue) cmd.Parameters.AddWithValue("@a", accuracy.Value); else cmd.Parameters.AddWithValue("@a", DBNull.Value);
            cmd.Parameters.AddWithValue("@s", trainingSamples);
            cmd.ExecuteNonQuery();
        }

        public static List<TrainingSample> LoadManualSamplesAfter(DateTime since)
        {
            var list = new List<TrainingSample>();
            using var connection = new SqliteConnection(GetConnectionString());
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = @"SELECT ImagePath, TagId, IsManual, COALESCE(Confidence,1.0)
                                    FROM TrainingData
                                    WHERE IsManual = 1 AND CreatedAt > @since";
            command.Parameters.AddWithValue("@since", since);
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                list.Add(new TrainingSample
                {
                    ImagePath = reader.GetString(0),
                    TagId = reader.GetInt32(1),
                    IsManual = reader.GetInt32(2) == 1,
                    Confidence = reader.GetDouble(3)
                });
            }
            return list;
        }

        /// <summary>
        /// 初始化数据库
        /// </summary>
        public static void InitializeDatabase()
        {
            var dbPath = GetConnectionString();
            using var connection = new SqliteConnection(dbPath);
            connection.Open();

            var createTrainingDataTable = @"
                CREATE TABLE IF NOT EXISTS TrainingData (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    ImagePath TEXT NOT NULL,
                    TagId INTEGER NOT NULL,
                    Confidence REAL DEFAULT 1.0,
                    IsManual INTEGER DEFAULT 1,
                    CreatedAt DATETIME DEFAULT CURRENT_TIMESTAMP
                )";

            var createModelVersionsTable = @"
                CREATE TABLE IF NOT EXISTS ModelVersions (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Version TEXT NOT NULL,
                    ModelPath TEXT NOT NULL,
                    Accuracy REAL,
                    TrainingSamples INTEGER,
                    CreatedAt DATETIME DEFAULT CURRENT_TIMESTAMP
                )";

            var createTagNamesTable = @"
                CREATE TABLE IF NOT EXISTS TagNames (
                    TagId INTEGER PRIMARY KEY,
                    TagName TEXT NOT NULL,
                    CreatedAt DATETIME DEFAULT CURRENT_TIMESTAMP,
                    UpdatedAt DATETIME DEFAULT CURRENT_TIMESTAMP
                )";

            var createTagCategoriesTable = @"
                CREATE TABLE IF NOT EXISTS TagCategories (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Name TEXT NOT NULL UNIQUE,
                    Color TEXT,
                    SortOrder INTEGER DEFAULT 0,
                    CreatedAt DATETIME DEFAULT CURRENT_TIMESTAMP,
                    UpdatedAt DATETIME DEFAULT CURRENT_TIMESTAMP
                )";

            var createTagCategoryMappingTable = @"
                CREATE TABLE IF NOT EXISTS TagCategoryMapping (
                    TagId INTEGER NOT NULL,
                    CategoryId INTEGER NOT NULL,
                    PRIMARY KEY (TagId, CategoryId),
                    FOREIGN KEY (CategoryId) REFERENCES TagCategories (Id) ON DELETE CASCADE
                )";

            // 创建索引提高查询性能
            var createIndexes = @"
                CREATE INDEX IF NOT EXISTS idx_trainingdata_imagepath ON TrainingData(ImagePath);
                CREATE INDEX IF NOT EXISTS idx_trainingdata_tagid ON TrainingData(TagId);
                CREATE INDEX IF NOT EXISTS idx_tagcategorymapping_tagid ON TagCategoryMapping(TagId);
                CREATE INDEX IF NOT EXISTS idx_tagcategorymapping_categoryid ON TagCategoryMapping(CategoryId);
            ";

            using var command = connection.CreateCommand();
            command.CommandText = createTrainingDataTable;
            command.ExecuteNonQuery();
            
            command.CommandText = createModelVersionsTable;
            command.ExecuteNonQuery();

            command.CommandText = createTagNamesTable;
            command.ExecuteNonQuery();

            command.CommandText = createTagCategoriesTable;
            command.ExecuteNonQuery();

            command.CommandText = createTagCategoryMappingTable;
            command.ExecuteNonQuery();

            command.CommandText = createIndexes;
            command.ExecuteNonQuery();
        }

        /// <summary>
        /// 保存标签名称
        /// </summary>
        public static void SaveTagName(int tagId, string tagName)
        {
            using var connection = new SqliteConnection(GetConnectionString());
            connection.Open();

            var insertOrUpdate = @"
                INSERT INTO TagNames (TagId, TagName, UpdatedAt)
                VALUES (@tagId, @tagName, CURRENT_TIMESTAMP)
                ON CONFLICT(TagId) DO UPDATE SET 
                    TagName = @tagName,
                    UpdatedAt = CURRENT_TIMESTAMP";

            using var command = connection.CreateCommand();
            command.CommandText = insertOrUpdate;
            command.Parameters.AddWithValue("@tagId", tagId);
            command.Parameters.AddWithValue("@tagName", tagName);
            command.ExecuteNonQuery();
        }

        /// <summary>
        /// 获取标签名称
        /// </summary>
        public static string GetTagName(int tagId)
        {
            using var connection = new SqliteConnection(GetConnectionString());
            connection.Open();

            var select = "SELECT TagName FROM TagNames WHERE TagId = @tagId";
            using var command = connection.CreateCommand();
            command.CommandText = select;
            command.Parameters.AddWithValue("@tagId", tagId);

            var result = command.ExecuteScalar();
            return result?.ToString() ?? $"标签{tagId}";
        }

        /// <summary>
        /// 获取所有标签名称
        /// </summary>
        public static Dictionary<int, string> GetAllTagNames()
        {
            var tagNames = new Dictionary<int, string>();
            
            using var connection = new SqliteConnection(GetConnectionString());
            connection.Open();

            var select = "SELECT TagId, TagName FROM TagNames";
            using var command = connection.CreateCommand();
            command.CommandText = select;

            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                tagNames[reader.GetInt32(0)] = reader.GetString(1);
            }

            return tagNames;
        }

        /// <summary>
        /// 更新标签名称（只修改名称，保留所有相关数据）
        /// </summary>
        /// <param name="oldTagName">旧标签名称</param>
        /// <param name="newTagName">新标签名称</param>
        /// <returns>是否更新成功</returns>
        public static bool UpdateTagName(string oldTagName, string newTagName)
        {
            if (string.IsNullOrWhiteSpace(oldTagName) || string.IsNullOrWhiteSpace(newTagName))
            {
                return false;
            }

            if (oldTagName == newTagName)
            {
                return true; // 名称相同，无需更新
            }

            using var connection = new SqliteConnection(GetConnectionString());
            connection.Open();

            using var transaction = connection.BeginTransaction();
            try
            {
                // 查找旧标签名称对应的TagId
                var select = "SELECT TagId FROM TagNames WHERE TagName = @oldTagName LIMIT 1";
                using var selectCommand = connection.CreateCommand();
                selectCommand.CommandText = select;
                selectCommand.Parameters.AddWithValue("@oldTagName", oldTagName);
                selectCommand.Transaction = transaction;
                
                var result = selectCommand.ExecuteScalar();
                if (result == null || result == DBNull.Value)
                {
                    // 旧标签不存在
                    transaction.Rollback();
                    return false;
                }

                var tagId = Convert.ToInt32(result);

                // 检查新标签名称是否已存在（如果存在且不是同一个TagId，则冲突）
                var checkNew = "SELECT TagId FROM TagNames WHERE TagName = @newTagName LIMIT 1";
                using var checkCommand = connection.CreateCommand();
                checkCommand.CommandText = checkNew;
                checkCommand.Parameters.AddWithValue("@newTagName", newTagName);
                checkCommand.Transaction = transaction;
                
                var existingTagId = checkCommand.ExecuteScalar();
                if (existingTagId != null && existingTagId != DBNull.Value)
                {
                    var existingId = Convert.ToInt32(existingTagId);
                    if (existingId != tagId)
                    {
                        // 新标签名称已存在且属于不同的TagId，冲突
                        transaction.Rollback();
                        return false;
                    }
                }

                // 更新标签名称（TagId保持不变，所有训练数据自动保留）
                // 直接在事务中执行，避免创建新连接导致数据库锁定
                var updateTagName = @"
                    INSERT INTO TagNames (TagId, TagName, UpdatedAt)
                    VALUES (@tagId, @tagName, CURRENT_TIMESTAMP)
                    ON CONFLICT(TagId) DO UPDATE SET 
                        TagName = @tagName,
                        UpdatedAt = CURRENT_TIMESTAMP";
                
                using var updateCommand = connection.CreateCommand();
                updateCommand.CommandText = updateTagName;
                updateCommand.Parameters.AddWithValue("@tagId", tagId);
                updateCommand.Parameters.AddWithValue("@tagName", newTagName);
                updateCommand.Transaction = transaction;
                updateCommand.ExecuteNonQuery();

                transaction.Commit();
                return true;
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
        }

        /// <summary>
        /// 根据标签名称查找或创建TagId（确保同名标签使用相同的TagId）
        /// </summary>
        public static int GetOrCreateTagId(string tagName)
        {
            using var connection = new SqliteConnection(GetConnectionString());
            connection.Open();

            // 先查找是否已存在该标签名称
            var select = "SELECT TagId FROM TagNames WHERE TagName = @tagName LIMIT 1";
            using var selectCommand = connection.CreateCommand();
            selectCommand.CommandText = select;
            selectCommand.Parameters.AddWithValue("@tagName", tagName);
            
            var result = selectCommand.ExecuteScalar();
            if (result != null && result != DBNull.Value)
            {
                // 已存在，返回现有的TagId
                return Convert.ToInt32(result);
            }

            // 不存在，创建新的TagId（使用标签名称的哈希值）
            var tagId = tagName.GetHashCode();
            if (tagId < 0) tagId = Math.Abs(tagId);

            // 确保TagId唯一（如果冲突，尝试递增）
            while (true)
            {
                var checkExists = "SELECT COUNT(*) FROM TagNames WHERE TagId = @tagId";
                using var checkCommand = connection.CreateCommand();
                checkCommand.CommandText = checkExists;
                checkCommand.Parameters.AddWithValue("@tagId", tagId);
                
                var count = Convert.ToInt32(checkCommand.ExecuteScalar());
                if (count == 0)
                {
                    // TagId可用，保存标签名称
                    SaveTagName(tagId, tagName);
                    return tagId;
                }
                
                // TagId冲突，递增
                tagId++;
            }
        }

        /// <summary>
        /// 保存训练样本
        /// </summary>
        public static void SaveTrainingSample(string imagePath, int tagId, bool isManual = true, double confidence = 1.0, string tagName = null)
        {
            using var connection = new SqliteConnection(GetConnectionString());
            connection.Open();

            // 检查是否已存在相同的样本，如果存在则更新
            var checkExists = "SELECT Id FROM TrainingData WHERE ImagePath = @imagePath AND TagId = @tagId";
            using var checkCommand = connection.CreateCommand();
            checkCommand.CommandText = checkExists;
            checkCommand.Parameters.AddWithValue("@imagePath", imagePath);
            checkCommand.Parameters.AddWithValue("@tagId", tagId);
            
            var existingId = checkCommand.ExecuteScalar();

            if (existingId != null)
            {
                // 更新现有记录
                var update = @"
                    UPDATE TrainingData 
                    SET Confidence = @confidence, IsManual = @isManual, CreatedAt = CURRENT_TIMESTAMP
                    WHERE Id = @id";
                
                using var updateCommand = connection.CreateCommand();
                updateCommand.CommandText = update;
                updateCommand.Parameters.AddWithValue("@id", existingId);
                updateCommand.Parameters.AddWithValue("@confidence", confidence);
                updateCommand.Parameters.AddWithValue("@isManual", isManual ? 1 : 0);
                updateCommand.ExecuteNonQuery();
            }
            else
            {
                // 插入新记录
                var insert = @"
                    INSERT INTO TrainingData (ImagePath, TagId, Confidence, IsManual)
                    VALUES (@imagePath, @tagId, @confidence, @isManual)";

                using var insertCommand = connection.CreateCommand();
                insertCommand.CommandText = insert;
                insertCommand.Parameters.AddWithValue("@imagePath", imagePath);
                insertCommand.Parameters.AddWithValue("@tagId", tagId);
                insertCommand.Parameters.AddWithValue("@confidence", confidence);
                insertCommand.Parameters.AddWithValue("@isManual", isManual ? 1 : 0);
                insertCommand.ExecuteNonQuery();
            }

            // 如果有标签名称，保存标签名称
            if (!string.IsNullOrEmpty(tagName))
            {
                SaveTagName(tagId, tagName);
            }
        }

        /// <summary>
        /// 加载所有训练数据
        /// </summary>
        public static List<TrainingSample> LoadAllTrainingData()
        {
            var samples = new List<TrainingSample>();
            
            using var connection = new SqliteConnection(GetConnectionString());
            connection.Open();

            var select = "SELECT ImagePath, TagId, IsManual, Confidence FROM TrainingData";
            using var command = connection.CreateCommand();
            command.CommandText = select;
            
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                samples.Add(new TrainingSample
                {
                    ImagePath = reader.GetString(0),
                    TagId = reader.GetInt32(1),
                    IsManual = reader.GetInt32(2) == 1,
                    Confidence = reader.GetDouble(3)
                });
            }

            return samples;
        }

        /// <summary>
        /// 获取未标注的图片列表
        /// </summary>
        public static List<string> GetUnlabeledImages(string directory)
        {
            if (!Directory.Exists(directory))
                return new List<string>();

            var allImages = Directory.GetFiles(directory, "*.*", SearchOption.AllDirectories)
                .Where(f => IsImageFile(f))
                .ToList();

            var labeledImages = new HashSet<string>(
                LoadAllTrainingData().Select(s => s.ImagePath));

            return allImages.Where(img => !labeledImages.Contains(img)).ToList();
        }

        /// <summary>
        /// 获取指定图片的标签
        /// </summary>
        public static List<int> GetImageTags(string imagePath)
        {
            var tags = new List<int>();
            
            using var connection = new SqliteConnection(GetConnectionString());
            connection.Open();

            var select = "SELECT TagId FROM TrainingData WHERE ImagePath = @imagePath";
            using var command = connection.CreateCommand();
            command.CommandText = select;
            command.Parameters.AddWithValue("@imagePath", imagePath);
            
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                tags.Add(reader.GetInt32(0));
            }

            return tags;
        }

        /// <summary>
        /// 删除指定图片和标签的训练样本
        /// </summary>
        public static void DeleteTrainingSample(string imagePath, int tagId)
        {
            using var connection = new SqliteConnection(GetConnectionString());
            connection.Open();

            var delete = "DELETE FROM TrainingData WHERE ImagePath = @imagePath AND TagId = @tagId";
            using var command = connection.CreateCommand();
            command.CommandText = delete;
            command.Parameters.AddWithValue("@imagePath", imagePath);
            command.Parameters.AddWithValue("@tagId", tagId);
            command.ExecuteNonQuery();
        }

        /// <summary>
        /// 删除标签及其所有训练数据
        /// </summary>
        public static void DeleteTag(int tagId)
        {
            using var connection = new SqliteConnection(GetConnectionString());
            connection.Open();

            using var transaction = connection.BeginTransaction();
            try
            {
                // 删除训练数据
                var deleteTraining = "DELETE FROM TrainingData WHERE TagId = @tagId";
                using var deleteTrainingCommand = connection.CreateCommand();
                deleteTrainingCommand.CommandText = deleteTraining;
                deleteTrainingCommand.Parameters.AddWithValue("@tagId", tagId);
                deleteTrainingCommand.ExecuteNonQuery();

                // 删除标签名称
                var deleteTagName = "DELETE FROM TagNames WHERE TagId = @tagId";
                using var deleteTagNameCommand = connection.CreateCommand();
                deleteTagNameCommand.CommandText = deleteTagName;
                deleteTagNameCommand.Parameters.AddWithValue("@tagId", tagId);
                deleteTagNameCommand.ExecuteNonQuery();

                transaction.Commit();
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
        }

        public static TrainingStatistics GetStatistics()
        {
            using var connection = new SqliteConnection(GetConnectionString());
            connection.Open();

            var stats = new TrainingStatistics();

            // 总样本数
            var totalCount = "SELECT COUNT(*) FROM TrainingData";
            using var command = connection.CreateCommand();
            command.CommandText = totalCount;
            stats.TotalSamples = Convert.ToInt32(command.ExecuteScalar());

            // 手动标注数
            var manualCount = "SELECT COUNT(*) FROM TrainingData WHERE IsManual = 1";
            command.CommandText = manualCount;
            stats.ManualSamples = Convert.ToInt32(command.ExecuteScalar());

            // 唯一图片数
            var uniqueImages = "SELECT COUNT(DISTINCT ImagePath) FROM TrainingData";
            command.CommandText = uniqueImages;
            stats.UniqueImages = Convert.ToInt32(command.ExecuteScalar());

            // 唯一标签数
            var uniqueTags = "SELECT COUNT(DISTINCT TagId) FROM TrainingData";
            command.CommandText = uniqueTags;
            stats.UniqueTags = Convert.ToInt32(command.ExecuteScalar());

            return stats;
        }

        /// <summary>
        /// 清理并整合重复的标签（将同名标签合并到一个TagId，保留所有训练数据）
        /// </summary>
        /// <returns>返回清理结果：{合并的标签组数, 更新的训练数据条数, 删除的重复TagId数}</returns>
        public static (int MergedGroups, int UpdatedSamples, int DeletedTagIds) ConsolidateDuplicateTags()
        {
            using var connection = new SqliteConnection(GetConnectionString());
            connection.Open();

            using var transaction = connection.BeginTransaction();
            try
            {
                // 1. 查找所有重复的标签名称（多个TagId对应同一个名称）
                var findDuplicates = @"
                    SELECT TagName, GROUP_CONCAT(TagId) as TagIds
                    FROM TagNames
                    GROUP BY TagName
                    HAVING COUNT(*) > 1";
                
                using var findCommand = connection.CreateCommand();
                findCommand.CommandText = findDuplicates;
                findCommand.Transaction = transaction;
                
                var duplicateGroups = new List<(string TagName, List<int> TagIds)>();
                using var reader = findCommand.ExecuteReader();
                while (reader.Read())
                {
                    var tagName = reader.GetString(0);
                    var tagIdsStr = reader.GetString(1);
                    var tagIds = tagIdsStr.Split(',').Select(int.Parse).OrderBy(id => id).ToList();
                    duplicateGroups.Add((tagName, tagIds));
                }
                reader.Close();

                int mergedGroups = 0;
                int updatedSamples = 0;
                int deletedTagIds = 0;

                // 2. 对每个重复的标签组，选择主TagId（使用最小的TagId）
                foreach (var (tagName, tagIds) in duplicateGroups)
                {
                    if (tagIds.Count <= 1) continue;

                    var primaryTagId = tagIds[0]; // 使用最小的TagId作为主TagId
                    var duplicateTagIds = tagIds.Skip(1).ToList();

                    // 3. 将所有重复TagId的训练数据更新为主TagId
                    foreach (var duplicateTagId in duplicateTagIds)
                    {
                        // 检查是否有冲突（同一张图片已经有主TagId的标签）
                        var checkConflict = @"
                            SELECT td1.Id 
                            FROM TrainingData td1
                            INNER JOIN TrainingData td2 ON td1.ImagePath = td2.ImagePath
                            WHERE td1.TagId = @duplicateTagId 
                            AND td2.TagId = @primaryTagId";
                        
                        using var conflictCommand = connection.CreateCommand();
                        conflictCommand.CommandText = checkConflict;
                        conflictCommand.Parameters.AddWithValue("@duplicateTagId", duplicateTagId);
                        conflictCommand.Parameters.AddWithValue("@primaryTagId", primaryTagId);
                        conflictCommand.Transaction = transaction;
                        
                        var conflictIds = new List<int>();
                        using var conflictReader = conflictCommand.ExecuteReader();
                        while (conflictReader.Read())
                        {
                            conflictIds.Add(conflictReader.GetInt32(0));
                        }
                        conflictReader.Close();

                        // 如果有冲突，删除重复的记录（因为主TagId已经存在）
                        if (conflictIds.Count > 0)
                        {
                            var deleteConflicts = "DELETE FROM TrainingData WHERE Id IN (" + 
                                string.Join(",", conflictIds) + ")";
                            using var deleteCommand = connection.CreateCommand();
                            deleteCommand.CommandText = deleteConflicts;
                            deleteCommand.Transaction = transaction;
                            deleteCommand.ExecuteNonQuery();
                        }

                        // 更新所有非冲突的训练数据为主TagId
                        var updateTraining = @"
                            UPDATE TrainingData 
                            SET TagId = @primaryTagId 
                            WHERE TagId = @duplicateTagId";
                        
                        using var updateCommand = connection.CreateCommand();
                        updateCommand.CommandText = updateTraining;
                        updateCommand.Parameters.AddWithValue("@primaryTagId", primaryTagId);
                        updateCommand.Parameters.AddWithValue("@duplicateTagId", duplicateTagId);
                        updateCommand.Transaction = transaction;
                        
                        var rowsAffected = updateCommand.ExecuteNonQuery();
                        updatedSamples += rowsAffected;

                        // 4. 删除重复的TagId记录
                        var deleteTagName = "DELETE FROM TagNames WHERE TagId = @duplicateTagId";
                        using var deleteTagCommand = connection.CreateCommand();
                        deleteTagCommand.CommandText = deleteTagName;
                        deleteTagCommand.Parameters.AddWithValue("@duplicateTagId", duplicateTagId);
                        deleteTagCommand.Transaction = transaction;
                        deleteTagCommand.ExecuteNonQuery();
                        
                        deletedTagIds++;
                    }

                    mergedGroups++;
                }

                transaction.Commit();
                return (mergedGroups, updatedSamples, deletedTagIds);
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
        }

        // ========== 标签分组管理 ==========
        
        /// <summary>
        /// 标签分组
        /// </summary>
        public class TagCategory
        {
            public int Id { get; set; }
            public string Name { get; set; }
            public string Color { get; set; }
            public int SortOrder { get; set; }
            public DateTime CreatedAt { get; set; }
            public DateTime UpdatedAt { get; set; }
        }

        /// <summary>
        /// 获取所有分组
        /// </summary>
        public static List<TagCategory> GetAllCategories()
        {
            var categories = new List<TagCategory>();
            using var connection = new SqliteConnection(GetConnectionString());
            connection.Open();

            var select = "SELECT Id, Name, Color, SortOrder, CreatedAt, UpdatedAt FROM TagCategories ORDER BY SortOrder, Name";
            using var command = connection.CreateCommand();
            command.CommandText = select;

            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                categories.Add(new TagCategory
                {
                    Id = reader.GetInt32(0),
                    Name = reader.GetString(1),
                    Color = reader.IsDBNull(2) ? null : reader.GetString(2),
                    SortOrder = reader.GetInt32(3),
                    CreatedAt = reader.GetDateTime(4),
                    UpdatedAt = reader.GetDateTime(5)
                });
            }

            return categories;
        }

        /// <summary>
        /// 获取标签所属的分组ID列表
        /// </summary>
        public static List<int> GetTagCategories(int tagId)
        {
            var categoryIds = new List<int>();
            using var connection = new SqliteConnection(GetConnectionString());
            connection.Open();

            var select = "SELECT CategoryId FROM TagCategoryMapping WHERE TagId = @tagId";
            using var command = connection.CreateCommand();
            command.CommandText = select;
            command.Parameters.AddWithValue("@tagId", tagId);

            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                categoryIds.Add(reader.GetInt32(0));
            }

            return categoryIds;
        }

        /// <summary>
        /// 获取标签到分组的映射（批量加载，避免重复查询）
        /// </summary>
        public static Dictionary<int, List<int>> GetTagCategoryMap()
        {
            var map = new Dictionary<int, List<int>>();
            using var connection = new SqliteConnection(GetConnectionString());
            connection.Open();

            var select = "SELECT TagId, CategoryId FROM TagCategoryMapping";
            using var command = connection.CreateCommand();
            command.CommandText = select;

            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                var tagId = reader.GetInt32(0);
                var categoryId = reader.GetInt32(1);
                if (!map.TryGetValue(tagId, out var categories))
                {
                    categories = new List<int>();
                    map[tagId] = categories;
                }
                categories.Add(categoryId);
            }

            return map;
        }

        /// <summary>
        /// 获取分组下的所有标签ID列表
        /// </summary>
        public static List<int> GetCategoryTags(int categoryId)
        {
            var tagIds = new List<int>();
            using var connection = new SqliteConnection(GetConnectionString());
            connection.Open();

            var select = "SELECT TagId FROM TagCategoryMapping WHERE CategoryId = @categoryId";
            using var command = connection.CreateCommand();
            command.CommandText = select;
            command.Parameters.AddWithValue("@categoryId", categoryId);

            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                tagIds.Add(reader.GetInt32(0));
            }

            return tagIds;
        }

        /// <summary>
        /// 将标签分配到分组
        /// </summary>
        public static void AssignTagToCategory(int tagId, int categoryId)
        {
            using var connection = new SqliteConnection(GetConnectionString());
            connection.Open();

            var insert = @"
                INSERT OR IGNORE INTO TagCategoryMapping (TagId, CategoryId)
                VALUES (@tagId, @categoryId)";

            using var command = connection.CreateCommand();
            command.CommandText = insert;
            command.Parameters.AddWithValue("@tagId", tagId);
            command.Parameters.AddWithValue("@categoryId", categoryId);
            command.ExecuteNonQuery();
        }

        /// <summary>
        /// 从分组中移除标签
        /// </summary>
        public static void RemoveTagFromCategory(int tagId, int categoryId)
        {
            using var connection = new SqliteConnection(GetConnectionString());
            connection.Open();

            var delete = "DELETE FROM TagCategoryMapping WHERE TagId = @tagId AND CategoryId = @categoryId";

            using var command = connection.CreateCommand();
            command.CommandText = delete;
            command.Parameters.AddWithValue("@tagId", tagId);
            command.Parameters.AddWithValue("@categoryId", categoryId);
            command.ExecuteNonQuery();
        }

        /// <summary>
        /// 创建新分组
        /// </summary>
        public static int CreateCategory(string name, string color = null, int sortOrder = 0)
        {
            using var connection = new SqliteConnection(GetConnectionString());
            connection.Open();

            var insert = @"
                INSERT INTO TagCategories (Name, Color, SortOrder, CreatedAt, UpdatedAt)
                VALUES (@name, @color, @sortOrder, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP);
                SELECT last_insert_rowid();";

            using var command = connection.CreateCommand();
            command.CommandText = insert;
            command.Parameters.AddWithValue("@name", name);
            command.Parameters.AddWithValue("@color", color ?? (object)DBNull.Value);
            command.Parameters.AddWithValue("@sortOrder", sortOrder);

            var result = command.ExecuteScalar();
            return Convert.ToInt32(result);
        }

        /// <summary>
        /// 更新分组
        /// </summary>
        public static void UpdateCategory(int categoryId, string name, string color = null, int? sortOrder = null)
        {
            using var connection = new SqliteConnection(GetConnectionString());
            connection.Open();

            var update = @"
                UPDATE TagCategories 
                SET Name = @name, 
                    Color = @color, 
                    SortOrder = COALESCE(@sortOrder, SortOrder),
                    UpdatedAt = CURRENT_TIMESTAMP
                WHERE Id = @categoryId";

            using var command = connection.CreateCommand();
            command.CommandText = update;
            command.Parameters.AddWithValue("@categoryId", categoryId);
            command.Parameters.AddWithValue("@name", name);
            command.Parameters.AddWithValue("@color", color ?? (object)DBNull.Value);
            command.Parameters.AddWithValue("@sortOrder", sortOrder ?? (object)DBNull.Value);
            command.ExecuteNonQuery();
        }

        /// <summary>
        /// 删除分组
        /// </summary>
        public static void DeleteCategory(int categoryId)
        {
            using var connection = new SqliteConnection(GetConnectionString());
            connection.Open();

            using var transaction = connection.BeginTransaction();
            try
            {
                // 删除分组映射（CASCADE会自动处理，但显式删除更安全）
                var deleteMapping = "DELETE FROM TagCategoryMapping WHERE CategoryId = @categoryId";
                using var mappingCommand = connection.CreateCommand();
                mappingCommand.CommandText = deleteMapping;
                mappingCommand.Parameters.AddWithValue("@categoryId", categoryId);
                mappingCommand.Transaction = transaction;
                mappingCommand.ExecuteNonQuery();

                // 删除分组
                var deleteCategory = "DELETE FROM TagCategories WHERE Id = @categoryId";
                using var categoryCommand = connection.CreateCommand();
                categoryCommand.CommandText = deleteCategory;
                categoryCommand.Parameters.AddWithValue("@categoryId", categoryId);
                categoryCommand.Transaction = transaction;
                categoryCommand.ExecuteNonQuery();

                transaction.Commit();
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
        }

        /// <summary>
        /// 获取分组信息
        /// </summary>
        public static TagCategory GetCategory(int categoryId)
        {
            using var connection = new SqliteConnection(GetConnectionString());
            connection.Open();

            var select = "SELECT Id, Name, Color, SortOrder, CreatedAt, UpdatedAt FROM TagCategories WHERE Id = @categoryId";
            using var command = connection.CreateCommand();
            command.CommandText = select;
            command.Parameters.AddWithValue("@categoryId", categoryId);

            using var reader = command.ExecuteReader();
            if (reader.Read())
            {
                return new TagCategory
                {
                    Id = reader.GetInt32(0),
                    Name = reader.GetString(1),
                    Color = reader.IsDBNull(2) ? null : reader.GetString(2),
                    SortOrder = reader.GetInt32(3),
                    CreatedAt = reader.GetDateTime(4),
                    UpdatedAt = reader.GetDateTime(5)
                };
            }

            return null;
        }

        /// <summary>
        /// 检查文件是否为图片
        /// </summary>
        private static bool IsImageFile(string path)
        {
            var ext = Path.GetExtension(path).ToLower();
            return new[] { ".jpg", ".jpeg", ".png", ".bmp", ".gif", ".webp" }.Contains(ext);
        }
    }

    /// <summary>
    /// 训练样本
    /// </summary>
    public class TrainingSample
    {
        public string ImagePath { get; set; }
        public int TagId { get; set; }
        public bool IsManual { get; set; }
        public double Confidence { get; set; }
    }

    /// <summary>
    /// 训练统计信息
    /// </summary>
    public class TrainingStatistics
    {
        public int TotalSamples { get; set; }
        public int ManualSamples { get; set; }
        public int UniqueImages { get; set; }
        public int UniqueTags { get; set; }
    }
}

