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
            DependencyProperty.Register("CurrentViewMode", typeof(YiboFile.Models.Enums.FileListViewMode), typeof(FileListControl),
                new PropertyMetadata(YiboFile.Models.Enums.FileListViewMode.List, OnViewModeChanged));

        public YiboFile.Models.Enums.FileListViewMode CurrentViewMode
        {
            get { return (YiboFile.Models.Enums.FileListViewMode)GetValue(CurrentViewModeProperty); }
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
                YiboFile.Models.Enums.FileListViewMode mode = (YiboFile.Models.Enums.FileListViewMode)e.NewValue;
                control.ApplyViewMode();

                // 触发缩略图刷新
                if (control.FilesListView?.ItemsSource != null)
                {
                    int size = 32;
                    if (mode == YiboFile.Models.Enums.FileListViewMode.Thumbnail) size = (int)control.ThumbnailSize;
                    else if (mode == YiboFile.Models.Enums.FileListViewMode.Tiles) size = 64;
                    else if (mode == YiboFile.Models.Enums.FileListViewMode.Content) size = 48;
                    control._thumbnailService?.LoadThumbnailsAsync(control.FilesListView.ItemsSource, size);
                }
            }
        }
        #endregion

        public FileListControl()
        {
            InitializeComponent();

            this.DataContextChanged += (s, e) =>
            {
                if (IsLoaded)
                {
                    LoadColumnWidths();
                    _autoColumnWidthBehavior?.AdjustTargetColumnWidth();
                }
            };

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

                // 转发 SizeChanged 事件
                FilesListView.SizeChanged += (s, e) =>
                {
                    SizeChanged?.Invoke(s, e);
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

            // 列宽加载延迟到 Loaded 事件（确保 DataContext 已就绪）
            // → 见下方 this.Loaded 回调

            // 延迟调整名称列宽度并禁用横向滚动条
            this.Loaded += (s, e) =>
            {
                // 禁用横向滚动条
                // 1. 在 DataContext 就绪后，首先加载持久化的列宽配置
                LoadColumnWidths();
                var config = GetConfig();
                _cachedNotesWidth = config.ColNotesWidth;

                // 2. 重新应用当前的视图模式（确保恢复后的状态正确初始化面板策略和模板）
                ApplyViewMode();

                // 3. 针对列表模式初始化名称列的自动填充计算
                if (FilesListView != null && CurrentViewMode == YiboFile.Models.Enums.FileListViewMode.List)
                {
                    if (_autoColumnWidthBehavior == null)
                    {
                        _autoColumnWidthBehavior = new AutoColumnWidthBehavior(FilesListView, "Name");
                    }
                    _autoColumnWidthBehavior.AdjustTargetColumnWidth();
                }

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

        public void SetViewMode(YiboFile.Models.Enums.FileListViewMode mode)
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

                // 根据当前内容上下文（如标签模式或路径模式），刷新列宽和可见性
                if (control.IsLoaded)
                {
                    control.LoadColumnWidths();
                    control._autoColumnWidthBehavior?.AdjustTargetColumnWidth();
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

        private void RenameOverlay_RenameConfirmed(object sender, RenameConfirmedEventArgs e)
        {
            if (sender is RenameOverlay overlay && overlay.DataContext is FileSystemItem item)
            {
                item.RenameText = overlay.Text;
                RenameHandler.CommitRename(item, this.DataContext);
            }
        }

        private void RenameOverlay_RenameCancelled(object sender, EventArgs e)
        {
            if (sender is RenameOverlay overlay && overlay.DataContext is FileSystemItem item)
            {
                RenameHandler.CancelRename(item);
            }
        }

        #endregion

        /// <summary>
        /// Load column widths from config
        /// </summary>
        public void LoadColumnWidths()
        {
            try
            {
                var browser = FindFileBrowser();
                if (browser != null)
                {
                    var colService = App.ServiceProvider?.GetService(typeof(YiboFile.Services.ColumnManagement.ColumnService)) as YiboFile.Services.ColumnManagement.ColumnService;
                    colService?.LoadColumnWidths(browser);
                }
                
                // 重新调整名称列宽度以适应新的列宽或隐藏状态
                _autoColumnWidthBehavior?.AdjustTargetColumnWidth();
            }
            catch
            {
                // Ignore errors
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

        public AutoColumnWidthBehavior AutoColumnWidthBehavior => _autoColumnWidthBehavior;

        public void RequestColumnRecalculation()
        {
            _autoColumnWidthBehavior?.AdjustTargetColumnWidth();
        }



        #endregion

        #region 列头拖拽指示器逻辑

        private GridViewHeaderRowPresenter _headerRowPresenter;
        private ColumnReorderBehavior _columnReorderBehavior;
        private AutoColumnWidthBehavior _autoColumnWidthBehavior;
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
                    var canvas = FindName("ColumnDropIndicatorCanvas") as Canvas;
                    var indicator = FindName("ColumnDropIndicator") as Border;
                    if (canvas != null && indicator != null)
                    {
                        _columnReorderBehavior?.Detach();
                        _columnReorderBehavior = new ColumnReorderBehavior(_headerRowPresenter, canvas, indicator, SaveColumnWidths);
                    }
                }
            }), System.Windows.Threading.DispatcherPriority.Loaded);
        }


        /// <summary>
        /// 保存列宽度和顺序
        /// </summary>
        public void SaveColumnWidths()
        {
            try
            {
                var browser = FindFileBrowser();
                if (browser != null)
                {
                    var colService = App.ServiceProvider?.GetService(typeof(YiboFile.Services.ColumnManagement.ColumnService)) as YiboFile.Services.ColumnManagement.ColumnService;
                    colService?.SaveColumnWidths(browser);
                }
            }
            catch { }
        }

        private FileBrowserControl FindFileBrowser()
        {
            DependencyObject current = this;
            while (current != null)
            {
                if (current is FileBrowserControl browser) return browser;
                current = VisualTreeHelper.GetParent(current);
            }
            return null;
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
