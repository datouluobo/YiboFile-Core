using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using TagTrain.Services;

namespace TagTrain.UI
{
    /// <summary>
    /// ConfigWindow.xaml 的交互逻辑
    /// </summary>
    public partial class ConfigWindow : Window
    {
        public string ImageDirectory { get; private set; }

        public int TagsPerRow { get; private set; } = 5;
        public double PredictionThreshold { get; private set; } = 50.0;

        private bool _windowPositionLoaded = false;

        public ConfigWindow()
        {
            InitializeComponent();
            
            // 从统一配置管理器加载图片目录
            ImageDirectory = SettingsManager.GetImageDirectory();
            if (!string.IsNullOrEmpty(ImageDirectory))
            {
                DirectoryTextBox.Text = ImageDirectory;
                ValidateDirectory();
            }

            // 加载数据保存目录
            var storageDir = SettingsManager.GetDataStorageDirectory();
            DataStoragePathTextBox.Text = storageDir;

            // 加载设置
            LoadSettings();

            // 绑定滑块事件
            TagsPerRowSlider.ValueChanged += (s, e) => TagsPerRowValue.Text = ((int)TagsPerRowSlider.Value).ToString();
            PredictionThresholdSlider.ValueChanged += (s, e) => PredictionThresholdValue.Text = ((int)PredictionThresholdSlider.Value).ToString();
            
            // 添加键盘事件处理
            KeyDown += ConfigWindow_KeyDown;
        }

        private void ConfigWindow_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
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

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // 确保窗口位置已加载（作为备用）
            if (!_windowPositionLoaded)
            {
                LoadWindowPosition();
                _windowPositionLoaded = true;
            }
        }

        private void LoadSettings()
        {
            // 从统一配置管理器加载设置
            TagsPerRow = SettingsManager.GetTagsPerRow();
            PredictionThreshold = SettingsManager.GetPredictionThreshold();
            
            TagsPerRowSlider.Value = TagsPerRow;
            TagsPerRowValue.Text = TagsPerRow.ToString();
            PredictionThresholdSlider.Value = PredictionThreshold;
            PredictionThresholdValue.Text = ((int)PredictionThreshold).ToString();
        }

        private void SaveSettings()
        {
            // 使用统一配置管理器保存设置
            SettingsManager.SetTagsPerRow(TagsPerRow);
            SettingsManager.SetPredictionThreshold(PredictionThreshold);
        }

        private void Browse_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new System.Windows.Forms.FolderBrowserDialog();
            dialog.Description = "选择包含图片的目录";
            
