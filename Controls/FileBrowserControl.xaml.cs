using System;
using System.Collections.Generic;
using System.Linq;
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
        public event MouseEventHandler FilesPreviewMouseMove;
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
                FileList.PreviewMouseDoubleClick += (s, e) =>
                {
                    FilesPreviewMouseDoubleClick?.Invoke(s, e);
                    // Check for blank area double click
                    if (!IsMouseOverItem(s as ListView, e.GetPosition(s as IInputElement)))
                    {
                        FilesPreviewMouseDoubleClickForBlank?.Invoke(s, e);
                    }
                };
                FileList.PreviewKeyDown += (s, e) => FilesPreviewKeyDown?.Invoke(s, e);
                FileList.PreviewMouseLeftButtonDown += (s, e) => FilesPreviewMouseLeftButtonDown?.Invoke(s, e);
                FileList.MouseLeftButtonUp += (s, e) => FilesMouseLeftButtonUp?.Invoke(s, e);
                FileList.PreviewMouseDown += (s, e) => FilesPreviewMouseDown?.Invoke(s, e);
                FileList.PreviewMouseMove += (s, e) => FilesPreviewMouseMove?.Invoke(s, e);
                FileList.SizeChanged += (s, e) => FilesSizeChanged?.Invoke(s, e);
                FileList.GridViewColumnHeaderClick += (s, e) => GridViewColumnHeaderClick?.Invoke(s, e);
                FileList.LoadMoreClick += (s, e) => LoadMoreBtn_Click(s, e);
                FileList.CommitRename += (s, e) =>
                {
                    CommitRename?.Invoke(s, e);
                };

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

            // 订阅标题操作栏事件
            if (TitleActionBar != null)
            {
                TitleActionBar.NewFolderClicked += (s, e) => FileNewFolder?.Invoke(s, e);
                TitleActionBar.NewFileClicked += (s, e) => FileNewFile?.Invoke(s, e);
                TitleActionBar.CopyClicked += (s, e) => FileCopy?.Invoke(s, e);
                TitleActionBar.PasteClicked += (s, e) => FilePaste?.Invoke(s, e);
                TitleActionBar.DeleteClicked += (s, e) => FileDelete?.Invoke(s, e);
                TitleActionBar.RefreshClicked += (s, e) => FileRefresh?.Invoke(s, e);
                TitleActionBar.NewTagClicked += (s, e) => FileAddTag?.Invoke(s, e);
                // TitleActionBar.ManageTagsClicked += ... (Pending implementation if needed)
                // TitleActionBar.BatchAddTagsClicked += ...
            }
        }


        // 公共属性（保持向后兼容）
        public AddressBarControl AddressBar => AddressBarControl;
        public ListView FilesList => FileList?.FilesList;
        public GridView FilesGrid => FileList?.FilesGrid;

        // TabManager moved to MainWindow
        // public TabManagerControl TabManagerControl => TabManager;
        // public StackPanel TabsPanelControl => TabManager?.TabsPanelControl;
        // public Border TabsBorderControl => TabManager?.TabsBorderControl;

        public Border FocusBorderControl => FocusBorder; // 焦点边框
        public StackPanel FileInfoPanelControl => FileInfoPanel;
        public TextBlock EmptyStateTextControl => FileList?.EmptyStateTextControl;

        // TitleActionBar 现在在 FileBrowserControl 中，提供公共访问
        public TitleActionBar ActionBar => TitleActionBar;


        /// <summary>
        /// 获取 FileListControl 实例（供设置面板调用）
        /// </summary>
        public FileListControl GetFileListControl() => FileList;

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
            get => FileList?.FilesItemsSource;
            set
            {
                if (FileList != null)
                {
                    // 使用FilesItemsSource属性，它会自动调用Items.Refresh()
                    FileList.FilesItemsSource = value;
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

        // 标签页相关方法（已移动到MainWindow）
        public bool TabsVisible
        {
            get => false; // TabManager?.IsVisible ?? false;
            set
            {
                // Move logic to MainWindow if needed
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
        public event RoutedEventHandler FileAddTag;
        public event RoutedEventHandler FileNewFolder;
        public event RoutedEventHandler FileNewFile;
        public event EventHandler<RenameEventArgs> CommitRename;

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

        /// <summary>
        /// 搜索模式变更事件
        /// </summary>
        public event EventHandler<string> SearchModeChanged;

        /// <summary>
        /// 获取当前选中的搜索模式
        /// </summary>
        public string SelectedSearchMode
        {
            get
            {
                if (SearchModeFileName?.IsChecked == true) return "FileName";
                if (SearchModeTags?.IsChecked == true) return "Tags";
                if (SearchModeNotes?.IsChecked == true) return "Notes";
                return "FileName";
            }
        }

        private void SearchMode_Checked(object sender, RoutedEventArgs e)
        {
            var mode = SelectedSearchMode;
            SearchModeChanged?.Invoke(this, mode);
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

            // 添加标签 (仅当TagTrain可用时显示)
            if (App.IsTagTrainAvailable)
            {
                var addTagItem = new MenuItem { Header = "添加标签", Name = "AddTagItem" };
                addTagItem.Click += (s, e) => FileAddTag?.Invoke(s, e);
                cm.Items.Add(addTagItem);
            }

            var separator3 = new Separator { Name = "Separator3" };
            cm.Items.Add(separator3);

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

                // 添加标签项可见性
                if (App.IsTagTrainAvailable)
                {
                    var addTagItem = cm.Items.Cast<object>()
                        .OfType<MenuItem>()
                        .FirstOrDefault(m => m.Name == "AddTagItem");
                    if (addTagItem != null)
                    {
                        addTagItem.Visibility = hasSelection ? Visibility.Visible : Visibility.Collapsed;
                    }
                }

                // 始终可用的操作
                pasteItem.Visibility = Visibility.Visible;
                refreshItem.Visibility = Visibility.Visible;

                // 更新分隔符可见性
                separator1.Visibility = (hasSelection || pasteItem.Visibility == Visibility.Visible) ? Visibility.Visible : Visibility.Collapsed;
                separator2.Visibility = (hasSelection || refreshItem.Visibility == Visibility.Visible || propertiesItem.Visibility == Visibility.Visible) ? Visibility.Visible : Visibility.Collapsed;
            };

            FileList.FilesList.ContextMenu = cm;
        }
        // 视图模式变更事件
        public event EventHandler<string> ViewModeChanged;

        public event EventHandler<double> InfoHeightChanged;

        private void GridSplitter_DragCompleted(object sender, System.Windows.Controls.Primitives.DragCompletedEventArgs e)
        {
            if (this.Content is Grid rootGrid && rootGrid.RowDefinitions.Count > 4)
            {
                var height = rootGrid.RowDefinitions[4].Height.Value;
                InfoHeightChanged?.Invoke(this, height);
            }
        }
        private void ViewModeBtn_Click(object sender, RoutedEventArgs e)
        {
            if (sender is RadioButton btn && btn.Tag is string mode)
            {
                FileList?.SetViewMode(mode);
                ViewModeChanged?.Invoke(this, mode);
            }
        }

        private bool IsMouseOverItem(ListView listView, Point point)
        {
            if (listView == null) return false;
            var hit = VisualTreeHelper.HitTest(listView, point);
            if (hit == null) return false;

            var current = hit.VisualHit;
            while (current != null && current != listView)
            {
                if (current is ListViewItem) return true;
                current = VisualTreeHelper.GetParent(current);
            }
            return false;
        }
    }
}
