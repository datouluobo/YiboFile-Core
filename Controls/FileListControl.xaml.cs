using System;
using System.Collections;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Threading;
using Microsoft.Extensions.DependencyInjection;
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
using YiboFile.Controls.Helpers;

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

        // 依赖属性：当前视图模式
        public static readonly DependencyProperty CurrentViewModeProperty =
            DependencyProperty.Register("CurrentViewMode", typeof(string), typeof(FileListControl),
                new PropertyMetadata("List", OnViewModeChanged));

        public string CurrentViewMode
        {
            get { return (string)GetValue(CurrentViewModeProperty); }
            set { SetValue(CurrentViewModeProperty, value); }
        }

        // 依赖属性：加载更多可见性
        public static readonly DependencyProperty IsLoadMoreVisibleProperty =
            DependencyProperty.Register("IsLoadMoreVisible", typeof(bool), typeof(FileListControl),
                new PropertyMetadata(false, OnIsLoadMoreVisibleChanged));

        public bool IsLoadMoreVisible
        {
            get { return (bool)GetValue(IsLoadMoreVisibleProperty); }
            set { SetValue(IsLoadMoreVisibleProperty, value); }
        }

        private static void OnIsLoadMoreVisibleChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is FileListControl control && control.LoadMoreBtn != null)
            {
                control.LoadMoreBtn.Visibility = (bool)e.NewValue ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        // 依赖属性：加载更多命令
        public static readonly DependencyProperty LoadMoreCommandProperty =
            DependencyProperty.Register("LoadMoreCommand", typeof(ICommand), typeof(FileListControl),
                new PropertyMetadata(null));

        public ICommand LoadMoreCommand
        {
            get { return (ICommand)GetValue(LoadMoreCommandProperty); }
            set { SetValue(LoadMoreCommandProperty, value); }
        }

        private static void OnViewModeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is FileListControl control)
            {
                string mode = e.NewValue as string;
                control.ApplyViewMode();

                // 触发缩略图刷新
                if (control.FilesListView?.ItemsSource != null)
                {
                    int size = 32;
                    if (mode == "Thumbnail") size = (int)control.ThumbnailSize;
                    else if (mode == "Tiles") size = 64;
                    else if (mode == "Content") size = 48;
                    control._thumbnailService?.LoadThumbnailsAsync(control.FilesListView.ItemsSource, size);
                }
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
            var config = GetConfig();
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

                // 初始化列头拖拽监听
                InitializeColumnReorderHeaderListener();
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
            FileListViewModeHelper.ApplyViewMode(
                CurrentViewMode,
                FilesListView,
                FilesGridView,
                _thumbnailService,
                FindResource);
        }

        private void FilesListView_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            FileListViewModeHelper.HandlePreviewMouseWheel(
                e,
                CurrentViewMode,
                ThumbnailSize,
                newSize => ThumbnailSize = newSize);
        }

        public void SetViewMode(string mode)
        {
            CurrentViewMode = mode;
        }


        // 公共属性
        public ListView FilesList => FilesListView;
        public GridView FilesGrid => FilesGridView;
        public TextBlock EmptyStateTextControl => EmptyStateText;

        // 缩略图管理器

        // 分组列头控件（由XAML自动生成字段）
        // GroupedHeaderListView 和 GroupedHeaderGridView 在XAML中定义

        // 文件列表数据源
        // 文件列表数据源
        public static readonly DependencyProperty ItemsSourceProperty =
            DependencyProperty.Register("ItemsSource", typeof(System.Collections.IEnumerable), typeof(FileListControl),
                new PropertyMetadata(null, OnItemsSourceChanged));

        public System.Collections.IEnumerable ItemsSource
        {
            get => (System.Collections.IEnumerable)GetValue(ItemsSourceProperty);
            set => SetValue(ItemsSourceProperty, value);
        }

        private static void OnItemsSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var control = (FileListControl)d;
            var value = (System.Collections.IEnumerable)e.NewValue;

            if (control.FilesListView != null)
            {
                if (control._isGroupedMode)
                {
                    control.SwitchToNormalView();
                }

                control.FilesListView.ItemsSource = value;

                // 强制刷新ListView
                control.FilesListView.Items.Refresh();

                // 触发缩略图加载
                FileListViewModeHelper.TriggerThumbnailLoad(
                    value,
                    control.CurrentViewMode,
                    control.ThumbnailSize,
                    control._thumbnailService);
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
            FileListViewModeHelper.SetGroupedSearchResults(
                groupedItems,
                FilesListView,
                val => _isGroupedMode = val);
        }

        public void ApplyGrouping()
        {
            FileListViewModeHelper.ApplyGrouping(
                FilesListView,
                val => _isGroupedMode = val);
        }

        public void SwitchToNormalView()
        {
            FileListViewModeHelper.SwitchToNormalView(
                FilesListView,
                val => _isGroupedMode = val);
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
            GridViewColumnHeader header = sender as GridViewColumnHeader;
            if (header == null && e.OriginalSource is GridViewColumnHeader h)
            {
                header = h;
            }

            if (header != null && header.Role != GridViewColumnHeaderRole.Padding)
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
        /// 根据列Tag显示/隐藏列
        /// </summary>
        public void ApplyColumnVisibility(string tag, bool visible)
        {
            if (string.IsNullOrEmpty(tag)) return;

            var column = FilesGridView?.Columns.FirstOrDefault(c =>
                c.Header is GridViewColumnHeader h && h.Tag?.ToString() == tag);

            if (column != null)
            {
                column.Width = visible ? GetReferenceWidth(tag) : 0;
            }
        }

        private double GetReferenceWidth(string tag)
        {
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




        #region Inline Rename


        private void RenameTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            RenameHandler.HandleKeyDown(sender, e, this.DataContext);
        }

        private void RenameTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            RenameHandler.HandleLostFocus(sender, e, this.DataContext);
        }

        private void RenameTextBox_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            RenameHandler.HandleIsVisibleChanged(sender, e, Dispatcher);
        }

        private void CommitRenameLogic(FileSystemItem item)
        {
            RenameHandler.CommitRename(item, this.DataContext);
        }

        private void CancelRenameLogic(FileSystemItem item)
        {
            RenameHandler.CancelRename(item);
        }

        #endregion

        /// <summary>
        /// Load column widths from config
        /// </summary>
        public void LoadColumnWidths()
        {
            try
            {
                var config = GetConfig();

                // 加载列顺序
                if (FilesGridView != null && !string.IsNullOrEmpty(config.ColumnOrder))
                {
                    var columns = FilesGridView.Columns;
                    if (columns.Count >= 7)
                    {
                        // 创建列名到列的映射（从当前列的 Header Tag 获取）
                        var columnMap = new Dictionary<string, GridViewColumn>();
                        foreach (var col in columns)
                        {
                            var tag = GetColumnTag(col);
                            if (!string.IsNullOrEmpty(tag) && !columnMap.ContainsKey(tag))
                            {
                                columnMap[tag] = col;
                            }
                        }

                        var savedOrder = config.ColumnOrder.Split(',');
                        var newColumns = new List<GridViewColumn>();

                        foreach (var colName in savedOrder)
                        {
                            var trimmedName = colName.Trim();
                            if (columnMap.ContainsKey(trimmedName))
                            {
                                newColumns.Add(columnMap[trimmedName]);
                            }
                        }

                        // 添加未在顺序中的列（向后兼容）
                        foreach (var kvp in columnMap)
                        {
                            if (!savedOrder.Any(s => s.Trim() == kvp.Key))
                            {
                                newColumns.Add(kvp.Value);
                            }
                        }

                        // 重新排序列
                        if (newColumns.Count == columns.Count)
                        {
                            FilesGridView.Columns.Clear();
                            foreach (var col in newColumns)
                            {
                                FilesGridView.Columns.Add(col);
                            }
                        }
                    }
                }

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

        #region 列头拖拽指示器逻辑

        private GridViewHeaderRowPresenter _headerRowPresenter;
        private bool _isColumnDragging;
        private Point _lastMousePos;
        private AppConfig _config;

        private AppConfig GetConfig()
        {
            if (_config == null)
            {
                try
                {
                    var configService = App.ServiceProvider?.GetService<YiboFile.Services.Config.IConfigurationService>();
                    _config = configService?.Config;
                }
                catch { }
            }

            // Fallback (e.g. design time)
            if (_config == null)
            {
                _config = YiboFile.Services.Config.ConfigurationService.Instance.Config;
            }
            return _config;
        }

        private void InitializeColumnReorderHeaderListener()
        {
            if (FilesListView == null) return;

            // Register Sorting Click Event
            FilesListView.RemoveHandler(GridViewColumnHeader.ClickEvent, new RoutedEventHandler(ColumnHeader_Click));
            FilesListView.AddHandler(GridViewColumnHeader.ClickEvent, new RoutedEventHandler(ColumnHeader_Click));

            // 延迟查找 HeaderRowPresenter 并设置事件监听
            this.Dispatcher.BeginInvoke(new Action(() =>
            {
                _headerRowPresenter = FindVisualChild<GridViewHeaderRowPresenter>(FilesListView);
                if (_headerRowPresenter != null)
                {
                    // 使用 Preview 事件确保能捕获到，但不监听 MouseDown 以避免干扰
                    _headerRowPresenter.PreviewMouseMove += HeaderRowPresenter_PreviewMouseMove;
                    _headerRowPresenter.PreviewMouseLeftButtonUp += HeaderRowPresenter_PreviewMouseLeftButtonUp;
                    _headerRowPresenter.MouseLeave += HeaderRowPresenter_MouseLeave;
                }
            }), System.Windows.Threading.DispatcherPriority.Loaded);
        }

        private void HeaderRowPresenter_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            // 检测是否在拖拽列头（鼠标按下且移动中）
            if (e.LeftButton != MouseButtonState.Pressed)
            {
                if (_isColumnDragging)
                {
                    HideColumnDropIndicator();
                    _isColumnDragging = false;
                }
                return;
            }

            Point mousePos = e.GetPosition(_headerRowPresenter);

            // 检测是否移动了足够距离来显示指示器
            if (!_isColumnDragging)
            {
                if (Math.Abs(mousePos.X - _lastMousePos.X) > 20 || Math.Abs(mousePos.Y - _lastMousePos.Y) > 20)
                {
                    _isColumnDragging = true;
                }
                _lastMousePos = mousePos;
            }

            // 已在拖拽状态，更新指示器
            if (_isColumnDragging)
            {
                UpdateColumnDropIndicator(mousePos);
            }
        }

        private void HeaderRowPresenter_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (_isColumnDragging)
            {
                HideColumnDropIndicator();
                _isColumnDragging = false;

                // 延迟保存列顺序
                this.Dispatcher.BeginInvoke(new Action(() => SaveColumnWidths()), System.Windows.Threading.DispatcherPriority.Background);
            }
        }

        private void HeaderRowPresenter_MouseLeave(object sender, MouseEventArgs e)
        {
            if (_isColumnDragging)
            {
                HideColumnDropIndicator();
                _isColumnDragging = false;
            }
        }

        private void UpdateColumnDropIndicator(Point mousePos)
        {
            var canvas = FindName("ColumnDropIndicatorCanvas") as Canvas;
            var indicator = FindName("ColumnDropIndicator") as Border;
            if (_headerRowPresenter == null || canvas == null || indicator == null) return;

            // 显示指示器
            canvas.Visibility = Visibility.Visible;
            indicator.Visibility = Visibility.Visible;

            // 找到所有可见的 GridViewColumnHeader
            var headers = GetVisualChildren<GridViewColumnHeader>(_headerRowPresenter)
                .Where(h => h.Visibility == Visibility.Visible && h.ActualWidth > 0 && h.Role == GridViewColumnHeaderRole.Normal)
                .OrderBy(h => h.TranslatePoint(new Point(0, 0), _headerRowPresenter).X)
                .ToList();

            // 获取列头高度（即使列表为空也使用默认值）
            double headerHeight = 28;
            if (headers.Count > 0 && headers[0].ActualHeight > 0)
            {
                headerHeight = headers[0].ActualHeight;
            }

            // 立即设置高度，确保不会显示为点
            double newHeight = Math.Max(24, headerHeight - 2);
            indicator.Height = newHeight;

            if (headers.Count == 0) return;

            // 计算插入位置
            double indicatorX = 0;
            foreach (var header in headers)
            {
                Point headerPos = header.TranslatePoint(new Point(0, 0), _headerRowPresenter);
                double headerCenter = headerPos.X + header.ActualWidth / 2;

                if (mousePos.X < headerCenter)
                {
                    indicatorX = headerPos.X;
                    break;
                }
                indicatorX = headerPos.X + header.ActualWidth;
            }

            // 设置指示器位置
            Point presenterPosInCanvas = _headerRowPresenter.TranslatePoint(new Point(0, 0), canvas);
            Canvas.SetLeft(indicator, presenterPosInCanvas.X + indicatorX - (indicator.Width / 2));
            Canvas.SetTop(indicator, presenterPosInCanvas.Y + 1);
        }

        private void HideColumnDropIndicator()
        {
            var canvas = FindName("ColumnDropIndicatorCanvas") as Canvas;
            var indicator = FindName("ColumnDropIndicator") as Border;
            if (canvas != null) canvas.Visibility = Visibility.Collapsed;
            if (indicator != null) indicator.Visibility = Visibility.Collapsed;
        }


        /// <summary>
        /// 保存列宽度和顺序
        /// </summary>
        public void SaveColumnWidths()
        {
            if (FilesGridView == null) return;

            try
            {
                var config = GetConfig();
                var columns = FilesGridView.Columns;

                // 保存列顺序
                var columnOrder = new List<string>();
                foreach (var column in columns)
                {
                    var tag = GetColumnTag(column);
                    if (!string.IsNullOrEmpty(tag))
                    {
                        columnOrder.Add(tag);
                    }
                }
                config.ColumnOrder = string.Join(",", columnOrder);

                // 保存各列宽度 (非0列)
                foreach (var column in columns)
                {
                    var tag = GetColumnTag(column);
                    if (!string.IsNullOrEmpty(tag))
                    {
                        var width = column.ActualWidth > 0 ? column.ActualWidth : column.Width;

                        if (width > 0)
                        {
                            switch (tag)
                            {
                                case "Name": config.ColNameWidth = width; break;
                                case "Size": config.ColSizeWidth = width; break;
                                case "Type": config.ColTypeWidth = width; break;
                                case "ModifiedDate": config.ColModifiedDateWidth = width; break;
                                case "CreatedTime": config.ColCreatedTimeWidth = width; break;
                                case "Tags": config.ColTagsWidth = width; break;
                                case "Notes": config.ColNotesWidth = width; break;
                            }
                        }
                    }
                }
                ConfigManager.Save(config);
            }
            catch { }
        }

        private string GetColumnTag(GridViewColumn column)
        {
            if (column == null) return null;
            if (column.Header is FrameworkElement fe) return fe.Tag?.ToString();
            return column.Header?.ToString();
        }

        private T FindVisualChild<T>(DependencyObject obj) where T : DependencyObject
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(obj); i++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(obj, i);
                if (child != null && child is T t) return t;
                T childOfChild = FindVisualChild<T>(child);
                if (childOfChild != null) return childOfChild;
            }
            return null;
        }

        private IEnumerable<T> GetVisualChildren<T>(DependencyObject depObj) where T : DependencyObject
        {
            if (depObj != null)
            {
                for (int i = 0; i < VisualTreeHelper.GetChildrenCount(depObj); i++)
                {
                    DependencyObject child = VisualTreeHelper.GetChild(depObj, i);
                    if (child != null && child is T t) yield return t;

                    foreach (T childOfChild in GetVisualChildren<T>(child))
                    {
                        yield return childOfChild;
                    }
                }
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
