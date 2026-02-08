using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using YiboFile.Dialogs;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using YiboFile.Controls.Converters;
using YiboFile.Models;
using YiboFile.Services.Favorite;
using YiboFile.Services.Search;
using YiboFile.Services.UI;
using YiboFile; // For Library class
using YiboFile.ViewModels.Messaging.Messages;
using YiboFile.Services.Navigation;

namespace YiboFile.Controls
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
        public event EventHandler<string> ViewModeChanged;
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
                FileList.SelectionChanged += (s, e) =>
                {
                    if (DataContext is ViewModels.PaneViewModel vm)
                    {
                        vm.UpdateSelection(FileList.SelectedItems);
                    }
                    FilesSelectionChanged?.Invoke(s, e);
                };
                FileList.MouseDoubleClick += (s, e) => FilesMouseDoubleClick?.Invoke(s, e);
                FileList.PreviewMouseDoubleClick += (s, e) =>
                {
                    FilesPreviewMouseDoubleClick?.Invoke(s, e);
                    // Check for blank area double click
                    var hit = FileList.InputHitTest(e.GetPosition(FileList));
                    bool isItem = false;
                    var current = hit as DependencyObject;
                    while (current != null)
                    {
                        if (current is ListViewItem) { isItem = true; break; }
                        if (current == FileList) break;
                        current = VisualTreeHelper.GetParent(current);
                    }

                    if (!isItem)
                    {
                        FilesPreviewMouseDoubleClickForBlank?.Invoke(s, e);
                    }
                };
                FileList.PreviewKeyDown += (s, e) => FilesPreviewKeyDown?.Invoke(s, e);
                FileList.PreviewMouseLeftButtonDown += (s, e) => FilesPreviewMouseLeftButtonDown?.Invoke(s, e);
                FileList.MouseLeftButtonUp += (s, e) => FilesMouseLeftButtonUp?.Invoke(s, e);
                FileList.PreviewMouseDown += (s, e) => OnFilesPreviewMouseDown(s, e);
                FileList.PreviewMouseMove += (s, e) => FilesPreviewMouseMove?.Invoke(s, e);
                FileList.SizeChanged += (s, e) => FilesSizeChanged?.Invoke(s, e);
                FileList.GridViewColumnHeaderClick += (s, e) => GridViewColumnHeaderClick?.Invoke(s, e);

                // 订阅列标题点击事件（用于记录默认列宽）
                if (FileList.FilesGrid != null)
                {
                    foreach (GridViewColumn column in FileList.FilesGrid.Columns)
                    {
                        if (!_columnDefaultWidths.ContainsKey(column))
                            _columnDefaultWidths[column] = column.Width;
                    }

                    if (FileList.FilesList != null)
                    {
                        if (FileList.FilesList.IsLoaded)
                        {
                            HookColumnHeaders();
                        }
                        else
                        {
                            FileList.FilesList.Loaded += (s, e) => HookColumnHeaders();
                        }
                    }
                }

                FileList.TagClicked += (s, e) => TagClicked?.Invoke(s, e);
            }

            this.PreviewMouseDown += OnPreviewMouseDown;

            if (FilterPanelControl != null)
            {
                FilterPanelControl.FilterChanged += (s, e) =>
                {
                    if (DataContext is ViewModels.PaneViewModel vm)
                    {
                        vm.ApplyFilterCommand?.Execute(null);
                    }
                };
            }

            this.Loaded += FileBrowserControl_Loaded;
            this.DataContextChanged += OnDataContextChanged;
        }

        private void OnPreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (AddressBarControl != null && AddressBarControl.IsEditMode)
            {
                var source = e.OriginalSource as DependencyObject;
                bool isInAddressBar = false;
                var current = source;
                while (current != null)
                {
                    if (current == AddressBarControl) { isInAddressBar = true; break; }
                    current = VisualTreeHelper.GetParent(current);
                }
                if (!isInAddressBar) AddressBarControl.SwitchToBreadcrumbMode();
            }
            RequestActivation();
        }

        private void OnFilesPreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (AddressBarControl != null && AddressBarControl.IsAddressTextBoxFocused)
            {
                AddressBarControl.SwitchToBreadcrumbMode();
            }
            FilesPreviewMouseDown?.Invoke(sender, e);
            RequestActivation();
        }

        private void RequestActivation()
        {
            if (DataContext is ViewModels.PaneViewModel vm)
            {
                vm.RequestActivation();
            }
        }

        private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (e.NewValue is ViewModels.PaneViewModel newVm)
            {
                newVm.MessageBus.Subscribe<SelectAllRequestMessage>(msg =>
                {
                    bool isMain = msg.TargetPane == PaneId.Main;
                    bool isThisMain = !(newVm.IsSecondary);
                    if (isMain == isThisMain) FileList?.FilesList?.SelectAll();
                });
            }
        }

        private void FileBrowserControl_Loaded(object sender, RoutedEventArgs e)
        {
            if (FileList?.FilesList != null && FileList.FilesList.ContextMenu == null)
            {
                try
                {
                    FileList.FilesList.ContextMenu = (ContextMenu)FindResource("FileListContextMenu");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Failed to load ContextMenu: {ex.Message}");
                }
            }
        }

        // 公共属性
        public AddressBarControl AddressBar => AddressBarControl;
        public ListView FilesList => FileList?.FilesList;
        public GridView FilesGrid => FileList?.FilesGrid;
        public Border FocusBorderControl => FocusBorder;
        public StackPanel FileInfoPanelControl => FileInfoPanel;
        public TextBlock EmptyStateTextControl => FileList?.EmptyStateTextControl;
        public TitleActionBar ActionBar => TitleActionBar;

        public FileListControl GetFileListControl() => FileList;

        public string AddressText
        {
            get => AddressBarControl?.AddressText ?? "";
            set { if (AddressBarControl != null) AddressBarControl.AddressText = value; }
        }

        public bool TabsVisible
        {
            get => false; // Or implement logic if needed
            set { /* No individual tab manager anymore in FileBrowserControl */ }
        }

        public bool NavUpEnabled
        {
            get => NavUpBtn?.IsEnabled ?? false;
            set { if (NavUpBtn != null) NavUpBtn.IsEnabled = value; }
        }

        public bool NavBackEnabled
        {
            get => NavBackBtn?.IsEnabled ?? false;
            set { if (NavBackBtn != null) NavBackBtn.IsEnabled = value; }
        }

        public bool NavForwardEnabled
        {
            get => NavForwardBtn?.IsEnabled ?? false;
            set { if (NavForwardBtn != null) NavForwardBtn.IsEnabled = value; }
        }

        public void SetPropertiesButtonVisibility(Visibility visibility)
        {
            if (PropertiesBtn != null) PropertiesBtn.Visibility = visibility;
        }

        public bool LoadMoreVisible
        {
            get => FileList?.IsLoadMoreVisible ?? false;
            set { if (FileList != null) FileList.IsLoadMoreVisible = value; }
        }

        public event RoutedEventHandler LoadMoreClicked;

        protected virtual void OnLoadMoreClicked()
        {
            LoadMoreClicked?.Invoke(this, new RoutedEventArgs());
        }

        public bool IsAddressReadOnly
        {
            get => AddressBarControl?.IsReadOnly ?? false;
            set { if (AddressBarControl != null) AddressBarControl.IsReadOnly = value; }
        }

        // 辅助方法
        public void UpdateBreadcrumb(string path) => AddressBarControl?.UpdateBreadcrumb(path);
        public void UpdateBreadcrumbText(string text) => AddressBarControl?.UpdateBreadcrumbText(text);
        public void SetBreadcrumbCustomText(string text) => AddressBarControl?.SetBreadcrumbCustomText(text);
        public void SetTagBreadcrumb(string tagName) => AddressBarControl?.SetTagBreadcrumb(tagName);
        public void SetSearchBreadcrumb(string keyword) => AddressBarControl?.SetSearchBreadcrumb(keyword);
        public void SetLibraryBreadcrumb(string libraryName) => AddressBarControl?.SetLibraryBreadcrumb(libraryName);

        public void SetSearchStatus(bool isVisible, string text = null)
        {
            if (SearchStatusBar == null) return;
            SearchStatusBar.Visibility = isVisible ? Visibility.Visible : Visibility.Collapsed;
            if (isVisible && !string.IsNullOrEmpty(text) && SearchStatusText != null) SearchStatusText.Text = text;
        }

        public void SetGroupedSearchResults(Dictionary<SearchResultType, List<FileSystemItem>> groupedItems)
        {
            FileList?.SetGroupedSearchResults(groupedItems);
        }

        public object FilesSelectedItem
        {
            get => FileList?.SelectedItem;
            set { if (FileList?.FilesList != null) FileList.FilesList.SelectedItem = value; }
        }

        public System.Collections.IList FilesSelectedItems => FileList?.SelectedItems;

        public bool UndoEnabled
        {
            get => UndoBtn?.IsEnabled ?? false;
            set { if (UndoBtn != null) UndoBtn.IsEnabled = value; }
        }

        public bool RedoEnabled
        {
            get => RedoBtn?.IsEnabled ?? false;
            set { if (RedoBtn != null) RedoBtn.IsEnabled = value; }
        }

        public string UndoToolTipText
        {
            get => UndoBtn?.ToolTip as string;
            set { if (UndoBtn != null) UndoBtn.ToolTip = value; }
        }

        public string RedoToolTipText
        {
            get => RedoBtn?.ToolTip as string;
            set { if (RedoBtn != null) RedoBtn.ToolTip = value; }
        }

        public void ShowEmptyState(string message = "暂无文件") => FileList?.ShowEmptyState(message);
        public void HideEmptyState() => FileList?.HideEmptyState();

        // 事件转发
        public event EventHandler<string> PathChanged;
        public event EventHandler<string> BreadcrumbClicked;
        public event EventHandler<string> BreadcrumbMiddleClicked;
        public event EventHandler<TagViewModel> TagClicked;

        private void AddressBarControl_PathChanged(object sender, string path) => PathChanged?.Invoke(this, path);
        private void AddressBarControl_BreadcrumbClicked(object sender, string path) => BreadcrumbClicked?.Invoke(this, path);
        private void AddressBarControl_BreadcrumbMiddleClicked(object sender, string path) => BreadcrumbMiddleClicked?.Invoke(this, path);

        private void GridSplitter_DragCompleted(object sender, System.Windows.Controls.Primitives.DragCompletedEventArgs e)
        {
            if (this.Content is Grid rootGrid && rootGrid.RowDefinitions.Count > 4)
            {
                var height = rootGrid.RowDefinitions[4].Height.Value;
                InfoHeightChanged?.Invoke(this, height);
            }
        }
        public event EventHandler<double> InfoHeightChanged;

        private void ViewModeBtn_DropDown(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.ContextMenu != null)
            {
                btn.ContextMenu.PlacementTarget = btn;
                btn.ContextMenu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
                btn.ContextMenu.IsOpen = true;
            }
        }

        private void HookColumnHeaders()
        {
            if (FileList?.FilesList == null) return;
            var headers = FindVisualChildren<GridViewColumnHeader>(FileList.FilesList);
            foreach (var header in headers)
            {
                header.Click -= Header_Click;
                header.Click += Header_Click;
                FixHeaderTag(header);
            }
        }

        private void Header_Click(object sender, RoutedEventArgs e)
        {
            if (sender is GridViewColumnHeader header)
            {
                FixHeaderTag(header);
                GridViewColumnHeaderClick?.Invoke(header, e);
            }
        }

        private void FixHeaderTag(GridViewColumnHeader header)
        {
            if (header.Tag == null && header.Content is FrameworkElement content && content.Tag != null)
                header.Tag = content.Tag;
        }

        private IEnumerable<T> FindVisualChildren<T>(DependencyObject depObj) where T : DependencyObject
        {
            if (depObj == null) yield break;
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(depObj); i++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(depObj, i);
                if (child != null && child is T t) yield return t;
                foreach (T childOfChild in FindVisualChildren<T>(child)) yield return childOfChild;
            }
        }

        [Obsolete("This field is deprecated and will be removed after MVVM migration is complete")]
        private object _filterChangedStub;
    }
}
