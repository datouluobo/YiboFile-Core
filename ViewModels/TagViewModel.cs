using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows;
using OoiMRR.Controls;
using OoiMRR.Services;

namespace OoiMRR.ViewModels
{
    /// <summary>
    /// 标签管理 ViewModel
    /// 负责管理标签的加载、过滤等功能
    /// </summary>
    public class TagViewModel : BaseViewModel
    {
        private readonly Window _ownerWindow;
        private readonly Action<Tag> _onTagSelected;
        private readonly Action<Tag, List<FileSystemItem>> _onTagFilesLoaded;

        private ObservableCollection<Tag> _tags = new ObservableCollection<Tag>();
        private Tag _currentTagFilter;
        private Tag _selectedTag;

        public ObservableCollection<Tag> Tags
        {
            get => _tags;
            set => SetProperty(ref _tags, value);
        }

        public Tag CurrentTagFilter
        {
            get => _currentTagFilter;
            set => SetProperty(ref _currentTagFilter, value);
        }

        public Tag SelectedTag
        {
            get => _selectedTag;
            set => SetProperty(ref _selectedTag, value);
        }

        public TagViewModel(Window ownerWindow, Action<Tag> onTagSelected, Action<Tag, List<FileSystemItem>> onTagFilesLoaded)
        {
            _ownerWindow = ownerWindow ?? throw new ArgumentNullException(nameof(ownerWindow));
            _onTagSelected = onTagSelected;
            _onTagFilesLoaded = onTagFilesLoaded;
        }

        /// <summary>
        /// 加载标签列表
        /// </summary>
        public void LoadTags()
        {
            // 标签加载现在由TagTrain面板处理
            // 这个方法主要用于初始化TagTrain面板
            if (App.IsTagTrainAvailable)
            {
                // TagTrain面板的初始化由外部处理
                // 这里只是占位
            }
        }

        /// <summary>
        /// 根据标签过滤文件
        /// </summary>
        public void FilterByTag(Tag tag)
        {
            if (tag == null)
            {
                CurrentTagFilter = null;
                _onTagFilesLoaded?.Invoke(null, new List<FileSystemItem>());
                return;
            }

            CurrentTagFilter = tag;
            SelectedTag = tag;

            try
            {
                // 获取标签关联的文件
                var tagFiles = GetTagFiles(tag);
                _onTagFilesLoaded?.Invoke(tag, tagFiles);
            }
            catch (Exception ex)
            {
                MessageBox.Show(_ownerWindow, $"加载标签文件失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 获取标签关联的文件
        /// </summary>
        private List<FileSystemItem> GetTagFiles(Tag tag)
        {
            var files = new List<FileSystemItem>();

            try
            {
                // 通过OoiMRRIntegration获取标签关联的文件路径
                var filePaths = App.IsTagTrainAvailable 
                    ? (OoiMRRIntegration.GetFilePathsByTag(tag.Id) ?? new List<string>())
                    : new List<string>();
                
                foreach (var filePath in filePaths)
                {
                    try
                    {
                        if (System.IO.File.Exists(filePath))
                        {
                            var fileInfo = new System.IO.FileInfo(filePath);
                            files.Add(new FileSystemItem
                            {
                                Name = fileInfo.Name,
                                Path = fileInfo.FullName,
                                Type = FileTypeManager.GetFileCategory(fileInfo.FullName),
                                Size = FormatFileSize(fileInfo.Length),
                                IsDirectory = false,
                                ModifiedDate = fileInfo.LastWriteTime.ToString("yyyy-MM-dd HH:mm:ss"),
                                CreatedTime = fileInfo.CreationTime.ToString("yyyy-MM-dd HH:mm:ss")
                            });
                        }
                    }
                    catch (Exception)
                    {
                    }
                }
            }
            catch (Exception)
            {
            }

            return files;
        }

        private string FormatFileSize(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB", "TB" };
            double len = bytes;
            int order = 0;
            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len = len / 1024;
            }
            return $"{len:0.##} {sizes[order]}";
        }
    }
}

