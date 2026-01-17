using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using YiboFile.Models;

namespace YiboFile.Models
{
    public class FileSystemItem : INotifyPropertyChanged
    {
        public string Name { get; set; }
        public string Path { get; set; }
        public string Type { get; set; }
        public string Size { get; set; }
        public string ModifiedDate { get; set; }
        public string CreatedTime { get; set; }
        public string Tags { get; set; }

        private List<TagViewModel> _tagList;
        public List<TagViewModel> TagList
        {
            get => _tagList;
            set
            {
                _tagList = value;
                OnPropertyChanged(nameof(TagList));
            }
        }

        public void NotifyTagsChanged()
        {
            OnPropertyChanged(nameof(TagList));
        }
        public string Notes { get; set; }
        // Metadata
        public int PixelWidth { get; set; } // 0 if N/A
        public int PixelHeight { get; set; } // 0 if N/A
        public long DurationMs { get; set; } // 0 if N/A, in milliseconds
        public bool IsDirectory { get; set; }
        public string SourcePath { get; set; } // 库模式下的来源路径
        public string GroupingKey { get; set; } // UI分组键

        // Inline Rename Support
        private bool _isRenaming;
        public bool IsRenaming
        {
            get => _isRenaming;
            set
            {
                if (_isRenaming != value)
                {
                    _isRenaming = value;
                    OnPropertyChanged(nameof(IsRenaming));
                    if (_isRenaming)
                    {
                        // Use full filename from Path (including extension) for rename
                        // Name might not have extension when ShowFullFileName is false
                        RenameText = !string.IsNullOrEmpty(Path)
                            ? System.IO.Path.GetFileName(Path)
                            : Name;
                    }
                }
            }
        }

        private string _renameText;
        public string RenameText
        {
            get => _renameText;
            set
            {
                if (_renameText != value)
                {
                    _renameText = value;
                    OnPropertyChanged(nameof(RenameText));
                }
            }
        }

        // 原始数据用于排序，避免字符串解析异常
        public long SizeBytes { get; set; } = -1;
        public DateTime ModifiedDateTime { get; set; } = DateTime.MinValue;
        public DateTime CreatedDateTime { get; set; } = DateTime.MinValue;

        /// <summary>
        /// 是否来自备注搜索
        /// </summary>
        public bool IsFromNotesSearch { get; set; }

        /// <summary>
        /// 是否来自名称搜索
        /// </summary>
        public bool IsFromNameSearch { get; set; }

        /// <summary>
        /// 搜索结果类型（用于搜索结果显示）
        /// </summary>
        public YiboFile.Services.Search.SearchResultType? SearchResultType { get; set; }

        // 缩略图属性（用于缩略图视图）
        private BitmapSource _thumbnail;

        /// <summary>
        /// 文件缩略图（支持属性变更通知）
        /// </summary>
        public BitmapSource Thumbnail
        {
            get => _thumbnail;
            set
            {
                if (_thumbnail != value)
                {
                    _thumbnail = value;
                    OnPropertyChanged(nameof(Thumbnail));
                }
            }
        }

        // Icon属性保持向后兼容（绑定到Thumbnail）
        public BitmapSource Icon
        {
            get => _thumbnail;
            set => Thumbnail = value;
        }

        /// <summary>
        /// 格式化相对时间
        /// </summary>
        /// <summary>
        /// 格式化相对时间
        /// </summary>
        public static string FormatTimeAgo(DateTime time)
        {
            var span = DateTime.Now - time;
            if (span.TotalSeconds < 60) return $"{Math.Max(0, (int)span.TotalSeconds)} 秒";
            if (span.TotalMinutes < 60) return $"{(int)span.TotalMinutes}'";
            if (span.TotalHours < 24) return $"{(int)span.TotalHours} 时";
            if (span.TotalDays < 65) return $"{(int)span.TotalDays} 天"; // 保持"天"直到约2个月
            if (span.TotalDays < 365) return $"{(int)(span.TotalDays / 30)} 月";
            return $"{(int)(span.TotalDays / 365)} 年";
        }

        /// <summary>
        /// 获取相对时间的背景颜色 (Hex String)
        /// </summary>
        public string CreatedTimeBrush
        {
            get
            {
                var span = DateTime.Now - CreatedDateTime;
                if (span.TotalMinutes < 60) return "#FFCDD2"; // Red 100 (分钟)
                if (span.TotalHours < 24) return "#FFF9C4";   // Yellow 100 (小时)
                if (span.TotalDays < 3) return "#FFF9C4";     // Yellow 100 (< 3天)
                if (span.TotalDays < 30) return "#C8E6C9";    // Green 100 (天)
                if (span.TotalDays < 90) return "#B2EBF2";    // Cyan 100 (1-3个月)
                if (span.TotalDays < 365) return "#BBDEFB";   // Blue 100 (月)
                return "#E1BEE7";                             // Purple 100 (年)
            }
        }

        // INotifyPropertyChanged 实现
        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}

