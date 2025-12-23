using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using TagTrain.Services;

namespace TagTrain.UI
{
    /// <summary>
    /// TrainingStatusWindow.xaml 的交互逻辑
    /// </summary>
    public partial class TrainingStatusWindow : Window
    {
        private ImageTagTrainer _trainer;
        private bool _windowPositionLoaded = false;

        public TrainingStatusWindow(ImageTagTrainer trainer)
        {
            InitializeComponent();
            _trainer = trainer;
            LoadStatus();
            
            // 添加键盘事件处理
            KeyDown += TrainingStatusWindow_KeyDown;
        }

        private void TrainingStatusWindow_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Escape)
            {
                Close();
            }
        }

        private void Window_SourceInitialized(object sender, EventArgs e)
        {
            // 在窗口完全初始化后加载窗口位置
            if (!_windowPositionLoaded)
            {
                LoadWindowPosition();
                _windowPositionLoaded = true;
            }
        }

        private void LoadStatus()
        {
            // 模型状态
            var modelPath = _trainer.GetModelPath();
            ModelPathText.Text = modelPath;
            
            // 尝试重新加载模型（如果文件存在但未加载）
            if (_trainer.ModelExists() && !_trainer.IsModelLoaded())
            {
                                var loadResult = _trainer.LoadModel();
                            }
            
            var modelExists = _trainer.ModelExists();
            if (modelExists)
            {
                if (_trainer.IsModelLoaded())
                {
                    ModelStatusText.Text = "✅ 已训练并加载";
                    ModelStatusText.Foreground = System.Windows.Media.Brushes.Green;
                }
                else
                {
                    ModelStatusText.Text = "⚠️ 模型文件存在但未加载";
                    ModelStatusText.Foreground = System.Windows.Media.Brushes.Orange;
                }
                
                // 获取模型文件信息
                try
                {
                    var fileInfo = new FileInfo(modelPath);
                    if (fileInfo.Exists)
                    {
                        ModelSizeText.Text = FormatFileSize(fileInfo.Length);
                        ModelLastUpdateText.Text = fileInfo.LastWriteTime.ToString("yyyy-MM-dd HH:mm:ss");
                    }
                }
                catch
                {
                    ModelSizeText.Text = "未知";
                    ModelLastUpdateText.Text = "未知";
                }
            }
            else
            {
                ModelStatusText.Text = "❌ 未训练";
                ModelStatusText.Foreground = System.Windows.Media.Brushes.Red;
                ModelSizeText.Text = "-";
                ModelLastUpdateText.Text = "-";
            }

            // 训练数据统计
            var stats = DataManager.GetStatistics();
            TotalSamplesText.Text = stats.TotalSamples.ToString();
            ManualSamplesText.Text = stats.ManualSamples.ToString();
            UniqueImagesText.Text = stats.UniqueImages.ToString();
            UniqueTagsText.Text = stats.UniqueTags.ToString();

            // 数据库路径
            var dbPath = DataManager.GetDatabasePath();
            DatabasePathText.Text = dbPath;

            // 检查ML.NET安装
            CheckMLNetInstallation();
            
            // 如果模型文件存在但未加载，自动验证
            if (modelExists && !_trainer.IsModelLoaded())
            {
                ValidateModelFile();
            }
        }
        
        private void ValidateModel_Click(object sender, RoutedEventArgs e)
        {
            ValidateModelFile();
        }
        
        private void ValidateModelFile()
        {
            ValidateModelBtn.IsEnabled = false;
            ValidationResultText.Visibility = Visibility.Visible;
            ValidationResultText.Text = "验证中...";
            ValidationResultText.Foreground = System.Windows.Media.Brushes.Blue;
            
            System.Threading.Tasks.Task.Run(() =>
            {
                try
                {
                    var validationResult = _trainer.ValidateModelFile();
                    
                    Dispatcher.Invoke(() =>
                    {
                        var resultText = new System.Text.StringBuilder();
                        resultText.AppendLine($"模型路径: {validationResult.ModelPath}");
                        resultText.AppendLine($"文件存在: {(validationResult.FileExists ? "是" : "否")}");
                        
                        if (validationResult.FileExists)
                        {
                            resultText.AppendLine($"文件大小: {FormatFileSize(validationResult.FileSize)}");
                            resultText.AppendLine($"最后修改: {validationResult.LastModified:yyyy-MM-dd HH:mm:ss}");
                            resultText.AppendLine($"是否为ZIP文件: {(validationResult.IsZipFile ? "是" : "否")}");
                            
                            if (validationResult.IsZipFile)
                            {
                                resultText.AppendLine($"ZIP条目数: {validationResult.ZipEntryCount}");
                                resultText.AppendLine($"包含模型结构: {(validationResult.HasModelStructure ? "是" : "否")}");
                            }
                            
                            resultText.AppendLine($"可以加载: {(validationResult.CanLoad ? "是" : "否")}");
                            if (validationResult.CanLoad)
                            {
                                resultText.AppendLine($"有输入架构: {(validationResult.HasInputSchema ? "是" : "否")}");
                                resultText.AppendLine($"可以创建引擎: {(validationResult.CanCreateEngine ? "是" : "否")}");
                            }
                            
                            if (validationResult.IsValid)
                            {
                                resultText.AppendLine($"\n✅ {validationResult.SuccessMessage}");
                                ValidationResultText.Foreground = System.Windows.Media.Brushes.Green;
                            }
                            else
                            {
                                resultText.AppendLine($"\n❌ 验证失败:");
                                resultText.AppendLine(validationResult.ErrorMessage);
                                ValidationResultText.Foreground = System.Windows.Media.Brushes.Red;
                                
                                if (!string.IsNullOrEmpty(validationResult.ExceptionDetails))
                                {
                                    resultText.AppendLine($"\n异常详情:\n{validationResult.ExceptionDetails}");
                                }
                                
                                resultText.AppendLine($"\n建议: 删除该模型文件并重新训练");
                            }
                            
                            if (!string.IsNullOrEmpty(validationResult.WarningMessage))
                            {
                                resultText.AppendLine($"\n⚠️ 警告: {validationResult.WarningMessage}");
                                if (ValidationResultText.Foreground != System.Windows.Media.Brushes.Red)
                                {
                                    ValidationResultText.Foreground = System.Windows.Media.Brushes.Orange;
                                }
                            }
                        }
                        else
                        {
                            resultText.AppendLine($"\n❌ {validationResult.ErrorMessage}");
                            ValidationResultText.Foreground = System.Windows.Media.Brushes.Red;
                        }
                        
                        ValidationResultText.Text = resultText.ToString();
                        ValidateModelBtn.IsEnabled = true;
                    });
                }
                catch (Exception ex)
                {
                    Dispatcher.Invoke(() =>
                    {
                        ValidationResultText.Text = $"验证过程出错: {ex.Message}\n\n{ex.StackTrace}";
                        ValidationResultText.Foreground = System.Windows.Media.Brushes.Red;
                        ValidateModelBtn.IsEnabled = true;
                    });
                }
            });
        }

        private void TestModel_Click(object sender, RoutedEventArgs e)
        {
            if (!_trainer.ModelExists())
            {
                TestResultText.Text = "❌ 模型未训练，无法测试";
                TestResultText.Foreground = System.Windows.Media.Brushes.Red;
                TestResultText.Visibility = Visibility.Visible;
                return;
            }

            // 确保模型已加载
            if (!_trainer.IsModelLoaded())
            {
                                if (!_trainer.LoadModel())
                {
                    TestResultText.Text = "❌ 模型加载失败，无法测试";
                    TestResultText.Foreground = System.Windows.Media.Brushes.Red;
                    TestResultText.Visibility = Visibility.Visible;
                    return;
                }
                            }

            // 获取一些训练数据用于测试
            var trainingData = DataManager.LoadAllTrainingData();
            if (trainingData.Count == 0)
            {
                TestResultText.Text = "❌ 没有训练数据，无法测试";
                TestResultText.Foreground = System.Windows.Media.Brushes.Red;
                TestResultText.Visibility = Visibility.Visible;
                return;
            }

            TestModelBtn.IsEnabled = false;
            TestResultText.Text = "测试中...";
            TestResultText.Foreground = System.Windows.Media.Brushes.Blue;
            TestResultText.Visibility = Visibility.Visible;

            try
            {
                // 随机选择几张图片进行预测测试
                var random = new Random();
                var testSamples = trainingData
                    .Where(t => t.IsManual && File.Exists(t.ImagePath))
                    .OrderBy(x => random.Next())
                    .Take(5)
                    .ToList();

                if (testSamples.Count == 0)
                {
                    TestResultText.Text = "❌ 没有可用的测试图片";
                    TestResultText.Foreground = System.Windows.Media.Brushes.Red;
                    TestModelBtn.IsEnabled = true;
                    return;
                }

                int successCount = 0;
                int totalCount = 0;

                foreach (var sample in testSamples)
                {
                    try
                    {
                                                var predictions = _trainer.PredictTags(sample.ImagePath);
                        totalCount++;
                        
                                                foreach (var p in predictions)
                        {
                                                    }
                        
                        // 检查预测结果中是否包含正确的标签（降低置信度阈值用于测试）
                        if (predictions.Any(p => p.TagId == sample.TagId))
                        {
                            var matchingPrediction = predictions.FirstOrDefault(p => p.TagId == sample.TagId);
                                                        successCount++;
                        }
                        else
                        {
                        }
                    }
                    catch (Exception)
                    {
                        // 继续测试其他图片
                    }
                }

                if (totalCount > 0)
                {
                    var accuracy = (double)successCount / totalCount * 100;
                    
                    // 根据成功率判断模型状态
                    if (accuracy >= 60)
                    {
                        TestResultText.Text = $"✅ 测试完成: {successCount}/{totalCount} 预测成功 ({accuracy:F1}%)\n" +
                                            "模型工作正常，预测准确率良好！";
                        TestResultText.Foreground = System.Windows.Media.Brushes.Green;
                    }
                    else if (accuracy >= 30)
                    {
                        TestResultText.Text = $"⚠️ 测试完成: {successCount}/{totalCount} 预测成功 ({accuracy:F1}%)\n" +
                                            "模型可以预测，但准确率较低，建议增加训练数据。";
                        TestResultText.Foreground = System.Windows.Media.Brushes.Orange;
                    }
                    else if (accuracy > 0)
                    {
                        TestResultText.Text = $"⚠️ 测试完成: {successCount}/{totalCount} 预测成功 ({accuracy:F1}%)\n" +
                                            "模型预测准确率很低，建议重新训练或增加更多训练数据。";
                        TestResultText.Foreground = System.Windows.Media.Brushes.Orange;
                    }
                    else
                    {
                        TestResultText.Text = $"❌ 测试完成: {successCount}/{totalCount} 预测成功 ({accuracy:F1}%)\n" +
                                            "模型无法正确预测，请检查训练数据或重新训练模型。";
                        TestResultText.Foreground = System.Windows.Media.Brushes.Red;
                    }
                }
                else
                {
                    TestResultText.Text = "⚠️ 测试失败：无法加载测试图片";
                    TestResultText.Foreground = System.Windows.Media.Brushes.Orange;
                }
            }
            catch (Exception ex)
            {
                TestResultText.Text = $"❌ 测试出错: {ex.Message}";
                TestResultText.Foreground = System.Windows.Media.Brushes.Red;
            }
            finally
            {
                TestModelBtn.IsEnabled = true;
            }
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            SettingsManager.SaveWindowPosition("TrainingStatusWindow", this);
            Close();
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            SettingsManager.SaveWindowPosition("TrainingStatusWindow", this);
        }

        private void Window_LocationChanged(object sender, EventArgs e)
        {
            if (this.WindowState == WindowState.Normal && this.IsLoaded)
            {
                SettingsManager.SaveWindowPosition("TrainingStatusWindow", this);
            }
        }

        private void Window_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (this.WindowState == WindowState.Normal && this.IsLoaded)
            {
                SettingsManager.SaveWindowPosition("TrainingStatusWindow", this);
            }
        }

        private void LoadWindowPosition()
        {
            SettingsManager.LoadWindowPosition("TrainingStatusWindow", this);
        }

        private string FormatFileSize(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB" };
            double len = bytes;
            int order = 0;
            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len = len / 1024;
            }
            return $"{len:0.##} {sizes[order]}";
        }

        private void CheckMLNetInstallation()
        {
            try
            {
                // 检查ML.NET程序集是否可用
                var mlContext = new Microsoft.ML.MLContext(seed: 0);
                
                // 尝试加载关键类型来验证程序集是否可用
                var missing = new List<string>();
                var issues = new List<string>();
                
                // 检查Microsoft.ML
                try
                {
                    var mlType = typeof(Microsoft.ML.MLContext);
                }
                catch (Exception ex)
                {
                    missing.Add("Microsoft.ML");
                    issues.Add($"Microsoft.ML: {ex.Message}");
                }
                
                // 检查Microsoft.ML.ImageAnalytics - 尝试加载程序集
                try
                {
                    var assembly = System.Reflection.Assembly.Load("Microsoft.ML.ImageAnalytics");
                    if (assembly == null)
                    {
                        missing.Add("Microsoft.ML.ImageAnalytics");
                    }
                    else
                    {
                        // 尝试获取类型
                        var type = assembly.GetType("Microsoft.ML.Transforms.Image.ImagePixelExtractingEstimator");
                        if (type == null)
                        {
                            issues.Add("ImageAnalytics: 类型未找到");
                        }
                    }
                }
                catch (Exception ex)
                {
                    missing.Add("Microsoft.ML.ImageAnalytics");
                    issues.Add($"ImageAnalytics: {ex.Message}");
                }
                
                // 检查Microsoft.ML.Vision - 尝试加载程序集
                try
                {
                    var assembly = System.Reflection.Assembly.Load("Microsoft.ML.Vision");
                    if (assembly == null)
                    {
                        missing.Add("Microsoft.ML.Vision");
                    }
                    else
                    {
                        // 尝试获取类型
                        var type = assembly.GetType("Microsoft.ML.Vision.ImageClassificationTrainer");
                        if (type == null)
                        {
                            issues.Add("Vision: 类型未找到");
                        }
                    }
                }
                catch (Exception ex)
                {
                    missing.Add("Microsoft.ML.Vision");
                    issues.Add($"Vision: {ex.Message}");
                }
                
                if (missing.Count == 0 && issues.Count == 0)
                {
                    MLNetStatusText.Text = "✅ ML.NET安装正常";
                    MLNetStatusText.Foreground = System.Windows.Media.Brushes.Green;
                }
                else if (missing.Count > 0)
                {
                    MLNetStatusText.Text = $"⚠️ 缺少组件: {string.Join(", ", missing)}";
                    MLNetStatusText.Foreground = System.Windows.Media.Brushes.Orange;
                    MLNetStatusText.ToolTip = "请运行 'dotnet restore' 或 'dotnet build' 来安装缺失的NuGet包";
                }
                else
                {
                    MLNetStatusText.Text = $"⚠️ 部分组件有问题: {string.Join(", ", issues)}";
                    MLNetStatusText.Foreground = System.Windows.Media.Brushes.Orange;
                    MLNetStatusText.ToolTip = string.Join("\n", issues);
                }
            }
            catch (Exception ex)
            {
                MLNetStatusText.Text = $"❌ ML.NET检查失败: {ex.GetType().Name}";
                MLNetStatusText.Foreground = System.Windows.Media.Brushes.Red;
                MLNetStatusText.ToolTip = ex.Message;
            }
        }
    }
}

