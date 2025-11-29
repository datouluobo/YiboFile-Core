using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using TagTrain.Services;
using Microsoft.Data.Sqlite;

namespace OoiMRR.Services
{
    /// <summary>
    /// OoiMRR集成接口 - 可以被OoiMRR调用的静态方法
    /// </summary>
    public static class OoiMRRIntegration
    {
        public enum TagSortMode
        {
            Name,
            Count,
            Prediction // 预留；当前实现等同于 Name
        }
        private static ImageTagTrainer _trainer;
        private static bool _initialized = false;
        private static readonly object _lockObject = new object();

        /// <summary>
        /// 初始化TagTrain系统
        /// </summary>
        public static void Initialize()
        {
            if (_initialized) return;
            
            lock (_lockObject)
            {
                if (_initialized) return;
                
                try
                {
                    DataManager.InitializeDatabase();
                    _trainer = new ImageTagTrainer();
                    _trainer.LoadModel();
                    _initialized = true;
                }
                catch (Exception ex)
                {
                                    }
            }
        }

        /// <summary>
        /// 为图片自动预测标签（供OoiMRR调用）
        /// </summary>
        /// <param name="imagePath">图片路径</param>
        /// <returns>预测结果列表（按置信度降序，最多5个）</returns>
        public static List<TagPredictionResult> PredictTagsForImage(string imagePath)
        {
            Initialize();
            
            if (_trainer == null || string.IsNullOrEmpty(imagePath))
            {
                return new List<TagPredictionResult>();
            }

            try
            {
                return _trainer.PredictTags(imagePath);
            }
            catch (Exception ex)
            {
                                return new List<TagPredictionResult>();
            }
        }

        /// <summary>
        /// 批量预测标签
        /// </summary>
        /// <param name="imagePaths">图片路径列表</param>
        /// <returns>每个图片的预测结果字典</returns>
        public static Dictionary<string, List<TagPredictionResult>> PredictTagsForImages(
            List<string> imagePaths)
        {
            Initialize();
            
            var results = new Dictionary<string, List<TagPredictionResult>>();
            
            if (_trainer == null || imagePaths == null)
            {
                return results;
            }

            foreach (var path in imagePaths)
            {
                try
                {
                    results[path] = _trainer.PredictTags(path);
                }
                catch (Exception ex)
                {
                                        results[path] = new List<TagPredictionResult>();
                }
            }
            
            return results;
        }

        /// <summary>
        /// 添加训练样本（当用户在OoiMRR中手动标注时调用）
        /// </summary>
        /// <param name="imagePath">图片路径</param>
        /// <param name="tagId">标签ID</param>
        public static void AddTrainingSample(string imagePath, int tagId)
        {
            Initialize();
            
            try
            {
                DataManager.SaveTrainingSample(imagePath, tagId, isManual: true);
            }
            catch (Exception ex)
            {
                            }
        }

        /// <summary>
        /// 批量添加训练样本
        /// </summary>
        public static void AddTrainingSamples(Dictionary<string, List<int>> imageTags)
        {
            Initialize();
            
            if (imageTags == null) return;

            foreach (var kvp in imageTags)
            {
                foreach (var tagId in kvp.Value)
                {
                    try
                    {
                        DataManager.SaveTrainingSample(kvp.Key, tagId, isManual: true);
                    }
                    catch (Exception ex)
                    {
                                            }
                }
            }
        }

        /// <summary>
        /// 触发增量训练
        /// </summary>
        /// <param name="forceRetrain">是否强制重新训练（使用所有数据）</param>
        /// <param name="progress">进度报告</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>训练结果</returns>
        public static TrainingResult TriggerIncrementalTraining(
            bool forceRetrain = false, 
            IProgress<TrainingProgress> progress = null, 
            CancellationToken cancellationToken = default)
        {
            Initialize();
            
            if (_trainer == null)
            {
                return new TrainingResult
                {
                    Success = false,
                    Message = "训练器未初始化"
                };
            }

            try
            {
                if (forceRetrain)
                {
                    var all = DataManager.LoadAllTrainingData();
                    if (all.Count == 0)
                        return new TrainingResult { Success = false, Message = "没有训练数据" };
                    var result = _trainer.TrainModel(all, progress, cancellationToken);
                    if (result.Success)
                    {
                        DataManager.AddModelVersion($"full-{DateTime.Now:yyyyMMddHHmmss}", _trainer.GetModelPath(), all.Count);
                    }
                    return result;
                }
                else
                {
                    // 增量：基于最近一次模型训练时间获取新增手动样本
                    var last = DataManager.GetLatestModelTrainedAt();
                    List<TrainingSample> newSamples;
                    if (last.HasValue)
                        newSamples = DataManager.LoadManualSamplesAfter(last.Value);
                    else
                        newSamples = DataManager.LoadAllTrainingData().Where(s => s.IsManual).ToList();
                    
                    if (newSamples.Count == 0)
                        return new TrainingResult { Success = false, Message = "没有新增样本，无需增量训练" };
                    
                    var result = _trainer.IncrementalTrain(newSamples, progress, cancellationToken);
                    if (result.Success)
                    {
                        DataManager.AddModelVersion($"inc-{DateTime.Now:yyyyMMddHHmmss}", _trainer.GetModelPath(), newSamples.Count);
                    }
                    return result;
                }
            }
            catch (OperationCanceledException)
            {
                return new TrainingResult
                {
                    Success = false,
                    Message = "训练已取消"
                };
            }
            catch (Exception ex)
            {
                return new TrainingResult
                {
                    Success = false,
                    Message = $"训练失败: {ex.Message}"
                };
            }
        }

