using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using YiboFile.Interfaces.Plugins;
using YiboFile.ViewModels;

namespace YiboFile.Services.Tabs
{
    /// <summary>
    /// 标签页类型枚举。
    /// 已弃用：请使用 <see cref="PathTab.ContentTypeId"/> 字符串标识替代。
    /// 将在 v1.2 中移除。
    /// </summary>
    [Obsolete("使用 PathTab.ContentTypeId 替代。将在 v1.2 中移除。")]
    public enum TabType
    {
        Path,
        Library,
        Search,
        Tag
    }

    public class PathTab : BaseViewModel
    {
        private TabType _type;
        public TabType Type
        {
            get => _type;
            set => SetProperty(ref _type, value);
        }

        private string _path;
        public string Path
        {
            get => _path;
            set => SetProperty(ref _path, value);
        }

        private string _title;
        public string Title
        {
            get => _title;
            set => SetProperty(ref _title, value);
        }

        private bool _isPinned;
        public bool IsPinned
        {
            get => _isPinned;
            set => SetProperty(ref _isPinned, value);
        }

        private Library _library;
        public Library Library
        {
            get => _library;
            set => SetProperty(ref _library, value);
        }

        private string _overrideTitle;
        public string OverrideTitle
        {
            get => _overrideTitle;
            set => SetProperty(ref _overrideTitle, value);
        }

        private ICommand _closeCommand;
        public ICommand CloseCommand
        {
            get => _closeCommand;
            set => SetProperty(ref _closeCommand, value);
        }

        private ICommand _selectCommand;
        public ICommand SelectCommand
        {
            get => _selectCommand;
            set => SetProperty(ref _selectCommand, value);
        }

        private bool _isActive;
        public bool IsActive
        {
            get => _isActive;
            set => SetProperty(ref _isActive, value);
        }

        private bool _isDragging;
        public bool IsDragging
        {
            get => _isDragging;
            set => SetProperty(ref _isDragging, value);
        }

        private double _targetWidth = 160.0;
        /// <summary>
        /// 标签页理想宽度（由 WidthCalculator 计算）
        /// </summary>
        public double TargetWidth
        {
            get => _targetWidth;
            set => SetProperty(ref _targetWidth, value);
        }

        private DateTime _lastAccessTime = DateTime.Now;
        public DateTime LastAccessTime
        {
            get => _lastAccessTime;
            set => SetProperty(ref _lastAccessTime, value);
        }

        // ── 新增：TabContent 架构扩展 ──

        private string _contentTypeId = TabContentTypes.Path;
        /// <summary>
        /// 内容类型标识（取代 TabType 枚举）。
        /// 参见 <see cref="TabContentTypes"/> 中的常量定义。
        /// </summary>
        public string ContentTypeId
        {
            get => _contentTypeId;
            set => SetProperty(ref _contentTypeId, value);
        }

        /// <summary>
        /// 自定义内容实例缓存。
        /// 当 ContentTypeId 不是文件浏览类时，持有对应的 ITabContent 实例。
        /// 文件浏览类标签此属性为 null。
        /// </summary>
        public ITabContent CustomContent { get; set; }

        private string _iconKey;
        /// <summary>
        /// 标签页图标的资源键（引用 Icon Contract 中的键名）。
        /// 文件浏览类标签通常为 null（图标由文件类型推导）。
        /// </summary>
        public string IconKey
        {
            get => _iconKey;
            set => SetProperty(ref _iconKey, value);
        }

        // Navigation History State
        public Stack<string> BackStack { get; set; } = new Stack<string>();
        public Stack<string> ForwardStack { get; set; } = new Stack<string>();
    }
}
