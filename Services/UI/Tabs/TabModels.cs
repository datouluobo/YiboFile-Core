using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using YiboFile.ViewModels;

namespace YiboFile.Services.Tabs
{
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
    }
}