        /// <summary>
        /// 获取训练统计信息
        /// </summary>
        public static TrainingStatistics GetStatistics()
        {
            Initialize();
            
            try
            {
                return DataManager.GetStatistics();
            }
            catch
            {
                return new TrainingStatistics();
            }
        }

        /// <summary>
        /// 检查模型是否存在
        /// </summary>
        public static bool ModelExists()
        {
            Initialize();
            return _trainer != null && _trainer.ModelExists();
        }

        /// <summary>
        /// 获取模型路径
        /// </summary>
        public static string GetModelPath()
        {
            Initialize();
            return _trainer?.GetModelPath() ?? "";
        }
        
        /// <summary>
        /// 模型是否已加载
        /// </summary>
        public static bool IsModelLoaded()
        {
            Initialize();
            return _trainer != null && _trainer.IsModelLoaded();
        }

        /// <summary>
        /// 获取所有标签信息（包含标签ID、名称和使用次数）
        /// </summary>
        public static List<TagInfo> GetAllTags(TagSortMode sortMode = TagSortMode.Name)
        {
            Initialize();
            
            try
            {
                var tagNames = DataManager.GetAllTagNames();
                if (tagNames == null || tagNames.Count == 0)
                {
                    return new List<TagInfo>();
                }
                
                var tags = new List<TagInfo>();
                
                // 获取每个标签的使用次数
                var dbPath = DataManager.GetDatabasePath();
                if (string.IsNullOrEmpty(dbPath) || !File.Exists(dbPath))
                {
                    // 数据库不存在，返回空列表
                    return new List<TagInfo>();
                }
                
                var connectionString = $"Data Source={dbPath}";
                var tagCounts = new Dictionary<int, int>();
                
                using (var connection = new SqliteConnection(connectionString))
                {
                    connection.Open();
                    var command = connection.CreateCommand();
                    command.CommandText = "SELECT TagId, COUNT(*) FROM TrainingData GROUP BY TagId";
                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            if (reader.FieldCount >= 2)
                            {
                                tagCounts[reader.GetInt32(0)] = reader.GetInt32(1);
                            }
                        }
                    }
                }
                
                foreach (var kvp in tagNames)
                {
                    tags.Add(new TagInfo
                    {
                        Id = kvp.Key,
                        Name = kvp.Value ?? "",
                        Count = tagCounts.GetValueOrDefault(kvp.Key, 0)
                    });
                }

