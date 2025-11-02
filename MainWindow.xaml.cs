using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Drawing;
using System.Drawing.Imaging;
using Microsoft.Win32;
using System.Runtime.InteropServices;

namespace OoiMRR
{
    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow : Window
    {
        private string _currentPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
        private List<string> _navigationHistory = new List<string>();
        private int _currentHistoryIndex = -1;
        private List<FileSystemItem> _currentFiles = new List<FileSystemItem>();
        private AppConfig _config = new AppConfig();
        private bool _isApplyingConfig = false;
        private FileSystemWatcher _fileWatcher;
        private System.Windows.Threading.DispatcherTimer _refreshDebounceTimer;
        private List<string> _copiedPaths = new List<string>();
        private bool _isCutOperation = false;

        public MainWindow()
        {
            InitializeComponent();
            InitializeApplication();
        }

        private void InitializeApplication()
        {
            // 加载配置
            _config = ConfigManager.Load();
            
            // 先应用路径配置（在Loaded之前）
            if (!string.IsNullOrEmpty(_config.LastPath) && Directory.Exists(_config.LastPath))
            {
                _currentPath = _config.LastPath;
            }
            
            // 加载初始数据
            LoadCurrentDirectory();
            LoadLibraries();
            LoadTags();
            LoadQuickAccess();
            LoadDrives();
            
            // 初始化导航，默认显示路径
            SwitchNavigationMode("Path");

            // 窗口加载完成后应用配置（确保控件已完全初始化）
            this.Loaded += (s, e) => 
            {
                ApplyConfig(_config);
                
                // 连接右侧面板事件（按钮已移到主窗口，不再需要按钮事件）
                if (RightPanel != null)
                {
                    RightPanel.NotesTextChanged += NotesTextBox_TextChanged;
                    RightPanel.NotesAutoSaved += NotesAutoSaved_Handler;
                    RightPanel.PreviewMiddleClickRequested += RightPanel_PreviewMiddleClickRequested;
                    RightPanel.PreviewOpenFileRequested += RightPanel_PreviewOpenFileRequested;
                    // 按钮已移到主窗口，不再从右侧面板获取事件
                }
                
                // 标题栏拖拽功能已在XAML中绑定到TitleBar_MouseDown，无需额外设置
                // TopDragArea 和 RightPanel 的拖拽功能已移除，统一使用顶部标题栏
                
                // 设置窗口最小宽度=三列MinWidth之和+两个分割器宽度
                this.MinWidth = ColLeft.MinWidth + ColCenter.MinWidth + ColRight.MinWidth + 12;

                // 设置ListView列的最小宽度和压缩顺序
                if (FilesListView != null)
                {
                    FilesListView.SizeChanged += ListView_SizeChanged;
                }

                // 强制初始化列2和列3为固定宽度（不使用Star模式）
                this.Dispatcher.BeginInvoke(new Action(() =>
                {
                    ForceColumnWidthsToFixed();
                    // 更新列2操作按钮位置，使其居中对齐列2区域
                    UpdateActionButtonsPosition();
                }), System.Windows.Threading.DispatcherPriority.Loaded);
                
                // 监听列宽度变化，更新按钮位置
                this.SizeChanged += (s, e) =>
                {
                    this.Dispatcher.BeginInvoke(new Action(() =>
                    {
                        UpdateActionButtonsPosition();
                    }), System.Windows.Threading.DispatcherPriority.Loaded);
                };
                
                // 监听GridSplitter拖拽，更新按钮位置
                var splitters1 = RootGrid.Children.OfType<GridSplitter>().ToList();
                foreach (var splitter in splitters1)
                {
                    splitter.DragCompleted += (s, e) =>
                    {
                        this.Dispatcher.BeginInvoke(new Action(() =>
                        {
                            UpdateActionButtonsPosition();
                        }), System.Windows.Threading.DispatcherPriority.Loaded);
                    };
                }

                // 监听GridSplitter拖拽事件，实时限制最小宽度
                var splitters = FindVisualChildren<GridSplitter>(this).ToList();
                for (int i = 0; i < splitters.Count; i++)
                {
                    var splitter = splitters[i];
                    int splitterColumn = Grid.GetColumn(splitter);
                    
                    // 判断是哪个分割线：Column=1是列1和列2之间，Column=3是列2和列3之间
                    bool isRightSplitter = (splitterColumn == 3); // 列2和列3之间的分割线
                    
                    // 在拖拽开始时，记录当前宽度
                    double? savedCenterWidth = null;
                    double? savedRightWidth = null;
                    
                    splitter.DragStarted += (s2, e2) =>
                    {
                        // 保存当前宽度
                        savedCenterWidth = ColCenter.ActualWidth;
                        savedRightWidth = ColRight.ActualWidth;
                        
                        Debug.WriteLine($"[DragStarted] Splitter Column={splitterColumn}, IsRightSplitter={isRightSplitter}");
                        Debug.WriteLine($"[DragStarted] ColCenter: IsStar={ColCenter.Width.IsStar}, ActualWidth={ColCenter.ActualWidth}, MinWidth={ColCenter.MinWidth}, Width.Value={ColCenter.Width.Value}");
                        Debug.WriteLine($"[DragStarted] ColRight: IsStar={ColRight.Width.IsStar}, ActualWidth={ColRight.ActualWidth}, MinWidth={ColRight.MinWidth}, Width.Value={ColRight.Width.Value}");
                        
                        // 强制将列2设置为固定宽度（不使用Star模式）
                        if (ColCenter.Width.IsStar && savedCenterWidth.HasValue)
                        {
                            double newWidth = Math.Max(ColCenter.MinWidth, savedCenterWidth.Value);
                            ColCenter.Width = new GridLength(newWidth);
                            Debug.WriteLine($"[DragStarted] ColCenter: 从Star模式改为固定宽度 {newWidth}");
                        }
                        
                        // 确保列3使用Star模式
                        if (!ColRight.Width.IsStar && savedRightWidth.HasValue)
                        {
                            // 如果列3不是Star模式，改为Star模式以允许调整
                            ColRight.Width = new GridLength(1, GridUnitType.Star);
                            Debug.WriteLine($"[DragStarted] ColRight: 改为Star模式，允许调整宽度");
                        }
                    };
                    
                    // 在拖拽过程中实时检查并限制最小宽度
                    splitter.DragDelta += (s2, e2) =>
                    {
                        // 延迟检查，在GridSplitter改变列宽之后立即修复
                        this.Dispatcher.BeginInvoke(new Action(() =>
                        {
                            double centerActual = ColCenter.ActualWidth;
                            double rightActual = ColRight.ActualWidth;
                            double minCenter = ColCenter.MinWidth;
                            double minRight = ColRight.MinWidth;
                            
                            Debug.WriteLine($"[DragDelta] HorizontalChange={e2.HorizontalChange}, IsRightSplitter={isRightSplitter}");
                            Debug.WriteLine($"[DragDelta] ColCenter: IsStar={ColCenter.Width.IsStar}, ActualWidth={centerActual}, MinWidth={minCenter}, Width.Value={ColCenter.Width.Value}");
                            Debug.WriteLine($"[DragDelta] ColRight: IsStar={ColRight.Width.IsStar}, ActualWidth={rightActual}, MinWidth={minRight}, Width.Value={ColRight.Width.Value}");
                            
                            bool needFix = false;
                            
                            // 检查并修复列2（中间列）
                            if (centerActual < minCenter)
                            {
                                // 如果列2小于最小宽度，设置为最小宽度
                                ColCenter.Width = new GridLength(minCenter);
                                needFix = true;
                                Debug.WriteLine($"[DragDelta] 修复列2: {centerActual} < {minCenter}, 设置为 {minCenter}");
                            }
                            else if (ColCenter.Width.IsStar)
                            {
                                // 如果列2是Star模式，改为固定宽度
                                double newWidth = Math.Max(minCenter, centerActual);
                                ColCenter.Width = new GridLength(newWidth);
                                needFix = true;
                                Debug.WriteLine($"[DragDelta] 列2从Star模式改为固定宽度: {newWidth}");
                            }
                            
                            // 只检查列3最小宽度，保持Star模式
                            if (rightActual < minRight)
                            {
                                // 如果列3小于最小宽度，设置为最小宽度（但保持Star模式）
                                // 需要改为固定宽度才能设置最小宽度
                                ColRight.Width = new GridLength(minRight);
                                needFix = true;
                                Debug.WriteLine($"[DragDelta] 修复列3最小宽度: {rightActual} < {minRight}, 设置为 {minRight}");
                            }
                            else if (!ColRight.Width.IsStar)
                            {
                                // 如果列3不是Star模式，改为Star模式以允许调整
                                ColRight.Width = new GridLength(1, GridUnitType.Star);
                                needFix = true;
                                Debug.WriteLine($"[DragDelta] 列3改为Star模式，允许调整宽度");
                            }
                            
                            // 如果修复了列宽，触发布局更新
                            if (needFix)
                            {
                                Debug.WriteLine($"[DragDelta] 触发布局更新");
                                this.UpdateLayout();
                            }
                        }), System.Windows.Threading.DispatcherPriority.Input);
                    };
                    
                    // 拖拽结束后保存配置
                    splitter.DragCompleted += (s2, e2) => 
                    {
                        System.Threading.Thread.Sleep(100);
                        
                        // 确保列2和列3最小宽度
                        double centerActual = ColCenter.ActualWidth;
                        double rightActual = ColRight.ActualWidth;
                        double minCenter = ColCenter.MinWidth;
                        double minRight = ColRight.MinWidth;
                        
                        Debug.WriteLine($"[DragCompleted] IsRightSplitter={isRightSplitter}");
                        Debug.WriteLine($"[DragCompleted] ColCenter: IsStar={ColCenter.Width.IsStar}, ActualWidth={centerActual}, MinWidth={minCenter}, Width.Value={ColCenter.Width.Value}");
                        Debug.WriteLine($"[DragCompleted] ColRight: IsStar={ColRight.Width.IsStar}, ActualWidth={rightActual}, MinWidth={minRight}, Width.Value={ColRight.Width.Value}");
                        
                        bool needFix = false;
                        if (centerActual < minCenter)
                        {
                            ColCenter.Width = new GridLength(minCenter);
                            needFix = true;
                            Debug.WriteLine($"[DragCompleted] 修复列2: {centerActual} < {minCenter}, 设置为 {minCenter}");
                        }
                        
                        // 只检查列3最小宽度，保持Star模式
                        if (rightActual < minRight)
                        {
                            ColRight.Width = new GridLength(minRight);
                            needFix = true;
                            Debug.WriteLine($"[DragCompleted] 修复列3最小宽度: {rightActual} < {minRight}, 设置为 {minRight}");
                        }
                        else if (!ColRight.Width.IsStar)
                        {
                            // 如果列3不是Star模式，改为Star模式使用剩余空间
                            ColRight.Width = new GridLength(1, GridUnitType.Star);
                            needFix = true;
                            Debug.WriteLine($"[DragCompleted] 列3改为Star模式，使用剩余空间");
                        }
                        
                        // 强制确保列2不是Star模式
                        ForceColumnWidthsToFixed();
                        
                        if (needFix)
                        {
                            Debug.WriteLine($"[DragCompleted] 调用AdjustColumnWidths重新分配");
                            AdjustColumnWidths();
                        }
                        
                        SaveCurrentConfig();
                    };
                }
            };

            // 根据窗口大小动态约束列宽（优先保持列3，再列2，最后列1）
            this.SizeChanged += (s, e) =>
            {
                // 防止小于三列最小宽度总和
                double minTotal = ColLeft.MinWidth + ColCenter.MinWidth + ColRight.MinWidth + 12;
                if (this.ActualWidth < minTotal)
                {
                    // 不要强制设置窗口宽度，否则可能影响窗口边缘调整
                    // 只在极端情况下设置MinWidth
                    if (this.WindowState == WindowState.Normal && this.Width < minTotal)
                    {
                        // 使用MinWidth而不是Width，避免干扰窗口边缘调整
                        this.MinWidth = minTotal;
                    }
                    return;
                }

                // 等待布局完成后再调整
                this.Dispatcher.BeginInvoke(new Action(() =>
                {
                    AdjustColumnWidths();
                }), System.Windows.Threading.DispatcherPriority.Loaded);
            };
            
            // 监听布局更新，确保列3最小宽度始终生效（带防抖）
            System.Windows.Threading.DispatcherTimer layoutCheckTimer = null;
            this.LayoutUpdated += (s, e) =>
            {
                if (RootGrid == null || ColRight == null) return;
                
                // 使用防抖机制，避免频繁检查
                if (layoutCheckTimer == null)
                {
                    layoutCheckTimer = new System.Windows.Threading.DispatcherTimer();
                    layoutCheckTimer.Interval = TimeSpan.FromMilliseconds(50);
                    layoutCheckTimer.Tick += (s2, e2) =>
                    {
                        layoutCheckTimer.Stop();
                        
                        double centerActual = ColCenter.ActualWidth;
                        double rightActual = ColRight.ActualWidth;
                        double minCenter = ColCenter.MinWidth;
                        double minRight = ColRight.MinWidth;
                        
                        Debug.WriteLine($"[LayoutUpdated] ColCenter: IsStar={ColCenter.Width.IsStar}, ActualWidth={centerActual}, MinWidth={minCenter}");
                        Debug.WriteLine($"[LayoutUpdated] ColRight: IsStar={ColRight.Width.IsStar}, ActualWidth={rightActual}, MinWidth={minRight}");
                        
                        // 强制确保列2和列3不使用Star模式
                        ForceColumnWidthsToFixed();
                        
                        // 检查列2和列3宽度是否小于最小宽度
                        centerActual = ColCenter.ActualWidth;
                        rightActual = ColRight.ActualWidth;
                        
                        // 如果列2或列3宽度小于最小宽度，强制调整
                        if ((centerActual > 0 && centerActual < minCenter) || 
                            (rightActual > 0 && rightActual < minRight))
                        {
                            Debug.WriteLine($"[LayoutUpdated] 检测到宽度小于最小宽度，调用AdjustColumnWidths");
                            // 调用AdjustColumnWidths重新分配，确保最小宽度生效
                            AdjustColumnWidths();
                        }
                    };
                }
                
                layoutCheckTimer.Stop();
                layoutCheckTimer.Start();
            };
            UpdateWindowStateUI();

            // 延迟保存配置，避免频繁保存
            System.Windows.Threading.DispatcherTimer saveTimer = null;
            Action delayedSave = () =>
            {
                if (saveTimer != null)
                {
                    saveTimer.Stop();
                }
                saveTimer = new System.Windows.Threading.DispatcherTimer
                {
                    Interval = TimeSpan.FromMilliseconds(500)
                };
                saveTimer.Tick += (s2, e2) =>
                {
                    saveTimer.Stop();
                    SaveCurrentConfig();
                };
                saveTimer.Start();
            };
            
            // 窗口大小改变时延迟保存配置
            this.SizeChanged += (s, e) => delayedSave();
            
            // 布局更新完成后延迟保存（包括GridSplitter拖拽）
            if (this.RootGrid != null)
            {
                this.RootGrid.LayoutUpdated += (s, e) => delayedSave();
            }
            
            // 关闭时立即保存配置
            this.Closing += (s, e) => 
            {
                // 保存当前路径
                _config.LastPath = _currentPath;
                
                // 强制保存备注
                if (RightPanel != null)
                {
                    RightPanel.ForceSaveNotes();
                }
                
                if (saveTimer != null) saveTimer.Stop();
                
                // 停止文件监视器
                if (_fileWatcher != null)
                {
                    _fileWatcher.EnableRaisingEvents = false;
                    _fileWatcher.Dispose();
                    _fileWatcher = null;
                }
                
                // 停止刷新定时器
                if (_refreshDebounceTimer != null)
                {
                    _refreshDebounceTimer.Stop();
                }
                
                // 强制更新布局并等待
                if (this.RootGrid != null && this.RootGrid.IsLoaded)
                {
                    this.RootGrid.UpdateLayout();
                    this.UpdateLayout();
                }
                
                // 等待确保尺寸已更新
                System.Threading.Thread.Sleep(100);
                SaveCurrentConfig();
            };

            // 定期保存配置（作为备份）
            var periodicTimer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(10)
            };
            periodicTimer.Tick += (s, e) => SaveCurrentConfig();
            periodicTimer.Start();
        }

