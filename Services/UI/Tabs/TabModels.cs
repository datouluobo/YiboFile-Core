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
    public class PathTab
    {
        public TabType Type { get; set; } = TabType.Path;
        public string Path { get; set; }  // 路径标签页使用路径，库/标签页使用名称或标识
        public string Title { get; set; }
        public Button TabButton { get; set; }
        public FrameworkElement CloseButton { get; set; }
        public Library Library { get; set; }  // 库标签页时使用
        public bool IsPinned { get; set; }
        public StackPanel TabContainer { get; set; }
        public TextBlock TitleTextBlock { get; set; }
        public TextBlock IconTextBlock { get; set; }
        public string OverrideTitle { get; set; }

        /// <summary>
        /// 最后访问时间 - 用于智能标签页复用策略
        /// </summary>
        public DateTime LastAccessTime { get; set; } = DateTime.Now;
    }
}