                // 按需排序，默认按名称
                var comparer = StringComparer.CurrentCultureIgnoreCase;
                switch (sortMode)
                {
                    case TagSortMode.Count:
                        return tags.OrderByDescending(t => t.Count).ThenBy(t => t.Name, comparer).ToList();
                    case TagSortMode.Prediction:
                        // 预留：当前无预测强度字段，退化为名称排序
                        return tags.OrderBy(t => t.Name, comparer).ToList();
                    case TagSortMode.Name:
                    default:
                        return tags.OrderBy(t => t.Name, comparer).ToList();
                }
            }
            catch (Exception ex)
            {
                                return new List<TagInfo>();
            }
        }

        /// <summary>
        /// 获取或创建标签ID
        /// </summary>
        public static int GetOrCreateTagId(string tagName)
        {
            Initialize();
            
            try
            {
                return DataManager.GetOrCreateTagId(tagName);
            }
            catch (Exception ex)
            {
                                return -1;
            }
        }

        /// <summary>
        /// 获取标签名称
        /// </summary>
        public static string GetTagName(int tagId)
        {
            Initialize();
            
            try
            {
                return DataManager.GetTagName(tagId);
            }
            catch (Exception ex)
            {
                                return null;
            }
        }

        /// <summary>
        /// 更新标签名称
        /// </summary>
        public static bool UpdateTagName(string oldTagName, string newTagName)
        {
            Initialize();
            
            try
            {
                return DataManager.UpdateTagName(oldTagName, newTagName);
            }
            catch (Exception ex)
            {
                                return false;
            }
        }

        /// <summary>
        /// 删除标签及其所有训练数据
        /// </summary>
        public static bool DeleteTag(int tagId)
        {
            Initialize();
            
            try
            {
                DataManager.DeleteTag(tagId);
                return true;
            }
            catch (Exception ex)
            {
                                return false;
            }
        }

        /// <summary>
        /// 清理重复标签
        /// </summary>
        public static ConsolidationResult ConsolidateDuplicateTags()
        {
            Initialize();
            
            try
            {
                var result = DataManager.ConsolidateDuplicateTags();
                return new ConsolidationResult
                {
                    MergedGroups = result.MergedGroups,
                    UpdatedSamples = result.UpdatedSamples,
                    DeletedTagIds = result.DeletedTagIds
                };
            }
            catch (Exception ex)
            {
                                return new ConsolidationResult
                {
                    MergedGroups = 0,
                    UpdatedSamples = 0,
                    DeletedTagIds = 0
                };
            }
        }

        /// <summary>
        /// 获取指定图片的标签
        /// </summary>
        public static List<int> GetImageTags(string imagePath)
        {
            Initialize();
            
            try
            {
                return DataManager.GetImageTags(imagePath);
            }
            catch (Exception ex)
            {
                                return new List<int>();
            }
        }

        /// <summary>
        /// 删除指定图片的标签
        /// </summary>
        public static void DeleteImageTag(string imagePath, int tagId)
        {
            Initialize();
            
            try
            {
                DataManager.DeleteTrainingSample(imagePath, tagId);
            }
            catch (Exception ex)
            {
                            }
        }

        /// <summary>
        /// 获取指定标签的所有文件路径
        /// </summary>
        public static List<string> GetFilePathsByTag(int tagId)
        {
            Initialize();
            
            try
            {
                var dbPath = DataManager.GetDatabasePath();
                var connectionString = $"Data Source={dbPath}";
                var filePaths = new List<string>();
                
                using (var connection = new SqliteConnection(connectionString))
                {
                    connection.Open();
                    var command = connection.CreateCommand();
                    command.CommandText = "SELECT DISTINCT ImagePath FROM TrainingData WHERE TagId = @tagId";
                    command.Parameters.AddWithValue("@tagId", tagId);
                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            var path = reader.GetString(0);
                            if (File.Exists(path))
                            {
                                filePaths.Add(path);
                            }
                        }
                    }
                }
                
                return filePaths;
            }
            catch (Exception ex)
            {
                                return new List<string>();
            }
        }

        /// <summary>
        /// 为文件添加标签（同时保存到 TagTrain 的训练数据）
        /// </summary>
        public static void AddTagToFile(string filePath, int tagId)
        {
            Initialize();
            
            try
            {
                // 检查是否为图片文件
                var ext = Path.GetExtension(filePath).ToLower();
                var imageExtensions = new[] { ".jpg", ".jpeg", ".png", ".bmp", ".gif", ".webp" };
                
                if (imageExtensions.Contains(ext))
                {
                    // 保存到 TagTrain 的训练数据
                    DataManager.SaveTrainingSample(filePath, tagId, isManual: true);
                }
            }
            catch (Exception ex)
            {
                            }
        }

        /// <summary>
        /// 从文件删除标签（同时从 TagTrain 的训练数据中删除）
        /// </summary>
        public static void RemoveTagFromFile(string filePath, int tagId)
        {
            Initialize();
            
            try
            {
                // 从 TagTrain 的训练数据中删除
                DataManager.DeleteTrainingSample(filePath, tagId);
            }
            catch (Exception ex)
            {
                            }
        }

        /// <summary>
        /// 获取文件的所有标签ID（从 TagTrain）
        /// </summary>
        public static List<int> GetFileTagIds(string filePath)
        {
            Initialize();
            
            try
            {
                return DataManager.GetImageTags(filePath);
            }
            catch (Exception ex)
            {
                                return new List<int>();
            }
        }
    }

    /// <summary>
    /// 标签信息
    /// </summary>
    public class TagInfo
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public int Count { get; set; }
    }

    /// <summary>
    /// 清理重复标签的结果
    /// </summary>
    public class ConsolidationResult
    {
        public int MergedGroups { get; set; }
        public int UpdatedSamples { get; set; }
        public int DeletedTagIds { get; set; }
    }
}