        private void ApplyConfig(AppConfig cfg)
        {
            try
            {
                _isApplyingConfig = true;

                if (cfg.IsMaximized)
                {
                    // 使用伪最大化而不是WindowState.Maximized，避免系统边距
                    _restoreBounds = new Rect(0, 0, cfg.WindowWidth, cfg.WindowHeight);
                    var wa = GetCurrentMonitorWorkAreaDIPs();
                    this.WindowState = WindowState.Normal;
                    this.Left = wa.Left;
                    this.Top = wa.Top;
                    this.Width = wa.Width;
                    this.Height = wa.Height;
                    _isPseudoMaximized = true;
                    this.ResizeMode = ResizeMode.NoResize;
                    
                    // 等待窗口加载完成后再移除边框
                    this.Loaded += (s, e) =>
                    {
                        if (_isPseudoMaximized)
                        {
                            var hwnd = new WindowInteropHelper(this).Handle;
                            var margins = new NativeMethods.MARGINS { cxLeftWidth = -1, cxRightWidth = -1, cyTopHeight = -1, cyBottomHeight = -1 };
                            NativeMethods.DwmExtendFrameIntoClientArea(hwnd, ref margins);
                        }
                    };
                }
                else
                {
                    this.Width = cfg.WindowWidth;
                    this.Height = cfg.WindowHeight;
                    if (!double.IsNaN(cfg.WindowTop)) this.Top = cfg.WindowTop;
                    if (!double.IsNaN(cfg.WindowLeft)) this.Left = cfg.WindowLeft;
                    _isPseudoMaximized = false;
                    this.ResizeMode = ResizeMode.CanResize;
                }

                // 应用主Grid列宽
                if (this.RootGrid != null)
                {
                    // 列 0,2 分别对应 左/中；右侧自适应
                    // 确保设置的宽度不小于最小宽度
                    var leftWidth = Math.Max(ColLeft.MinWidth, cfg.LeftPanelWidth);
                    var centerWidth = Math.Max(ColCenter.MinWidth, cfg.MiddlePanelWidth);
                    
                    this.RootGrid.ColumnDefinitions[0].Width = new GridLength(leftWidth);
                    this.RootGrid.ColumnDefinitions[2].Width = new GridLength(centerWidth);
                    // 右侧(列4)使用*自适应，不设置固定宽度
                    
                    // 确保窗口最小宽度正确设置
                    var minTotalWidth = ColLeft.MinWidth + ColCenter.MinWidth + ColRight.MinWidth + 12;
                    if (this.MinWidth < minTotalWidth)
                    {
                        this.MinWidth = minTotalWidth;
                    }
                }
                
                // 重要：应用完配置后，立即调整列宽以确保MinWidth生效
                this.UpdateLayout();
                AdjustColumnWidths();
                // 确保列3的最小宽度约束生效
                EnsureColumnMinWidths();
            }
            catch { }
            finally
            {
                _isApplyingConfig = false;
            }
        }

        private void SaveCurrentConfig()
        {
            // 如果正在应用配置，不保存
            if (_isApplyingConfig) return;
            
            try
            {
                // 保存当前路径
                _config.LastPath = _currentPath;
                _config.IsMaximized = _isPseudoMaximized; // 使用_isPseudoMaximized而不是WindowState
                if (!_config.IsMaximized)
                {
                    _config.WindowWidth = this.Width;
                    _config.WindowHeight = this.Height;
                    _config.WindowTop = this.Top;
                    _config.WindowLeft = this.Left;
                }

                if (this.RootGrid != null && this.RootGrid.IsLoaded)
                {
                    // 强制更新布局
                    this.RootGrid.UpdateLayout();
                    
                    // 获取实际尺寸
                    var leftWidth = this.RootGrid.ColumnDefinitions[0].ActualWidth;
                    var middleWidth = this.RootGrid.ColumnDefinitions[2].ActualWidth;
                    var rightWidth = this.RootGrid.ColumnDefinitions[4].ActualWidth;
                    
                    // 只有当实际尺寸大于0时才保存
                    if (leftWidth > 0) _config.LeftPanelWidth = leftWidth;
                    if (middleWidth > 0) _config.MiddlePanelWidth = middleWidth;
                    // 右侧为自适应宽度，不保存
                }
                else if (this.RootGrid != null)
                {
                    // 如果Grid未加载，尝试使用Width值
                    var leftWidth = this.RootGrid.ColumnDefinitions[0].Width;
                    var middleWidth = this.RootGrid.ColumnDefinitions[2].Width;
                    var rightWidth = this.RootGrid.ColumnDefinitions[4].Width;
                    
                    if (leftWidth.IsAbsolute) _config.LeftPanelWidth = leftWidth.Value;
                    if (middleWidth.IsAbsolute) _config.MiddlePanelWidth = middleWidth.Value;
                    // 右侧为自适应宽度，不保存
                }

                ConfigManager.Save(_config);
            }
            catch { }
        }
        
        #region 导航切换
        
        private void SwitchNavigationMode(string mode)
        {
            // 隐藏所有导航内容
            NavPathContent.Visibility = Visibility.Collapsed;
            NavLibraryContent.Visibility = Visibility.Collapsed;
            NavTagContent.Visibility = Visibility.Collapsed;
            NavSearchContent.Visibility = Visibility.Collapsed;
            
            // 隐藏所有操作按钮
            PathActionButtons.Visibility = Visibility.Collapsed;
            LibraryActionButtons.Visibility = Visibility.Collapsed;
            TagActionButtons.Visibility = Visibility.Collapsed;
            SearchActionButtons.Visibility = Visibility.Collapsed;
            
            // 根据模式显示对应内容和按钮
            switch (mode)
            {
                case "Path":
                    NavPathContent.Visibility = Visibility.Visible;
                    PathActionButtons.Visibility = Visibility.Visible;
                    break;
                case "Library":
                    NavLibraryContent.Visibility = Visibility.Visible;
                    LibraryActionButtons.Visibility = Visibility.Visible;
                    break;
                case "Tag":
                    NavTagContent.Visibility = Visibility.Visible;
                    TagActionButtons.Visibility = Visibility.Visible;
                    break;
                case "Search":
                    NavSearchContent.Visibility = Visibility.Visible;
                    SearchActionButtons.Visibility = Visibility.Visible;
                    break;
            }
            
            // 更新文件列表
            RefreshFileList();
        }
        
        private void NavPathBtn_Click(object sender, RoutedEventArgs e)
        {
            SwitchNavigationMode("Path");
        }
        
        private void NavLibraryBtn_Click(object sender, RoutedEventArgs e)
        {
            SwitchNavigationMode("Library");
        }
        
        private void NavTagBtn_Click(object sender, RoutedEventArgs e)
        {
            SwitchNavigationMode("Tag");
        }
        
        private void NavSearchBtn_Click(object sender, RoutedEventArgs e)
        {
            SwitchNavigationMode("Search");
        }
        
        private void RefreshFileList()
        {
            // 根据当前导航模式刷新文件列表
            // 这个方法的实现取决于当前模式
            // 暂时保持原有逻辑
            LoadFiles();
        }
        
        #endregion

        #region 导航功能

        private void LoadCurrentDirectory()
        {
            try
            {
                AddressTextBox.Text = _currentPath;
                UpdateBreadcrumb();
                LoadFiles();
                SetupFileWatcher(_currentPath);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"无法加载目录: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SetupFileWatcher(string path)
        {
            // 停止并释放旧的监视器
            if (_fileWatcher != null)
            {
                _fileWatcher.EnableRaisingEvents = false;
                _fileWatcher.Dispose();
                _fileWatcher = null;
            }

            // 初始化防抖定时器（只初始化一次）
            if (_refreshDebounceTimer == null)
            {
                _refreshDebounceTimer = new System.Windows.Threading.DispatcherTimer
                {
                    Interval = TimeSpan.FromMilliseconds(300) // 300ms 防抖延迟
                };
                _refreshDebounceTimer.Tick += (s, e) =>
                {
                    _refreshDebounceTimer.Stop();
                    try
                    {
                        System.Diagnostics.Debug.WriteLine("自动刷新文件列表...");
                        LoadFiles();
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"自动刷新失败: {ex.Message}");
                    }
                };
            }

            try
            {
                // 创建新的文件系统监视器
                _fileWatcher = new FileSystemWatcher
                {
                    Path = path,
                    NotifyFilter = NotifyFilters.FileName | 
                                   NotifyFilters.DirectoryName | 
                                   NotifyFilters.LastWrite | 
                                   NotifyFilters.Size,
                    Filter = "*.*",
                    IncludeSubdirectories = false
                };

                // 文件创建事件
                _fileWatcher.Created += OnFileSystemChanged;
                // 文件删除事件
                _fileWatcher.Deleted += OnFileSystemChanged;
                // 文件重命名事件
                _fileWatcher.Renamed += OnFileSystemChanged;
                // 文件修改事件
                _fileWatcher.Changed += OnFileSystemChanged;

                // 启用监视
                _fileWatcher.EnableRaisingEvents = true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"无法设置文件监视器: {ex.Message}");
            }
        }