            if (!string.IsNullOrEmpty(DirectoryTextBox.Text) && Directory.Exists(DirectoryTextBox.Text))
            {
                dialog.SelectedPath = DirectoryTextBox.Text;
            }

            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                DirectoryTextBox.Text = dialog.SelectedPath;
                ValidateDirectory();
            }
        }

        private void ValidateDirectory()
        {
            var path = DirectoryTextBox.Text.Trim();
            
            if (string.IsNullOrEmpty(path))
            {
                StatusText.Text = "";
                return;
            }

            if (!Directory.Exists(path))
            {
                StatusText.Text = "❌ 目录不存在";
                StatusText.Foreground = System.Windows.Media.Brushes.Red;
                return;
            }

            // 检查目录中是否有图片文件
            var imageExtensions = new[] { ".jpg", ".jpeg", ".png", ".bmp", ".gif", ".webp" };
            var hasImages = Directory.GetFiles(path, "*.*", SearchOption.AllDirectories)
                .Any(f => imageExtensions.Contains(Path.GetExtension(f).ToLower()));

            if (hasImages)
            {
                StatusText.Text = "✅ 目录有效，包含图片文件";
                StatusText.Foreground = System.Windows.Media.Brushes.Green;
            }
            else
            {
                StatusText.Text = "⚠️ 目录中未找到图片文件";
                StatusText.Foreground = System.Windows.Media.Brushes.Orange;
            }
        }

        private void OK_Click(object sender, RoutedEventArgs e)
        {
            var path = DirectoryTextBox.Text.Trim();
            // 图片目录可选：若填写则校验保存；未填写则跳过，不影响数据目录保存
            if (!string.IsNullOrEmpty(path))
            {
                if (!Directory.Exists(path))
                {
                    MessageBox.Show("目录不存在", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
                ImageDirectory = path;
            }
            
            // 收集所有要保存的设置
            TagsPerRow = (int)TagsPerRowSlider.Value;
            PredictionThreshold = PredictionThresholdSlider.Value;
            
            // 设置数据保存目录（只加载选定目录的数据，不迁移）
            try
            {
                var newStorageDir = DataStoragePathTextBox.Text.Trim();
                
                if (!Directory.Exists(newStorageDir))
                {
                    MessageBox.Show("选择的目录不存在", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
                
                var currentStorageDir = SettingsManager.GetDataStorageDirectory();
                var isDirectoryChanged = !Path.GetFullPath(newStorageDir).Equals(Path.GetFullPath(currentStorageDir), StringComparison.OrdinalIgnoreCase);
                
                // 保存设置
                if (!string.IsNullOrEmpty(ImageDirectory))
                {
                    SettingsManager.SetImageDirectory(ImageDirectory);
                }
                SaveSettings();
                
                // 如果目录已更改，更新数据保存目录设置
                if (isDirectoryChanged)
                {
                    // 确保新目录存在
                    Directory.CreateDirectory(newStorageDir);
                    
                    // 更新数据保存目录设置
                    SettingsManager.SetDataStorageDirectory(newStorageDir);
                    
                    // 清除缓存，确保使用新路径
                    SettingsManager.ClearCache();
                    DataManager.ClearDatabasePathCache();
                    
                    // 如果新目录中没有settings.txt，创建它并保存当前设置
                    var newSettingsPath = Path.Combine(newStorageDir, "settings.txt");
                    if (!File.Exists(newSettingsPath))
                    {
                        var currentSettings = SettingsManager.GetAllSettings();
                        currentSettings["DataStorageDirectory"] = newStorageDir;
                        File.WriteAllLines(newSettingsPath, currentSettings.Select(kvp => $"{kvp.Key}={kvp.Value}"));
                    }
                    
                    // 写入默认目录的引导配置（确保下次启动能立即定位到新目录）
                    try
                    {
                        var assemblyLocation = System.Reflection.Assembly.GetExecutingAssembly().Location;
                        var appDir = string.IsNullOrEmpty(assemblyLocation)
                            ? Environment.CurrentDirectory
                            : Path.GetDirectoryName(assemblyLocation);
                        var defaultDataDir = Path.Combine(appDir ?? Environment.CurrentDirectory, "data");
                        Directory.CreateDirectory(defaultDataDir);
                        var bootstrapSettingsPath = Path.Combine(defaultDataDir, "settings.txt");
                        
                        var lines = new List<string>();
                        if (File.Exists(bootstrapSettingsPath))
                        {
                            lines = File.ReadAllLines(bootstrapSettingsPath).ToList();
                            // 移除旧键
                            lines = lines.Where(l =>
                                !(l.TrimStart().StartsWith("DataStorageDirectory=", StringComparison.OrdinalIgnoreCase) ||
                                  l.TrimStart().StartsWith("ImageDirectory=", StringComparison.OrdinalIgnoreCase) ||
                                  l.TrimStart().StartsWith("TagsPerRow=", StringComparison.OrdinalIgnoreCase) ||
                                  l.TrimStart().StartsWith("PredictionThreshold=", StringComparison.OrdinalIgnoreCase))
                            ).ToList();
                        }
                        // 写入引导配置（不影响实际存储目录下的完整设置）
                        lines.Add($"DataStorageDirectory={newStorageDir}");
                        if (!string.IsNullOrEmpty(ImageDirectory))
                            lines.Add($"ImageDirectory={ImageDirectory}");
                        lines.Add($"TagsPerRow={TagsPerRow}");
                        lines.Add($"PredictionThreshold={PredictionThreshold}");
                        File.WriteAllLines(bootstrapSettingsPath, lines);
                    }
                    catch { }
                    
                    MessageBox.Show("已切换到新数据目录\n\n程序将加载此目录中的数据（tt_settings.txt, tt_training.db, tt_model.zip）\n\n请重新启动程序以确保所有连接使用新路径。", "成功", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    // 目录未更改，同步写入默认目录的引导配置
                    try
                    {
                        var storageDir = SettingsManager.GetDataStorageDirectory();
                        var assemblyLocation = System.Reflection.Assembly.GetExecutingAssembly().Location;
                        var appDir = string.IsNullOrEmpty(assemblyLocation)
                            ? Environment.CurrentDirectory
                            : Path.GetDirectoryName(assemblyLocation);
                        var defaultDataDir = Path.Combine(appDir ?? Environment.CurrentDirectory, "data");
                        Directory.CreateDirectory(defaultDataDir);
                        var bootstrapSettingsPath = Path.Combine(defaultDataDir, "settings.txt");
                        
                        var lines = new List<string>();
                        if (File.Exists(bootstrapSettingsPath))
                        {
                            lines = File.ReadAllLines(bootstrapSettingsPath).ToList();
                            lines = lines.Where(l =>
                                !(l.TrimStart().StartsWith("DataStorageDirectory=", StringComparison.OrdinalIgnoreCase) ||
                                  l.TrimStart().StartsWith("ImageDirectory=", StringComparison.OrdinalIgnoreCase) ||
                                  l.TrimStart().StartsWith("TagsPerRow=", StringComparison.OrdinalIgnoreCase) ||
                                  l.TrimStart().StartsWith("PredictionThreshold=", StringComparison.OrdinalIgnoreCase))
                            ).ToList();
                        }
                        lines.Add($"DataStorageDirectory={storageDir}");
                        if (!string.IsNullOrEmpty(ImageDirectory))
                            lines.Add($"ImageDirectory={ImageDirectory}");
                        lines.Add($"TagsPerRow={TagsPerRow}");
                        lines.Add($"PredictionThreshold={PredictionThreshold}");
                        File.WriteAllLines(bootstrapSettingsPath, lines);
                    }
                    catch { }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"保存配置失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            SettingsManager.SaveWindowPosition("ConfigWindow", this);
            DialogResult = true;
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            SettingsManager.SaveWindowPosition("ConfigWindow", this);
            DialogResult = false;
            Close();
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            // 确保在关闭时保存位置（无论是否通过OK或Cancel按钮）
            SettingsManager.SaveWindowPosition("ConfigWindow", this);
        }

        private void Window_LocationChanged(object sender, EventArgs e)
        {
            if (this.WindowState == WindowState.Normal && this.IsLoaded)
            {
                SettingsManager.SaveWindowPosition("ConfigWindow", this);
            }
        }

        private void Window_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (this.WindowState == WindowState.Normal && this.IsLoaded)
            {
                SettingsManager.SaveWindowPosition("ConfigWindow", this);
            }
        }

        private void LoadWindowPosition()
        {
            SettingsManager.LoadWindowPosition("ConfigWindow", this);
        }

        private void DirectoryTextBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            ValidateDirectory();
        }


        private void SelectDataStorageFolder_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new System.Windows.Forms.FolderBrowserDialog();
            dialog.Description = "选择数据目录（程序将从此目录加载数据，不迁移旧数据）";
            
            var currentPath = DataStoragePathTextBox.Text;
            if (!string.IsNullOrEmpty(currentPath) && Directory.Exists(currentPath))
            {
                dialog.SelectedPath = currentPath;
            }

            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                DataStoragePathTextBox.Text = dialog.SelectedPath;
            }
        }
    }
}

