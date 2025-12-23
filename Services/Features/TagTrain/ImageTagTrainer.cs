using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Microsoft.ML;
using Microsoft.ML.Data;
using Microsoft.ML.Transforms.Image;
using Microsoft.ML.Vision;

namespace TagTrain.Services
{
    /// <summary>
    /// 图片标签训练器
    /// </summary>
    public class ImageTagTrainer
    {
        private MLContext _mlContext;
        private ITransformer _model;
        private PredictionEngine<ImageData, TagPrediction> _predictionEngine;
        private string _modelPath;
        private bool _isModelLoaded = false;

        public ImageTagTrainer(string modelPath = null)
        {
            _mlContext = new MLContext(seed: 0);
            _modelPath = modelPath ?? SettingsManager.GetModelPath();
            
            // 确保模型目录存在
            var modelDir = Path.GetDirectoryName(_modelPath);
            if (!string.IsNullOrEmpty(modelDir))
            {
                Directory.CreateDirectory(modelDir);
            }
        }

        /// <summary>
        /// 加载模型
        /// </summary>
        public bool LoadModel()
        {
            try
            {
                // 确保使用最新的模型路径
                _modelPath = SettingsManager.GetModelPath();
                
                if (File.Exists(_modelPath))
                {
                    // 检查文件大小，如果为0或太小，可能损坏
                    var fileInfo = new FileInfo(_modelPath);
                    if (fileInfo.Length < 100)
                    {
                                                _isModelLoaded = false;
                        return false;
                    }

                                        _model = _mlContext.Model.Load(_modelPath, out var inputSchema);
                    
                    if (_model == null)
                    {
                                                _isModelLoaded = false;
                        return false;
                    }
                    
                                        _predictionEngine = _mlContext.Model.CreatePredictionEngine<ImageData, TagPrediction>(_model);
                    
                    if (_predictionEngine == null)
                    {
                                                _isModelLoaded = false;
                        return false;
                    }
                    
                                        _isModelLoaded = true;
                    return true;
                }
                else
                {
                                    }
            }
            catch (Exception ex)
            {
                                                if (ex.InnerException != null)
                {
                                    }
            }
            
            _isModelLoaded = false;
            return false;
        }

        /// <summary>
        /// 预测标签
        /// </summary>
        public List<TagPredictionResult> PredictTags(string imagePath)
        {
            if (!_isModelLoaded || _predictionEngine == null)
            {
                                return new List<TagPredictionResult>();
            }

            if (!File.Exists(imagePath))
            {
                                return new List<TagPredictionResult>();
            }

            try
            {
                var imageData = new ImageData { ImagePath = imagePath };
                var prediction = _predictionEngine.Predict(imageData);
                
                                if (prediction == null)
                {
                                        return new List<TagPredictionResult>();
                }
                
                if (prediction.Scores == null || prediction.Scores.Length == 0)
                {
                                        return new List<TagPredictionResult>();
                }

                // 获取所有标签名称，建立 Key 值到 TagId 的映射
                // MapValueToKey 按照标签值的字符串形式排序（字典序），所以需要获取所有唯一的 TagId 并按字符串排序
                var allTagNames = DataManager.GetAllTagNames();
                if (allTagNames.Count == 0)
                {
                                        return new List<TagPredictionResult>();
                }

                // 获取所有唯一的 TagId 并按字符串排序（MapValueToKey 会按字符串字典序分配 Key 值）
                // 训练时使用 Label = TagId.ToString()，所以 MapValueToKey 会按字符串排序
                var sortedTagIds = allTagNames.Keys.OrderBy(id => id.ToString()).ToList();
                
                // 建立 Key 值（索引）到 TagId 的映射
                var keyToTagId = new Dictionary<int, int>();
                for (int i = 0; i < sortedTagIds.Count && i < prediction.Scores.Length; i++)
                {
                    keyToTagId[i] = sortedTagIds[i];
                }
                // 返回Top 10预测结果，使用正确的 TagId 映射
                // 注意：先取Top 10，再过滤低置信度，这样可以确保即使置信度较低也能看到预测结果
                var results = prediction.Scores
                    .Select((score, index) => new TagPredictionResult
                    {
                        TagId = keyToTagId.ContainsKey(index) ? keyToTagId[index] : -1,
                        Confidence = score
                    })
                    .Where(x => x.TagId >= 0) // 过滤无效的 TagId
                    .OrderByDescending(x => x.Confidence)
                    .Take(10) // 增加到Top 10，提高找到正确标签的概率
                    .ToList(); // 移除置信度过滤，让测试能看到所有预测结果

                                foreach (var r in results)
                {
                                    }

                return results;
            }
            catch (Exception ex)
            {
                                                if (ex.InnerException != null)
                {
                                    }
                return new List<TagPredictionResult>();
            }
        }

