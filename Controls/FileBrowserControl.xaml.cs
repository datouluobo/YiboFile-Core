using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using OoiMRR.Controls.Converters;
using OoiMRR.Services.Search;

namespace OoiMRR.Controls
{
    /// <summary>
    /// FileBrowserControl.xaml 的交互逻辑
    /// 统一的文件浏览控件，支持路径、库、标签三种模式
    /// </summary>
    public partial class FileBrowserControl : UserControl
    {
        private readonly Dictionary<GridViewColumn, double> _columnDefaultWidths = new Dictionary<GridViewColumn, double>();
        
        // 事件定义（保持向后兼容）
        public event SelectionChangedEventHandler FilesSelectionChanged;
        public event MouseButtonEventHandler FilesMouseDoubleClick;
        public event MouseButtonEventHandler FilesPreviewMouseDoubleClick;
        public event KeyEventHandler FilesPreviewKeyDown;
        public event MouseButtonEventHandler FilesPreviewMouseLeftButtonDown;
        public event MouseButtonEventHandler FilesMouseLeftButtonUp;
        public event MouseButtonEventHandler FilesPreviewMouseDown;
        public event RoutedEventHandler GridViewColumnHeaderClick;
        public event SizeChangedEventHandler FilesSizeChanged;
#pragma warning disable CS0067 // Event is never used (used in XAML)
        public event MouseButtonEventHandler FilesPreviewMouseDoubleClickForBlank;
#pragma warning restore CS0067

        public FileBrowserControl()
        {
            InitializeComponent();
            
            // 订阅地址栏控件的事件
            if (AddressBarControl != null)
            {
                AddressBarControl.PathChanged += AddressBarControl_PathChanged;
                AddressBarControl.BreadcrumbClicked += AddressBarControl_BreadcrumbClicked;
                AddressBarControl.BreadcrumbMiddleClicked += AddressBarControl_BreadcrumbMiddleClicked;
            }
            
            // 订阅文件列表控件的事件（转发到外部事件）
            if (FileList != null)
            {
                FileList.SelectionChanged += (s, e) => FilesSelectionChanged?.Invoke(s, e);
                FileList.MouseDoubleClick += (s, e) => FilesMouseDoubleClick?.Invoke(s, e);
                FileList.PreviewMouseDoubleClick += (s, e) => FilesPreviewMouseDoubleClick?.Invoke(s, e);
                FileList.PreviewKeyDown += (s, e) => FilesPreviewKeyDown?.Invoke(s, e);
                FileList.PreviewMouseLeftButtonDown += (s, e) => FilesPreviewMouseLeftButtonDown?.Invoke(s, e);
                FileList.MouseLeftButtonUp += (s, e) => FilesMouseLeftButtonUp?.Invoke(s, e);
                FileList.PreviewMouseDown += (s, e) => FilesPreviewMouseDown?.Invoke(s, e);
                FileList.SizeChanged += (s, e) => FilesSizeChanged?.Invoke(s, e);
                FileList.GridViewColumnHeaderClick += (s, e) => GridViewColumnHeaderClick?.Invoke(s, e);
                FileList.LoadMoreClick += (s, e) => LoadMoreBtn_Click(s, e);
                
                // 订阅列标题点击事件（用于记录默认列宽）
                if (FileList.FilesGrid != null)
                {
                    foreach (GridViewColumn column in FileList.FilesGrid.Columns)
                    {
                        if (column.Header is GridViewColumnHeader header)
                        {
                            // 记录默认列宽
                            if (!_columnDefaultWidths.ContainsKey(column))
                                _columnDefaultWidths[column] = column.Width;
                        }
                    }
                    // 右键菜单：与预览窗口一致的列显示/隐藏
                    SetupFileContextMenu();
                }
            }
        }
        

        // 公共属性（保持向后兼容）
        public AddressBarControl AddressBar => AddressBarControl;
        public ListView FilesList => FileList?.FilesList;
        public GridView FilesGrid => FileList?.FilesGrid;
        public TabManagerControl TabManagerControl => TabManager;
        public StackPanel TabsPanelControl => TabManager?.TabsPanelControl; // 返回TabManagerControl内部的TabsPanel
        public Border TabsBorderControl => TabManager?.TabsBorderControl; // 返回TabManagerControl内部的TabsBorder
        public StackPanel FileInfoPanelControl => FileInfoPanel;
        public TextBlock EmptyStateTextControl => FileList?.EmptyStateTextControl;

        // 地址栏相关方法
        public string AddressText
        {
            get => AddressBarControl?.AddressText ?? "";
            set
            {
                if (AddressBarControl != null)
                    AddressBarControl.AddressText = value;
            }
        }

        public bool IsAddressReadOnly
        {
            get => AddressBarControl?.IsReadOnly ?? false;
            set
            {
                if (AddressBarControl != null)
                    AddressBarControl.IsReadOnly = value;
            }
        }

