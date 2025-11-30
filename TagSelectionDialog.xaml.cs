using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using OoiMRR.Services;

namespace OoiMRR
{
    /// <summary>
    /// TagSelectionDialog.xaml 的交互逻辑
    /// </summary>
    public partial class TagSelectionDialog : Window
    {
        public List<int> SelectedTagIds { get; private set; } = new List<int>();
        private readonly HashSet<int> _preselectedTagIds;

        public TagSelectionDialog(IEnumerable<int> preselectedTagIds = null)
        {
            InitializeComponent();
            _preselectedTagIds = preselectedTagIds != null
                ? new HashSet<int>(preselectedTagIds)
                : new HashSet<int>();
            LoadTags();
            this.KeyDown += TagSelectionDialog_KeyDown;
        }

        private void TagSelectionDialog_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Enter)
            {
                OK_Click(null, null);
            }
            else if (e.Key == System.Windows.Input.Key.Escape)
            {
                Cancel_Click(null, null);
            }
        }

        private void LoadTags()
        {
            try
            {
                // 从 TagTrain 获取标签（默认按名称排序）
                var tagTrainTags = OoiMRRIntegration.GetAllTags(OoiMRR.Services.OoiMRRIntegration.TagSortMode.Name);
                
                // 添加空值检查
                if (tagTrainTags == null || tagTrainTags.Count == 0)
                {
                    TagsListBox.ItemsSource = new List<Tag>();
                    return;
                }
                
                // 转换为 OoiMRR 的 Tag 格式（添加颜色）
                var tags = tagTrainTags.Select(tt => new Tag
                {
                    Id = tt.Id,
                    Name = tt.Name ?? "",
                    Color = GenerateTagColor(tt.Name ?? "") // 根据标签名称生成颜色
                }).ToList();
                
                TagsListBox.ItemsSource = tags;

                if (_preselectedTagIds.Count > 0)
                {
                    this.Dispatcher.BeginInvoke(new Action(() =>
                    {
                        foreach (Tag tag in tags)
                        {
                            if (_preselectedTagIds.Contains(tag.Id))
                            {
                                TagsListBox.SelectedItems.Add(tag);
                            }
                        }
                    }), System.Windows.Threading.DispatcherPriority.Loaded);
                }
            }
            catch (Exception)
            {TagsListBox.ItemsSource = new List<Tag>();}
        }

        /// <summary>
        /// 根据标签名称生成颜色（确保相同名称总是生成相同颜色）
        /// </summary>
        private string GenerateTagColor(string tagName)
        {
            if (string.IsNullOrEmpty(tagName))
                return "#FF0000";

            // 使用标签名称的哈希值生成颜色
            int hash = tagName.GetHashCode();
            if (hash < 0) hash = -hash;
            
            int hue = hash % 360;
            var color = HslToRgb(hue, 0.7, 0.5);
            return $"#{color.R:X2}{color.G:X2}{color.B:X2}";
        }

        /// <summary>
        /// HSL 转 RGB
        /// </summary>
        private System.Drawing.Color HslToRgb(int h, double s, double l)
        {
            double c = (1 - Math.Abs(2 * l - 1)) * s;
            double x = c * (1 - Math.Abs((h / 60.0) % 2 - 1));
            double m = l - c / 2;

            double r = 0, g = 0, b = 0;
            if (h >= 0 && h < 60)
            {
                r = c; g = x; b = 0;
            }
            else if (h >= 60 && h < 120)
            {
                r = x; g = c; b = 0;
            }
            else if (h >= 120 && h < 180)
            {
                r = 0; g = c; b = x;
            }
            else if (h >= 180 && h < 240)
            {
                r = 0; g = x; b = c;
            }
            else if (h >= 240 && h < 300)
            {
                r = x; g = 0; b = c;
            }
            else if (h >= 300 && h < 360)
            {
                r = c; g = 0; b = x;
            }

            return System.Drawing.Color.FromArgb(
                (int)((r + m) * 255),
                (int)((g + m) * 255),
                (int)((b + m) * 255));
        }

        private void OK_Click(object sender, RoutedEventArgs e)
        {
            SelectedTagIds.Clear();
            foreach (Tag selectedTag in TagsListBox.SelectedItems)
            {
                SelectedTagIds.Add(selectedTag.Id);
            }
            
            DialogResult = true;
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
