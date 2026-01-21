using System;
using System.Collections;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Media.Effects;
using System.Windows.Data;
using System.ComponentModel;
using YiboFile.Controls.Converters;
using YiboFile.Controls.Behaviors;
using YiboFile.ViewModels;
using YiboFile.Services.Search;
using YiboFile.Services.ColumnManagement;
using YiboFile.Services.UI;
using YiboFile.Models;

namespace YiboFile.Controls
{
    /// <summary>
    /// FileListControl.xaml 的交互逻辑
    /// 独立的文件列表控件，使用详细信息视图
    /// </summary>
    public partial class FileListControl : UserControl
    {
        private ThumbnailService _thumbnailService;
        private Services.FileList.FileListService _fileListService;
        private LassoSelectionBehavior _lassoSelectionBehavior;

        // 配置缓存
        private double _cachedNotesWidth = 200;

        // 事件定义
        public event SelectionChangedEventHandler SelectionChanged;
        public new event MouseButtonEventHandler MouseDoubleClick;
        public new event MouseButtonEventHandler PreviewMouseDoubleClick;
        public new event KeyEventHandler PreviewKeyDown;
        public new event MouseButtonEventHandler PreviewMouseLeftButtonDown;
        public new event MouseButtonEventHandler MouseLeftButtonUp;
        public new event MouseButtonEventHandler PreviewMouseDown;
        public new event MouseEventHandler PreviewMouseMove;
        public event RoutedEventHandler GridViewColumnHeaderClick;
        public new event SizeChangedEventHandler SizeChanged;
        public event RoutedEventHandler LoadMoreClick;

        #region 缩略图尺寸控制
        // 依赖属性：缩略图大小 (默认100)
        public static readonly DependencyProperty ThumbnailSizeProperty =
            DependencyProperty.Register("ThumbnailSize", typeof(double), typeof(FileListControl),
                new PropertyMetadata(100.0, OnThumbnailSizeChanged));

        public double ThumbnailSize
        {
            get { return (double)GetValue(ThumbnailSizeProperty); }
            set { SetValue(ThumbnailSizeProperty, value); }
        }

        // 依赖属性：Item项宽度 (自动计算: Size + 20)
        public static readonly DependencyProperty ItemWidthProperty =
            DependencyProperty.Register("ItemWidth", typeof(double), typeof(FileListControl), new PropertyMetadata(120.0));

        public double ItemWidth
        {
            get { return (double)GetValue(ItemWidthProperty); }
            private set { SetValue(ItemWidthProperty, value); }
        }

        // 依赖属性：Item项高度 (自动计算: Size + 40)
        public static readonly DependencyProperty ItemHeightProperty =
            DependencyProperty.Register("ItemHeight", typeof(double), typeof(FileListControl), new PropertyMetadata(140.0));

        public double ItemHeight
        {
            get { return (double)GetValue(ItemHeightProperty); }
            private set { SetValue(ItemHeightProperty, value); }
        }