        public void UpdateBreadcrumb(string path)
        {
            AddressBarControl?.UpdateBreadcrumb(path);
        }

        public void UpdateBreadcrumbText(string text)
        {
            AddressBarControl?.UpdateBreadcrumbText(text);
        }

        public void SetBreadcrumbCustomText(string text)
        {
            AddressBarControl?.SetBreadcrumbCustomText(text);
        }

        public void SetTagBreadcrumb(string tagName)
        {
            AddressBarControl?.SetTagBreadcrumb(tagName);
        }

        public void SetSearchBreadcrumb(string keyword)
        {
            AddressBarControl?.SetSearchBreadcrumb(keyword);
        }

        public void SetLibraryBreadcrumb(string libraryName)
        {
            AddressBarControl?.SetLibraryBreadcrumb(libraryName);
        }

        // 文件列表相关方法
        public System.Collections.IEnumerable FilesItemsSource
        {
            get => FileList?.ItemsSource;
            set
            {
                if (FileList != null)
                {
                    FileList.ItemsSource = value;
                }
            }
        }
        
        /// <summary>
        /// 设置分组搜索结果
        /// </summary>
        public void SetGroupedSearchResults(Dictionary<SearchResultType, List<FileSystemItem>> groupedItems)
        {
            FileList?.SetGroupedSearchResults(groupedItems);
        }

        public object FilesSelectedItem
        {
            get => FileList?.SelectedItem;
            set
            {
                if (FileList?.FilesList != null)
                    FileList.FilesList.SelectedItem = value;
            }
        }

        public System.Collections.IList FilesSelectedItems
        {
            get => FileList?.SelectedItems;
        }

        // 标签页相关方法（委托给TabManagerControl）
        public bool TabsVisible
        {
            get => TabManager?.IsVisible ?? false;
            set
            {
                if (TabManager != null)
                    TabManager.IsVisible = value;
            }
        }

        // 空状态提示
        public void ShowEmptyState(string message = "暂无文件")
        {
            FileList?.ShowEmptyState(message);
        }

        public void HideEmptyState()
        {
            FileList?.HideEmptyState();
        }

        // 地址栏事件（转发给外部）
        public event EventHandler<string> PathChanged;
        public event EventHandler<string> BreadcrumbClicked;
        public event EventHandler<string> BreadcrumbMiddleClicked;
        public event RoutedEventHandler NavigationBack;
        public event RoutedEventHandler NavigationForward;
        public event RoutedEventHandler NavigationUp;
        public event RoutedEventHandler SearchClicked;
        public event RoutedEventHandler FilterClicked;
        public event RoutedEventHandler LoadMoreClicked;
        
        // 文件操作事件
        public event RoutedEventHandler FileCopy;
        public event RoutedEventHandler FileCut;
        public event RoutedEventHandler FilePaste;
        public event RoutedEventHandler FileDelete;
        public event RoutedEventHandler FileRename;
        public event RoutedEventHandler FileRefresh;
        public event RoutedEventHandler FileProperties;

        // 地址栏事件处理
        private void AddressBarControl_PathChanged(object sender, string path)
        {
            PathChanged?.Invoke(this, path);
        }

        private void AddressBarControl_BreadcrumbClicked(object sender, string path)
        {
            BreadcrumbClicked?.Invoke(this, path);
        }

        private void AddressBarControl_BreadcrumbMiddleClicked(object sender, string path)
        {
            // 中键打开新标签，触发专门的事件
            BreadcrumbMiddleClicked?.Invoke(this, path);
        }
        
        private void NavBackBtn_Click(object sender, RoutedEventArgs e)
        {
            NavigationBack?.Invoke(sender, e);
        }

        private void NavForwardBtn_Click(object sender, RoutedEventArgs e)
        {
            NavigationForward?.Invoke(sender, e);
        }

        private void NavUpBtn_Click(object sender, RoutedEventArgs e)
        {
            NavigationUp?.Invoke(sender, e);
        }

        private void SearchBtn_Click(object sender, RoutedEventArgs e)
        {
            SearchClicked?.Invoke(sender, e);
        }

        private void FilterBtn_Click(object sender, RoutedEventArgs e)
        {
            FilterClicked?.Invoke(sender, e);
        }

        private void LoadMoreBtn_Click(object sender, RoutedEventArgs e)
        {
            LoadMoreClicked?.Invoke(sender, e);
        }

        public bool LoadMoreVisible
        {
            get => FileList?.LoadMoreVisible ?? false;
            set
            {
                if (FileList != null)
                    FileList.LoadMoreVisible = value;
            }
        }

        public bool NavUpEnabled
        {
            get => NavUpBtn?.IsEnabled ?? false;
            set
            {
                if (NavUpBtn != null)
                    NavUpBtn.IsEnabled = value;
            }
        }