        private void OnFileSystemChanged(object sender, FileSystemEventArgs e)
        {
            // 使用防抖机制，避免频繁刷新
            System.Diagnostics.Debug.WriteLine($"文件系统变化: {e.ChangeType} - {e.Name}");
            
            Dispatcher.BeginInvoke(new Action(() =>
            {
                if (_refreshDebounceTimer != null)
                {
                    _refreshDebounceTimer.Stop();
                    _refreshDebounceTimer.Start();
                }
            }), System.Windows.Threading.DispatcherPriority.Background);
        }

        private void UpdateBreadcrumb()
        {
            BreadcrumbPanel.Children.Clear();
            
            var parts = _currentPath.Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries);
            var currentPath = "";
            
            for (int i = 0; i < parts.Length; i++)
            {
                currentPath = Path.Combine(currentPath, parts[i]);
                
                var button = new Button
                {
                    Content = parts[i],
                    Style = (Style)FindResource("BreadcrumbButtonStyle"),
                    Margin = new Thickness(2, 2, 2, 2) // 添加边距以便多行时更好看
                };
                
                var path = currentPath;
                button.Click += (s, e) => NavigateToPath(path);
                
                BreadcrumbPanel.Children.Add(button);
                
                if (i < parts.Length - 1)
                {
                    var separator = new TextBlock 
                    { 
                        Text = " › ", 
                        Margin = new Thickness(4, 0, 4, 0),
                        Foreground = System.Windows.Media.Brushes.Gray,
                        VerticalAlignment = VerticalAlignment.Center
                    };
                    BreadcrumbPanel.Children.Add(separator);
                }
            }
        }

        private void NavigateToPath(string path)
        {
            if (Directory.Exists(path))
            {
                AddToHistory(_currentPath);
                _currentPath = path;
                // 清除任何过滤状态
                ClearFilter();
                LoadCurrentDirectory();
                // 保存当前路径到配置
                _config.LastPath = _currentPath;
                ConfigManager.Save(_config);
            }
        }

        private void AddToHistory(string path)
        {
            if (_currentHistoryIndex >= 0 && _currentHistoryIndex < _navigationHistory.Count - 1)
            {
                _navigationHistory.RemoveRange(_currentHistoryIndex + 1, _navigationHistory.Count - _currentHistoryIndex - 1);
            }
            
            _navigationHistory.Add(path);
            _currentHistoryIndex = _navigationHistory.Count - 1;
        }

        private void LoadFiles()
        {
            _currentFiles.Clear();
            
            try
            {
                // 加载文件夹
                var directories = Directory.GetDirectories(_currentPath)
                    .Select(d =>
                    {
                        var dirInfo = new DirectoryInfo(d);
                        var item = new FileSystemItem
                        {
                            Name = Path.GetFileName(d),
                            Path = d,
                            Type = "文件夹",
                            Size = "计算中...",
                            ModifiedDate = dirInfo.LastWriteTime.ToString("yyyy-MM-dd HH:mm"),
                            CreatedTime = FileSystemItem.FormatTimeAgo(dirInfo.CreationTime),
                            IsDirectory = true
                        };
                        // 文件夹通常没有备注，但为了统一处理也设置
                        item.Notes = "";
                        return item;
                    }).ToList();

                // 加载文件
                var files = Directory.GetFiles(_currentPath)
                    .Select(f =>
                    {
                        var fileInfo = new FileInfo(f);
                        return new FileSystemItem
                        {
                            Name = Path.GetFileName(f),
                            Path = f,
                            Type = Path.GetExtension(f),
                            Size = FormatFileSize(fileInfo.Length),
                            ModifiedDate = fileInfo.LastWriteTime.ToString("yyyy-MM-dd HH:mm"),
                            CreatedTime = FileSystemItem.FormatTimeAgo(fileInfo.CreationTime),
                            IsDirectory = false
                        };
                    }).ToList();

                _currentFiles.AddRange(directories);
                _currentFiles.AddRange(files);

                // 加载标签信息和备注
                foreach (var item in _currentFiles)
                {
                    var tags = DatabaseManager.GetFileTags(item.Path);
                    item.Tags = string.Join(", ", tags.Select(t => t.Name));
                    
                    // 加载备注的第一行
                    var notes = DatabaseManager.GetFileNotes(item.Path);
                    if (!string.IsNullOrEmpty(notes))
                    {
                        // 获取第一行，如果包含换行符则只取第一行
                        var firstLine = notes.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "";
                        // 限制显示长度，避免过长
                        item.Notes = firstLine.Length > 100 ? firstLine.Substring(0, 100) + "..." : firstLine;
                    }
                    else
                    {
                        item.Notes = "";
                    }
                }

                FilesListView.ItemsSource = _currentFiles;

                // 异步计算文件夹大小
                foreach (var dir in directories)
                {
                    var path = dir.Path;
                    System.Threading.Tasks.Task.Run(() =>
                    {
                        try
                        {
                            var size = CalculateDirectorySize(path);
                            Dispatcher.Invoke(() =>
                            {
                                var item = _currentFiles.FirstOrDefault(f => f.Path == path);
                                if (item != null)
                                {
                                    item.Size = FormatFileSize(size);
                                    // 触发UI更新
                                    var collectionView = System.Windows.Data.CollectionViewSource.GetDefaultView(FilesListView.ItemsSource);
                                    collectionView?.Refresh();
                                }
                            });
                        }
                        catch { }
                    });
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"无法加载文件: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private long CalculateDirectorySize(string directory)
        {
            long size = 0;
            try
            {
                var dirInfo = new DirectoryInfo(directory);
                // 获取文件大小
                foreach (var file in dirInfo.GetFiles("*", SearchOption.AllDirectories))
                {
                    try
                    {
                        size += file.Length;
                    }
                    catch { }
                }
            }
            catch { }
            return size;
        }

        private string FormatFileSize(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB", "TB" };
            double len = bytes;
            int order = 0;
            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len = len / 1024;
            }
            return $"{len:0.##} {sizes[order]}";
        }

        #endregion

        #region 事件处理

        private void NavigateBack_Click(object sender, RoutedEventArgs e)
        {
            if (_currentHistoryIndex > 0)
            {
                _currentHistoryIndex--;
                _currentPath = _navigationHistory[_currentHistoryIndex];
                ClearFilter();
                LoadCurrentDirectory();
            }
        }

        private void NavigateForward_Click(object sender, RoutedEventArgs e)
        {
            if (_currentHistoryIndex < _navigationHistory.Count - 1)
            {
                _currentHistoryIndex++;
                _currentPath = _navigationHistory[_currentHistoryIndex];
                ClearFilter();
                LoadCurrentDirectory();
            }
        }

        private void NavigateUp_Click(object sender, RoutedEventArgs e)
        {
            var parent = Directory.GetParent(_currentPath);
            if (parent != null)
            {
                NavigateToPath(parent.FullName);
            }
        }

        private void AddressTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            // 地址栏文本变化时的处理
        }

        private void AddressTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                var path = AddressTextBox.Text.Trim();
                if (Directory.Exists(path))
                {
                    NavigateToPath(path);
                }
                else
                {
                    MessageBox.Show("指定的路径不存在", "错误", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
        }

        private void Refresh_Click(object sender, RoutedEventArgs e)
        {
            ClearFilter();
            LoadCurrentDirectory();
        }

        private void ClearFilter_Click(object sender, RoutedEventArgs e)
        {
            ClearFilter();
            LoadCurrentDirectory();
        }

        private void ClearFilter()
        {
            // 清除过滤状态，恢复正常的文件浏览
            _currentFiles.Clear();
            FilesListView.ItemsSource = null;
        }

        private void FilesListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (FilesListView.SelectedItem is FileSystemItem selectedItem)
            {
                ShowFileInfo(selectedItem);
                LoadFilePreview(selectedItem);
                LoadFileNotes(selectedItem);
            }
        }

        private void FilesListView_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (FilesListView.SelectedItem is FileSystemItem selectedItem)
            {
                if (selectedItem.IsDirectory)
                {
                    NavigateToPath(selectedItem.Path);
                }
                else
                {
                    // 打开文件
                    try
                    {
                        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                        {
                            FileName = selectedItem.Path,
                            UseShellExecute = true
                        });
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"无法打开文件: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
        }
        
        private void RightPanel_PreviewMiddleClickRequested(object sender, MouseButtonEventArgs e)
        {
            // 预览区中键打开文件
            if (FilesListView.SelectedItem is FileSystemItem selectedItem && !selectedItem.IsDirectory)
            {
                try
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = selectedItem.Path,
                        UseShellExecute = true
                    });
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"无法打开文件: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
        
        private void MainWindow_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            // 空格键触发 QuickLook 预览
            if (e.Key == Key.Space)
            {
                // 检查是否有选中的文件
                if (FilesListView.SelectedItem is FileSystemItem selectedItem && !selectedItem.IsDirectory)
                {
                    // 检查 QuickLook 是否安装
                    if (OoiMRR.Previews.PreviewHelper.IsQuickLookInstalled())
                    {
                        try
                        {
                            var quickLookPath = OoiMRR.Previews.PreviewHelper.GetQuickLookPath();
                            if (!string.IsNullOrEmpty(quickLookPath))
                            {
                                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                                {
                                    FileName = quickLookPath,
                                    Arguments = $@"""{selectedItem.Path}""",
                                    UseShellExecute = false
                                });
                                e.Handled = true; // 标记事件已处理
                            }
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show($"无法启动 QuickLook: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                        }
                    }
                }
            }
        }

        private void MainWindow_KeyDown(object sender, KeyEventArgs e)
        {
            // 空格键触发 QuickLook 预览
            if (e.Key == Key.Space)
            {
                // 检查是否有选中的文件
                if (FilesListView.SelectedItem is FileSystemItem selectedItem && !selectedItem.IsDirectory)
                {
                    // 检查 QuickLook 是否安装
                    if (OoiMRR.Previews.PreviewHelper.IsQuickLookInstalled())
                    {
                        try
                        {
                            var quickLookPath = OoiMRR.Previews.PreviewHelper.GetQuickLookPath();
                            if (!string.IsNullOrEmpty(quickLookPath))
                            {
                                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                                {
                                    FileName = quickLookPath,
                                    Arguments = $@"""{selectedItem.Path}""",
                                    UseShellExecute = false
                                });
                                e.Handled = true; // 标记事件已处理
                            }
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show($"无法启动 QuickLook: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                        }
                    }
                }
            }
        }

        private void RightPanel_PreviewOpenFileRequested(object sender, string filePath)
        {
            // 预览区打开文件请求 - 在当前预览区显示文件内容
            if (!string.IsNullOrEmpty(filePath) && File.Exists(filePath))
            {
                LoadFilePreview(new FileSystemItem
                {
                    Path = filePath,
                    Name = System.IO.Path.GetFileName(filePath),
                    IsDirectory = false
                });
            }
        }

        #endregion

        #region 文件信息显示

        private void ShowFileInfo(FileSystemItem item)
        {
            // 恢复列2的详细信息显示
            if (FileInfoPanel == null) return;
            
            FileInfoPanel.Children.Clear();

            if (item.IsDirectory)
            {
                // 文件夹详细信息
                try
                {
                    var files = Directory.GetFiles(item.Path);
                    var directories = Directory.GetDirectories(item.Path);
                    long totalSize = files.Sum(f => new FileInfo(f).Length);

                    var infoItems = new[]
                    {
                        ("名称", item.Name),
                        ("路径", item.Path),
                        ("类型", "文件夹"),
                        ("文件数", files.Length.ToString()),
                        ("文件夹数", directories.Length.ToString()),
                        ("总大小", FormatFileSize(totalSize)),
                        ("修改日期", item.ModifiedDate),
                        ("标签", item.Tags)
                    };

                    foreach (var (label, value) in infoItems)
                    {
                        var panel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 2, 0, 2) };
                        panel.Children.Add(new TextBlock { Text = $"{label}: ", FontWeight = FontWeights.Bold, Width = 80 });
                        panel.Children.Add(new TextBlock { Text = value, TextWrapping = TextWrapping.Wrap });
                        FileInfoPanel.Children.Add(panel);
                    }
                }
                catch (Exception ex)
                {
                    var errorPanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 2, 0, 2) };
                    errorPanel.Children.Add(new TextBlock { Text = "错误: ", FontWeight = FontWeights.Bold, Width = 80 });
                    errorPanel.Children.Add(new TextBlock { Text = ex.Message, TextWrapping = TextWrapping.Wrap, Foreground = System.Windows.Media.Brushes.Red });
                    FileInfoPanel.Children.Add(errorPanel);
                }
            }
            else
            {
                // 文件详细信息
                var infoItems = new[]
                {
                    ("名称", item.Name),
                    ("路径", item.Path),
                    ("类型", item.Type),
                    ("大小", item.Size),
                    ("修改日期", item.ModifiedDate),
                    ("标签", item.Tags)
                };

                foreach (var (label, value) in infoItems)
                {
                    var panel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 2, 0, 2) };
                    panel.Children.Add(new TextBlock { Text = $"{label}: ", FontWeight = FontWeights.Bold, Width = 80 });
                    panel.Children.Add(new TextBlock { Text = value, TextWrapping = TextWrapping.Wrap });
                    FileInfoPanel.Children.Add(panel);
                }
            }
        }

        private void LoadFilePreview(FileSystemItem item)
        {
            if (RightPanel?.PreviewGrid == null) return;
            
            RightPanel.PreviewGrid.Children.Clear();

            try
            {
                // 设置刷新回调
                OoiMRR.Previews.PreviewFactory.OnFileListRefreshRequested = () =>
                {
                    Dispatcher.Invoke(() =>
                    {
                        LoadCurrentDirectory();
                    });
                };

                // PreviewFactory 会自动处理文件夹和文件
                var previewElement = OoiMRR.Previews.PreviewFactory.CreatePreview(item.Path);
                RightPanel.PreviewGrid.Children.Add(previewElement);
                
                // 为预览元素中的按钮绑定事件
                AttachPreviewButtonEvents(previewElement, item.Path);
            }
            catch (Exception ex)
            {
                RightPanel.PreviewGrid.Children.Add(new TextBlock 
                { 
                    Text = $"预览失败: {ex.Message}", 
                    HorizontalAlignment = HorizontalAlignment.Center, 
                    VerticalAlignment = VerticalAlignment.Center,
                    Foreground = System.Windows.Media.Brushes.Red
                });
            }
        }
        
        private void AttachPreviewButtonEvents(UIElement element, string filePath)
        {
            // 递归查找所有按钮并绑定事件
            if (element == null) return;
            
            var allElements = FindVisualChildren<Button>(element).ToList();
            foreach (var button in allElements)
            {
                if (button.Tag is string tagPath && tagPath == filePath)
                {
                    // 检查按钮内容确定是哪个按钮
                    string content = button.Content?.ToString() ?? "";
                    if (content.Contains("预览区打开"))
                    {
                        button.Click -= PreviewButton_Click;
                        button.Click += PreviewButton_Click;
                    }
                    // 系统打开已经绑定好了，不需要重新绑定
                }
            }
        }
        
        private void PreviewButton_Click(object sender, RoutedEventArgs e)
        {
            // 预览区打开按钮点击 - 在预览区中重新加载文件
            var button = sender as Button;
            var filePath = button?.Tag as string;
            if (!string.IsNullOrEmpty(filePath))
            {
                RightPanel_PreviewOpenFileRequested(sender, filePath);
            }
        }

        #endregion

        #region 备注功能

        private void LoadFileNotes(FileSystemItem item)
        {
            if (RightPanel?.NotesTextBox == null) return;
            
            if (item != null)
            {
                var notes = DatabaseManager.GetFileNotes(item.Path);
                RightPanel.NotesTextBox.Text = notes;
            }
            else
            {
                RightPanel.NotesTextBox.Text = "";
            }
        }

        private void NotesTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            // 备注文本变化时，实时更新列表中显示的备注
            if (RightPanel?.NotesTextBox == null) return;
            if (FilesListView.SelectedItem is FileSystemItem selectedItem)
            {
                var notesText = RightPanel.NotesTextBox.Text;
                // 更新备注的第一行显示
                if (!string.IsNullOrEmpty(notesText))
                {
                    var firstLine = notesText.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "";
                    selectedItem.Notes = firstLine.Length > 100 ? firstLine.Substring(0, 100) + "..." : firstLine;
                }
                else
                {
                    selectedItem.Notes = "";
                }
                
                // 刷新显示
                FilesListView.Items.Refresh();
            }
        }

        private async void NotesAutoSaved_Handler(object sender, RoutedEventArgs e)
        {
            if (RightPanel?.NotesTextBox == null) return;
            
            try
            {
                if (FilesListView.SelectedItem is FileSystemItem selectedItem)
                {
                    // 异步保存，提升性能
                    await DatabaseManager.SetFileNotesAsync(selectedItem.Path, RightPanel.NotesTextBox.Text);
                    
                    // 确保备注显示已更新
                    var notesText = RightPanel.NotesTextBox.Text;
                    if (!string.IsNullOrEmpty(notesText))
                    {
                        var firstLine = notesText.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "";
                        selectedItem.Notes = firstLine.Length > 100 ? firstLine.Substring(0, 100) + "..." : firstLine;
                    }
                    else
                    {
                        selectedItem.Notes = "";
                    }
                    
                    // 刷新显示
                    FilesListView.Items.Refresh();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"保存备注失败: {ex.Message}");
            }
        }

        #endregion

        #region 库功能

        private void LoadLibraries()
        {
            var libraries = DatabaseManager.GetAllLibraries();
            LibrariesListBox.ItemsSource = libraries;
        }

        private void AddLibrary_Click(object sender, RoutedEventArgs e)
        {
            // 使用简单的输入对话框
            var dialog = new LibraryDialog();
            if (dialog.ShowDialog() == true)
            {
                DatabaseManager.AddLibrary(dialog.LibraryName, dialog.LibraryPath);
                LoadLibraries();
            }
        }

        private void ManageLibraries_Click(object sender, RoutedEventArgs e)
        {
            // TODO: 实现库管理窗口
            MessageBox.Show("库管理功能待实现", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void LibrariesListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (LibrariesListBox.SelectedItem is Library selectedLibrary)
            {
                NavigateToPath(selectedLibrary.Path);
            }
        }

        #endregion

        #region 标签功能

        private void LoadTags()
        {
            var tags = DatabaseManager.GetAllTags();
            TagsPanel.Children.Clear();
            
            foreach (var tag in tags)
            {
                var button = new Button
                {
                    Content = tag.Name,
                    Margin = new Thickness(2, 2, 2, 2),
                    Padding = new Thickness(5, 2, 5, 2),
                    Background = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(tag.Color))
                };
                
                button.Click += (s, e) => FilterByTag(tag);
                TagsPanel.Children.Add(button);
            }
        }

        private void FilterByTag(Tag tag)
        {
            try
            {
                var filteredFiles = new List<FileSystemItem>();
                
                // 搜索当前目录及其子目录下的所有文件
                var allFiles = Directory.GetFiles(_currentPath, "*", SearchOption.AllDirectories)
                    .Concat(Directory.GetDirectories(_currentPath, "*", SearchOption.AllDirectories))
                    .Select(f =>
                    {
                        var item = new FileSystemItem
                        {
                            Name = Path.GetFileName(f),
                            Path = f,
                            Type = Directory.Exists(f) ? "文件夹" : Path.GetExtension(f),
                            Size = Directory.Exists(f) ? "" : FormatFileSize(new FileInfo(f).Length),
                            ModifiedDate = Directory.Exists(f) ? 
                                Directory.GetLastWriteTime(f).ToString("yyyy-MM-dd HH:mm") : 
                                File.GetLastWriteTime(f).ToString("yyyy-MM-dd HH:mm"),
                            IsDirectory = Directory.Exists(f)
                        };
                        // 初始化备注为空，后续会在需要时加载
                        item.Notes = "";
                        return item;
                    }).ToList();

                // 过滤出包含指定标签的文件，并加载标签和备注信息
                foreach (var file in allFiles)
                {
                    var fileTags = DatabaseManager.GetFileTags(file.Path);
                    if (fileTags.Any(t => t.Id == tag.Id))
                    {
                        file.Tags = string.Join(", ", fileTags.Select(t => t.Name));
                        
                        // 加载备注的第一行
                        var notes = DatabaseManager.GetFileNotes(file.Path);
                        if (!string.IsNullOrEmpty(notes))
                        {
                            var firstLine = notes.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "";
                            file.Notes = firstLine.Length > 100 ? firstLine.Substring(0, 100) + "..." : firstLine;
                        }
                        else
                        {
                            file.Notes = "";
                        }
                        
                        filteredFiles.Add(file);
                    }
                }

                // 更新文件列表
                _currentFiles = filteredFiles;
                FilesListView.ItemsSource = _currentFiles;
                
                // 更新地址栏显示
                AddressTextBox.Text = $"标签: {tag.Name}";
                
                // 清空面包屑导航
                BreadcrumbPanel.Children.Clear();
                BreadcrumbPanel.Children.Add(new TextBlock 
                { 
                    Text = $"当前显示标签: {tag.Name}", 
                    Margin = new Thickness(2, 2, 2, 2),
                    Foreground = System.Windows.Media.Brushes.Blue
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"过滤标签时发生错误: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void NewTag_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new TagDialog();
            if (dialog.ShowDialog() == true)
            {
                DatabaseManager.AddTag(dialog.TagName, dialog.TagColor);
                LoadTags();
            }
        }

        private void ManageTags_Click(object sender, RoutedEventArgs e)
        {
            // TODO: 实现标签管理窗口
            MessageBox.Show("标签管理功能待实现", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void AddTagToFile_Click(object sender, RoutedEventArgs e)
        {
            if (FilesListView.SelectedItem is FileSystemItem selectedItem)
            {
                var dialog = new TagSelectionDialog();
                if (dialog.ShowDialog() == true)
                {
                    foreach (var tagId in dialog.SelectedTagIds)
                    {
                        DatabaseManager.AddFileTag(selectedItem.Path, tagId);
                    }
                    LoadFiles(); // 刷新文件列表以显示新标签
                }
            }
            else
            {
                MessageBox.Show("请先选择一个文件", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        #endregion

        #region 搜索功能

        private void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            // 搜索文本变化时的处理
        }

        private void SearchTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                PerformSearch();
            }
        }

        private void Search_Click(object sender, RoutedEventArgs e)
        {
            PerformSearch();
        }

        private void PerformSearch()
        {
            var searchText = SearchTextBox.Text.Trim();
            if (string.IsNullOrEmpty(searchText))
            {
                SearchResultsListBox.ItemsSource = null;
                return;
            }

            var results = new List<FileSystemItem>();
            
            // 搜索当前目录下的文件
            try
            {
                var allFiles = Directory.GetFiles(_currentPath, "*", SearchOption.AllDirectories)
                    .Concat(Directory.GetDirectories(_currentPath, "*", SearchOption.AllDirectories))
                    .Select(f =>
                    {
                        var item = new FileSystemItem
                        {
                            Name = Path.GetFileName(f),
                            Path = f,
                            Type = Directory.Exists(f) ? "文件夹" : Path.GetExtension(f),
                            Size = Directory.Exists(f) ? "" : FormatFileSize(new FileInfo(f).Length),
                            ModifiedDate = Directory.Exists(f) ? 
                                Directory.GetLastWriteTime(f).ToString("yyyy-MM-dd HH:mm") : 
                                File.GetLastWriteTime(f).ToString("yyyy-MM-dd HH:mm"),
                            IsDirectory = Directory.Exists(f)
                        };
                        // 初始化备注为空，后续会在需要时加载
                        item.Notes = "";
                        return item;
                    }).ToList();

                foreach (var file in allFiles)
                {
                    bool matches = false;
                    
                    if (SearchInNames.IsChecked == true && file.Name.ToLower().Contains(searchText.ToLower()))
                        matches = true;
                    
                    if (SearchInTags.IsChecked == true)
                    {
                        var tags = DatabaseManager.GetFileTags(file.Path);
                        if (tags.Any(t => t.Name.ToLower().Contains(searchText.ToLower())))
                            matches = true;
                    }
                    
                    string notes = null;
                    if (SearchInNotes.IsChecked == true)
                    {
                        notes = DatabaseManager.GetFileNotes(file.Path);
                        if (notes.ToLower().Contains(searchText.ToLower()))
                            matches = true;
                    }
                    
                    if (matches)
                    {
                        var tags = DatabaseManager.GetFileTags(file.Path);
                        file.Tags = string.Join(", ", tags.Select(t => t.Name));
                        
                        // 加载备注的第一行用于显示
                        if (notes == null)
                        {
                            notes = DatabaseManager.GetFileNotes(file.Path);
                        }
                        if (!string.IsNullOrEmpty(notes))
                        {
                            var firstLine = notes.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "";
                            file.Notes = firstLine.Length > 100 ? firstLine.Substring(0, 100) + "..." : firstLine;
                        }
                        else
                        {
                            file.Notes = "";
                        }
                        
                        results.Add(file);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"搜索时发生错误: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }

            SearchResultsListBox.ItemsSource = results;
        }

        private void SearchResultsListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (SearchResultsListBox.SelectedItem is FileSystemItem selectedItem)
            {
                ShowFileInfo(selectedItem);
                LoadFilePreview(selectedItem);
                LoadFileNotes(selectedItem);
            }
        }

        #endregion

        #region 快速访问

        private void LoadQuickAccess()
        {
            QuickAccessPanel.Children.Clear();
            
            var quickAccessPaths = new[]
            {
                (Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "🖥️ 桌面"),
                (Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "📄 文档"),
                (Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "🖼️ 图片"),
                (Environment.GetFolderPath(Environment.SpecialFolder.MyMusic), "🎵 音乐"),
                (Environment.GetFolderPath(Environment.SpecialFolder.MyVideos), "🎬 视频"),
                (Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "👤 用户")
            };

            foreach (var (path, displayName) in quickAccessPaths)
            {
                if (Directory.Exists(path))
                {
                    var button = new Button
                    {
                        Content = displayName,
                        Style = (Style)FindResource("QuickAccessButtonStyle")
                    };
                    
                    button.Click += (s, e) => NavigateToPath(path);
                    QuickAccessPanel.Children.Add(button);
                }
            }
        }

        #endregion

        #region 驱动器功能

        private void LoadDrives()
        {
            DrivesPanel.Children.Clear();
            
            try
            {
                var drives = DriveInfo.GetDrives().Where(d => d.IsReady).ToList();
                
                foreach (var drive in drives)
                {
                    var button = new Button
                    {
                        Content = $"{drive.Name} ({drive.VolumeLabel})",
                        Style = (Style)FindResource("DriveButtonStyle"),
                        ToolTip = $"总空间: {FormatFileSize(drive.TotalSize)}\n可用空间: {FormatFileSize(drive.AvailableFreeSpace)}"
                    };
                    
                    button.Click += (s, e) => NavigateToPath(drive.Name);
                    DrivesPanel.Children.Add(button);
                }
            }
            catch (Exception ex)
            {
                var errorText = new TextBlock
                {
                    Text = $"无法加载驱动器: {ex.Message}",
                    Foreground = System.Windows.Media.Brushes.Red,
                    Margin = new Thickness(5)
                };
                DrivesPanel.Children.Add(errorText);
            }
        }

        #endregion

        #region 菜单事件

        private void NewFolder_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // 使用简单的输入对话框
                var dialog = new PathInputDialog
                {
                    Title = "新建文件夹",
                    PromptText = "请输入文件夹名称：",
                    InputText = "新建文件夹",
                    Owner = this
                };

                if (dialog.ShowDialog() == true)
                {
                    var folderName = dialog.InputText.Trim();
                    
                    // 验证文件夹名称
                    if (string.IsNullOrEmpty(folderName))
                    {
                        MessageBox.Show("文件夹名称不能为空", "错误", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }

                    // 检查非法字符
                    char[] invalidChars = Path.GetInvalidFileNameChars();
                    if (folderName.IndexOfAny(invalidChars) >= 0)
                    {
                        MessageBox.Show("文件夹名称包含非法字符", "错误", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }

                    var folderPath = Path.Combine(_currentPath, folderName);

                    // 如果已存在，自动添加序号
                    if (Directory.Exists(folderPath))
                    {
                        int counter = 2;
                        string newFolderName;
                        do
                        {
                            newFolderName = $"{folderName} ({counter})";
                            folderPath = Path.Combine(_currentPath, newFolderName);
                            counter++;
                        }
                        while (Directory.Exists(folderPath));
                    }

                    // 创建文件夹
                    Directory.CreateDirectory(folderPath);
                    
                    // 文件监视器会自动刷新列表
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"创建文件夹失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void NewFile_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // 显示文件类型选择菜单
                var contextMenu = new ContextMenu
                {
                    Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom,
                    PlacementTarget = sender as UIElement
                };
                
                // 常用文件类型列表
                var fileTypes = new[]
                {
                    ("📄 文本文件", ".txt"),
                    ("📝 Word 文档", ".docx"),
                    ("📊 Excel 表格", ".xlsx"),
                    ("📽️ PowerPoint", ".pptx"),
                    ("🖼️ PNG 图片", ".png"),
                    ("🖼️ JPEG 图片", ".jpg"),
                    ("🖼️ GIF 图片", ".gif"),
                    ("🖼️ BMP 图片", ".bmp"),
                    ("🖼️ SVG 矢量图", ".svg"),
                    ("💻 C# 代码", ".cs"),
                    ("🌐 HTML 网页", ".html"),
                    ("🎨 CSS 样式", ".css"),
                    ("⚡ JavaScript", ".js"),
                    ("🐍 Python", ".py"),
                    ("☕ Java", ".java"),
                    ("📋 JSON", ".json"),
                    ("📋 XML", ".xml"),
                    ("📋 Markdown", ".md"),
                    ("⚙️ 配置文件", ".ini"),
                    ("📦 批处理", ".bat"),
                    ("🔧 PowerShell", ".ps1")
                };

                foreach (var (name, extension) in fileTypes)
                {
                    var menuItem = new MenuItem
                    {
                        Header = name,
                        Tag = extension,
                        Padding = new Thickness(10, 5, 10, 5)
                    };
                    menuItem.Click += (s, args) =>
                    {
                        CreateNewFileWithExtension(extension);
                    };
                    contextMenu.Items.Add(menuItem);
                }

                // 添加分隔符和自定义选项
                contextMenu.Items.Add(new Separator());
                
                var customMenuItem = new MenuItem
                {
                    Header = "✏️ 自定义扩展名...",
                    Padding = new Thickness(10, 5, 10, 5)
                };
                customMenuItem.Click += (s, args) =>
                {
                    var dialog = new PathInputDialog
                    {
                        Title = "新建文件",
                        PromptText = "请输入文件扩展名（如 .txt）：",
                        InputText = ".txt",
                        Owner = this
                    };

                    if (dialog.ShowDialog() == true)
                    {
                        var extension = dialog.InputText.Trim();
                        if (!extension.StartsWith("."))
                        {
                            extension = "." + extension;
                        }
                        CreateNewFileWithExtension(extension);
                    }
                };
                contextMenu.Items.Add(customMenuItem);

                // 显示菜单
                contextMenu.IsOpen = true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"创建文件失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void CreateNewFileWithExtension(string extension)
        {
            try
            {
                // 根据扩展名生成文件名
                string baseFileName = $"新建文件{extension}";
                string filePath = Path.Combine(_currentPath, baseFileName);

                // 如果已存在，自动添加序号
                if (File.Exists(filePath))
                {
                    string fileNameWithoutExt = Path.GetFileNameWithoutExtension(baseFileName);
                    int counter = 2;
                    
                    do
                    {
                        string candidateFileName = $"{fileNameWithoutExt} ({counter}){extension}";
                        filePath = Path.Combine(_currentPath, candidateFileName);
                        counter++;
                    }
                    while (File.Exists(filePath));
                }

                // 根据文件类型创建合适的文件内容
                CreateFileWithProperFormat(filePath, extension.ToLower());
                
                // 文件监视器会自动刷新列表
            }
            catch (Exception ex)
            {
                MessageBox.Show($"创建文件失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void CreateFileWithProperFormat(string filePath, string extension)
        {
            switch (extension)
            {
                case ".docx":
                case ".xlsx":
                case ".pptx":
                    // Office 文件需要使用 COM 或库创建
                    CreateOfficeFile(filePath, extension);
                    break;

                case ".html":
                    File.WriteAllText(filePath, @"<!DOCTYPE html>
<html lang=""zh-CN"">
<head>
    <meta charset=""UTF-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
    <title>新建网页</title>
</head>
<body>
    <h1>Hello World</h1>
</body>
</html>");
                    break;

                case ".css":
                    File.WriteAllText(filePath, @"/* CSS Stylesheet */

body {
    margin: 0;
    padding: 0;
    font-family: Arial, sans-serif;
}
");
                    break;

                case ".js":
                    File.WriteAllText(filePath, @"// JavaScript

console.log('Hello World');
");
                    break;

                case ".cs":
                    File.WriteAllText(filePath, @"using System;

namespace MyNamespace
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine(""Hello World"");
        }
    }
}
");
                    break;

                case ".py":
                    File.WriteAllText(filePath, @"# Python Script

def main():
    print('Hello World')

if __name__ == '__main__':
    main()
");
                    break;

                case ".java":
                    string className = Path.GetFileNameWithoutExtension(filePath).Replace(" ", "_");
                    File.WriteAllText(filePath, $@"public class {className} {{
    public static void main(String[] args) {{
        System.out.println(""Hello World"");
    }}
}}
");
                    break;

                case ".json":
                    File.WriteAllText(filePath, @"{
    ""name"": ""example"",
    ""version"": ""1.0.0""
}
");
                    break;

                case ".xml":
                    File.WriteAllText(filePath, @"<?xml version=""1.0"" encoding=""UTF-8""?>
<root>
    <item>Example</item>
</root>
");
                    break;

                case ".md":
                    File.WriteAllText(filePath, @"# 标题

这是一个 Markdown 文档。

## 二级标题

- 列表项 1
- 列表项 2
");
                    break;

                case ".ini":
                    File.WriteAllText(filePath, @"[Settings]
Key=Value
");
                    break;

                case ".bat":
                    File.WriteAllText(filePath, @"@echo off
echo Hello World
pause
");
                    break;

                case ".ps1":
                    File.WriteAllText(filePath, @"# PowerShell Script

Write-Host ""Hello World""
");
                    break;

                case ".png":
                case ".jpg":
                case ".jpeg":
                case ".bmp":
                case ".gif":
                    // 创建一个简单的图片文件
                    CreateImageFile(filePath, extension);
                    break;

                case ".svg":
                    // 创建一个简单的SVG文件
                    File.WriteAllText(filePath, @"<?xml version=""1.0"" encoding=""UTF-8""?>
<svg width=""500"" height=""500"" xmlns=""http://www.w3.org/2000/svg"">
    <rect width=""500"" height=""500"" fill=""#FFFFFF""/>
</svg>");
                    break;

                default:
                    // 其他文件类型创建空文件
                    File.WriteAllText(filePath, string.Empty);
                    break;
            }
        }

        private void CreateImageFile(string filePath, string extension)
        {
            try
            {
                // 创建一个500x500的空白图片
                using (var bitmap = new Bitmap(500, 500))
                {
                    using (var graphics = Graphics.FromImage(bitmap))
                    {
                        // 填充白色背景
                        graphics.Clear(System.Drawing.Color.White);
                    }

                    // 根据扩展名保存为相应格式
                    switch (extension.ToLower())
                    {
                        case ".png":
                            bitmap.Save(filePath, System.Drawing.Imaging.ImageFormat.Png);
                            break;
                        case ".jpg":
                        case ".jpeg":
                            bitmap.Save(filePath, System.Drawing.Imaging.ImageFormat.Jpeg);
                            break;
                        case ".bmp":
                            bitmap.Save(filePath, System.Drawing.Imaging.ImageFormat.Bmp);
                            break;
                        case ".gif":
                            bitmap.Save(filePath, System.Drawing.Imaging.ImageFormat.Gif);
                            break;
                        default:
                            bitmap.Save(filePath, System.Drawing.Imaging.ImageFormat.Png);
                            break;
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"创建图片文件失败: {ex.Message}\n将创建空文件", "警告", MessageBoxButton.OK, MessageBoxImage.Warning);
                File.WriteAllText(filePath, string.Empty);
            }
        }

        private void CreateOfficeFile(string filePath, string extension)
        {
            try
            {
                dynamic app = null;
                dynamic doc = null;

                switch (extension)
                {
                    case ".docx":
                        try
                        {
                            var wordType = Type.GetTypeFromProgID("Word.Application");
                            if (wordType == null)
                            {
                                MessageBox.Show("未检测到 Microsoft Word，将创建空文件", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                                File.WriteAllText(filePath, string.Empty);
                                return;
                            }

                            app = Activator.CreateInstance(wordType);
                            app.Visible = false;
                            app.DisplayAlerts = 0;
                            doc = app.Documents.Add();
                            doc.SaveAs2(filePath);
                            doc.Close(false);
                        }
                        finally
                        {
                            if (app != null)
                            {
                                app.Quit(false);
                                System.Runtime.InteropServices.Marshal.ReleaseComObject(app);
                            }
                        }
                        break;

                    case ".xlsx":
                        try
                        {
                            var excelType = Type.GetTypeFromProgID("Excel.Application");
                            if (excelType == null)
                            {
                                MessageBox.Show("未检测到 Microsoft Excel，将创建空文件", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                                File.WriteAllText(filePath, string.Empty);
                                return;
                            }

                            app = Activator.CreateInstance(excelType);
                            app.Visible = false;
                            app.DisplayAlerts = false;
                            doc = app.Workbooks.Add();
                            doc.SaveAs(filePath);
                            doc.Close(false);
                        }
                        finally
                        {
                            if (app != null)
                            {
                                app.Quit();
                                System.Runtime.InteropServices.Marshal.ReleaseComObject(app);
                            }
                        }
                        break;

                    case ".pptx":
                        try
                        {
                            var pptType = Type.GetTypeFromProgID("PowerPoint.Application");
                            if (pptType == null)
                            {
                                MessageBox.Show("未检测到 Microsoft PowerPoint，将创建空文件", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                                File.WriteAllText(filePath, string.Empty);
                                return;
                            }

                            app = Activator.CreateInstance(pptType);
                            doc = app.Presentations.Add();
                            doc.SaveAs(filePath);
                            doc.Close();
                        }
                        finally
                        {
                            if (app != null)
                            {
                                app.Quit();
                                System.Runtime.InteropServices.Marshal.ReleaseComObject(app);
                            }
                        }
                        break;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"创建 Office 文件失败: {ex.Message}\n将创建空文件", "警告", MessageBoxButton.OK, MessageBoxImage.Warning);
                File.WriteAllText(filePath, string.Empty);
            }
        }

        private void Exit_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }


        private void SelectAll_Click(object sender, RoutedEventArgs e)
        {
            FilesListView.SelectAll();
        }

        private void ViewLargeIcons_Click(object sender, RoutedEventArgs e)
        {
            // TODO: 实现大图标视图
            MessageBox.Show("大图标视图功能待实现", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void ViewSmallIcons_Click(object sender, RoutedEventArgs e)
        {
            // TODO: 实现小图标视图
            MessageBox.Show("小图标视图功能待实现", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void ViewList_Click(object sender, RoutedEventArgs e)
        {
            // TODO: 实现列表视图
            MessageBox.Show("列表视图功能待实现", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void ViewDetails_Click(object sender, RoutedEventArgs e)
        {
            // TODO: 实现详细信息视图
            MessageBox.Show("详细信息视图功能待实现", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void Settings_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("设置已包含导入/导出配置功能，请使用工具菜单中的相应项。", "设置", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void ImportConfig_Click(object sender, RoutedEventArgs e)
        {
            var ofd = new OpenFileDialog
            {
                Filter = "配置文件 (*.json)|*.json|所有文件 (*.*)|*.*"
            };
            if (ofd.ShowDialog() == true)
            {
                try
                {
                    ConfigManager.Import(ofd.FileName);
                    _config = ConfigManager.Load();
                    ApplyConfig(_config);
                    MessageBox.Show("配置已导入并应用。", "成功", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"导入失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void ExportConfig_Click(object sender, RoutedEventArgs e)
        {
            var sfd = new SaveFileDialog
            {
                FileName = "config.json",
                Filter = "配置文件 (*.json)|*.json|所有文件 (*.*)|*.*"
            };
            if (sfd.ShowDialog() == true)
            {
                try
                {
                    SaveCurrentConfig();
                    ConfigManager.Export(sfd.FileName);
                    MessageBox.Show("配置已导出。", "成功", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"导出失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void EditNotes_Click(object sender, RoutedEventArgs e)
        {
            if (FilesListView.SelectedItem is FileSystemItem selectedItem)
            {
                RightPanel?.NotesTextBox?.Focus();
            }
            else
            {
                MessageBox.Show("请先选择一个文件", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void About_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("OoiMRR - 文件资源管理器\n版本 1.0\n\n一个功能强大的Windows文件管理工具", 
                "关于", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        #endregion

        #region 新增按钮事件处理

        private void BatchAddTags_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("批量添加标签功能待实现", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void TagStatistics_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("标签统计功能待实现", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void ImportLibrary_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("导入库功能待实现", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void ExportLibrary_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("导出库功能待实现", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void AdvancedSearch_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("高级搜索功能待实现", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void SaveSearch_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("保存搜索功能待实现", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void SearchHistory_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("搜索历史功能待实现", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void ClearSearchHistory_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("清除搜索历史功能待实现", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void AddFileToLibrary_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("添加文件到库功能待实现", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void EditLibrary_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("编辑库功能待实现", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void WindowMinimize_Click(object sender, RoutedEventArgs e)
        {
            this.WindowState = WindowState.Minimized;
        }

        private bool _isPseudoMaximized = false;
        private Rect _restoreBounds;

        private void WindowMaximize_Click(object sender, RoutedEventArgs e)
        {
            // 使用系统最大化并限制到工作区，确保铺满且不遮挡任务栏
            if (_isPseudoMaximized)
            {
                // 还原到最后一次记录值
                this.WindowState = WindowState.Normal;
                this.Left = _restoreBounds.Left;
                this.Top = _restoreBounds.Top;
                this.Width = _restoreBounds.Width;
                this.Height = _restoreBounds.Height;
                _isPseudoMaximized = false;
                this.ResizeMode = ResizeMode.CanResize;
                
                // 恢复窗口边框
                var hwnd = new WindowInteropHelper(this).Handle;
                var margins = new NativeMethods.MARGINS();
                NativeMethods.DwmExtendFrameIntoClientArea(hwnd, ref margins);
            }
            else
            {
                // 记录还原尺寸
                _restoreBounds = new Rect(this.Left, this.Top, this.Width, this.Height);
                var wa = GetCurrentMonitorWorkAreaDIPs();
                // 最大化时，使用工作区尺寸，不遮挡任务栏
                this.WindowState = WindowState.Normal;
                this.Left = wa.Left;
                this.Top = wa.Top;
                this.Width = wa.Width;
                this.Height = wa.Height;
                _isPseudoMaximized = true;
                this.ResizeMode = ResizeMode.NoResize;
                
                // 移除窗口边框，将客户区扩展到整个窗口
                var hwnd = new WindowInteropHelper(this).Handle;
                var margins = new NativeMethods.MARGINS { cxLeftWidth = 0, cxRightWidth = 0, cyTopHeight = 0, cyBottomHeight = 0 };
                NativeMethods.DwmExtendFrameIntoClientArea(hwnd, ref margins);
            }
            UpdateWindowStateUI();
        }

        private void WindowClose_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void ListView_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            AdjustListViewColumnWidths();
        }

        private void AdjustListViewColumnWidths()
        {
            if (FilesListView == null) return;

            var gridView = FilesListView.View as GridView;
            if (gridView == null || gridView.Columns.Count < 5) return;

            // 获取可用宽度（减去名称、修改日期、标签列的宽度和边距）
            double availableWidth = FilesListView.ActualWidth - 50; // 减去一些边距和滚动条

            // 名称列固定宽度
            double nameColWidth = 200;
            // 修改日期列固定宽度
            double modifiedDateColWidth = 150;
            // 标签列固定宽度
            double tagsColWidth = 150;

            // 计算剩余可用宽度
            double remainingWidth = availableWidth - nameColWidth - modifiedDateColWidth - tagsColWidth;

            // 设置最小宽度
            double minSizeColWidth = 80;
            double minTypeColWidth = 80;

            // 获取当前大小列和类型列的宽度
            double sizeColWidth = 100;
            double typeColWidth = 100;

            if (remainingWidth < sizeColWidth + typeColWidth)
            {
                // 空间不足，需要压缩
                if (remainingWidth >= minSizeColWidth + minTypeColWidth)
                {
                    // 可以容纳最小宽度，先压缩类型列
                    double minTotal = minSizeColWidth + minTypeColWidth;
                    double extraWidth = remainingWidth - minTotal;
                    
                    // 先压缩类型列
                    double typeShrink = Math.Max(0, typeColWidth - minTypeColWidth);
                    double typeCanShrink = Math.Min(typeShrink, extraWidth);
                    typeColWidth -= typeCanShrink;
                    
                    // 如果还有空间，给大小列
                    if (typeCanShrink < extraWidth)
                    {
                        sizeColWidth = minSizeColWidth + (extraWidth - typeCanShrink);
                    }
                    else
                    {
                        sizeColWidth = minSizeColWidth;
                    }
                }
                else
                {
                    // 空间不足，都设置为最小宽度
                    sizeColWidth = minSizeColWidth;
                    typeColWidth = minTypeColWidth;
                }
            }

            gridView.Columns[0].Width = nameColWidth;
            gridView.Columns[1].Width = sizeColWidth;
            gridView.Columns[2].Width = typeColWidth;
            gridView.Columns[3].Width = modifiedDateColWidth;
            gridView.Columns[4].Width = tagsColWidth;
        }

        private void AdjustColumnWidths()
        {
            // 获取可用总宽度（去掉两个垂直分割器）
            if (RootGrid == null) return;
            double total = RootGrid.ActualWidth - 12; // 两个6像素分割器

            // 当前实际宽度
            // 列1和列2使用固定宽度值（如果已设置），否则使用ActualWidth
            // 列3始终使用固定宽度360
            double left = ColLeft.Width.IsAbsolute ? ColLeft.Width.Value : ColLeft.ActualWidth;
            double center = ColCenter.Width.IsAbsolute ? ColCenter.Width.Value : ColCenter.ActualWidth;
            double right = ColRight.Width.IsAbsolute ? ColRight.Width.Value : ColRight.ActualWidth; // 列3使用实际宽度

            double minLeft = ColLeft.MinWidth;
            double minCenter = ColCenter.MinWidth;
            double minRight = ColRight.MinWidth;

            double sum = left + center + right;
            
            // 确保窗口最小宽度不小于三列MinWidth总和
            var minTotalWidth = minLeft + minCenter + minRight + 12;
            if (this.MinWidth < minTotalWidth)
            {
                this.MinWidth = minTotalWidth;
            }
            
            // 如果总宽度小于最小宽度总和，强制设置窗口宽度
            if (total < minTotalWidth)
            {
                // 按比例分配最小宽度，但列3优先保持最小宽度
                double scale = total / minTotalWidth;
                left = minLeft * scale;
                center = minCenter * scale;
                
                // 如果还有空间不够，继续压缩列2和列1
                double needed = left + center + minRight;
                if (needed > total)
                {
                    double shortage = needed - total;
                    double canShrinkCenter = center - minCenter;
                    if (canShrinkCenter >= shortage)
                    {
                        center -= shortage;
                    }
                    else
                    {
                        double remaining = shortage - canShrinkCenter;
                        center = minCenter;
                        left = Math.Max(minLeft, left - remaining);
                    }
                }
                
                // 应用像素宽度，确保不低于最小宽度
                ColLeft.Width = new GridLength(Math.Max(minLeft, left));
                ColCenter.Width = new GridLength(Math.Max(minCenter, center));
                // 列3始终使用Star模式，确保顶到窗口右边缘
                ColRight.Width = new GridLength(1, GridUnitType.Star);
                return;
            }
            else if (total < sum)
            {
                // 需要压缩
                double shortage = sum - total;

                // 先从列2收缩
                    double canShrinkCenter = Math.Max(0, center - minCenter);
                    double shrinkCenter = Math.Min(canShrinkCenter, shortage);
                    center -= shrinkCenter;
                    shortage -= shrinkCenter;

                // 然后列1（保持固定宽度，只在必要时压缩）
                if (shortage > 0)
                {
                    double canShrinkLeft = Math.Max(0, left - minLeft);
                    double shrinkLeft = Math.Min(canShrinkLeft, shortage);
                    left -= shrinkLeft;
                    shortage -= shrinkLeft;
                    // 更新后保存列1宽度，确保后续使用固定值
                    if (left > minLeft)
                    {
                        // 可以在这里保存列1的固定宽度
                    }
                }

                // 应用像素宽度，确保不低于最小宽度
                ColLeft.Width = new GridLength(Math.Max(minLeft, left));
                ColCenter.Width = new GridLength(Math.Max(minCenter, center));
                // 列3始终使用Star模式，确保顶到窗口右边缘
                ColRight.Width = new GridLength(1, GridUnitType.Star);
            }
            else
            {
                // 宽度足够，不需要压缩
                // 压缩顺序：列3 -> 列2 -> 列1
                // 确保所有列都使用固定像素宽度（不使用Star），确保MinWidth生效
                
                // 确保列1（左侧列）保持固定宽度，不随窗口变化
                // 优先使用Width.Value（如果已设置固定值），否则使用ActualWidth，但不小于最小宽度
                double leftWidth;
                if (ColLeft.Width.IsAbsolute && ColLeft.Width.Value > 0)
                {
                    // 如果已有固定宽度值，使用该值（但不小于最小宽度）
                    leftWidth = Math.Max(minLeft, ColLeft.Width.Value);
                }
                else
                {
                    // 否则使用实际宽度，但不小于最小宽度
                    leftWidth = Math.Max(minLeft, ColLeft.ActualWidth > 0 ? ColLeft.ActualWidth : minLeft);
                }
                
                // 确保列2（中间列）保持固定宽度，不随窗口变化
                // 如果列2当前是Star模式，使用ActualWidth；否则使用当前计算值，但不小于最小宽度
                double centerWidth;
                if (ColCenter.Width.IsStar)
                {
                    // 如果是Star模式，使用实际宽度，但不小于最小宽度
                    centerWidth = Math.Max(minCenter, ColCenter.ActualWidth > 0 ? ColCenter.ActualWidth : minCenter);
                }
                else
                {
                    // 如果不是Star模式，使用当前宽度，但不小于最小宽度
                    centerWidth = Math.Max(minCenter, center > 0 ? center : minCenter);
                }
                
                // 列1和列2使用固定像素宽度，列3使用Star模式自动填充剩余空间
                ColLeft.Width = new GridLength(leftWidth);
                ColCenter.Width = new GridLength(centerWidth);
                ColRight.Width = new GridLength(1, GridUnitType.Star);
            }
        }

        private void ForceColumnWidthsToFixed()
        {
            // 强制将列2和列3设置为固定宽度（不使用Star模式），确保MinWidth生效
            if (RootGrid == null || ColCenter == null || ColRight == null) return;
            
            double centerActual = ColCenter.ActualWidth;
            double rightActual = ColRight.ActualWidth;
            double minCenter = ColCenter.MinWidth;
            double minRight = ColRight.MinWidth;
            
            Debug.WriteLine($"[ForceColumnWidthsToFixed] 开始检查");
            Debug.WriteLine($"[ForceColumnWidthsToFixed] ColCenter: IsStar={ColCenter.Width.IsStar}, ActualWidth={centerActual}, MinWidth={minCenter}, Width.Value={ColCenter.Width.Value}");
            Debug.WriteLine($"[ForceColumnWidthsToFixed] ColRight: IsStar={ColRight.Width.IsStar}, ActualWidth={rightActual}, MinWidth={minRight}, Width.Value={ColRight.Width.Value}");
            
            bool needFix = false;
            
            // 检查列2：如果是Star模式，或者宽度小于最小宽度，强制改为固定宽度
            if (ColCenter.Width.IsStar || (centerActual > 0 && centerActual < minCenter))
            {
                double newCenterWidth = Math.Max(minCenter, centerActual > 0 ? centerActual : minCenter);
                ColCenter.Width = new GridLength(newCenterWidth);
                needFix = true;
                Debug.WriteLine($"[ForceColumnWidthsToFixed] 修复列2: IsStar={ColCenter.Width.IsStar}, 实际宽度={centerActual}, 设置为 {newCenterWidth}");
            }
            else if (!ColCenter.Width.IsStar && ColCenter.Width.Value < minCenter)
            {
                ColCenter.Width = new GridLength(minCenter);
                needFix = true;
                Debug.WriteLine($"[ForceColumnWidthsToFixed] 修复列2宽度值: {ColCenter.Width.Value} < {minCenter}, 设置为 {minCenter}");
            }
            
            // 检查列3：如果宽度小于最小宽度，修复它；否则保持Star模式或当前宽度
            // 列3应该使用剩余空间（Star模式）
            if (ColRight.Width.IsStar)
            {
                // Star模式是好的，让AdjustColumnWidths来处理
                Debug.WriteLine($"[ForceColumnWidthsToFixed] 列3是Star模式，这是正常的");
            }
            else if (rightActual < minRight)
            {
                // 如果列3宽度小于最小宽度，改为Star模式让它使用剩余空间
                ColRight.Width = new GridLength(1, GridUnitType.Star);
                needFix = true;
                Debug.WriteLine($"[ForceColumnWidthsToFixed] 列3宽度小于最小宽度，改为Star模式使用剩余空间");
            }
            else if (!ColRight.Width.IsStar)
            {
                // 如果列3不是Star模式，改为Star模式让它使用剩余空间
                ColRight.Width = new GridLength(1, GridUnitType.Star);
                needFix = true;
                Debug.WriteLine($"[ForceColumnWidthsToFixed] 列3不是Star模式，改为Star模式使用剩余空间");
            }
            
            // 如果修复了列宽，触发布局更新
            if (needFix)
            {
                Debug.WriteLine($"[ForceColumnWidthsToFixed] 触发布局更新");
                this.UpdateLayout();
            }
            else
            {
                Debug.WriteLine($"[ForceColumnWidthsToFixed] 无需修复");
            }
        }

        private void EnsureColumnMinWidths()
        {
            // 强制检查并应用所有列的最小宽度约束
            if (RootGrid == null) return;
            
            // 先强制设置为固定宽度（不使用Star模式）
            ForceColumnWidthsToFixed();
            
            // 获取当前实际宽度
            double leftActual = ColLeft.ActualWidth;
            double centerActual = ColCenter.ActualWidth;
            double rightActual = ColRight.ActualWidth;
            
            double minLeft = ColLeft.MinWidth;
            double minCenter = ColCenter.MinWidth;
            double minRight = ColRight.MinWidth;
            
            bool needAdjust = false;
            
            // 检查列2（中间列）是否小于最小宽度
            if (centerActual < minCenter)
            {
                ColCenter.Width = new GridLength(minCenter);
                needAdjust = true;
            }
            
            // 检查列3（右侧面板）是否小于最小宽度
            if (rightActual < minRight)
            {
                // 计算可用空间
                double totalWidth = RootGrid.ActualWidth - 12; // 减去两个分割器宽度
                double availableWidth = totalWidth - minLeft - (centerActual >= minCenter ? centerActual : minCenter);
                
                // 确保右侧面板至少达到最小宽度
                if (availableWidth >= minRight)
                {
                    ColRight.Width = new GridLength(minRight);
                    needAdjust = true;
                }
                else
                {
                    // 空间不足，需要重新分配
                    AdjustColumnWidths();
                    return;
                }
            }
            
            // 检查列1（左侧列）
            if (leftActual < minLeft)
            {
                ColLeft.Width = new GridLength(minLeft);
                needAdjust = true;
            }
            
            // 如果需要调整，触发布局更新
            if (needAdjust)
            {
                this.UpdateLayout();
            }
        }

        private void UpdateWindowStateUI()
        {
            bool isMax = _isPseudoMaximized;

            // 更新主窗口右上角按钮图标
            if (TitleBarMaxRestoreButton != null)
            {
                // Segoe MDL2 Assets: Maximize E922, Restore E923
                TitleBarMaxRestoreButton.Content = isMax ? "\uE923" : "\uE922";
                TitleBarMaxRestoreButton.ToolTip = isMax ? "还原" : "最大化";
            }
        }

        private Rect GetCurrentMonitorWorkAreaDIPs()
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            IntPtr monitor = NativeMethods.MonitorFromWindow(hwnd, NativeMethods.MONITOR_DEFAULTTONEAREST);
            var mi = new NativeMethods.MONITORINFO();
            mi.cbSize = Marshal.SizeOf(mi);
            if (NativeMethods.GetMonitorInfo(monitor, ref mi))
            {
                // 使用WPF提供的从设备像素到DIPs的转换，避免缩放误差
                var source = HwndSource.FromHwnd(hwnd);
                var m = source?.CompositionTarget?.TransformFromDevice ?? Matrix.Identity;
                // 使用rcWork以排除任务栏区域
                var tl = m.Transform(new System.Windows.Point(mi.rcWork.Left, mi.rcWork.Top));
                var br = m.Transform(new System.Windows.Point(mi.rcWork.Right, mi.rcWork.Bottom));
                return new Rect(tl.X, tl.Y, br.X - tl.X, br.Y - tl.Y);
            }
            // 回退到工作区尺寸
            var wa = SystemParameters.WorkArea;
            return new Rect(wa.Left, wa.Top, wa.Width, wa.Height);
        }

        private static class NativeMethods
        {
            public const uint MONITOR_DEFAULTTONEAREST = 0x00000002;
            public const int SWP_NOSIZE = 0x0001;
            public const int SWP_NOMOVE = 0x0002;
            public const int SWP_NOZORDER = 0x0004;
            public const int SWP_FRAMECHANGED = 0x0020;

            [DllImport("user32.dll")]
            public static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint dwFlags);

            [DllImport("user32.dll", CharSet = CharSet.Auto)]
            public static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

            [DllImport("user32.dll")]
            public static extern int GetSystemMetrics(int nIndex);

            [DllImport("user32.dll", SetLastError = true)]
            public static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

            [DllImport("dwmapi.dll")]
            public static extern int DwmExtendFrameIntoClientArea(IntPtr hwnd, ref MARGINS pMarInset);

            [StructLayout(LayoutKind.Sequential)]
            public struct RECT
            {
                public int Left;
                public int Top;
                public int Right;
                public int Bottom;
            }

            [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
            public struct MONITORINFO
            {
                public int cbSize;
                public RECT rcMonitor;
                public RECT rcWork;
                public int dwFlags;
            }

            [StructLayout(LayoutKind.Sequential)]
            public struct MARGINS
            {
                public int cxLeftWidth;
                public int cxRightWidth;
                public int cyTopHeight;
                public int cyBottomHeight;
            }
        }
        private void UpdateActionButtonsPosition()
        {
            // 更新列2操作按钮位置，使其在标题栏中居中对齐列2区域
            if (ActionButtonsGrid == null || ColLeft == null || ColCenter == null || RootGrid == null) return;
            
            try
            {
                // 计算列2在RootGrid中的起始位置（列1宽度 + 分割器宽度）
                double col2Start = ColLeft.ActualWidth + 6;
                // 计算列2的中心位置
                double col2Center = col2Start + ColCenter.ActualWidth / 2;
                // 计算ActionButtonsGrid的宽度（测量实际宽度）
                ActionButtonsGrid.Measure(new System.Windows.Size(double.PositiveInfinity, double.PositiveInfinity));
                double buttonsWidth = ActionButtonsGrid.DesiredSize.Width;
                if (buttonsWidth == 0) buttonsWidth = 400; // 如果尚未测量，使用估算值
                
                // 设置Margin，使按钮中心对齐列2中心
                // 标题栏Grid的Column 1是*，从列1按钮结束后开始，所以需要减去列1按钮区域
                double leftMargin = col2Center - buttonsWidth / 2 - ColLeft.ActualWidth - 16; // 16是列1按钮的左右Margin
                // 限制Margin范围，确保按钮不超出可见区域
                double maxMargin = RootGrid.ActualWidth - buttonsWidth - 102 - ColLeft.ActualWidth - 16; // 102是右上角按钮宽度
                leftMargin = Math.Max(8, Math.Min(leftMargin, maxMargin));
                
                ActionButtonsGrid.Margin = new Thickness(leftMargin, 0, 0, 0);
                ActionButtonsGrid.HorizontalAlignment = HorizontalAlignment.Left;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"UpdateActionButtonsPosition error: {ex.Message}");
            }
        }

        private void TitleBar_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (e.ChangedButton == System.Windows.Input.MouseButton.Left)
            {
                if (e.ClickCount == 2)
                {
                    // 双击切换最大化/还原
                    WindowMaximize_Click(sender, e);
                }
                else if (e.ClickCount == 1)
                {
                    // 单击拖拽窗口
                    this.DragMove();
                }
            }
        }

        private static IEnumerable<T> FindVisualChildren<T>(DependencyObject depObj) where T : DependencyObject
        {
            if (depObj != null)
            {
                for (int i = 0; i < VisualTreeHelper.GetChildrenCount(depObj); i++)
                {
                    DependencyObject child = VisualTreeHelper.GetChild(depObj, i);
                    if (child != null && child is T)
                    {
                        yield return (T)child;
                    }

                    foreach (T childOfChild in FindVisualChildren<T>(child))
                    {
                        yield return childOfChild;
                    }
                }
            }
        }

        #endregion

        #region 键盘快捷键和文件操作

        private void FilesListView_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            var listView = sender as ListView;
            if (listView == null)
                return;

            // Ctrl+A - 全选
            if (e.Key == Key.A && Keyboard.Modifiers == ModifierKeys.Control)
            {
                listView.SelectAll();
                e.Handled = true;
                return;
            }

            // Ctrl+C - 复制
            if (e.Key == Key.C && Keyboard.Modifiers == ModifierKeys.Control)
            {
                Copy_Click(null, null);
                e.Handled = true;
                return;
            }

            // Ctrl+V - 粘贴
            if (e.Key == Key.V && Keyboard.Modifiers == ModifierKeys.Control)
            {
                Paste_Click(null, null);
                e.Handled = true;
                return;
            }

            // Ctrl+X - 剪切
            if (e.Key == Key.X && Keyboard.Modifiers == ModifierKeys.Control)
            {
                Cut_Click(null, null);
                e.Handled = true;
                return;
            }

            // Delete - 删除
            if (e.Key == Key.Delete)
            {
                Delete_Click(null, null);
                e.Handled = true;
                return;
            }

            // F2 - 重命名
            if (e.Key == Key.F2)
            {
                Rename_Click(null, null);
                e.Handled = true;
                return;
            }

            // F5 - 刷新
            if (e.Key == Key.F5)
            {
                Refresh_Click(null, null);
                e.Handled = true;
                return;
            }

            // Alt+Enter - 属性
            if (e.Key == Key.Enter && Keyboard.Modifiers == ModifierKeys.Alt)
            {
                ShowProperties_Click(null, null);
                e.Handled = true;
                return;
            }

            // Backspace - 返回上一级
            if (e.Key == Key.Back)
            {
                NavigateBack_Click(null, null);
                e.Handled = true;
                return;
            }

            // 处理方向键，防止焦点跑到分割器
            if (e.Key == Key.Up || e.Key == Key.Down || e.Key == Key.Left || e.Key == Key.Right || e.Key == Key.Home || e.Key == Key.End)
            {
                if (listView.Items.Count == 0)
                    return;

                int currentIndex = listView.SelectedIndex;
                
                if (e.Key == Key.Down)
                {
                    if (currentIndex < listView.Items.Count - 1)
                    {
                        listView.SelectedIndex = currentIndex + 1;
                        listView.ScrollIntoView(listView.SelectedItem);
                    }
                    e.Handled = true;
                }
                else if (e.Key == Key.Up)
                {
                    if (currentIndex > 0)
                    {
                        listView.SelectedIndex = currentIndex - 1;
                        listView.ScrollIntoView(listView.SelectedItem);
                    }
                    e.Handled = true;
                }
                else if (e.Key == Key.Home)
                {
                    listView.SelectedIndex = 0;
                    listView.ScrollIntoView(listView.SelectedItem);
                    e.Handled = true;
                }
                else if (e.Key == Key.End)
                {
                    listView.SelectedIndex = listView.Items.Count - 1;
                    listView.ScrollIntoView(listView.SelectedItem);
                    e.Handled = true;
                }
                else if (e.Key == Key.Left)
                {
                    // 返回上一级
                    NavigateBack_Click(null, null);
                    e.Handled = true;
                }
                else if (e.Key == Key.Right)
                {
                    // 如果是文件夹，进入
                    if (listView.SelectedItem is FileSystemItem selectedItem && selectedItem.IsDirectory)
                    {
                        NavigateToPath(selectedItem.Path);
                    }
                    e.Handled = true;
                }
            }
            // 处理 Enter 键打开文件/文件夹
            else if (e.Key == Key.Enter && Keyboard.Modifiers == ModifierKeys.None)
            {
                if (FilesListView.SelectedItem is FileSystemItem selectedItem)
                {
                    if (selectedItem.IsDirectory)
                    {
                        NavigateToPath(selectedItem.Path);
                    }
                    else
                    {
                        try
                        {
                            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                            {
                                FileName = selectedItem.Path,
                                UseShellExecute = true
                            });
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show($"无法打开文件: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                        }
                    }
                }
                e.Handled = true;
            }
        }

        private void Copy_Click(object sender, RoutedEventArgs e)
        {
            if (FilesListView.SelectedItems.Count == 0)
                return;

            _copiedPaths.Clear();
            _isCutOperation = false;

            foreach (FileSystemItem item in FilesListView.SelectedItems)
            {
                _copiedPaths.Add(item.Path);
            }
        }

        private void Cut_Click(object sender, RoutedEventArgs e)
        {
            if (FilesListView.SelectedItems.Count == 0)
                return;

            _copiedPaths.Clear();
            _isCutOperation = true;

            foreach (FileSystemItem item in FilesListView.SelectedItems)
            {
                _copiedPaths.Add(item.Path);
            }
        }

        private void Paste_Click(object sender, RoutedEventArgs e)
        {
            if (_copiedPaths.Count == 0)
                return;

            try
            {
                foreach (var sourcePath in _copiedPaths)
                {
                    var fileName = Path.GetFileName(sourcePath);
                    var destPath = Path.Combine(_currentPath, fileName);

                    // 如果目标已存在，添加序号
                    if (File.Exists(destPath) || Directory.Exists(destPath))
                    {
                        var extension = Path.GetExtension(fileName);
                        var nameWithoutExt = Path.GetFileNameWithoutExtension(fileName);
                        int counter = 1;

                        do
                        {
                            fileName = $"{nameWithoutExt} ({counter}){extension}";
                            destPath = Path.Combine(_currentPath, fileName);
                            counter++;
                        }
                        while (File.Exists(destPath) || Directory.Exists(destPath));
                    }

                    if (File.Exists(sourcePath))
                    {
                        if (_isCutOperation)
                        {
                            File.Move(sourcePath, destPath);
                        }
                        else
                        {
                            File.Copy(sourcePath, destPath);
                        }
                    }
                    else if (Directory.Exists(sourcePath))
                    {
                        if (_isCutOperation)
                        {
                            Directory.Move(sourcePath, destPath);
                        }
                        else
                        {
                            CopyDirectory(sourcePath, destPath);
                        }
                    }
                }

                if (_isCutOperation)
                {
                    _copiedPaths.Clear();
                    _isCutOperation = false;
                }

                LoadCurrentDirectory();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"粘贴失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void CopyDirectory(string sourceDir, string destDir)
        {
            Directory.CreateDirectory(destDir);

            foreach (var file in Directory.GetFiles(sourceDir))
            {
                var fileName = Path.GetFileName(file);
                File.Copy(file, Path.Combine(destDir, fileName), true);
            }

            foreach (var dir in Directory.GetDirectories(sourceDir))
            {
                var dirName = Path.GetFileName(dir);
                CopyDirectory(dir, Path.Combine(destDir, dirName));
            }
        }

        private void Delete_Click(object sender, RoutedEventArgs e)
        {
            if (FilesListView.SelectedItems.Count == 0)
                return;

            var itemCount = FilesListView.SelectedItems.Count;
            var message = itemCount == 1 
                ? $"确定要删除 \"{(FilesListView.SelectedItem as FileSystemItem)?.Name}\" 吗？"
                : $"确定要删除这 {itemCount} 个项目吗？";

            if (!ConfirmDialog.Show(message, "确认删除", ConfirmDialog.DialogType.Warning, this))
                return;

            var itemsToDelete = new List<FileSystemItem>();
            foreach (FileSystemItem item in FilesListView.SelectedItems)
            {
                itemsToDelete.Add(item);
            }

            var failedItems = new List<string>();

            foreach (var item in itemsToDelete)
            {
                try
                {
                    if (item.IsDirectory)
                    {
                        Directory.Delete(item.Path, true);
                    }
                    else
                    {
                        File.Delete(item.Path);
                    }
                }
                catch (Exception ex)
                {
                    failedItems.Add($"{item.Name}: {ex.Message}");
                }
            }

            LoadCurrentDirectory();

            if (failedItems.Count > 0)
            {
                MessageBox.Show(
                    $"以下项目删除失败:\n\n{string.Join("\n", failedItems)}",
                    "删除失败",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
        }

        private void Rename_Click(object sender, RoutedEventArgs e)
        {
            if (FilesListView.SelectedItem is not FileSystemItem selectedItem)
                return;

            var dialog = new PathInputDialog
            {
                Title = "重命名",
                PromptText = "请输入新名称：",
                InputText = selectedItem.Name,
                Owner = this
            };

            if (dialog.ShowDialog() == true)
            {
                try
                {
                    var newName = dialog.InputText.Trim();
                    if (string.IsNullOrEmpty(newName))
                    {
                        MessageBox.Show("名称不能为空", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }

                    var newPath = Path.Combine(Path.GetDirectoryName(selectedItem.Path), newName);

                    if (selectedItem.IsDirectory)
                    {
                        Directory.Move(selectedItem.Path, newPath);
                    }
                    else
                    {
                        File.Move(selectedItem.Path, newPath);
                    }

                    LoadCurrentDirectory();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"重命名失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void ShowProperties_Click(object sender, RoutedEventArgs e)
        {
            if (FilesListView.SelectedItem is not FileSystemItem selectedItem)
                return;

            try
            {
                var info = selectedItem.IsDirectory
                    ? (FileSystemInfo)new DirectoryInfo(selectedItem.Path)
                    : new FileInfo(selectedItem.Path);

                var message = $"名称: {info.Name}\n" +
                             $"位置: {Path.GetDirectoryName(info.FullName)}\n" +
                             $"大小: {selectedItem.Size}\n" +
                             $"创建时间: {info.CreationTime}\n" +
                             $"修改时间: {info.LastWriteTime}\n" +
                             $"访问时间: {info.LastAccessTime}\n" +
                             $"属性: {info.Attributes}";

                MessageBox.Show(message, "属性", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"无法获取属性: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        #endregion
    }

    public class FileSystemItem
    {
        public string Name { get; set; }
        public string Path { get; set; }
        public string Type { get; set; }
        public string Size { get; set; }
        public string ModifiedDate { get; set; }
        public string CreatedTime { get; set; }
        public string Tags { get; set; }
        public string Notes { get; set; }
        public bool IsDirectory { get; set; }

        /// <summary>
        /// 格式化时间为简洁显示（s/m/h/d/mo/y）
        /// </summary>
        public static string FormatTimeAgo(DateTime createdTime)
        {
            var timeSpan = DateTime.Now - createdTime;
            
            if (timeSpan.TotalSeconds < 60)
                return $"{(int)timeSpan.TotalSeconds}s";
            
            if (timeSpan.TotalMinutes < 60)
                return $"{(int)timeSpan.TotalMinutes}m";
            
            if (timeSpan.TotalHours < 24)
                return $"{(int)timeSpan.TotalHours}h";
            
            if (timeSpan.TotalDays < 30)
                return $"{(int)timeSpan.TotalDays}d";
            
            if (timeSpan.TotalDays < 365)
                return $"{(int)(timeSpan.TotalDays / 30)}mo";
            
            return $"{(int)(timeSpan.TotalDays / 365)}y";
        }
    }

    /// <summary>
    /// 时间单位转换器 - 提取时间字符串中的单位
    /// </summary>
    public class TimeUnitConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (value == null) return null;
            
            string timeStr = value.ToString();
            if (string.IsNullOrEmpty(timeStr)) return null;

            // 提取单位（最后的字母）
            if (timeStr.EndsWith("s"))
                return "s";
            else if (timeStr.EndsWith("m"))
                return "m";
            else if (timeStr.EndsWith("h"))
                return "h";
            else if (timeStr.EndsWith("d"))
                return "d";
            else if (timeStr.EndsWith("mo"))
                return "mo";
            else if (timeStr.EndsWith("y"))
                return "y";
            
            return null;
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