        /// <summary>
        /// 训练模型
        /// </summary>
        public TrainingResult TrainModel(List<TrainingSample> samples, IProgress<TrainingProgress> progress = null, CancellationToken cancellationToken = default)
        {
            if (samples == null || samples.Count == 0)
            {
                return new TrainingResult
                {
                    Success = false,
                    Message = "没有训练数据"
                };
            }

            try
            {
                // 确保使用最新的模型路径
                _modelPath = SettingsManager.GetModelPath();
                
                                                progress?.Report(new TrainingProgress { Stage = "初始化", Progress = 0, Message = "准备训练环境..." });
                cancellationToken.ThrowIfCancellationRequested();
                
                // 确保目录存在
                var modelDir = Path.GetDirectoryName(_modelPath);
                if (!string.IsNullOrEmpty(modelDir) && !Directory.Exists(modelDir))
                {
                    Directory.CreateDirectory(modelDir);
                                    }

                // 准备数据
                                progress?.Report(new TrainingProgress { Stage = "数据准备", Progress = 5, Message = "准备训练数据..." });
                cancellationToken.ThrowIfCancellationRequested();
                
                // 过滤掉不存在的文件，避免训练时出错
                var validSamples = new List<TrainingSample>();
                var missingFiles = new List<string>();
                
                foreach (var sample in samples)
                {
                    if (File.Exists(sample.ImagePath))
                    {
                        validSamples.Add(sample);
                    }
                    else
                    {
                        missingFiles.Add(sample.ImagePath);
                                            }
                }
                
                if (validSamples.Count == 0)
                {
                    return new TrainingResult
                    {
                        Success = false,
                        Message = $"所有训练样本文件都不存在。共 {samples.Count} 个样本，{missingFiles.Count} 个文件缺失。"
                    };
                }
                
                if (missingFiles.Count > 0)
                {
                                    }
                
                var imageDataList = validSamples.Select(s => new ImageData
                {
                    ImagePath = s.ImagePath,
                    Label = s.TagId.ToString()
                }).ToList();

                                progress?.Report(new TrainingProgress { Stage = "数据加载", Progress = 10, Message = $"加载 {validSamples.Count} 个有效样本到训练管道..." });
                cancellationToken.ThrowIfCancellationRequested();
                
                var data = _mlContext.Data.LoadFromEnumerable(imageDataList);

                                progress?.Report(new TrainingProgress { Stage = "构建管道", Progress = 15, Message = "构建训练管道..." });
                cancellationToken.ThrowIfCancellationRequested();
                
                // 构建训练管道
                // 由于 ImageClassification 训练器存在 TensorFlow 兼容性问题（TF_StringEncodedSize 不存在）
                // 改用传统的机器学习算法：先提取图像特征，然后使用 LbfgsMaximumEntropy 分类器
                // 使用128x128的图像尺寸以保留更多特征信息，提高预测准确率
                var pipeline = _mlContext.Transforms.Conversion.MapValueToKey("Label")
                    .Append(_mlContext.Transforms.LoadImages("Image", null, "ImagePath"))
                    .Append(_mlContext.Transforms.ResizeImages("Image", 128, 128, "Image"))
                    .Append(_mlContext.Transforms.ExtractPixels("Features", "Image", interleavePixelColors: true, colorsToExtract: Microsoft.ML.Transforms.Image.ImagePixelExtractingEstimator.ColorBits.Rgb));

                                progress?.Report(new TrainingProgress { Stage = "预处理", Progress = 20, Message = "预处理数据（加载和调整图像大小）..." });
                cancellationToken.ThrowIfCancellationRequested();
                
                // 先执行预处理步骤，这样可以看到进度
                var preprocessingModel = pipeline.Fit(data);
                var preprocessedData = preprocessingModel.Transform(data);
                                progress?.Report(new TrainingProgress { Stage = "训练中", Progress = 50, Message = "训练分类器（这可能需要几分钟，请耐心等待）..." });
                cancellationToken.ThrowIfCancellationRequested();

                // 创建训练器
                var trainer = _mlContext.MulticlassClassification.Trainers.LbfgsMaximumEntropy(
                    labelColumnName: "Label",
                    featureColumnName: "Features");

                                // 训练模型
                var trainerModel = trainer.Fit(preprocessedData);
                                progress?.Report(new TrainingProgress { Stage = "组合模型", Progress = 80, Message = "组合预处理和训练模型..." });
                cancellationToken.ThrowIfCancellationRequested();
                
                // 将预处理和训练器组合
                var combinedModel = preprocessingModel.Append(trainerModel);
                
                // 应用组合模型以获取输出，然后添加 MapKeyToValue 将 Key<UInt32> 转换回字符串
                // 因为 TagPrediction.PredictedLabel 是 string 类型，而模型输出的是 Key<UInt32> 类型
                                var transformedData = combinedModel.Transform(data);
                var keyToValueTransformer = _mlContext.Transforms.Conversion.MapKeyToValue("PredictedLabel").Fit(transformedData);
                _model = combinedModel.Append(keyToValueTransformer);
                
                if (_model == null)
                {
                    throw new Exception("模型训练返回null");
                }
                
                                progress?.Report(new TrainingProgress { Stage = "保存模型", Progress = 90, Message = $"保存模型到: {Path.GetFileName(_modelPath)}..." });
                cancellationToken.ThrowIfCancellationRequested();
                
                // 保存模型
                _mlContext.Model.Save(_model, data.Schema, _modelPath);
                
                // 验证文件是否真的保存了
                if (!File.Exists(_modelPath))
                {
                    throw new Exception($"模型保存失败，文件不存在: {_modelPath}");
                }
                
                var fileInfo = new FileInfo(_modelPath);
                if (fileInfo.Length < 100)
                {
                    throw new Exception($"模型文件太小，可能损坏: {fileInfo.Length} 字节");
                }
                
                                // 更新预测引擎
                                progress?.Report(new TrainingProgress { Stage = "完成", Progress = 95, Message = "创建预测引擎..." });
                cancellationToken.ThrowIfCancellationRequested();
                
                _predictionEngine = _mlContext.Model.CreatePredictionEngine<ImageData, TagPrediction>(_model);
                
                if (_predictionEngine == null)
                {
                    throw new Exception("预测引擎创建失败");
                }
                
                _isModelLoaded = true;
                                progress?.Report(new TrainingProgress { Stage = "完成", Progress = 100, Message = "训练完成！" });

                // 记录一次模型版本
                try
                {
                    DataManager.AddModelVersion($"auto-{DateTime.Now:yyyyMMddHHmmss}", _modelPath, validSamples.Count);
                }
                catch { }

                var resultMessage = missingFiles.Count > 0 
                    ? $"训练完成（已跳过 {missingFiles.Count} 个不存在的文件）" 
                    : "训练完成";
                
                return new TrainingResult
                {
                    Success = true,
                    Message = resultMessage,
                    SampleCount = validSamples.Count
                };
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
                var errorMsg = $"训练失败: {ex.Message}";
                                                // 如果是内部异常，也记录
                if (ex.InnerException != null)
                {
                                                            errorMsg += $"\n内部异常: {ex.InnerException.Message}";
                }
                
                return new TrainingResult
                {
                    Success = false,
                    Message = errorMsg
                };
            }
        }

