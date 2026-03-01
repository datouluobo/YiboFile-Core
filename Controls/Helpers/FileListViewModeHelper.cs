using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Data;
using System.Windows.Media;
using YiboFile.Services.UI;
using YiboFile.Models;
using YiboFile.Services.Search;
using YiboFile.Controls;

namespace YiboFile.Controls.Helpers
{
    /// <summary>
    /// 文件列表视图模式助手
    /// 处理列表视图切换、模板应用、分组显示及缩略图加载逻辑
    /// </summary>
    public static class FileListViewModeHelper
    {
        /// <summary>
        /// 应用指定的视图模式
        /// </summary>
        public static void ApplyViewMode(
            YiboFile.Models.Enums.FileListViewMode currentMode,
            ListView filesListView,
            GridView filesGridView,
            ThumbnailService thumbnailService,
            Func<string, object> findResource)
        {
            if (filesListView == null) return;

            switch (currentMode)
            {
                case YiboFile.Models.Enums.FileListViewMode.Thumbnail:
                    ApplyWrapPanelView(filesListView, "ThumbnailTemplate", loadThumbnails: true, thumbnailService, findResource);
                    break;
                case YiboFile.Models.Enums.FileListViewMode.Tiles:
                    ApplyWrapPanelView(filesListView, "TilesTemplate", loadThumbnails: true, thumbnailService, findResource);
                    break;
                case YiboFile.Models.Enums.FileListViewMode.SmallIcons:
                    ApplyWrapPanelView(filesListView, "SmallIconsTemplate", loadThumbnails: true, thumbnailService, findResource);
                    break;
                case YiboFile.Models.Enums.FileListViewMode.Content:
                    ApplyStackPanelView(filesListView, "ContentTemplate", loadThumbnails: true, thumbnailService, findResource);
                    break;
                case YiboFile.Models.Enums.FileListViewMode.Compact:
                    ApplyStackPanelView(filesListView, "CompactTemplate", loadThumbnails: true, thumbnailService, findResource);
                    break;
                default: // List
                    ApplyListView(filesListView, filesGridView, thumbnailService, findResource);
                    break;
            }
        }