        private static void OnThumbnailSizeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is FileListControl control)
            {
                double size = (double)e.NewValue;
                control.ItemWidth = size + 20;
                control.ItemHeight = size + 40;
            }
        }
        #endregion

        public FileListControl()
        {
            InitializeComponent();

            // 订阅文件列表的事件
            if (FilesListView != null)
            {
                FilesListView.SelectionChanged += (s, e) => SelectionChanged?.Invoke(s, e);
                FilesListView.MouseDoubleClick += (s, e) => MouseDoubleClick?.Invoke(s, e);
                FilesListView.PreviewMouseDoubleClick += (s, e) => PreviewMouseDoubleClick?.Invoke(s, e);
                FilesListView.PreviewKeyDown += (s, e) => PreviewKeyDown?.Invoke(s, e);
                FilesListView.PreviewMouseLeftButtonDown += (s, e) => PreviewMouseLeftButtonDown?.Invoke(s, e);
                FilesListView.MouseLeftButtonUp += (s, e) => MouseLeftButtonUp?.Invoke(s, e);
                FilesListView.PreviewMouseDown += (s, e) => PreviewMouseDown?.Invoke(s, e);
                FilesListView.PreviewMouseMove += (s, e) => PreviewMouseMove?.Invoke(s, e);

                // 订阅 SizeChanged 事件，手动调整名称列宽度
                FilesListView.SizeChanged += (s, e) =>
                {
                    SizeChanged?.Invoke(s, e);

                    if (e.WidthChanged)
                    {
                        AdjustNameColumnWidth();
                    }
                };

                // 旧的列标题订阅代码已移除，现在使用 Style 中的 EventSetter 处理
                FilesListView.PreviewMouseWheel += FilesListView_PreviewMouseWheel;
            }

            // 重新添加列头点击事件捕获 (因为 Style 中的 EventSetter 被移除了)
            // 现在通过 XAML 中的 EventSetter 恢复了原生处理，无需手动 AddHandler
            // if (FilesListView != null)
            // {
            //     FilesListView.AddHandler(GridViewColumnHeader.ClickEvent, new RoutedEventHandler(ColumnHeader_Click), true);
            // }

            // 订阅加载更多按钮事件
            if (LoadMoreBtn != null)
            {
                LoadMoreBtn.Click += (s, e) => LoadMoreClick?.Invoke(s, e);
            }

            // 初始化缩略图服务
            _thumbnailService = new ThumbnailService();

            // 初始化详细信息视图
            ApplyViewMode();

            // Load column widths from config
            LoadColumnWidths();

            // 加载并缓存配置
            var config = ConfigManager.Load();
            _cachedNotesWidth = config.ColNotesWidth;

            // 延迟调整名称列宽度并禁用横向滚动条
            this.Loaded += (s, e) =>
            {
                // 禁用横向滚动条
                if (FilesListView != null)
                {
                    ScrollViewer.SetHorizontalScrollBarVisibility(FilesListView, ScrollBarVisibility.Disabled);
                }

                AdjustNameColumnWidth();

                // 初始化框选行为
                if (LassoSelectionCanvas != null && FilesListView != null && _lassoSelectionBehavior == null)
                {
                    _lassoSelectionBehavior = new LassoSelectionBehavior(FilesListView, LassoSelectionCanvas);
                }
            };
        }

        public event EventHandler<TagViewModel> TagClicked;

        private void Tag_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is TagViewModel tag)
            {
                TagClicked?.Invoke(this, tag);
                e.Handled = true;
            }
        }

        private void LoadMoreBtn_Click(object sender, RoutedEventArgs e)
        {
            LoadMoreClick?.Invoke(sender, e);
        }

        private void ApplyViewMode()
        {
            if (FilesListView == null) return;

            switch (_currentViewMode)
            {
                case "Thumbnail":
                    ApplyWrapPanelView("ThumbnailTemplate", loadThumbnails: true);
                    break;
                case "Tiles":
                    ApplyWrapPanelView("TilesTemplate", loadThumbnails: true);
                    break;
                case "SmallIcons":
                    ApplyWrapPanelView("SmallIconsTemplate", loadThumbnails: true);
                    break;
                case "Content":
                    ApplyStackPanelView("ContentTemplate", loadThumbnails: true);
                    break;
                case "Compact":
                    ApplyStackPanelView("CompactTemplate", loadThumbnails: true);
                    break;
                default: // "List"
                    ApplyListView();
                    break;
            }
        }

        /// <summary>
        /// 应用 WrapPanel 布局视图（缩略图、平铺、小图标）
        /// </summary>
        private void ApplyWrapPanelView(string templateKey, bool loadThumbnails)
        {
            FilesListView.View = null;
            FilesListView.ItemTemplate = (DataTemplate)FindResource(templateKey);
            FilesListView.ItemsPanel = (ItemsPanelTemplate)FindResource("WrapPanelTemplate");
            ScrollViewer.SetHorizontalScrollBarVisibility(FilesListView, ScrollBarVisibility.Disabled);

            if (loadThumbnails && FilesListView.ItemsSource != null)
            {
                _thumbnailService?.LoadThumbnailsAsync(FilesListView.ItemsSource);
            }
            else
            {
                _thumbnailService?.Stop();
            }
        }

        /// <summary>
        /// 应用 StackPanel 布局视图（内容、紧凑）
        /// </summary>
        private void ApplyStackPanelView(string templateKey, bool loadThumbnails)
        {
            FilesListView.View = null;
            FilesListView.ItemTemplate = (DataTemplate)FindResource(templateKey);
            FilesListView.ItemsPanel = (ItemsPanelTemplate)FindResource("StackPanelTemplate");
            ScrollViewer.SetHorizontalScrollBarVisibility(FilesListView, ScrollBarVisibility.Disabled);

            if (loadThumbnails && FilesListView.ItemsSource != null)
            {
                _thumbnailService?.LoadThumbnailsAsync(FilesListView.ItemsSource);
            }
            else
            {
                _thumbnailService?.Stop();
            }
        }

        /// <summary>
        /// 应用列表视图（GridView）
        /// </summary>
        private void ApplyListView()
        {
            FilesListView.ItemTemplate = null;
            FilesListView.ItemsPanel = (ItemsPanelTemplate)FindResource("StackPanelTemplate");
            if (FilesGridView != null) FilesListView.View = FilesGridView;
            ScrollViewer.SetHorizontalScrollBarVisibility(FilesListView, ScrollBarVisibility.Auto);

            // 启用缩略图加载 (16px for small icons)
            if (FilesListView.ItemsSource != null)
            {
                _thumbnailService?.LoadThumbnailsAsync(FilesListView.ItemsSource, 16);
            }
        }

        private void FilesListView_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            // 仅在缩略图模式下且按住Ctrl键时处理
            if (_currentViewMode == "Thumbnail" && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
            {
                e.Handled = true; // 阻止 ScrollViewer 滚动

                double delta = e.Delta > 0 ? 10 : -10; // 每次增减10
                double newSize = ThumbnailSize + delta;

                // 限制范围: 64 (中等图标) - 256 (超大图标)
                if (newSize < 64) newSize = 64;
                if (newSize > 256) newSize = 256;

                ThumbnailSize = newSize;
            }
        }

        public void SetViewMode(string mode)
        {
            if (_currentViewMode != mode)
            {
                _currentViewMode = mode;
                ApplyViewMode();
            }
        }


        // 公共属性
        public ListView FilesList => FilesListView;
        public GridView FilesGrid => FilesGridView;
        public TextBlock EmptyStateTextControl => EmptyStateText;

        // 缩略图管理器
        private string _currentViewMode = "List"; // Default to List

        // 分组列头控件（由XAML自动生成字段）
        // GroupedHeaderListView 和 GroupedHeaderGridView 在XAML中定义

        // 文件列表数据源
        public System.Collections.IEnumerable ItemsSource
        {
            get => FilesListView?.ItemsSource;
            set
            {
                if (FilesListView != null)
                {
                    FilesListView.ItemsSource = value;

                    // 触发缩略图加载
                    if (value != null)
                    {
                        // 确定图标大小
                        int size = 32;
                        if (_currentViewMode == "Thumbnail") size = (int)ThumbnailSize; // 使用当前设定的缩略图大小
                        else if (_currentViewMode == "Tiles") size = 64;
                        else if (_currentViewMode == "Content") size = 48; // Content视图图标约40-48

                        _thumbnailService?.LoadThumbnailsAsync(value, size);
                    }
                }
            }
        }

        // 加载更多按钮可见性
        public bool LoadMoreVisible
        {
            get => LoadMoreBtn?.Visibility == Visibility.Visible;
            set
            {
                if (LoadMoreBtn != null)
                    LoadMoreBtn.Visibility = value ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        // 空状态显示
        public void ShowEmptyState(string message = "暂无文件")
        {
            if (EmptyStateTextControl != null)
            {
                EmptyStateTextControl.Text = message;
                EmptyStateTextControl.Visibility = Visibility.Visible;
            }
            if (FilesListView != null) FilesListView.Visibility = Visibility.Collapsed;
        }

        public void HideEmptyState()
        {
            if (EmptyStateTextControl != null)
                EmptyStateTextControl.Visibility = Visibility.Collapsed;
            if (FilesListView != null) FilesListView.Visibility = Visibility.Visible;
        }

        // 选中的项
        public object SelectedItem => FilesListView?.SelectedItem;

        public System.Collections.IList SelectedItems => FilesListView?.SelectedItems;

        // 分组显示相关
        private bool _isGroupedMode = false;

        public bool IsGroupedMode => _isGroupedMode;

        /// <summary>
        /// 设置分组搜索结果
        /// </summary>
        public void SetGroupedSearchResults(Dictionary<SearchResultType, List<FileSystemItem>> groupedItems)
        {
            if (groupedItems == null || groupedItems.Count == 0)
            {
                SwitchToNormalView();
                return;
            }

            _isGroupedMode = true;

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
            if (FilesListView != null)
            {
                FilesListView.Visibility = Visibility.Visible;
                FilesListView.ItemsSource = flatList;

                // 启用分组
                var view = System.Windows.Data.CollectionViewSource.GetDefaultView(FilesListView.ItemsSource);
                if (view != null)
                {
                    view.GroupDescriptions.Clear();
                    view.GroupDescriptions.Add(new System.Windows.Data.PropertyGroupDescription(nameof(FileSystemItem.GroupingKey)));
                }
            }
        }

        private string GetGroupName(SearchResultType type)
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

        public void SwitchToNormalView()
        {
            _isGroupedMode = false;

            if (FilesListView != null)
            {
                FilesListView.Visibility = Visibility.Visible;
                // 清除分组
                if (FilesListView.ItemsSource != null)
                {
                    var view = System.Windows.Data.CollectionViewSource.GetDefaultView(FilesListView.ItemsSource);
                    view?.GroupDescriptions.Clear();
                }
            }
        }

        /// <summary>
        /// 当分组列表加载完成后，为每个ListView订阅选择事件
        /// </summary>


        /// <summary>
        /// 在视觉树中查找所有ListView并订阅事件，同步列宽
        /// </summary>
        private void FindAndSubscribeListViews(DependencyObject parent)
        {
            /*
            if (parent == null) return;
            
            if (parent is ListView listView && listView != GroupedHeaderListView)
            {
                // 移除旧的事件处理（如果存在）
                listView.SelectionChanged -= GroupedListView_SelectionChanged;
                // 添加新的事件处理
                listView.SelectionChanged += GroupedListView_SelectionChanged;
                // 统一上下文菜单（复用主列表的右键菜单）
                if (FilesListView?.ContextMenu != null)
                {
                    listView.ContextMenu = FilesListView.ContextMenu;
                }
                if (!_groupedListViews.Contains(listView))
                {
                    _groupedListViews.Add(listView);
                }
            }
            
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                FindAndSubscribeListViews(child);
            }
            */
        }

        private T FindAncestor<T>(DependencyObject current) where T : DependencyObject
        {
            while (current != null)
            {
                if (current is T ancestor)
                    return ancestor;
                current = VisualTreeHelper.GetParent(current);
            }
            return null;
        }

        /// <summary>
        /// 订阅统一列头的点击事件，转发给外部处理（右键菜单/列显示）
        /// </summary>
        private void SubscribeGroupedHeaderClicks()
        {
            /*
            if (_headerClickSubscribed || GroupedHeaderListView == null) return;

            if (GroupedHeaderListView.View is GridView headerGridView)
            {
                foreach (var column in headerGridView.Columns)
                {
                    if (column.Header is GridViewColumnHeader header)
                    {
                        header.Click -= Header_Click;
                        header.Click += Header_Click;
                        header.MouseRightButtonUp -= Header_RightClick;
                        header.MouseRightButtonUp += Header_RightClick;
                    }
                }
                _headerClickSubscribed = true;
            }
            */
        }

        /// <summary>
        /// 捕获分组列头右键，转发给外部（显示列菜单）
        /// </summary>
        private void HookGroupedHeaderContextMenu()
        {
            /*
            if (_groupedHeaderContextHooked || GroupedHeaderListView == null) return;
            GroupedHeaderListView.AddHandler(UIElement.PreviewMouseRightButtonUpEvent,
                new MouseButtonEventHandler(GroupedHeader_PreviewMouseRightButtonUp), true);
            _groupedHeaderContextHooked = true;
            */
        }

        private void GroupedHeader_PreviewMouseRightButtonUp(object sender, MouseButtonEventArgs e)
        {
            var src = e.OriginalSource as DependencyObject;
            if (src == null) return;

            var header = FindAncestor<GridViewColumnHeader>(src);
            if (header != null)
            {
                GridViewColumnHeaderClick?.Invoke(header, e);
                e.Handled = true;
            }
        }

        private void ColumnHeader_Click(object sender, RoutedEventArgs e)
        {
            // 通过 EventSetter 调用时，sender 就是 GridViewColumnHeader
            if (sender is GridViewColumnHeader header)
            {
                GridViewColumnHeaderClick?.Invoke(header, e);
            }
        }

        private void Header_Click(object sender, RoutedEventArgs e)
        {
            GridViewColumnHeaderClick?.Invoke(sender, e);
        }

        private void Header_RightClick(object sender, MouseButtonEventArgs e)
        {
            GridViewColumnHeaderClick?.Invoke(sender, e);
        }

        /// <summary>
        /// 根据列Tag显示/隐藏列（同时作用于分组统一列头与分组数据列）
        /// </summary>
        /// <summary>
        /// 根据列Tag显示/隐藏列
        /// </summary>
        public void ApplyColumnVisibility(string tag, bool visible)
        {
            if (string.IsNullOrEmpty(tag)) return;

            // 只需要设置当前列宽，LoadColumnWidths 会处理加载
            // 但如果需要实时更新：
            var column = FilesGridView?.Columns.FirstOrDefault(c =>
                c.Header is GridViewColumnHeader h && h.Tag?.ToString() == tag);

            if (column != null)
            {
                if (visible)
                {
                    column.Width = GetReferenceWidth(tag);
                }
                else
                {
                    column.Width = 0;
                }
            }
        }

        private double GetReferenceWidth(string tag)
        {
            // 参考主列表对应列宽，如无则给默认
            if (FilesGridView != null)
            {
                foreach (var col in FilesGridView.Columns)
                {
                    if (col.Header is GridViewColumnHeader h && h.Tag?.ToString() == tag)
                    {
                        var w = col.ActualWidth > 0 ? col.ActualWidth : col.Width;
                        return w > 0 ? w : 100;
                    }
                }
            }
            return 100;
        }

        private int GetColumnIndexByTag(string tag)
        {
            // 与XAML定义顺序一致
            return tag switch
            {
                "Name" => 0,
                "Size" => 1,
                "Type" => 2,
                "ModifiedDate" => 3,
                "CreatedTime" => 4,
                "Notes" => 6,
                _ => -1
            };
        }

        /// <summary>
        /// 将主列表的列宽同步到分组统一列头
        /// </summary>
        private void SyncGroupedHeaderWidthsFromMainGrid()
        {
            /*
            if (GroupedHeaderListView?.View is GridView headerGrid && FilesGridView != null)
            {
                int count = Math.Min(headerGrid.Columns.Count, FilesGridView.Columns.Count);
                for (int i = 0; i < count; i++)
                {
                    var mainWidth = FilesGridView.Columns[i].ActualWidth > 0 ? FilesGridView.Columns[i].ActualWidth : FilesGridView.Columns[i].Width;
                    if (mainWidth > 0)
                    {
                        headerGrid.Columns[i].Width = mainWidth;
                    }
                }
            }
            */
        }

        /// <summary>
        /// 监听分组列头的宽度变化，并同步回主列表列宽（便于保存）
        /// </summary>
        private void SubscribeGroupedHeaderWidthChanges()
        {
            /*
            if (_headerWidthSubscribed || GroupedHeaderListView?.View is not GridView headerGrid || FilesGridView == null) return;

            int count = Math.Min(headerGrid.Columns.Count, FilesGridView.Columns.Count);
            for (int i = 0; i < count; i++)
            {
                var idx = i;
                var col = headerGrid.Columns[i];
                var dpd = DependencyPropertyDescriptor.FromProperty(GridViewColumn.WidthProperty, typeof(GridViewColumn));
                dpd?.AddValueChanged(col, (s, e) =>
                {
                    if (idx < FilesGridView.Columns.Count)
                    {
                        FilesGridView.Columns[idx].Width = col.Width;
                    }
                });
            }

            _headerWidthSubscribed = true;
            */
        }

        /// <summary>
        /// 绑定分组列头分隔线双击，自适应列宽
        /// </summary>




        /// <summary>
        /// 文件列表数据源（兼容性方法）
        /// </summary>
        public System.Collections.IEnumerable FilesItemsSource
        {
            get => FilesListView?.ItemsSource;
            set
            {
                if (_isGroupedMode)
                {
                    // If manually setting ItemsSource in Grouped Mode implies resetting to normal?
                    // Or we just update the list but keep grouping.
                    // Given previous logic blocked it, let's assume we allow it now but clear grouping flag 
                    // if it's a raw assignment not via SetGroupedSearchResults.
                    // Actually SetGroupedSearchResults sets FileList.ItemsSource directly.
                    // So this setter is used by external navigation.
                    SwitchToNormalView();
                }

                if (FilesListView != null)
                {
                    FilesListView.ItemsSource = value;
                    // 强制刷新ListView，确保排序后UI更新
                    FilesListView.Items.Refresh();

                    // 触发缩略图加载
                    if (value != null)
                    {
                        // 确定图标大小
                        int size = 16;
                        if (_currentViewMode == "Thumbnail") size = (int)ThumbnailSize; // 使用当前设定的缩略图大小
                        else if (_currentViewMode == "Tiles") size = 64;
                        else if (_currentViewMode == "Content") size = 48; // Content视图图标约40-48
                        else if (_currentViewMode == "SmallIcons") size = 16;
                        else if (_currentViewMode == "Compact") size = 16;

                        _thumbnailService?.LoadThumbnailsAsync(value, size);
                    }
                }
            }
        }

        #region Inline Rename

        public event EventHandler<RenameEventArgs> CommitRename;

        private void RenameTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (sender is TextBox textBox && textBox.DataContext is FileSystemItem item)
            {
                if (e.Key == Key.Enter)
                {                    // Force sync the TextBox text to RenameText in case binding hasn't updated
                    item.RenameText = textBox.Text;
                    CommitRenameLogic(item);
                    e.Handled = true;
                }
                else if (e.Key == Key.Escape)
                {
                    CancelRenameLogic(item);
                    e.Handled = true;
                }
            }
        }

        private void RenameTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (sender is TextBox textBox && textBox.DataContext is FileSystemItem item)
            {
                // Commit on lost focus
                if (item.IsRenaming)
                {
                    // Delay slightly to allow Cancel to process if Esc was pressed
                    // But actually, KeyDown happens before LostFocus.
                    CommitRenameLogic(item);
                }
            }
        }

        private void RenameTextBox_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (sender is TextBox textBox && textBox.DataContext is FileSystemItem item)
            {
                // 只在变为可见时处理
                bool isVisible = (bool)e.NewValue;
                if (!isVisible || !item.IsRenaming)
                    return;
                // 使用 Render 优先级以确保在布局完成后执行
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    // 再次检查 - 可能在调度期间状态已改变
                    if (!item.IsRenaming || !textBox.IsVisible)
                        return;

                    // 使用 Keyboard.Focus() 确保键盘焦点设置正确
                    Keyboard.Focus(textBox);
                    textBox.Focus();

                    // 从 Path 获取完整文件名（因为 RenameText 可能还没同步）
                    string name = !string.IsNullOrEmpty(item.RenameText)
                        ? item.RenameText
                        : System.IO.Path.GetFileName(item.Path);
                    if (!string.IsNullOrEmpty(name))
                    {
                        // 确保 TextBox 有正确的文本
                        if (string.IsNullOrEmpty(textBox.Text))
                        {
                            textBox.Text = name;
                        }

                        int lastDotIndex = name.LastIndexOf('.');
                        if (lastDotIndex > 0 && !item.IsDirectory)
                        {
                            // 选中文件名部分（不包含扩展名）
                            textBox.Select(0, lastDotIndex);
                        }
                        else
                        {
                            textBox.SelectAll();
                        }
                    }
                }), System.Windows.Threading.DispatcherPriority.Render);
            }
        }

        private void CommitRenameLogic(FileSystemItem item)
        {
            if (!item.IsRenaming) return;
            // Check if name actually changed
            if (string.IsNullOrWhiteSpace(item.RenameText) || item.RenameText == item.Name)
            {
                item.IsRenaming = false;
                return;
            }

            // 触发重命名提交事件
            CommitRename?.Invoke(this, new RenameEventArgs(item, item.RenameText));

            // 重置状态 (实际重命名逻辑完成后，或者失败后，由外部控制或者这里暂时关闭用于UI反馈)
            // 这里先关闭编辑状态，外部逻辑如果失败可以再次重新开启或者报错
            item.IsRenaming = false;
        }

        private void CancelRenameLogic(FileSystemItem item)
        {
            item.IsRenaming = false;
            // Revert text? Actually IsRenaming=false hides the box, so text doesn't matter much unless reused.
            item.RenameText = item.Name;
        }

        #endregion

        private string GetCellTextForColumn(object item, GridViewColumn column, GridViewColumnHeader header)
        {
            if (item == null) return "";

            if (column.DisplayMemberBinding is System.Windows.Data.Binding binding && binding.Path != null)
            {
                var prop = item.GetType().GetProperty(binding.Path.Path);
                var val = prop?.GetValue(item);
                return val?.ToString() ?? "";
            }

            var propName = header?.Tag?.ToString();
            if (!string.IsNullOrEmpty(propName))
            {
                var prop2 = item.GetType().GetProperty(propName);
                var val2 = prop2?.GetValue(item);
                if (val2 != null) return val2.ToString();
            }

            return "";
        }

        private double MeasureTextWidth(string text, ListView listView)
        {
            try
            {
                var tb = new TextBlock
                {
                    Text = text ?? "",
                    FontSize = listView?.FontSize ?? 12,
                    FontFamily = listView?.FontFamily ?? new System.Windows.Media.FontFamily("Segoe UI")
                };
                tb.Measure(new System.Windows.Size(double.PositiveInfinity, double.PositiveInfinity));
                return tb.DesiredSize.Width;
            }
            catch (Exception)
            {
                return 50; // Fallback
            }
        }

        /// <summary>
        /// Load column widths from config
        /// </summary>
        public void LoadColumnWidths()
        {
            try
            {
                var config = ConfigManager.Load();

                // Find Tags and Notes columns using FindName
                var colTags = FindName("ColTags") as GridViewColumn;
                var colNotes = FindName("ColNotes") as GridViewColumn;

                // Apply Tags and Notes column widths
                if (colTags != null && config.ColTagsWidth > 0)
                {
                    colTags.Width = config.ColTagsWidth;
                }

                if (colNotes != null && config.ColNotesWidth > 0)
                {
                    colNotes.Width = config.ColNotesWidth;
                }



                // 重新调整名称列宽度以适应新的列宽度
                AdjustNameColumnWidth();
            }
            catch
            {
                // Ignore errors, use default widths
            }
        }

        /// <summary>
        /// Apply column widths (called when settings change)
        /// </summary>
        public void ApplyColumnWidths()
        {
            LoadColumnWidths();
        }

        #region 响应式布局

        /// <summary>
        /// 设置 FileListService 引用（用于控制文件名显示）
        /// </summary>
        public void SetFileListService(Services.FileList.FileListService fileListService)
        {
            _fileListService = fileListService;
        }



        /// <summary>
        /// 设置列的可见性
        /// </summary>
        private void SetColumnVisibility(string columnName, bool isVisible)
        {
            var column = FindName(columnName) as GridViewColumn;
            if (column != null)
            {
                if (isVisible)
                {
                    // 显示列：恢复宽度
                    if (column == FindName("ColType"))
                        column.Width = 60;
                    else if (column == FindName("ColSize"))
                        column.Width = 90;
                    else if (column == FindName("ColModifiedDate"))
                        column.Width = 100;
                    else if (column == FindName("ColCreatedTime"))
                        column.Width = 60;
                }
                else
                {
                    // 隐藏列：设置宽度为0
                    column.Width = 0;
                }
            }
        }

        /// <summary>
        /// 刷新文件列表显示
        /// </summary>
        private void RefreshFileList()
        {
            // 强制刷新 ListView
            if (FilesListView != null && FilesListView.Items != null)
            {
                FilesListView.Items.Refresh();
            }
        }

        /// <summary>
        /// 调整名称列宽度以填满剩余空间
        /// </summary>
        private void AdjustNameColumnWidth()
        {
            try
            {
                if (FilesListView == null || !FilesListView.IsLoaded) return;

                var colName = FindName("ColName") as GridViewColumn;
                var colType = FindName("ColType") as GridViewColumn;
                var colSize = FindName("ColSize") as GridViewColumn;
                var colModifiedDate = FindName("ColModifiedDate") as GridViewColumn;
                var colCreatedTime = FindName("ColCreatedTime") as GridViewColumn;
                var colTags = FindName("ColTags") as GridViewColumn;
                var colNotes = FindName("ColNotes") as GridViewColumn;

                if (colName == null) return;

                // 直接从列获取实际宽度（而不是使用缓存）
                double otherColumnsWidth = 0;

                if (colType != null && colType.Width > 0)
                    otherColumnsWidth += colType.Width;
                if (colSize != null && colSize.Width > 0)
                    otherColumnsWidth += colSize.Width;
                if (colModifiedDate != null && colModifiedDate.Width > 0)
                    otherColumnsWidth += colModifiedDate.Width;
                if (colCreatedTime != null && colCreatedTime.Width > 0)
                    otherColumnsWidth += colCreatedTime.Width;

                // 标签和备注列使用实际宽度（这样设置修改后立即生效）
                if (colTags != null && !double.IsNaN(colTags.Width))
                    otherColumnsWidth += colTags.Width;
                if (colNotes != null && !double.IsNaN(colNotes.Width))
                    otherColumnsWidth += colNotes.Width;

                // 计算名称列应该的宽度
                double availableWidth = FilesListView.ActualWidth;
                double scrollBarWidth = System.Windows.SystemParameters.VerticalScrollBarWidth;
                // 减去滚动条宽度和额外边距（20px）确保不出现横向滚动条
                double nameColumnWidth = availableWidth - otherColumnsWidth - scrollBarWidth - 20;

                // 设置最小宽度
                if (nameColumnWidth < 120) nameColumnWidth = 120;

                colName.Width = nameColumnWidth;
            }
            catch
            {
                // 忽略错误
            }
        }

        #endregion
    }

    public class RenameEventArgs : EventArgs
    {
        public FileSystemItem Item { get; }
        public string NewName { get; }

        public RenameEventArgs(FileSystemItem item, string newName)
        {
            Item = item;
            NewName = newName;
        }
    }
}


