        /// <summary>
        /// 增量训练
        /// </summary>
        public TrainingResult IncrementalTrain(List<TrainingSample> newSamples, IProgress<TrainingProgress> progress = null, CancellationToken cancellationToken = default)
        {
            if (newSamples == null || newSamples.Count == 0)
            {
                return new TrainingResult
                {
                    Success = false,
                    Message = "没有新训练数据"
                };
            }

            // 加载所有历史数据
            var allSamples = DataManager.LoadAllTrainingData();
            
            // 合并新数据（去重）
            var existingPaths = new HashSet<string>(allSamples.Select(s => $"{s.ImagePath}_{s.TagId}"));
            foreach (var sample in newSamples)
            {
                var key = $"{sample.ImagePath}_{sample.TagId}";
                if (!existingPaths.Contains(key))
                {
                    allSamples.Add(sample);
                    existingPaths.Add(key);
                }
            }

            // 使用所有数据重新训练
            return TrainModel(allSamples, progress, cancellationToken);
        }

        /// <summary>
        /// 检查模型是否存在
        /// </summary>
        public bool ModelExists()
        {
            // 使用最新的模型路径检查
            var currentPath = GetModelPath();
            return File.Exists(currentPath);
        }

        /// <summary>
        /// 验证模型文件格式是否正确
        /// </summary>
        public ModelValidationResult ValidateModelFile()
        {
            var result = new ModelValidationResult();
            
            try
            {
                var modelPath = GetModelPath();
                result.ModelPath = modelPath;
                
                // 检查文件是否存在
                if (!File.Exists(modelPath))
                {
                    result.IsValid = false;
                    result.ErrorMessage = "模型文件不存在";
                    return result;
                }
                
                result.FileExists = true;
                
                // 检查文件大小
                var fileInfo = new FileInfo(modelPath);
                result.FileSize = fileInfo.Length;
                result.LastModified = fileInfo.LastWriteTime;
                
                if (fileInfo.Length < 100)
                {
                    result.IsValid = false;
                    result.ErrorMessage = $"模型文件太小（{fileInfo.Length} 字节），可能已损坏";
                    return result;
                }
                
                // 检查是否是有效的 ZIP 文件（ML.NET 模型通常是 ZIP 格式）
                try
                {
                    using (var zipFile = System.IO.Compression.ZipFile.OpenRead(modelPath))
                    {
                        result.IsZipFile = true;
                        result.ZipEntryCount = zipFile.Entries.Count;
                        
                        // 检查是否包含 ML.NET 模型的关键文件
                        var hasModelFile = zipFile.Entries.Any(e => 
                            e.Name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) || 
                            e.FullName.Contains("MLModel", StringComparison.OrdinalIgnoreCase) ||
                            e.FullName.Contains("model", StringComparison.OrdinalIgnoreCase));
                        
                        result.HasModelStructure = hasModelFile;
                        
                        if (!hasModelFile)
                        {
                            result.WarningMessage = "ZIP 文件中未找到 ML.NET 模型结构";
                        }
                    }
                }
                catch (Exception zipEx)
                {
                    result.IsZipFile = false;
                    result.WarningMessage = $"文件不是有效的 ZIP 格式: {zipEx.Message}";
                    // 不是 ZIP 文件不一定意味着无效，ML.NET 可能使用其他格式
                }
                
                // 尝试加载模型以验证格式
                try
                {
                                        var testModel = _mlContext.Model.Load(modelPath, out var inputSchema);
                    
                    if (testModel == null)
                    {
                        result.IsValid = false;
                        result.ErrorMessage = "模型加载返回 null，文件可能已损坏";
                        return result;
                    }
                    
                    result.CanLoad = true;
                    result.HasInputSchema = inputSchema != null;
                    
                    // 尝试创建预测引擎以进一步验证
                    try
                    {
                        var testEngine = _mlContext.Model.CreatePredictionEngine<ImageData, TagPrediction>(testModel);
                        if (testEngine != null)
                        {
                            result.CanCreateEngine = true;
                            result.IsValid = true;
                            result.SuccessMessage = "模型文件格式正确，可以正常加载";
                        }
                        else
                        {
                            result.IsValid = false;
                            result.ErrorMessage = "无法创建预测引擎，模型可能不兼容";
                        }
                    }
                    catch (Exception engineEx)
                    {
                        result.IsValid = false;
                        result.ErrorMessage = $"创建预测引擎失败: {engineEx.Message}";
                        if (engineEx.InnerException != null)
                        {
                            result.ErrorMessage += $"\n内部异常: {engineEx.InnerException.Message}";
                        }
                    }
                }
                catch (Exception loadEx)
                {
                    result.IsValid = false;
                    result.ErrorMessage = $"模型加载失败: {loadEx.Message}";
                    if (loadEx.InnerException != null)
                    {
                        result.ErrorMessage += $"\n内部异常: {loadEx.InnerException.Message}";
                    }
                    result.ExceptionDetails = loadEx.StackTrace;
                }
            }
            catch (Exception ex)
            {
                result.IsValid = false;
                result.ErrorMessage = $"验证过程出错: {ex.Message}";
                result.ExceptionDetails = ex.StackTrace;
            }
            
