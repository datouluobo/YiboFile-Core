using System;
using System.Collections;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using OoiMRR.Controls.Converters;

namespace OoiMRR.Controls
{
    /// <summary>
    /// FileListControl.xaml 的交互逻辑
    /// 独立的文件列表控件，支持详细信息视图和缩略图视图
    /// </summary>
    public partial class FileListControl : UserControl
    {
        private enum ViewMode { Details, Tiles }
        private ViewMode _currentViewMode = ViewMode.Details;
        private double _thumbnailSize = 128;
        private ThumbnailViewManager _thumbnailManager;

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
                    }
                }
            }

            // 订阅加载更多按钮事件
            if (LoadMoreBtn != null)
            {
                LoadMoreBtn.Click += (s, e) => LoadMoreClick?.Invoke(s, e);
            }
            
            // 初始化缩略图管理器
            _thumbnailManager = new ThumbnailViewManager(FilesListView, _thumbnailSize);
            
            ApplyViewMode();
        }
        
        // 公共方法：切换到详细信息视图
        public void SwitchToDetailsView()
        {
            _currentViewMode = ViewMode.Details;
            ApplyViewMode();
            // 切换到详细信息视图时，清除优先加载列表
            _thumbnailManager?.ClearPriorityLoad();
        }
        
        // 公共方法：切换到缩略图视图
        public void SwitchToTilesView()
        {
            _currentViewMode = ViewMode.Tiles;
            ApplyViewMode();
            // 切换到缩略图视图后，计算第一页文件并设置优先加载
            _thumbnailManager?.CalculateAndSetPriorityLoad();
        }
        
        // 公共方法：设置缩略图大小
        public void SetThumbnailSizeValue(double size)
        {
            _thumbnailSize = size;
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

        private void LoadMoreBtn_Click(object sender, RoutedEventArgs e)
        {
            LoadMoreClick?.Invoke(sender, e);
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
                var thumbnailBinding = new System.Windows.Data.Binding("Path")
                {
                    Converter = new Converters.ThumbnailConverter(),
                    ConverterParameter = _thumbnailSize,
                    FallbackValue = null
                };
                thumbnailImage.SetBinding(Image.SourceProperty, thumbnailBinding);
                
                // 文件格式标识图标
                var defaultFileFormatIconSize = Math.Max(2, Math.Min(30, (int)(_thumbnailSize * 0.15)));
                var fileFormatIconPadding = Math.Max(2, (int)(defaultFileFormatIconSize * 0.2));
                
                var fileFormatIconContainer = new FrameworkElementFactory(typeof(Border));
                var fileFormatIconBgBrush = new SolidColorBrush(Color.FromArgb(240, 255, 255, 255));
                fileFormatIconContainer.SetValue(Border.BackgroundProperty, fileFormatIconBgBrush);
                fileFormatIconContainer.SetValue(Border.CornerRadiusProperty, new CornerRadius(5));
                fileFormatIconContainer.SetValue(Border.BorderBrushProperty, new SolidColorBrush(Color.FromArgb(100, 200, 200, 200)));
                fileFormatIconContainer.SetValue(Border.BorderThicknessProperty, new Thickness(1));
                fileFormatIconContainer.SetValue(Border.PaddingProperty, new Thickness(fileFormatIconPadding));
                
                var fileFormatIconContainerSize = defaultFileFormatIconSize + fileFormatIconPadding * 2;
                fileFormatIconContainer.SetValue(Border.WidthProperty, (double)fileFormatIconContainerSize);
                fileFormatIconContainer.SetValue(Border.HeightProperty, (double)fileFormatIconContainerSize);
                fileFormatIconContainer.SetValue(Border.HorizontalAlignmentProperty, HorizontalAlignment.Left);
                fileFormatIconContainer.SetValue(Border.VerticalAlignmentProperty, VerticalAlignment.Bottom);
                fileFormatIconContainer.SetValue(Border.MarginProperty, new Thickness(5));
                
                var fileFormatIconDropShadow = new DropShadowEffect();
                fileFormatIconDropShadow.Color = Colors.Black;
                fileFormatIconDropShadow.Opacity = 0.25;
                fileFormatIconDropShadow.BlurRadius = 6;
                fileFormatIconDropShadow.ShadowDepth = 2;
                fileFormatIconDropShadow.Direction = 315;
                fileFormatIconContainer.SetValue(FrameworkElement.EffectProperty, fileFormatIconDropShadow);
                
                var fileFormatIconVisibilityBinding = new System.Windows.Data.Binding("Path")
                {
                    Converter = new Converters.ShouldShowFileFormatIconConverter(),
                    FallbackValue = System.Windows.Visibility.Collapsed
                };
                fileFormatIconContainer.SetBinding(UIElement.VisibilityProperty, fileFormatIconVisibilityBinding);
                
                var fileFormatIconImage = new FrameworkElementFactory(typeof(Image));
                var fileFormatIconSizeBinding = new System.Windows.Data.Binding("Path")
                {
                    Converter = new Converters.IconSizeConverter(),
                    ConverterParameter = _thumbnailSize,
                    FallbackValue = (double)defaultFileFormatIconSize
                };
                fileFormatIconImage.SetBinding(FrameworkElement.WidthProperty, fileFormatIconSizeBinding);
                fileFormatIconImage.SetBinding(FrameworkElement.HeightProperty, fileFormatIconSizeBinding);
                fileFormatIconImage.SetValue(Image.StretchProperty, System.Windows.Media.Stretch.Uniform);
                fileFormatIconImage.SetValue(Image.HorizontalAlignmentProperty, HorizontalAlignment.Center);
                fileFormatIconImage.SetValue(Image.VerticalAlignmentProperty, VerticalAlignment.Center);
                
                var fileFormatIconBinding = new System.Windows.Data.Binding("Path")
                {
                    Converter = new Converters.FileExtensionIconConverter(),
                    ConverterParameter = _thumbnailSize,
                    FallbackValue = null
                };
                fileFormatIconImage.SetBinding(Image.SourceProperty, fileFormatIconBinding);
                
                fileFormatIconContainer.AppendChild(fileFormatIconImage);
                thumbnailContainer.AppendChild(fileFormatIconContainer);
                stack.AppendChild(thumbnailContainer);
                
                var text = new FrameworkElementFactory(typeof(TextBlock));
                text.SetValue(TextBlock.TextAlignmentProperty, TextAlignment.Center);
                text.SetValue(TextBlock.TextWrappingProperty, TextWrapping.Wrap);
                text.SetValue(TextBlock.TextTrimmingProperty, TextTrimming.None);
                text.SetValue(TextBlock.LineHeightProperty, 24.0);
                text.SetValue(TextBlock.LineStackingStrategyProperty, LineStackingStrategy.BlockLineHeight);
                text.SetValue(TextBlock.FontSizeProperty, 12.0);
                var textWidth = _thumbnailSize - 8;
                text.SetValue(TextBlock.WidthProperty, textWidth);
                text.SetValue(TextBlock.MaxWidthProperty, textWidth);
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
                        ApplyViewMode();
                    }
                    e.Handled = true;
                }
            }
            catch { }
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
                    // 在切换数据源前，先取消之前的加载任务
                    _thumbnailManager?.ClearPriorityLoad();
                    
                    FilesListView.ItemsSource = value;
                    
                    // 如果是缩略图视图，ItemsSource 更新后需要重新计算优先加载列表
                    if (_currentViewMode == ViewMode.Tiles && value != null)
                    {
                        bool priorityCalculated = false;
                        RoutedEventHandler loadedHandler = null;
                        loadedHandler = (s, e) =>
                        {
                            FilesListView.Loaded -= loadedHandler;
                            if (!priorityCalculated)
                            {
                                priorityCalculated = true;
                                Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Loaded, new Action(() =>
                                {
                                    _thumbnailManager?.CalculateAndSetPriorityLoad();
                                    _thumbnailManager?.LoadThumbnailsAsync();
                                }));
                            }
                        };
                        FilesListView.Loaded += loadedHandler;
                        
                        EventHandler layoutHandler = null;
                        layoutHandler = (s, e) =>
                        {
                            FilesListView.LayoutUpdated -= layoutHandler;
                            if (!priorityCalculated)
                            {
                                priorityCalculated = true;
                                Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Loaded, new Action(() =>
                                {
                                    _thumbnailManager?.CalculateAndSetPriorityLoad();
                                    _thumbnailManager?.LoadThumbnailsAsync();
                                }));
                            }
                        };
                        FilesListView.LayoutUpdated += layoutHandler;
                    }
                }
            }
        }

        // 视图模式
        public enum ViewModeType { Details, Tiles }
        public ViewModeType CurrentViewMode
        {
            get => _currentViewMode == ViewMode.Details ? ViewModeType.Details : ViewModeType.Tiles;
            set
            {
                if (value == ViewModeType.Details)
                {
                    _currentViewMode = ViewMode.Details;
                    ApplyViewMode();
                    _thumbnailManager?.ClearPriorityLoad();
                }
                else
                {
                    _currentViewMode = ViewMode.Tiles;
                    ApplyViewMode();
                    _thumbnailManager?.CalculateAndSetPriorityLoad();
                }
            }
        }

        // 缩略图大小
        public double ThumbnailSize
        {
            get => _thumbnailSize;
            set
            {
                var newSize = Math.Max(80, Math.Min(256, value));
                if (Math.Abs(newSize - _thumbnailSize) > double.Epsilon)
                {
                    _thumbnailSize = newSize;
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
    }
}