        public bool NavBackEnabled
        {
            get => NavBackBtn?.IsEnabled ?? false;
            set
            {
                if (NavBackBtn != null)
                    NavBackBtn.IsEnabled = value;
            }
        }

        public bool NavForwardEnabled
        {
            get => NavForwardBtn?.IsEnabled ?? false;
            set
            {
                if (NavForwardBtn != null)
                    NavForwardBtn.IsEnabled = value;
            }
        }

        public void EnableAutoLoadMore()
        {
            try
            {
                var sv = GetScrollViewer(FileList?.FilesList);
                if (sv != null)
                {
                    sv.ScrollChanged -= Sv_ScrollChanged;
                    sv.ScrollChanged += Sv_ScrollChanged;
                }
            }
            catch { }
        }

        private void Sv_ScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            try
            {
                var sv = sender as ScrollViewer;
                if (sv == null) return;
                if (e.VerticalOffset + e.ViewportHeight >= e.ExtentHeight - 20)
                {
                    LoadMoreClicked?.Invoke(this, new RoutedEventArgs());
                }
            }
            catch { }
        }

        private ScrollViewer GetScrollViewer(DependencyObject root)
        {
            if (root == null) return null;
            if (root is ScrollViewer sv) return sv;
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
            {
                var child = VisualTreeHelper.GetChild(root, i);
                var result = GetScrollViewer(child);
                if (result != null) return result;
            }
            return null;
        }

        private void SetupFileContextMenu()
        {
            if (FileList?.FilesList == null) return;
            
            var cm = new ContextMenu();
            
            // 复制
            var copyItem = new MenuItem { Header = "复制", Name = "CopyItem" };
            copyItem.Click += (s, e) => FileCopy?.Invoke(s, e);
            cm.Items.Add(copyItem);
            
            // 剪切
            var cutItem = new MenuItem { Header = "剪切", Name = "CutItem" };
            cutItem.Click += (s, e) => FileCut?.Invoke(s, e);
            cm.Items.Add(cutItem);
            
            // 粘贴
            var pasteItem = new MenuItem { Header = "粘贴", Name = "PasteItem" };
            pasteItem.Click += (s, e) => FilePaste?.Invoke(s, e);
            cm.Items.Add(pasteItem);
            
            var separator1 = new Separator { Name = "Separator1" };
            cm.Items.Add(separator1);
            
            // 删除
            var deleteItem = new MenuItem { Header = "删除", Name = "DeleteItem" };
            deleteItem.Click += (s, e) => FileDelete?.Invoke(s, e);
            cm.Items.Add(deleteItem);
            
            // 重命名
            var renameItem = new MenuItem { Header = "重命名", Name = "RenameItem" };
            renameItem.Click += (s, e) => FileRename?.Invoke(s, e);
            cm.Items.Add(renameItem);
            
            var separator2 = new Separator { Name = "Separator2" };
            cm.Items.Add(separator2);
            
            // 刷新
            var refreshItem = new MenuItem { Header = "刷新", Name = "RefreshItem" };
            refreshItem.Click += (s, e) => FileRefresh?.Invoke(s, e);
            cm.Items.Add(refreshItem);
            
            // 属性
            var propertiesItem = new MenuItem { Header = "属性", Name = "PropertiesItem" };
            propertiesItem.Click += (s, e) => FileProperties?.Invoke(s, e);
            cm.Items.Add(propertiesItem);
            
            // 在菜单打开时动态更新菜单项可见性
            cm.Opened += (s, e) =>
            {
                bool hasSelection = FileList?.SelectedItems != null && FileList.SelectedItems.Count > 0;
                
                // 需要选中项的操作
                copyItem.Visibility = hasSelection ? Visibility.Visible : Visibility.Collapsed;
                cutItem.Visibility = hasSelection ? Visibility.Visible : Visibility.Collapsed;
                deleteItem.Visibility = hasSelection ? Visibility.Visible : Visibility.Collapsed;
                renameItem.Visibility = hasSelection && FileList.SelectedItems.Count == 1 ? Visibility.Visible : Visibility.Collapsed;
                propertiesItem.Visibility = hasSelection && FileList.SelectedItems.Count == 1 ? Visibility.Visible : Visibility.Collapsed;
                
                // 始终可用的操作
                pasteItem.Visibility = Visibility.Visible;
                refreshItem.Visibility = Visibility.Visible;
                
                // 更新分隔符可见性
                separator1.Visibility = (hasSelection || pasteItem.Visibility == Visibility.Visible) ? Visibility.Visible : Visibility.Collapsed;
                separator2.Visibility = (hasSelection || refreshItem.Visibility == Visibility.Visible || propertiesItem.Visibility == Visibility.Visible) ? Visibility.Visible : Visibility.Collapsed;
            };
            
            FileList.FilesList.ContextMenu = cm;
        }
    }
}
