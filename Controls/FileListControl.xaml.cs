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
using System.Windows.Media.Effects;
using System.ComponentModel;
using OoiMRR.Controls.Converters;
using OoiMRR.ViewModels;
using OoiMRR.Services.Search;

namespace OoiMRR.Controls
{
    /// <summary>
    /// FileListControl.xaml 的交互逻辑
    /// 独立的文件列表控件，使用详细信息视图
    /// </summary>
    public partial class FileListControl : UserControl
    {

        // 事件定义
        public event SelectionChangedEventHandler SelectionChanged;
        public new event MouseButtonEventHandler MouseDoubleClick;
        public new event MouseButtonEventHandler PreviewMouseDoubleClick;
        public new event KeyEventHandler PreviewKeyDown;
        public new event MouseButtonEventHandler PreviewMouseLeftButtonDown;
        public new event MouseButtonEventHandler MouseLeftButtonUp;
        public new event MouseButtonEventHandler PreviewMouseDown;
        public event RoutedEventHandler GridViewColumnHeaderClick;
        public new event SizeChangedEventHandler SizeChanged;
        public event RoutedEventHandler LoadMoreClick;

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
                FilesListView.SizeChanged += (s, e) => SizeChanged?.Invoke(s, e);
                
                // 订阅列标题点击事件
                if (FilesGridView != null)
                {
                    foreach (GridViewColumn column in FilesGridView.Columns)
                    {
                        if (column.Header is GridViewColumnHeader header)
                        {
                            header.Click += (s, e) => GridViewColumnHeaderClick?.Invoke(s, e);
                        }
                    }
                }
            }

            // 订阅加载更多按钮事件
            if (LoadMoreBtn != null)
            {
                LoadMoreBtn.Click += (s, e) => LoadMoreClick?.Invoke(s, e);
            }
            
