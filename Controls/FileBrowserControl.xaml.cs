using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using OoiMRR.Controls.Converters;

namespace OoiMRR.Controls
{
    /// <summary>
    /// FileBrowserControl.xaml 的交互逻辑
    /// 统一的文件浏览控件，支持路径、库、标签三种模式
    /// </summary>
    public partial class FileBrowserControl : UserControl
    {
        private readonly Dictionary<GridViewColumn, double> _columnDefaultWidths = new Dictionary<GridViewColumn, double>();
        private enum ViewMode { Details, Tiles }
        private ViewMode _currentViewMode = ViewMode.Details;
        private double _thumbnailSize = 128;
        private ThumbnailViewManager _thumbnailManager;
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
            }
            
            // 订阅文件列表的事件
            if (FilesListView != null)
            {
                FilesListView.SelectionChanged += (s, e) => FilesSelectionChanged?.Invoke(s, e);
                FilesListView.MouseDoubleClick += (s, e) => FilesMouseDoubleClick?.Invoke(s, e);
                FilesListView.PreviewMouseDoubleClick += (s, e) => FilesPreviewMouseDoubleClick?.Invoke(s, e);
                FilesListView.PreviewKeyDown += (s, e) => FilesPreviewKeyDown?.Invoke(s, e);
                FilesListView.PreviewMouseLeftButtonDown += (s, e) => FilesPreviewMouseLeftButtonDown?.Invoke(s, e);
                FilesListView.MouseLeftButtonUp += (s, e) => FilesMouseLeftButtonUp?.Invoke(s, e);
                FilesListView.PreviewMouseDown += (s, e) => FilesPreviewMouseDown?.Invoke(s, e);
                FilesListView.SizeChanged += (s, e) => FilesSizeChanged?.Invoke(s, e);
                FilesListView.PreviewMouseWheel += FilesListView_PreviewMouseWheel;
                
                // 订阅列标题点击事件
                if (FilesGridView != null)
                {
                    foreach (GridViewColumn column in FilesGridView.Columns)
                    {
                        if (column.Header is GridViewColumnHeader header)
                        {
                            header.Click += (s, e) => GridViewColumnHeaderClick?.Invoke(s, e);
                        }
                        // 记录默认列宽
                        if (!_columnDefaultWidths.ContainsKey(column))
                            _columnDefaultWidths[column] = column.Width;
                    }
                    // 右键菜单：与预览窗口一致的列显示/隐藏
                    SetupFileContextMenu();
                }
                
                // 初始化视图按钮状态和缩略图大小
                BtnViewDetails.IsEnabled = false;  // 默认是详细信息视图
                BtnViewTiles.IsEnabled = true;
                
                // 初始化缩略图管理器
                _thumbnailManager = new ThumbnailViewManager(FilesListView, _thumbnailSize);
                
                ApplyViewMode();
            }
        }
        
        private void BtnViewDetails_Click(object sender, RoutedEventArgs e)
        {
            _currentViewMode = ViewMode.Details;
            BtnViewDetails.IsEnabled = false;
            BtnViewTiles.IsEnabled = true;
            ApplyViewMode();
            // 切换到详细信息视图时，清除优先加载列表
            _thumbnailManager?.ClearPriorityLoad();
        }
        
        private void BtnViewTiles_Click(object sender, RoutedEventArgs e)
        {
            _currentViewMode = ViewMode.Tiles;
            BtnViewDetails.IsEnabled = true;
            BtnViewTiles.IsEnabled = false;
            ApplyViewMode();
            // 切换到缩略图视图后，计算第一页文件并设置优先加载
            _thumbnailManager?.CalculateAndSetPriorityLoad();
        }
        
        private void ThumbnailSizeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            _thumbnailSize = e.NewValue;
            // 更新缩略图管理器
            if (_thumbnailManager != null)
            {
                _thumbnailManager = new ThumbnailViewManager(FilesListView, _thumbnailSize);
            }
            
            if (_currentViewMode == ViewMode.Tiles)
            {
                ApplyViewMode();
                // 缩略图大小改变后，重新计算第一页文件
                _thumbnailManager?.CalculateAndSetPriorityLoad();
            }
        }
        
        private void ApplyViewMode()
        {
            if (FilesListView == null) return;
            if (_currentViewMode == ViewMode.Details)
            {
                // 详情模式：使用 GridView（已在XAML定义）
                FilesListView.ItemTemplate = null;
                var itemsPanel = new ItemsPanelTemplate(new FrameworkElementFactory(typeof(VirtualizingStackPanel)));
                FilesListView.ItemsPanel = itemsPanel;
                if (FilesGridView != null) FilesListView.View = FilesGridView;
                ScrollViewer.SetHorizontalScrollBarVisibility(FilesListView, ScrollBarVisibility.Auto);
            }
            else
            {
                // 缩略图模式：WrapPanel + 缩略图模板
                FilesListView.View = null;
                var fef = new FrameworkElementFactory(typeof(WrapPanel));
                fef.SetValue(WrapPanel.OrientationProperty, Orientation.Horizontal);
                var itemWidth = _thumbnailSize + 24; // 增加宽度以容纳边距和文本
                fef.SetValue(WrapPanel.ItemWidthProperty, itemWidth);
                // 移除ItemHeight限制，让项目根据内容自动调整高度
                var itemsPanel = new ItemsPanelTemplate(fef);
                FilesListView.ItemsPanel = itemsPanel;
                ScrollViewer.SetHorizontalScrollBarVisibility(FilesListView, ScrollBarVisibility.Disabled);
                ScrollViewer.SetVerticalScrollBarVisibility(FilesListView, ScrollBarVisibility.Auto);
                
                var border = new FrameworkElementFactory(typeof(Border));
                border.SetValue(Border.BorderBrushProperty, System.Windows.Media.Brushes.Transparent);
                border.SetValue(Border.BorderThicknessProperty, new Thickness(1));
                border.SetValue(Border.MarginProperty, new Thickness(6));
                border.SetValue(Border.PaddingProperty, new Thickness(4)); // 减少Padding以给文本更多空间
                border.SetValue(Border.HorizontalAlignmentProperty, HorizontalAlignment.Left);
                border.SetValue(Border.VerticalAlignmentProperty, VerticalAlignment.Top);
                // 固定高度：缩略图高度 + 固定文本区域高度（完整2行）+ 边距
                var fixedTextHeight = 56.0; // 固定文本区域高度，完整显示2行文本（每行约24px + 行间距，确保不被裁剪）
                var fixedItemHeight = _thumbnailSize + fixedTextHeight + 6; // 缩略图 + 文本 + 上边距
                border.SetValue(FrameworkElement.HeightProperty, fixedItemHeight);
                border.SetValue(FrameworkElement.MinHeightProperty, fixedItemHeight);
                border.SetValue(FrameworkElement.MaxHeightProperty, fixedItemHeight);
                
                var stack = new FrameworkElementFactory(typeof(StackPanel));
                stack.SetValue(StackPanel.OrientationProperty, Orientation.Vertical);
                stack.SetValue(StackPanel.HorizontalAlignmentProperty, HorizontalAlignment.Center);
                stack.SetValue(StackPanel.VerticalAlignmentProperty, VerticalAlignment.Top);
                
                // ============================================================================
                // 1. 主缩略图（Main Thumbnail）
                // ============================================================================
                // 说明：显示文件内容预览（图片/视频）或系统图标（Office文档/文件夹等）
                // 大小：由 _thumbnailSize 控制（用户可调整）
                // 位置：居中显示
                // ============================================================================
                
                // 使用 Grid 包裹主缩略图，确保在固定高度容器内能垂直居中
                var thumbnailContainer = new FrameworkElementFactory(typeof(System.Windows.Controls.Grid));
                thumbnailContainer.SetValue(FrameworkElement.WidthProperty, _thumbnailSize);
                thumbnailContainer.SetValue(FrameworkElement.HeightProperty, _thumbnailSize);
                thumbnailContainer.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Center);
                thumbnailContainer.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
                
                // 主缩略图 Image 控件
                var thumbnailImage = new FrameworkElementFactory(typeof(Image));
                thumbnailImage.SetValue(FrameworkElement.WidthProperty, _thumbnailSize);
                thumbnailImage.SetValue(FrameworkElement.HeightProperty, _thumbnailSize);
                thumbnailImage.SetValue(Image.StretchProperty, System.Windows.Media.Stretch.Uniform);
                thumbnailImage.SetValue(Image.HorizontalAlignmentProperty, HorizontalAlignment.Center);
                thumbnailImage.SetValue(Image.VerticalAlignmentProperty, VerticalAlignment.Center);
                
                thumbnailContainer.AppendChild(thumbnailImage);
                
                // 绑定主缩略图数据源
                // ThumbnailConverter 会根据文件类型生成：
                // - 图片文件：直接加载图片内容
                // - 视频文件：使用 FFmpeg 提取第一帧
                // - 其他文件（Office文档/文件夹等）：使用系统图标
                var thumbnailBinding = new System.Windows.Data.Binding("Path")
                {
                    Converter = new Converters.ThumbnailConverter(),
                    ConverterParameter = _thumbnailSize, // 传递目标尺寸，用于优化性能
                    FallbackValue = null
                };
                thumbnailImage.SetBinding(Image.SourceProperty, thumbnailBinding);
                
                // ============================================================================
                // 2. 文件格式标识图标（File Format Badge Icon）
                // ============================================================================
                // 说明：显示在缩略图左下角的小图标，用于标识文件格式
                // 显示规则：
                //   - 图片文件：显示（因为可以预览，需要标识格式）
                //   - 视频文件：显示（因为可以预览，需要标识格式）
                //   - Office文档：显示（用于区分不同Office格式）
                //   - 文件夹：不显示
                //   - 其他文件：不显示
                // 大小规则：
                //   - Office文档：缩略图大小的 10%
                //   - 其他文件：缩略图大小的 15%
                //   - 范围限制：2-30px（测试用）
                // ============================================================================
                
                // 计算文件格式标识图标的默认大小（用于容器尺寸计算）
                // 注意：实际图标大小会根据文件类型动态计算（Office文档10%，其他15%）
                var defaultFileFormatIconSize = Math.Max(2, Math.Min(30, (int)(_thumbnailSize * 0.15)));
                var fileFormatIconPadding = Math.Max(2, (int)(defaultFileFormatIconSize * 0.2)); // 内边距为图标大小的20%，最小2px
                
                // 文件格式标识图标的容器（Border，带半透明背景和阴影）
                var fileFormatIconContainer = new FrameworkElementFactory(typeof(Border));
                
                // 容器样式：半透明白色背景，圆角边框，带阴影
                var fileFormatIconBgBrush = new SolidColorBrush(Color.FromArgb(240, 255, 255, 255));
                fileFormatIconContainer.SetValue(Border.BackgroundProperty, fileFormatIconBgBrush);
                fileFormatIconContainer.SetValue(Border.CornerRadiusProperty, new CornerRadius(5));
                fileFormatIconContainer.SetValue(Border.BorderBrushProperty, new SolidColorBrush(Color.FromArgb(100, 200, 200, 200)));
                fileFormatIconContainer.SetValue(Border.BorderThicknessProperty, new Thickness(1));
                fileFormatIconContainer.SetValue(Border.PaddingProperty, new Thickness(fileFormatIconPadding));
                
                // 容器尺寸：默认图标大小 + 内边距*2（容器大小固定，内部图标大小动态）
                var fileFormatIconContainerSize = defaultFileFormatIconSize + fileFormatIconPadding * 2;
                fileFormatIconContainer.SetValue(Border.WidthProperty, (double)fileFormatIconContainerSize);
                fileFormatIconContainer.SetValue(Border.HeightProperty, (double)fileFormatIconContainerSize);
                
                // 位置：缩略图左下角
                fileFormatIconContainer.SetValue(Border.HorizontalAlignmentProperty, HorizontalAlignment.Left);
                fileFormatIconContainer.SetValue(Border.VerticalAlignmentProperty, VerticalAlignment.Bottom);
                fileFormatIconContainer.SetValue(Border.MarginProperty, new Thickness(5)); // 距离边缘5px
                
                // 添加柔和的阴影效果
                var fileFormatIconDropShadow = new DropShadowEffect();
                fileFormatIconDropShadow.Color = Colors.Black;
                fileFormatIconDropShadow.Opacity = 0.25;
                fileFormatIconDropShadow.BlurRadius = 6;
                fileFormatIconDropShadow.ShadowDepth = 2;
                fileFormatIconDropShadow.Direction = 315; // 左上方向阴影
                fileFormatIconContainer.SetValue(FrameworkElement.EffectProperty, fileFormatIconDropShadow);
                
                // 绑定可见性：根据文件类型决定是否显示
                var fileFormatIconVisibilityBinding = new System.Windows.Data.Binding("Path")
                {
                    Converter = new Converters.ShouldShowFileFormatIconConverter(),
                    FallbackValue = System.Windows.Visibility.Collapsed
                };
                fileFormatIconContainer.SetBinding(UIElement.VisibilityProperty, fileFormatIconVisibilityBinding);
                
                // 文件格式标识图标的 Image 控件
                var fileFormatIconImage = new FrameworkElementFactory(typeof(Image));
                
                // 动态绑定图标大小：根据文件类型计算（Office文档10%，其他15%）
                var fileFormatIconSizeBinding = new System.Windows.Data.Binding("Path")
                {
                    Converter = new Converters.IconSizeConverter(),
                    ConverterParameter = _thumbnailSize, // 传递缩略图大小，用于计算比例
                    FallbackValue = (double)defaultFileFormatIconSize
                };
                fileFormatIconImage.SetBinding(FrameworkElement.WidthProperty, fileFormatIconSizeBinding);
                fileFormatIconImage.SetBinding(FrameworkElement.HeightProperty, fileFormatIconSizeBinding);
                
                fileFormatIconImage.SetValue(Image.StretchProperty, System.Windows.Media.Stretch.Uniform);
                fileFormatIconImage.SetValue(Image.HorizontalAlignmentProperty, HorizontalAlignment.Center);
                fileFormatIconImage.SetValue(Image.VerticalAlignmentProperty, VerticalAlignment.Center);
                
                // 绑定文件格式标识图标的数据源
                // FileExtensionIconConverter 会根据文件路径获取系统提供的文件格式图标
                // 传递缩略图大小作为参数，Converter内部会根据文件类型计算图标大小
                var fileFormatIconBinding = new System.Windows.Data.Binding("Path")
                {
                    Converter = new Converters.FileExtensionIconConverter(),
                    ConverterParameter = _thumbnailSize, // 传递缩略图大小，Converter内部根据文件类型计算图标大小
                    FallbackValue = null
                };
                fileFormatIconImage.SetBinding(Image.SourceProperty, fileFormatIconBinding);
                
                // 组装文件格式标识图标
                fileFormatIconContainer.AppendChild(fileFormatIconImage);
                
                // 将文件格式标识图标添加到缩略图容器中（叠加在缩略图左下角）
                thumbnailContainer.AppendChild(fileFormatIconContainer);
                
                stack.AppendChild(thumbnailContainer); // 将缩略图容器（包含主缩略图和文件格式标识图标）添加到 StackPanel
                
                var text = new FrameworkElementFactory(typeof(TextBlock));
                text.SetValue(TextBlock.TextAlignmentProperty, TextAlignment.Center);
                text.SetValue(TextBlock.TextWrappingProperty, TextWrapping.Wrap);
                text.SetValue(TextBlock.TextTrimmingProperty, TextTrimming.None); // 允许完整显示2行，超出部分会被裁剪
                text.SetValue(TextBlock.LineHeightProperty, 24.0); // 设置行高，确保2行文本完整显示（增加行高避免文字被裁剪）
                text.SetValue(TextBlock.LineStackingStrategyProperty, LineStackingStrategy.BlockLineHeight); // 使用固定行高
                text.SetValue(TextBlock.FontSizeProperty, 12.0); // 设置字体大小，确保文本清晰可读
                // 文本宽度略小于缩略图宽度，留出边距空间，并减去Border的Padding
                var textWidth = _thumbnailSize - 8; // 减去左右Padding各4像素
                text.SetValue(TextBlock.WidthProperty, textWidth);
                text.SetValue(TextBlock.MaxWidthProperty, textWidth);
                // 固定文本高度，使用上面定义的 fixedTextHeight
                text.SetValue(TextBlock.HeightProperty, fixedTextHeight);
                text.SetValue(TextBlock.MinHeightProperty, fixedTextHeight);
                text.SetValue(TextBlock.MaxHeightProperty, fixedTextHeight);
                text.SetValue(TextBlock.MarginProperty, new Thickness(0, 6, 0, 0));
                text.SetValue(TextBlock.HorizontalAlignmentProperty, HorizontalAlignment.Center);
                text.SetValue(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Top);
                text.SetBinding(TextBlock.TextProperty, new System.Windows.Data.Binding("Name"));
                
                stack.AppendChild(text);
                border.AppendChild(stack);
                
                var template = new DataTemplate { VisualTree = border };
                FilesListView.ItemTemplate = template;
                
                // 延迟加载非优先文件的缩略图
                _thumbnailManager?.LoadThumbnailsAsync();
            }
        }

        // 公开方法：设置视图模式（详细/缩略图）
        public void SetViewModeTiles(bool tiles)
        {
            try
            {
                if (tiles)
                {
                    _currentViewMode = ViewMode.Tiles;
                    if (BtnViewDetails != null) BtnViewDetails.IsEnabled = true;
                    if (BtnViewTiles != null) BtnViewTiles.IsEnabled = false;
                    ApplyViewMode();
                    _thumbnailManager?.CalculateAndSetPriorityLoad();
                }
                else
                {
                    _currentViewMode = ViewMode.Details;
                    if (BtnViewDetails != null) BtnViewDetails.IsEnabled = false;
                    if (BtnViewTiles != null) BtnViewTiles.IsEnabled = true;
                    ApplyViewMode();
                    _thumbnailManager?.ClearPriorityLoad();
                }
            }
            catch { }
        }

        // 公开方法：设置缩略图大小（80-256）
        public void SetThumbnailSize(double size)
        {
            try
            {
                var newSize = Math.Max(80, Math.Min(256, size));
                if (Math.Abs(newSize - _thumbnailSize) > double.Epsilon)
                {
                    _thumbnailSize = newSize;
                    if (ThumbnailSizeSlider != null) ThumbnailSizeSlider.Value = _thumbnailSize;
                    // 更新缩略图管理器
                    if (_thumbnailManager != null)
                    {
                        _thumbnailManager = new ThumbnailViewManager(FilesListView, _thumbnailSize);
                    }
                    if (_currentViewMode == ViewMode.Tiles)
                    {
                        ApplyViewMode();
                        _thumbnailManager?.CalculateAndSetPriorityLoad();
                    }
                }
            }
            catch { }
        }

        private void FilesListView_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            try
            {
                if ((Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control
                    && _currentViewMode == ViewMode.Tiles)
                {
                    // Ctrl+滚轮调整缩略图大小
                    var delta = e.Delta > 0 ? 16 : -16;
                    var newSize = Math.Max(80, Math.Min(256, _thumbnailSize + delta));
                    if (Math.Abs(newSize - _thumbnailSize) > double.Epsilon)
                    {
                        _thumbnailSize = newSize;
                        if (ThumbnailSizeSlider != null)
                        {
                            ThumbnailSizeSlider.Value = _thumbnailSize;
                        }
                        ApplyViewMode();
                    }
                    e.Handled = true;
                }
            }
            catch
            {
            }
        }

        // 公共属性
        public AddressBarControl AddressBar => AddressBarControl;
        public ListView FilesList => FilesListView;
        public GridView FilesGrid => FilesGridView;
        public StackPanel TabsPanelControl => TabsPanel;
        public Border TabsBorderControl => TabsBorder;
        public StackPanel FileInfoPanelControl => FileInfoPanel;
        public TextBlock EmptyStateTextControl => EmptyStateText;

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
            get => FilesListView?.ItemsSource;
            set
            {
                if (FilesListView != null)
                {
                    FilesListView.ItemsSource = value;
                    
                    // 如果是缩略图视图，ItemsSource 更新后需要重新计算优先加载列表
                    if (_currentViewMode == ViewMode.Tiles && value != null)
                    {
                        // 清除之前的优先加载列表
                        _thumbnailManager?.ClearPriorityLoad();
                        
                        // 优化：立即开始计算，减少延迟
                        // 使用 Loaded 事件优先，如果未触发则使用 LayoutUpdated
                        bool priorityCalculated = false;
                        
                        RoutedEventHandler loadedHandler = null;
                        loadedHandler = (s, e) =>
                        {
                            FilesListView.Loaded -= loadedHandler;
                            if (!priorityCalculated)
                            {
                                priorityCalculated = true;
                                // 立即计算优先加载列表
                                Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Loaded, new Action(() =>
                                {
                                    _thumbnailManager?.CalculateAndSetPriorityLoad();
                                    // 立即开始异步加载第二页，不等待
                                    _thumbnailManager?.LoadThumbnailsAsync();
                                }));
                            }
                        };
                        
                        EventHandler layoutHandler = null;
                        layoutHandler = (s, e) =>
                        {
                            FilesListView.LayoutUpdated -= layoutHandler;
                            if (!priorityCalculated)
                            {
                                priorityCalculated = true;
                                // 减少延迟，更快开始加载（从Background改为Loaded优先级）
                                Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Loaded, new Action(() =>
                                {
                                    _thumbnailManager?.CalculateAndSetPriorityLoad();
                                    // 立即开始异步加载第二页
                                    _thumbnailManager?.LoadThumbnailsAsync();
                                }));
                            }
                        };
                        
                        FilesListView.Loaded += loadedHandler;
                        FilesListView.LayoutUpdated += layoutHandler;
                        
                        // 如果ListView已经加载，立即执行
                        if (FilesListView.IsLoaded)
                        {
                            Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Loaded, new Action(() =>
                            {
                                if (!priorityCalculated)
                                {
                                    priorityCalculated = true;
                                    _thumbnailManager?.CalculateAndSetPriorityLoad();
                                    _thumbnailManager?.LoadThumbnailsAsync();
                                }
                            }));
                        }
                    }
                    else if (value == null)
                    {
                        // 清空时也清除优先加载列表
                        _thumbnailManager?.ClearPriorityLoad();
                    }
                }
            }
        }

        public object FilesSelectedItem
        {
            get => FilesListView?.SelectedItem;
            set
            {
                if (FilesListView != null)
                    FilesListView.SelectedItem = value;
            }
        }

        public System.Collections.IList FilesSelectedItems
        {
            get => FilesListView?.SelectedItems;
        }

        // 标签页相关方法
        public bool TabsVisible
        {
            get => TabsBorder?.Visibility == Visibility.Visible;
            set
            {
                if (TabsBorder != null)
                    TabsBorder.Visibility = value ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        // 空状态提示
        public void ShowEmptyState(string message = "暂无文件")
        {
            if (EmptyStateText != null)
            {
                EmptyStateText.Text = message;
                EmptyStateText.Visibility = Visibility.Visible;
            }
        }

        public void HideEmptyState()
        {
            if (EmptyStateText != null)
                EmptyStateText.Visibility = Visibility.Collapsed;
        }

        // 地址栏事件（转发给外部）
        public event EventHandler<string> PathChanged;
        public event EventHandler<string> BreadcrumbClicked;
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
            get => LoadMoreBtn?.Visibility == Visibility.Visible;
            set
            {
                if (LoadMoreBtn != null)
                    LoadMoreBtn.Visibility = value ? Visibility.Visible : Visibility.Collapsed;
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

        public void EnableAutoLoadMore()
        {
            try
            {
                var sv = GetScrollViewer(FilesListView);
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
            if (FilesListView == null) return;
            
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
                bool hasSelection = FilesListView?.SelectedItems != null && FilesListView.SelectedItems.Count > 0;
                
                // 需要选中项的操作
                copyItem.Visibility = hasSelection ? Visibility.Visible : Visibility.Collapsed;
                cutItem.Visibility = hasSelection ? Visibility.Visible : Visibility.Collapsed;
                deleteItem.Visibility = hasSelection ? Visibility.Visible : Visibility.Collapsed;
                renameItem.Visibility = hasSelection && FilesListView.SelectedItems.Count == 1 ? Visibility.Visible : Visibility.Collapsed;
                propertiesItem.Visibility = hasSelection && FilesListView.SelectedItems.Count == 1 ? Visibility.Visible : Visibility.Collapsed;
                
                // 始终可用的操作
                pasteItem.Visibility = Visibility.Visible;
                refreshItem.Visibility = Visibility.Visible;
                
                // 更新分隔符可见性
                separator1.Visibility = (hasSelection || pasteItem.Visibility == Visibility.Visible) ? Visibility.Visible : Visibility.Collapsed;
                separator2.Visibility = (hasSelection || refreshItem.Visibility == Visibility.Visible || propertiesItem.Visibility == Visibility.Visible) ? Visibility.Visible : Visibility.Collapsed;
            };
            
            FilesListView.ContextMenu = cm;
        }
    }
}
