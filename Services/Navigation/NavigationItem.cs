using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace YiboFile.Services.Navigation
{
    /// <summary>
    /// 导航树节点模型
    /// </summary>
    public class NavigationItem : INotifyPropertyChanged
    {
        private bool _isExpanded;
        private bool _isSelected;
        private ObservableCollection<NavigationItem> _children;
        private string _usageText;
        private double _usagePercentage;
        private string _toolTip;

        // 基础属性
        public string Header { get; set; }        // 显示名称
        public string Path { get; set; }          // 完整路径
        public string IconKey { get; set; }       // 图标资源键
        public bool IsDrive { get; set; }         // 是否为驱动器节点

        // 驱动器特有属性
        public long TotalSize { get; set; }
        public long UsedSize { get; set; }

        // 驱动器显示属性（用于响应式隐藏）
        public string DriveLetter { get; set; }   // 驱动器盘符，如 "C:\"
        public string DriveLabel { get; set; }    // 驱动器别名，如 "Win11"
        public string UsedSizeText { get; set; }  // 已使用容量，如 "413.98 GB"
        public string TotalSizeText { get; set; } // 总容量，如 "930.99 GB"
        public string ToolTip
        {
            get => _toolTip;
            set { _toolTip = value; OnPropertyChanged(nameof(ToolTip)); }
        }
        public string UsageText
        {
            get => _usageText;
            set { _usageText = value; OnPropertyChanged(nameof(UsageText)); }
        }
        public double UsagePercentage
        {
            get => _usagePercentage;
            set { _usagePercentage = value; OnPropertyChanged(nameof(UsagePercentage)); }
        }

        // 树状结构属性
        public ObservableCollection<NavigationItem> Children
        {
            get => _children;
            set { _children = value; OnPropertyChanged(nameof(Children)); }
        }

        public bool IsExpanded
        {
            get => _isExpanded;
            set
            {
                if (_isExpanded != value)
                {
                    _isExpanded = value;
                    OnPropertyChanged(nameof(IsExpanded));

                    if (_isExpanded && HasDummyChild)
                    {
                        Children.Clear();
                        LoadChildrenAsync();
                    }
                }
            }
        }

        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected != value)
                {
                    _isSelected = value;
                    OnPropertyChanged(nameof(IsSelected));
                }
            }
        }

        public bool HasDummyChild => Children.Count == 1 && Children[0] == DummyChild;

        private static readonly NavigationItem DummyChild = new NavigationItem { Header = "Loading..." };

        public NavigationItem()
        {
            Children = new ObservableCollection<NavigationItem>();
        }

        // 初始化仅添加占位符（如果是文件夹或有子内容的驱动器）
        public void AddDummyChild()
        {
            Children.Add(DummyChild);
        }

        private async void LoadChildrenAsync()
        {
            try
            {
                await Task.Run(() =>
                {
                    try
                    {
                        var dirInfo = new DirectoryInfo(Path);
                        var subDirs = dirInfo.GetDirectories()
                            .Where(d => !d.Attributes.HasFlag(FileAttributes.Hidden) && !d.Attributes.HasFlag(FileAttributes.System))
                            .OrderBy(d => d.Name)
                            .Select(d => new NavigationItem
                            {
                                Header = d.Name,
                                Path = d.FullName,
                                IconKey = "Icon_Folder", // 默认文件夹图标
                                IsDrive = false
                            })
                            .ToList();

                        // 检查子文件夹是否有内容（为了显示展开箭头）
                        // 这步可能会比较慢，可以简化为直接假定有子文件或者简单检查
                        foreach (var item in subDirs)
                        {
                            try
                            {
                                // 简单的 Peek，不递归
                                if (System.IO.Directory.EnumerateDirectories(item.Path).Any())
                                {
                                    Application.Current.Dispatcher.Invoke(() => item.AddDummyChild());
                                }
                            }
                            catch { }
                        }

                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            Children.Clear();
                            foreach (var item in subDirs)
                            {
                                Children.Add(item);
                            }
                        });
                    }
                    catch (Exception)
                    {
                        Application.Current.Dispatcher.Invoke(() => Children.Clear());
                    }
                });
            }
            catch (Exception)
            {
                Children.Clear();
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}