            // 初始化详细信息视图
            ApplyViewMode();
        }

        private void LoadMoreBtn_Click(object sender, RoutedEventArgs e)
        {
            LoadMoreClick?.Invoke(sender, e);
        }

        private void ApplyViewMode()
        {
            if (FilesListView == null) return;
            // 详细信息视图：使用 GridView（已在XAML定义）
            FilesListView.ItemTemplate = null;
            var itemsPanel = new ItemsPanelTemplate(new FrameworkElementFactory(typeof(VirtualizingStackPanel)));
            FilesListView.ItemsPanel = itemsPanel;
            if (FilesGridView != null) FilesListView.View = FilesGridView;
            ScrollViewer.SetHorizontalScrollBarVisibility(FilesListView, ScrollBarVisibility.Auto);
        }


        // 公共属性
        public ListView FilesList => FilesListView;
        public GridView FilesGrid => FilesGridView;
        public TextBlock EmptyStateTextControl => EmptyStateText;
        
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
        }

        public void HideEmptyState()
        {
            if (EmptyStateTextControl != null)
                EmptyStateTextControl.Visibility = Visibility.Collapsed;
        }

        // 选中的项
        public object SelectedItem
        {
            get
            {
                if (_isGroupedMode)
                {
                    return _groupedSelectedItems?.FirstOrDefault();
                }
                return FilesListView?.SelectedItem;
            }
        }
        
        public System.Collections.IList SelectedItems
        {
            get
            {
                if (_isGroupedMode)
                {
                    return _groupedSelectedItems ?? new List<object>();
                }
                return FilesListView?.SelectedItems;
            }
        }
        
        /// <summary>
        /// 处理分组ListView的选择变化
        /// </summary>
        private void GroupedListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_isGroupedMode) return;
            
            // 如果未按下Ctrl/Shift，清除其他分组的选中，避免“点选变多选”
            if (Keyboard.Modifiers == ModifierKeys.None && sender is ListView sourceListView)
            {
                if (!_groupedListViews.Contains(sourceListView))
                {
                    _groupedListViews.Add(sourceListView);
                }
                foreach (var lv in _groupedListViews)
                {
                    if (lv == sourceListView) continue;
                    lv.SelectionChanged -= GroupedListView_SelectionChanged;
                    lv.SelectedItems.Clear();
                    lv.SelectionChanged += GroupedListView_SelectionChanged;
                }
            }

            // 重新收集所有选中的项
            _groupedSelectedItems.Clear();
            foreach (var lv in _groupedListViews)
            {
                foreach (var item in lv.SelectedItems)
                {
                    if (!_groupedSelectedItems.Contains(item))
                    {
                        _groupedSelectedItems.Add(item);
                    }
                }
            }
            
            // 触发选择变化事件
            SelectionChanged?.Invoke(sender, e);
        }
        
        // 分组显示相关
        private bool _isGroupedMode = false;
        private ObservableCollection<SearchResultGroupViewModel> _groupedResults;
        private List<object> _groupedSelectedItems = new List<object>();
        private List<ListView> _groupedListViews = new List<ListView>();
        private bool _headerClickSubscribed = false;
        private bool _headerWidthSubscribed = false;
        private bool _groupedThumbHooked = false;
        private bool _groupedHeaderContextHooked = false;

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
            _groupedSelectedItems.Clear();
            _groupedListViews.Clear();
            _groupedResults = new ObservableCollection<SearchResultGroupViewModel>();
            
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
                    var group = new SearchResultGroupViewModel
                    {
                        GroupType = type,
                        GroupName = GetGroupName(type),
                        Items = new ObservableCollection<FileSystemItem>(groupedItems[type])
                    };
                    _groupedResults.Add(group);
                }
            }
            
            // 切换到分组显示模式
            SwitchToGroupedView();

            // 确保视觉树生成后立即订阅（避免首次点击前未注册SelectionChanged）
            Dispatcher.BeginInvoke(new Action(() =>
            {
                FindAndSubscribeListViews(GroupedResultsList);
            }), System.Windows.Threading.DispatcherPriority.Loaded);
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
        
        private void SwitchToGroupedView()
        {
            if (FilesListView != null)
                FilesListView.Visibility = Visibility.Collapsed;
            
            if (GroupedResultsViewer != null)
            {
                GroupedResultsViewer.Visibility = Visibility.Visible;
                if (GroupedResultsList != null)
                {
                    GroupedResultsList.ItemsSource = _groupedResults;
                    
                    // 订阅Loaded事件，为每个ListView添加SelectionChanged事件处理
                    GroupedResultsList.Loaded -= GroupedResultsList_Loaded;
                    GroupedResultsList.Loaded += GroupedResultsList_Loaded;
                }

                // 统一列头点击事件（支持右键菜单/调节列宽）
                SubscribeGroupedHeaderClicks();

                // 与主列表同步列宽（使用当前列宽配置）
                SyncGroupedHeaderWidthsFromMainGrid();
                SubscribeGroupedHeaderWidthChanges();
                HookGroupedHeaderThumbs();
                HookGroupedHeaderContextMenu();
            }
        }
        
        /// <summary>
        /// 当分组列表加载完成后，为每个ListView订阅选择事件
        /// </summary>
        private void GroupedResultsList_Loaded(object sender, RoutedEventArgs e)
        {
            _groupedListViews.Clear();
            _groupedSelectedItems.Clear();
            // 查找所有ListView并订阅SelectionChanged事件
            FindAndSubscribeListViews(GroupedResultsList);
        }
        
        /// <summary>
        /// 在视觉树中查找所有ListView并订阅事件，同步列宽
        /// </summary>
        private void FindAndSubscribeListViews(DependencyObject parent)
        {
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
        }

        /// <summary>
        /// 捕获分组列头右键，转发给外部（显示列菜单）
        /// </summary>
        private void HookGroupedHeaderContextMenu()
        {
            if (_groupedHeaderContextHooked || GroupedHeaderListView == null) return;
            GroupedHeaderListView.AddHandler(UIElement.PreviewMouseRightButtonUpEvent,
                new MouseButtonEventHandler(GroupedHeader_PreviewMouseRightButtonUp), true);
            _groupedHeaderContextHooked = true;
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
        public void ApplyColumnVisibility(string tag, bool visible)
        {
            if (string.IsNullOrEmpty(tag)) return;

            // 更新分组统一列头
            if (GroupedHeaderListView?.View is GridView headerGrid)
            {
                for (int i = 0; i < headerGrid.Columns.Count; i++)
                {
                    if (headerGrid.Columns[i].Header is GridViewColumnHeader gh && gh.Tag?.ToString() == tag)
                    {
                        if (visible)
                        {
                            var width = GetReferenceWidth(tag);
                            headerGrid.Columns[i].Width = width;
                        }
                        else
                        {
                            headerGrid.Columns[i].Width = 0;
                        }
                        break;
                    }
                }
            }

            // 同步到分组数据列（各ListView共享同序列）
            foreach (var lv in _groupedListViews)
            {
                if (lv?.View is GridView gv && gv.Columns.Count >= 7)
                {
                    int idx = GetColumnIndexByTag(tag);
                    if (idx >= 0 && idx < gv.Columns.Count)
                    {
                        if (visible)
                        {
                            gv.Columns[idx].Width = GetReferenceWidth(tag);
                        }
                        else
                        {
                            gv.Columns[idx].Width = 0;
                        }
                    }
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
                "Tags" => 5,
                "Notes" => 6,
                _ => -1
            };
        }

        /// <summary>
        /// 将主列表的列宽同步到分组统一列头
        /// </summary>
        private void SyncGroupedHeaderWidthsFromMainGrid()
        {
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
        }

        /// <summary>
        /// 监听分组列头的宽度变化，并同步回主列表列宽（便于保存）
        /// </summary>
        private void SubscribeGroupedHeaderWidthChanges()
        {
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
        }

        /// <summary>
        /// 绑定分组列头分隔线双击，自适应列宽
        /// </summary>
        private void HookGroupedHeaderThumbs()
        {
            if (_groupedThumbHooked || GroupedHeaderListView == null) return;
            GroupedHeaderListView.AddHandler(UIElement.PreviewMouseLeftButtonDownEvent,
                new MouseButtonEventHandler(GroupedHeaderThumbDoubleClick), true);
            _groupedThumbHooked = true;
        }

        private void GroupedHeaderThumbDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount != 2) return;
            var src = e.OriginalSource as DependencyObject;
            if (src == null) return;
            var header = FindAncestor<GridViewColumnHeader>(src);
            var thumb = FindAncestor<Thumb>(src);
            if (header != null && thumb != null && header.Column != null)
            {
                AutoSizeGroupedColumn(header.Column);
                e.Handled = true;
            }
        }

        private void AutoSizeGroupedColumn(GridViewColumn column)
        {
            if (column == null || GroupedHeaderListView == null) return;

            double padding = 24;
            double maxWidth = 0;

            var header = column.Header as GridViewColumnHeader;
            var headerText = header?.Content?.ToString() ?? "";
            maxWidth = Math.Max(maxWidth, MeasureTextWidth(headerText, GroupedHeaderListView) + padding);

            foreach (var lv in _groupedListViews)
            {
                if (lv == null) continue;
                foreach (var item in lv.Items)
                {
                    string cellText = GetCellTextForColumn(item, column, header);
                    maxWidth = Math.Max(maxWidth, MeasureTextWidth(cellText, lv) + padding);
                }
            }

            if (maxWidth < 50) maxWidth = 50;
            column.Width = Math.Ceiling(maxWidth);
        }
        
        private void SwitchToNormalView()
        {
            _isGroupedMode = false;
            _groupedSelectedItems.Clear();
            _groupedListViews.Clear();
            
            if (FilesListView != null)
                FilesListView.Visibility = Visibility.Visible;
            
            if (GroupedResultsViewer != null)
                GroupedResultsViewer.Visibility = Visibility.Collapsed;
        }
        
        /// <summary>
        /// 分组标题点击事件（折叠/展开）
        /// </summary>
        private void GroupHeader_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is Border border && border.DataContext is SearchResultGroupViewModel group)
            {
                group.IsExpanded = !group.IsExpanded;
                e.Handled = true;
            }
        }
        
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
                    SwitchToNormalView();
                }
                
                if (FilesListView != null)
                {
                    FilesListView.ItemsSource = value;
                }
            }
        }

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
            var tb = new TextBlock
            {
                Text = text ?? "",
                FontSize = listView?.FontSize ?? 12,
                FontFamily = listView?.FontFamily
            };
            tb.Measure(new System.Windows.Size(double.PositiveInfinity, double.PositiveInfinity));
            return tb.DesiredSize.Width;
        }
    }
}

























