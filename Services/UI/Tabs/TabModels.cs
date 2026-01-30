using System;
using System.Windows;
using System.Windows.Controls;
using YiboFile;

namespace YiboFile.Services.Tabs
{
    /// <summary>
    /// 标签页类型枚举
    /// </summary>
    public enum TabType
    {
        Path,    // 路径标签页
        Library  // 库标签页
    }

    /// <summary>
    /// 标签页信息模型
    /// </summary>
    public class PathTab : YiboFile.ViewModels.BaseViewModel
    {
        private TabType _type = TabType.Path;
        private string _path;
        private string _title;
        private Library _library;
        private bool _isPinned;
        private string _overrideTitle;

        public TabType Type
        {
            get => _type;
            set => SetProperty(ref _type, value);
        }

        public string Path
        {
            get => _path;
            set => SetProperty(ref _path, value);
        }

        public string Title
        {
            get => _title;
            set => SetProperty(ref _title, value);
        }

        public Library Library
        {
            get => _library;
            set => SetProperty(ref _library, value);
        }

        public bool IsPinned
        {
            get => _isPinned;
            set => SetProperty(ref _isPinned, value);
        }

        public string OverrideTitle
        {
            get => _overrideTitle;
            set => SetProperty(ref _overrideTitle, value);
        }

        public Button TabButton { get; set; }
        public FrameworkElement CloseButton { get; set; }
        public StackPanel TabContainer { get; set; }
        public TextBlock TitleTextBlock { get; set; }
        public TextBlock IconTextBlock { get; set; }

        /// <summary>
        /// 最后访问时间 - 用于智能标签页复用策略
        /// </summary>
        public DateTime LastAccessTime { get; set; } = DateTime.Now;
    }
}