        /// <summary>
        /// 处理鼠标滚轮缩放缩略图大小
        /// </summary>
        public static void HandlePreviewMouseWheel(
            MouseWheelEventArgs e,
            YiboFile.Models.Enums.FileListViewMode currentViewMode,
            double currentThumbnailSize,
            Action<double> setThumbnailSize)
        {
            // 仅在缩略图模式下且按住Ctrl键时处理
            if (currentViewMode == YiboFile.Models.Enums.FileListViewMode.Thumbnail && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
            {
                e.Handled = true; // 阻止 ScrollViewer 滚动

                double delta = e.Delta > 0 ? 10 : -10; // 每次增减10
                double newSize = currentThumbnailSize + delta;

                // 限制范围: 64 (中等图标) - 256 (超大图标)
                if (newSize < 64) newSize = 64;
                if (newSize > 256) newSize = 256;

                setThumbnailSize(newSize);
            }
        }

        /// <summary>
        /// 触发缩略图加载（通常在 ItemsSource 变更时调用）
        /// </summary>
        public static void TriggerThumbnailLoad(
            System.Collections.IEnumerable items,
            YiboFile.Models.Enums.FileListViewMode currentViewMode,
            double thumbnailSize,
            ThumbnailService thumbnailService)
        {
            if (items == null || thumbnailService == null) return;

            // 确定图标大小
            int size = 32;
            if (currentViewMode == YiboFile.Models.Enums.FileListViewMode.Thumbnail) size = (int)thumbnailSize;
            else if (currentViewMode == YiboFile.Models.Enums.FileListViewMode.Tiles) size = 64;
            else if (currentViewMode == YiboFile.Models.Enums.FileListViewMode.Content) size = 48;
            else if (currentViewMode == YiboFile.Models.Enums.FileListViewMode.List) size = 16;
            
            thumbnailService.LoadThumbnailsAsync(items, size);
        }

        #region 私有视图切换逻辑

        private static void ApplyWrapPanelView(
            ListView filesListView,
            string templateKey,
            bool loadThumbnails,
            ThumbnailService thumbnailService,
            Func<string, object> findResource)
        {
            filesListView.View = null;
            var selector = (FileListTemplateSelector)findResource("FileListItemSelector");
            selector.DefaultTemplate = (DataTemplate)findResource(templateKey);

            filesListView.ItemTemplate = null;
            // Force refresh if selector instance is reused
            filesListView.ItemTemplateSelector = null;
            filesListView.ItemTemplateSelector = selector;

            filesListView.ItemsPanel = (ItemsPanelTemplate)findResource("WrapPanelTemplate");
            ScrollViewer.SetHorizontalScrollBarVisibility(filesListView, ScrollBarVisibility.Disabled);

            if (loadThumbnails && filesListView.ItemsSource != null)
            {
                // 注意：这里没有传 size，因为这只是重新应用视图。具体加载逻辑通常在 TriggerThumbnailLoad 或 LoadThumbnailsAsync 中处理
                // 但原代码只是调 LoadThumbnailsAsync(ItemsSource) 使用默认大小
                thumbnailService?.LoadThumbnailsAsync(filesListView.ItemsSource);
            }
            else
            {
                thumbnailService?.Stop();
            }
        }

        private static void ApplyStackPanelView(
            ListView filesListView,
            string templateKey,
            bool loadThumbnails,
            ThumbnailService thumbnailService,
            Func<string, object> findResource)
        {
            filesListView.View = null;
            var selector = (FileListTemplateSelector)findResource("FileListItemSelector");
            selector.DefaultTemplate = (DataTemplate)findResource(templateKey);

            filesListView.ItemTemplate = null;
            // Force refresh if selector instance is reused
            filesListView.ItemTemplateSelector = null;
            filesListView.ItemTemplateSelector = selector;

            filesListView.ItemsPanel = (ItemsPanelTemplate)findResource("StackPanelTemplate");
            ScrollViewer.SetHorizontalScrollBarVisibility(filesListView, ScrollBarVisibility.Disabled);

            if (loadThumbnails && filesListView.ItemsSource != null)
            {
                thumbnailService?.LoadThumbnailsAsync(filesListView.ItemsSource);
            }
            else
            {
                thumbnailService?.Stop();
            }
        }

        private static void ApplyListView(
            ListView filesListView,
            GridView filesGridView,
            ThumbnailService thumbnailService,
            Func<string, object> findResource)
        {
            filesListView.ItemTemplate = null;
            filesListView.ItemTemplateSelector = null;
            filesListView.ItemsPanel = (ItemsPanelTemplate)findResource("StackPanelTemplate");
            if (filesGridView != null) filesListView.View = filesGridView;
            ScrollViewer.SetHorizontalScrollBarVisibility(filesListView, ScrollBarVisibility.Auto);

            // 启用缩略图加载 (16px for small icons)
            if (filesListView.ItemsSource != null)
            {
                thumbnailService?.LoadThumbnailsAsync(filesListView.ItemsSource, 16);
            }
        }

        #endregion

        #region 分组逻辑

        /// <summary>
        /// 设置分组搜索结果
        /// </summary>
        public static void SetGroupedSearchResults(
            Dictionary<SearchResultType, List<FileSystemItem>> groupedItems,
            ListView filesListView,
            Action<bool> setIsGroupedMode)
        {
            if (groupedItems == null || groupedItems.Count == 0)
            {
                SwitchToNormalView(filesListView, setIsGroupedMode);
                return;
            }

            setIsGroupedMode(true);

            // 展平结果并在 FileSystemItem 上设置分组键
            var flatList = new List<FileSystemItem>();

            // 按优先级顺序显示：备注 > 文件夹 > 文件 > 其他
            var displayOrder = new[]
            {
                SearchResultType.Notes,
                SearchResultType.Folder,
                SearchResultType.File,
                SearchResultType.Tag,
                SearchResultType.Date,
                SearchResultType.Other
            };

            foreach (var type in displayOrder)
            {
                if (groupedItems.ContainsKey(type) && groupedItems[type].Count > 0)
                {
                    string groupName = GetGroupName(type);
                    foreach (var item in groupedItems[type])
                    {
                        item.GroupingKey = groupName;
                        flatList.Add(item);
                    }
                }
            }

            // 更新列表
            if (filesListView != null)
            {
                filesListView.Visibility = Visibility.Visible;
                filesListView.ItemsSource = flatList;

                // 启用分组
                var view = CollectionViewSource.GetDefaultView(filesListView.ItemsSource);
                if (view != null)
                {
                    view.GroupDescriptions.Clear();
                    view.GroupDescriptions.Add(new PropertyGroupDescription(nameof(FileSystemItem.GroupingKey)));
                }
            }
        }

        public static void ApplyGrouping(ListView filesListView, Action<bool> setIsGroupedMode)
        {
            if (filesListView != null)
            {
                setIsGroupedMode(true);
                filesListView.Visibility = Visibility.Visible;

                var view = CollectionViewSource.GetDefaultView(filesListView.ItemsSource);
                if (view != null)
                {
                    view.GroupDescriptions.Clear();
                    view.GroupDescriptions.Add(new PropertyGroupDescription(nameof(FileSystemItem.GroupingKey)));
                }
            }
        }

        public static void SwitchToNormalView(ListView filesListView, Action<bool> setIsGroupedMode)
        {
            setIsGroupedMode(false);

            if (filesListView != null)
            {
                filesListView.Visibility = Visibility.Visible;
                // 清除分组
                if (filesListView.ItemsSource != null)
                {
                    var view = CollectionViewSource.GetDefaultView(filesListView.ItemsSource);
                    view?.GroupDescriptions.Clear();
                }
            }
        }

        private static string GetGroupName(SearchResultType type)
        {
            return type switch
            {
                SearchResultType.Notes => "备注匹配",
                SearchResultType.Folder => "文件夹匹配",
                SearchResultType.File => "文件匹配",
                SearchResultType.Tag => "标签匹配",
                SearchResultType.Date => "日期匹配",
                _ => "其他"
            };
        }

        #endregion
    }
}
