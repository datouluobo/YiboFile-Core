using System;
using System.Collections;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
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
        public object SelectedItem => FilesListView?.SelectedItem;
        public System.Collections.IList SelectedItems => FilesListView?.SelectedItems;
        
        // 分组显示相关
        private bool _isGroupedMode = false;
        private ObservableCollection<SearchResultGroupViewModel> _groupedResults;
        
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
                }
            }
        }
        
        private void SwitchToNormalView()
        {
            _isGroupedMode = false;
            
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
    }
}



