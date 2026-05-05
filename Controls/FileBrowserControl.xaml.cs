using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Interop;
using YiboFile.Dialogs;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
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
using YiboFile.Services.Config;
using YiboFile.Services.Shell;
using YiboFile.Interop.Shell;
using Microsoft.Extensions.DependencyInjection;

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
        public event EventHandler<FileSystemItem> NotesIconClicked;
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
                FileList.NotesIconClicked += (s, item) => 
                {
                    // 选中该文件并打开笔记弹窗
                    if (FileList != null && item != null)
                    {
                        FileList.FilesList.SelectedItem = item;
                        
                        // 打开笔记编辑窗口
                        var currentNotes = YiboFile.Services.FileNotes.FileNotesService.GetFileNotes(item.Path);
                        var notesWindow = new YiboFile.Windows.NotesEditWindow(item.Path, item.Name, currentNotes);
                        notesWindow.Owner = Window.GetWindow(this);
                        
                        if (notesWindow.ShowDialog() == true && notesWindow.NotesSaved)
                        {
                            // 笔记已保存，刷新一下
                        }
                    }
                    NotesIconClicked?.Invoke(s, item);
                };
            }

            this.PreviewMouseDown += OnPreviewMouseDown;


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

                newVm.MessageBus.Subscribe<ShowGroupedSearchResultsMessage>(msg =>
                {
                    bool isMain = msg.TargetPaneId == "Primary" || msg.TargetPaneId == "Any";
                    bool isThisMain = !(newVm.IsSecondary);
                    if ((isMain && isThisMain) || (!isMain && !isThisMain))
                    {
                        FileList?.SetGroupedSearchResults(msg.GroupedItems);
                    }
                });

                newVm.MessageBus.Subscribe<ShowNewFileMenuMessage>(msg =>
                {
                    bool isMain = msg.Pane == PaneId.Main;
                    bool isThisMain = !(newVm.IsSecondary);
                    if ((isMain && isThisMain) || (!isMain && !isThisMain))
                    {
                        ShowNewFileContextMenu(msg.ParentPath, msg.Pane);
                    }
                });
            }
        }

        private void FileBrowserControl_Loaded(object sender, RoutedEventArgs e)
        {
            if (FileList?.FilesList != null)
            {
                if (FileList.FilesList.IsLoaded)
                {
                    SetupFileListContextMenu();
                }
                else
                {
                    FileList.FilesList.Loaded += (s, args) => SetupFileListContextMenu();
                }
            }
        }

        private void SetupFileListContextMenu()
        {
            if (FileList?.FilesList == null) return;
            
            if (FileList.FilesList.ContextMenu == null)
            {
                try
                {
                    FileList.FilesList.ContextMenu = (ContextMenu)FindResource("FileListContextMenu");
                }
                catch (Exception)
                {
                }
            }

            // Hook ContextMenuOpening to update shell menu items or intercept for native menu
            FileList.FilesList.ContextMenuOpening += FileList_ContextMenuOpening;
        }

        private void FileList_ContextMenuOpening(object sender, ContextMenuEventArgs e)
        {
            // Check if System mode is active
            var configService = App.ServiceProvider?.GetService(typeof(Services.Config.IConfigurationService))
                as Services.Config.IConfigurationService;
            string shellMenuMode = configService?.Config?.ShellMenuMode ?? "Native";

            if (shellMenuMode == "System")
            {
                // Suppress WPF context menu and show native shell menu instead
                e.Handled = true;

                if (DataContext is ViewModels.PaneViewModel vm && vm.Selection?.HasSelection == true)
                {
                    var paths = vm.Selection.SelectedItems.Select(item => item.Path).ToList();
                    if (paths.Count > 0)
                    {
                        var shellService = App.ServiceProvider?.GetService(typeof(IShellContextMenuService))
                            as IShellContextMenuService;
                        if (shellService != null)
                        {
                            shellService.RenameRequested -= OnNativeRenameRequested;
                            shellService.RenameRequested += OnNativeRenameRequested;
                            shellService.ShowNativeMenu(paths, PointToScreen(Mouse.GetPosition(this)),
                                Window.GetWindow(this));
                        }
                    }
                }
                return;
            }

            // Native mode: show WPF custom menu
            var menu = FileList?.FilesList?.ContextMenu;
            if (menu != null)
            {
                menu.DataContext = DataContext;
            }

            if (DataContext is ViewModels.PaneViewModel vm2)
            {
                vm2.Menu?.UpdateDynamicMenuItems();
            }
        }

        private void OnNativeRenameRequested(string filePath)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                if (FileList?.FilesList == null) return;

                foreach (var item in FileList.FilesList.Items)
                {
                    if (item is Models.FileSystemItem fsi && fsi.Path == filePath)
                    {
                        fsi.RenameText = fsi.Name;
                        fsi.IsRenaming = true;
                        break;
                    }
                }
            }));
        }

        private NativeShellMenuHost _pendingShellMenuHost;

        private void WindowsShellMenuItem_SubmenuOpened(object sender, RoutedEventArgs e)
        {
            if (sender is not MenuItem menuItem) return;
            if (!ReferenceEquals(e.OriginalSource, menuItem)) return;

            // Clear previous items and release COM
            menuItem.Items.Clear();
            _pendingShellMenuHost?.CleanupResources();
            _pendingShellMenuHost?.Dispose();
            _pendingShellMenuHost = null;

            if (DataContext is not ViewModels.PaneViewModel vm || vm.Selection?.HasSelection != true)
                return;

            var paths = vm.Selection.SelectedItems.Select(item => item.Path).ToList();
            if (paths.Count == 0) return;

            var window = Window.GetWindow(this);
            IntPtr hwnd = window != null ? new WindowInteropHelper(window).Handle : IntPtr.Zero;

            var host = new NativeShellMenuHost();
            host.RenameRequested += OnNativeRenameRequested;
            _pendingShellMenuHost = host;

            var wpfItems = host.BuildWpfMenuItems(paths, hwnd);

            foreach (var item in wpfItems)
            {
                if (item == null)
                    menuItem.Items.Add(new Separator());
                else
                    menuItem.Items.Add(item);
            }

            // If no items were added, show a disabled placeholder
            if (menuItem.Items.Count == 0)
            {
                menuItem.Items.Add(new MenuItem { Header = "(无可用命令)", IsEnabled = false });
            }
        }

        // 公共属性
        public AddressBarControl AddressBar => AddressBarControl;
        public ListView FilesList => FileList?.FilesList;
        public GridView FilesGrid => FileList?.FilesGrid;
        public StackPanel FileInfoPanelControl => FileInfoPanel;
        public TextBlock EmptyStateTextControl => FileList?.EmptyStateTextControl;
        public TitleActionBar ActionBar => TitleActionBar;

        public FileListControl GetFileListControl() => FileList;

        public Behaviors.AutoColumnWidthBehavior AutoColumnWidthBehavior => FileList?.AutoColumnWidthBehavior;

        public void RequestColumnRecalculation()
        {
            FileList?.RequestColumnRecalculation();
        }

        public string AddressText
        {
            get => AddressBarControl?.AddressText ?? "";
            set { if (AddressBarControl != null) AddressBarControl.AddressText = value; }
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

        private Canvas _globalIndicator;
        private System.Windows.Shapes.Line _globalLine;

        private double _snapTargetHeight = -1;

        private void GridSplitter_DragStarted(object sender, System.Windows.Controls.Primitives.DragStartedEventArgs e)
        {
            var window = Window.GetWindow(this);
            if (window != null)
            {
                _globalIndicator = window.FindName("GlobalSnapIndicator") as Canvas;
                _globalLine = window.FindName("GlobalSnapLine") as System.Windows.Shapes.Line;
            }
            _snapTargetHeight = -1;
        }

        private void GridSplitter_DragDelta(object sender, System.Windows.Controls.Primitives.DragDeltaEventArgs e)
        {
            var window = Window.GetWindow(this);
            if (window == null) return;

            var activeRootGrid = this.FindName("RootGrid") as Grid;
            if (activeRootGrid != null && activeRootGrid.RowDefinitions.Count > 5)
            {
                var row5 = activeRootGrid.RowDefinitions[5];
                
                // Read the height being natively resized by the GridSplitter (ShowsPreview=False)
                double currentHeight = row5.ActualHeight;
                bool snapped = false;
                _snapTargetHeight = -1;

                var otherBrowsers = FindVisualChildren<FileBrowserControl>(window).Where(b => b != this).ToList();
                foreach (var other in otherBrowsers)
                {
                    var otherRootGrid = other.FindName("RootGrid") as Grid;
                    if (otherRootGrid != null && otherRootGrid.RowDefinitions.Count > 5)
                    {
                        double otherHeight = otherRootGrid.RowDefinitions[5].ActualHeight;
                        if (Math.Abs(currentHeight - otherHeight) < 15) // Snap threshold
                        {
                            snapped = true;
                            _snapTargetHeight = otherHeight;

                            if (_globalIndicator != null && _globalLine != null)
                            {
                                var otherSplitter = other.FindName("BottomGridSplitter") as FrameworkElement;
                                var thisSplitter = sender as FrameworkElement;
                                if (otherSplitter != null && thisSplitter != null)
                                {
                                    Point otherP = otherSplitter.TransformToAncestor(window).Transform(new Point(0, otherSplitter.ActualHeight / 2));
                                    Point thisP = thisSplitter.TransformToAncestor(window).Transform(new Point(0, thisSplitter.ActualHeight / 2));
                                    
                                    // Y轴位置对齐到目标的高度
                                    Canvas.SetTop(_globalLine, otherP.Y);
                                    
                                    // 覆盖这俩参与对齐的分割器总宽度，直达远端（不贯穿系统全屏，最优雅）
                                    _globalLine.X1 = Math.Min(thisP.X, otherP.X);
                                    _globalLine.X2 = Math.Max(thisP.X + thisSplitter.ActualWidth, otherP.X + otherSplitter.ActualWidth);

                                    _globalIndicator.Visibility = Visibility.Visible;
                                }
                            }
                            break;
                        }
                    }
                }

                if (!snapped && _globalIndicator != null)
                {
                    _globalIndicator.Visibility = Visibility.Collapsed;
                }
            }
        }

        private void GridSplitter_DragCompleted(object sender, System.Windows.Controls.Primitives.DragCompletedEventArgs e)
        {
            if (_globalIndicator != null)
            {
                _globalIndicator.Visibility = Visibility.Collapsed;
            }

            var activeRootGrid = this.FindName("RootGrid") as Grid;
            if (activeRootGrid != null && activeRootGrid.RowDefinitions.Count > 5)
            {
                var row5 = activeRootGrid.RowDefinitions[5];
                
                if (_snapTargetHeight >= 0)
                {
                    row5.Height = new GridLength(_snapTargetHeight);
                }

                var height = row5.ActualHeight; // Row 5 is the InfoPanel 
                InfoHeightChanged?.Invoke(this, height);
            }
            _snapTargetHeight = -1;
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

        private void ShowNewFileContextMenu(string parentPath, PaneId pane)
        {
            try
            {
                var contextMenu = new ContextMenu
                {
                    Placement = PlacementMode.MousePoint,
                    PlacementTarget = this
                };

                // 常用文件类型列表
                var fileTypes = new (string Header, string Extension)[]
                {
                    ("📄 文本文件 (.txt)", ".txt"),
                    ("📝 Markdown (.md)", ".md"),
                    ("🌐 HTML 网页 (.html)", ".html"),
                    ("⚡ JavaScript (.js)", ".js"),
                    ("🐍 Python (.py)", ".py"),
                    ("📋 JSON (.json)", ".json"),
                    ("📋 XML (.xml)", ".xml"),
                    ("🎨 CSS (.css)", ".css"),
                    ("☕ Java (.java)", ".java"),
                    ("📦 批处理 (.bat)", ".bat"),
                    ("🔧 PowerShell (.ps1)", ".ps1"),
                    ("⚙️ 配置文件 (.ini)", ".ini"),
                    ("🖼️ PNG 图片 (.png)", ".png"),
                    ("🖼️ JPEG 图片 (.jpg)", ".jpg"),
                    ("🖼️ SVG 矢量图 (.svg)", ".svg"),
                    ("📝 Word 文档 (.docx)", ".docx"),
                    ("📊 Excel 表格 (.xlsx)", ".xlsx"),
                    ("📽️ PowerPoint (.pptx)", ".pptx"),
                };

                foreach (var (header, extension) in fileTypes)
                {
                    var ext = extension; // capture for lambda
                    var menuItem = new MenuItem
                    {
                        Header = header,
                        Tag = extension,
                        Padding = new Thickness(10, 5, 10, 5)
                    };
                    menuItem.Click += (s, args) =>
                    {
                        if (DataContext is ViewModels.PaneViewModel vm)
                        {
                            vm.MessageBus.Publish(new CreateFileRequestMessage(parentPath, null, ext, pane));
                        }
                    };
                    contextMenu.Items.Add(menuItem);
                }

                // 分隔符 + 自定义扩展名选项
                contextMenu.Items.Add(new Separator());

                var customMenuItem = new MenuItem
                {
                    Header = "✏️ 自定义扩展名...",
                    Padding = new Thickness(10, 5, 10, 5)
                };
                customMenuItem.Click += (s, args) =>
                {
                    var dialogService = App.ServiceProvider?.GetService<Services.UI.IDialogService>();
                    var inputExtension = dialogService?.ShowInput("请输入文件扩展名（如 .txt）：", ".txt", "新建文件");

                    if (inputExtension != null)
                    {
                        var ext = inputExtension.Trim();
                        if (!ext.StartsWith(".")) ext = "." + ext;
                        if (DataContext is ViewModels.PaneViewModel vm)
                        {
                            vm.MessageBus.Publish(new CreateFileRequestMessage(parentPath, null, ext, pane));
                        }
                    }
                };
                contextMenu.Items.Add(customMenuItem);

                contextMenu.IsOpen = true;
            }
            catch (Exception ex)
            {
                Services.Core.NotificationService.ShowError($"显示菜单失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 右键菜单中的新建文件类型项点击
        /// </summary>
        private void NewFileMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem menuItem && menuItem.Tag is string extension)
            {
                if (DataContext is ViewModels.PaneViewModel vm && !string.IsNullOrEmpty(vm.CurrentPath))
                {
                    vm.MessageBus.Publish(new CreateFileRequestMessage(vm.CurrentPath, null, extension, vm.MyPaneId));
                }
            }
        }

        /// <summary>
        /// 右键菜单中的"自定义扩展名"点击
        /// </summary>
        private void NewFileCustomMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is ViewModels.PaneViewModel vm && !string.IsNullOrEmpty(vm.CurrentPath))
            {
                var dialogService = App.ServiceProvider?.GetService<Services.UI.IDialogService>();
                var inputExtension = dialogService?.ShowInput("请输入文件扩展名（如 .txt）：", ".txt", "新建文件");

                if (inputExtension != null)
                {
                    var ext = inputExtension.Trim();
                    if (!ext.StartsWith(".")) ext = "." + ext;
                    vm.MessageBus.Publish(new CreateFileRequestMessage(vm.CurrentPath, null, ext, vm.MyPaneId));
                }
            }
        }

    }
}