            return result;
        }

        /// <summary>
        /// 获取模型路径（动态从配置读取，确保使用最新的数据保存目录）
        /// </summary>
        public string GetModelPath()
        {
            // 每次从配置管理器获取最新路径，而不是使用缓存的 _modelPath
            // 这样当用户更改数据保存目录时，模型路径会自动更新
            var currentPath = SettingsManager.GetModelPath();
            
            // 如果路径已更改，更新内部路径并重新加载模型（如果已加载）
            if (!string.IsNullOrEmpty(currentPath) && !currentPath.Equals(_modelPath, StringComparison.OrdinalIgnoreCase))
            {
                var wasLoaded = _isModelLoaded;
                _modelPath = currentPath;
                
                // 如果模型之前已加载，尝试从新路径重新加载
                if (wasLoaded)
                {
                    _isModelLoaded = false;
                    LoadModel();
                }
            }
            
            return _modelPath;
        }

        /// <summary>
        /// 检查模型是否已加载
        /// </summary>
        public bool IsModelLoaded()
        {
            return _isModelLoaded;
        }
    }

    /// <summary>
    /// 图片数据
    /// </summary>
    public class ImageData
    {
        [LoadColumn(0)]
        public string ImagePath { get; set; }
        
        [LoadColumn(1)]
        public string Label { get; set; }
    }

    /// <summary>
    /// 标签预测结果
    /// </summary>
    public class TagPrediction
    {
        [ColumnName("PredictedLabel")]
        public string PredictedLabel { get; set; }
        
        [ColumnName("Score")]
        public float[] Scores { get; set; }
    }

    /// <summary>
    /// 预测结果
    /// </summary>
    public class TagPredictionResult
    {
        public int TagId { get; set; }
        public float Confidence { get; set; }
    }

    /// <summary>
    /// 训练结果
    /// </summary>
    public class TrainingResult
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public int SampleCount { get; set; }
    }

    /// <summary>
    /// 训练进度信息
    /// </summary>
    public class TrainingProgress
    {
        /// <summary>
        /// 训练阶段（初始化、数据准备、数据加载、预处理、训练中、组合模型、保存模型、完成）
        /// </summary>
        public string Stage { get; set; }
        
        /// <summary>
        /// 进度百分比（0-100）
        /// </summary>
        public int Progress { get; set; }
        
        /// <summary>
        /// 当前阶段描述信息
        /// </summary>
        public string Message { get; set; }
    }

    /// <summary>
    /// 模型验证结果
    /// </summary>
    public class ModelValidationResult
    {
        public bool IsValid { get; set; }
        public string ModelPath { get; set; }
        public bool FileExists { get; set; }
        public long FileSize { get; set; }
        public DateTime LastModified { get; set; }
        public bool IsZipFile { get; set; }
        public int ZipEntryCount { get; set; }
        public bool HasModelStructure { get; set; }
        public bool CanLoad { get; set; }
        public bool HasInputSchema { get; set; }
        public bool CanCreateEngine { get; set; }
        public string ErrorMessage { get; set; }
        public string WarningMessage { get; set; }
        public string SuccessMessage { get; set; }
        public string ExceptionDetails { get; set; }
    }
}

