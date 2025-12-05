using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using OoiMRR;
using Library = OoiMRR.Library;
using Tag = OoiMRR.Tag;

namespace OoiMRR.Controls
{
    /// <summary>
    /// TabManagerControl.xaml 的交互逻辑
    /// 独立的标签页管理控件，负责标签页的创建、显示、切换、拖拽、关闭等功能
    /// </summary>
    public partial class TabManagerControl : UserControl
    {
        public enum TabType
        {
            Path,    // 路径标签页
            Library, // 库标签页
            Tag      // 标签标签页
        }

        /// <summary>
        /// 标签页信息
        /// </summary>
        public class TabInfo
        {
            public TabType Type { get; set; }
            public string Identifier { get; set; }  // 路径/库ID/标签ID
            public string Title { get; set; }
            public bool IsPinned { get; set; }
            public string OverrideTitle { get; set; }
            public object Data { get; set; }  // Library/Tag对象等
            
            // UI元素引用（内部使用）
            internal Button TabButton { get; set; }
            internal Border CloseButton { get; set; }
            internal StackPanel TabContainer { get; set; }
            internal TextBlock TitleTextBlock { get; set; }
        }

        private ObservableCollection<TabInfo> _tabs = new ObservableCollection<TabInfo>();
        private TabInfo _activeTab = null;
        private TabInfo _draggingTab = null;
        private System.Windows.Point _tabDragStartPoint;
        private AppConfig _config;
        private Window _parentWindow;

        // 事件定义
        public event EventHandler<TabInfo> TabActivated;
        public event EventHandler<TabInfo> TabClosed;
        public event EventHandler<TabInfo> TabPinned;
        public event EventHandler<TabInfo> TabTitleChanged;
        
        // 视图切换事件
        public event RoutedEventHandler ViewDetailsClicked;
        public event RoutedEventHandler ViewTilesClicked;
        public event RoutedPropertyChangedEventHandler<double> ThumbnailSizeChanged;

        public TabManagerControl()
        {
            InitializeComponent();
            _config = ConfigManager.Load();
            InitializeDragDrop();
            
            // 初始化视图按钮状态
            if (BtnViewDetails != null) BtnViewDetails.IsEnabled = false;  // 默认是详细信息视图
            if (BtnViewTiles != null) BtnViewTiles.IsEnabled = true;
        }
        
        // 视图切换按钮事件处理
        private void BtnViewDetails_Click(object sender, RoutedEventArgs e)
        {
            if (BtnViewDetails != null) BtnViewDetails.IsEnabled = false;
            if (BtnViewTiles != null) BtnViewTiles.IsEnabled = true;
            ViewDetailsClicked?.Invoke(sender, e);
        }
        
        private void BtnViewTiles_Click(object sender, RoutedEventArgs e)
        {
            if (BtnViewDetails != null) BtnViewDetails.IsEnabled = true;
            if (BtnViewTiles != null) BtnViewTiles.IsEnabled = false;
            ViewTilesClicked?.Invoke(sender, e);
        }
        
        private void ThumbnailSizeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            ThumbnailSizeChanged?.Invoke(sender, e);
        }
        
        // 公共方法：设置视图模式
        public void SetViewMode(bool isDetails)
        {
            if (BtnViewDetails != null) BtnViewDetails.IsEnabled = !isDetails;
            if (BtnViewTiles != null) BtnViewTiles.IsEnabled = isDetails;
        }
        
        // 公共属性：缩略图大小
        public double ThumbnailSize
        {
            get => ThumbnailSizeSlider?.Value ?? 128;
            set
            {
                if (ThumbnailSizeSlider != null)
                    ThumbnailSizeSlider.Value = value;
            }
        }

        /// <summary>
        /// 设置父窗口（用于对话框等）
        /// </summary>
        public void SetParentWindow(Window window)
        {
            _parentWindow = window;
        }

        /// <summary>
        /// 标签页集合
        /// </summary>
        public IReadOnlyList<TabInfo> Tabs => _tabs.ToList();

        /// <summary>
        /// 当前活动标签页
        /// </summary>
        public TabInfo ActiveTab => _activeTab;

        /// <summary>
        /// 标签页是否可见
        /// </summary>
        public new bool IsVisible
        {
            get => TabsBorder.Visibility == Visibility.Visible;
            set
            {
                TabsBorder.Visibility = value ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        /// <summary>
        /// 标签页容器（用于向后兼容）
        /// </summary>
        public StackPanel TabsPanelControl => this.TabsPanel;

        /// <summary>
        /// 标签页边框容器（用于向后兼容）
        /// </summary>
        public Border TabsBorderControl => this.TabsBorder;

        /// <summary>
        /// 统一的标签页打开逻辑
        /// </summary>
        /// <param name="type">标签页类型</param>
        /// <param name="identifier">标识符（路径/库ID/标签ID）</param>
        /// <param name="forceNewTab">强制新建标签页（中键/Ctrl+左键）</param>
        /// <param name="data">附加数据（Library对象/Tag对象等）</param>
        /// <param name="title">标题</param>
        public void OpenTab(TabType type, string identifier, bool forceNewTab = false, object data = null, string title = null)
        {
            if (string.IsNullOrEmpty(identifier)) return;

            // 1. 如果强制新标签页，直接创建
            if (forceNewTab)
            {
                CreateTabInternal(CreateTabInfo(type, identifier, data, title));
                return;
            }

            // 2. 查找是否已存在同地址的标签页
            var existingTab = FindTabByIdentifier(type, identifier);
            if (existingTab != null)
            {
                SwitchToTab(existingTab);
                return;
            }

            // 3. 检查当前标签页是否同类型
            if (_activeTab != null && _activeTab.Type == type)
            {
                // 使用当前标签页打开（更新内容）
                UpdateTabContent(_activeTab, type, identifier, data, title);
                SwitchToTab(_activeTab);
                return;
            }

            // 4. 创建新标签页
            CreateTabInternal(CreateTabInfo(type, identifier, data, title));
        }

        /// <summary>
        /// 查找标签页
        /// </summary>
        private TabInfo FindTabByIdentifier(TabType type, string identifier)
        {
            switch (type)
            {
                case TabType.Path:
                    return _tabs.FirstOrDefault(t => t.Type == TabType.Path && t.Identifier == identifier);
                case TabType.Library:
                    return _tabs.FirstOrDefault(t => t.Type == TabType.Library && t.Identifier == identifier);
                case TabType.Tag:
                    return _tabs.FirstOrDefault(t => t.Type == TabType.Tag && t.Identifier == identifier);
            }
            return null;
        }

        /// <summary>
        /// 创建标签页信息
        /// </summary>
        private TabInfo CreateTabInfo(TabType type, string identifier, object data, string title)
        {
            return new TabInfo
            {
                Type = type,
                Identifier = identifier,
                Title = title ?? GetDefaultTitle(type, identifier, data),
                Data = data
            };
        }

        /// <summary>
        /// 获取默认标题
        /// </summary>
        private string GetDefaultTitle(TabType type, string identifier, object data)
        {
            switch (type)
            {
                case TabType.Path:
                    return System.IO.Path.GetFileName(identifier) ?? identifier;
                case TabType.Library:
                    if (data is Library library)
                        return library.Name;
                    return identifier;
                case TabType.Tag:
                    if (data is Tag tag)
                        return tag.Name;
                    return identifier;
            }
            return identifier;
        }

        /// <summary>
        /// 更新标签页内容
        /// </summary>
        private void UpdateTabContent(TabInfo tab, TabType type, string identifier, object data, string title)
        {
            tab.Type = type;
            tab.Identifier = identifier;
            tab.Data = data;
            if (!string.IsNullOrEmpty(title))
                tab.Title = title;
            else
                tab.Title = GetDefaultTitle(type, identifier, data);

            // 更新UI
            if (tab.TitleTextBlock != null)
                tab.TitleTextBlock.Text = GetEffectiveTitle(tab);
            if (tab.TabButton != null)
                tab.TabButton.ToolTip = tab.Title;
        }

        /// <summary>
        /// 创建标签页内部实现
        /// </summary>
        private void CreateTabInternal(TabInfo tab)
        {
            if (TabsPanel == null) return;

            // 应用配置覆盖
            ApplyTabOverrides(tab);

            // 创建标签按钮容器
            var tabContainer = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(0, 0, 2, 0)
            };

            // 创建标签文本
            var titleText = new TextBlock
            {
                Text = GetEffectiveTitle(tab),
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Center,
                TextAlignment = TextAlignment.Left,
                TextTrimming = TextTrimming.CharacterEllipsis,
                TextWrapping = TextWrapping.NoWrap,
                Margin = new Thickness(0, 0, 8, 0)
            };

            // 创建关闭按钮
            var closeButtonText = new TextBlock
            {
                Text = "×",
                FontSize = 18,
                FontWeight = FontWeights.Bold,
                FontFamily = new FontFamily("Segoe UI Symbol"),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                TextAlignment = TextAlignment.Center,
                Tag = tab,
                Opacity = 0.0,
                Cursor = Cursors.Hand
            };

            var closeButton = new Border
            {
                Width = 24,
                Height = 24,
                Background = Brushes.Transparent,
                Tag = tab,
                Cursor = Cursors.Hand,
                Child = closeButtonText,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(4, 0, 0, 0)
            };

            closeButton.MouseLeftButtonDown += (s, e) =>
            {
                e.Handled = true;
                if (s is Border border && border.Tag is TabInfo tabToClose)
                {
                    CloseTab(tabToClose);
                }
            };

            // 创建按钮内容
            var buttonContent = new Grid
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch
            };
            buttonContent.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            buttonContent.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            Grid.SetColumn(titleText, 0);
            Grid.SetColumn(closeButton, 1);
            buttonContent.Children.Add(titleText);
            buttonContent.Children.Add(closeButton);

            // 创建标签按钮
            var button = new Button
            {
                Content = buttonContent,
                Tag = tab,
                Padding = new Thickness(12, 6, 12, 6),
                Margin = new Thickness(0)
            };

            // 应用样式（如果存在）
            try
            {
                if (_parentWindow != null)
                {
                    var style = _parentWindow.FindResource("TabButtonStyle") as Style;
                    if (style != null)
                        button.Style = style;
                }
            }
            catch { }

            button.Click += (s, e) =>
            {
                if (s is Button btn && btn.Tag is TabInfo tabInfo)
                {
                    SwitchToTab(tabInfo);
                }
            };

            // 拖拽支持
            button.PreviewMouseLeftButtonDown += (s, e) =>
            {
                _tabDragStartPoint = e.GetPosition(null);
                _draggingTab = tab;
            };

            button.MouseMove += (s, e) =>
            {
                if (_draggingTab == tab && e.LeftButton == MouseButtonState.Pressed)
                {
                    var pos = e.GetPosition(null);
                    if (Math.Abs(pos.X - _tabDragStartPoint.X) > 4 || Math.Abs(pos.Y - _tabDragStartPoint.Y) > 4)
                    {
                        var data = new DataObject();
                        data.SetData("OoiMRR_TabKey", GetTabKey(tab));
                        data.SetData("OoiMRR_TabPinned", tab.IsPinned);
                        DragDrop.DoDragDrop(button, data, DragDropEffects.Move);
                        _draggingTab = null;
                    }
                }
            };

            button.PreviewMouseLeftButtonUp += (s, e) => { _draggingTab = null; };

            // 中键关闭
            button.PreviewMouseDown += (s, e) =>
            {
                if (e.ChangedButton == MouseButton.Middle)
                {
                    if (s is Button btn && btn.Tag is TabInfo tabToClose)
                    {
                        CloseTab(tabToClose);
                        e.Handled = true;
                    }
                }
            };

            // 右键菜单
            var cm = new ContextMenu();
            var pinItem = new MenuItem { Header = "固定此标签页" };
            pinItem.Click += (s, e) => TogglePinTab(tab);
            var renameItem = new MenuItem { Header = "重命名显示标题" };
            renameItem.Click += (s, e) => RenameDisplayTitle(tab);
            cm.Items.Add(pinItem);
            cm.Items.Add(renameItem);
            cm.Opened += (s, e) => { pinItem.Header = tab.IsPinned ? "取消固定此标签页" : "固定此标签页"; };
            button.ContextMenu = cm;

            // 保存引用
            tab.TabButton = button;
            tab.CloseButton = closeButton;
            tab.TitleTextBlock = titleText;
            tab.TabContainer = tabContainer;

            tabContainer.Children.Add(button);
            _tabs.Add(tab);
            TabsPanel.Children.Add(tabContainer);

            ApplyPinVisual(tab);
            ReorderTabs();
            UpdateTabStyles();

            // 切换到新标签页
            SwitchToTab(tab);
        }

        /// <summary>
        /// 切换到指定标签页
        /// </summary>
        public void SwitchToTab(TabInfo tab)
        {
            if (tab == null) return;

            _activeTab = tab;
            UpdateTabStyles();
            TabActivated?.Invoke(this, tab);
        }

        /// <summary>
        /// 关闭标签页
        /// </summary>
        public void CloseTab(TabInfo tab)
        {
            if (tab == null) return;

            if (tab.TabContainer != null && TabsPanel != null)
            {
                TabsPanel.Children.Remove(tab.TabContainer);
            }

            _tabs.Remove(tab);

            if (tab == _activeTab)
            {
                _activeTab = null;
                if (_tabs.Count > 0)
                {
                    SwitchToTab(_tabs.First());
                }
            }

            TabClosed?.Invoke(this, tab);
        }

        /// <summary>
        /// 获取标签页键值
        /// </summary>
        private string GetTabKey(TabInfo tab)
        {
            switch (tab.Type)
            {
                case TabType.Path:
                    return "path:" + (tab.Identifier ?? string.Empty);
                case TabType.Library:
                    return "library:" + (tab.Identifier ?? "");
                case TabType.Tag:
                    return "tag:" + (tab.Identifier ?? "");
            }
            return "unknown:" + (tab.Title ?? "");
        }

        /// <summary>
        /// 应用标签页覆盖配置
        /// </summary>
        private void ApplyTabOverrides(TabInfo tab)
        {
            var key = GetTabKey(tab);
            if (_config.TabTitleOverrides != null && _config.TabTitleOverrides.TryGetValue(key, out var overrideTitle) && !string.IsNullOrWhiteSpace(overrideTitle))
            {
                tab.OverrideTitle = overrideTitle;
            }
            if (_config.PinnedTabs != null && _config.PinnedTabs.Contains(key))
            {
                tab.IsPinned = true;
            }
        }

        /// <summary>
        /// 获取有效标题
        /// </summary>
        private string GetEffectiveTitle(TabInfo tab)
        {
            return string.IsNullOrWhiteSpace(tab.OverrideTitle) ? tab.Title : tab.OverrideTitle;
        }

        /// <summary>
        /// 切换固定状态
        /// </summary>
        private void TogglePinTab(TabInfo tab)
        {
            if (tab == null) return;
            tab.IsPinned = !tab.IsPinned;
            var key = GetTabKey(tab);
            if (_config.PinnedTabs == null) _config.PinnedTabs = new List<string>();
            if (tab.IsPinned)
            {
                if (!_config.PinnedTabs.Contains(key)) _config.PinnedTabs.Insert(0, key);
            }
            else
            {
                _config.PinnedTabs.Remove(key);
            }
            ConfigManager.Save(_config);
            ApplyPinVisual(tab);
            ReorderTabs();
            TabPinned?.Invoke(this, tab);
        }

        /// <summary>
        /// 应用固定视觉效果
        /// </summary>
        private void ApplyPinVisual(TabInfo tab)
        {
            if (tab == null || tab.TabButton == null || tab.TitleTextBlock == null) return;
            if (tab.IsPinned)
            {
                tab.TabButton.Width = _config.PinnedTabWidth > 0 ? _config.PinnedTabWidth : 90;
                tab.TitleTextBlock.Text = "📌 " + GetEffectiveTitle(tab);
                tab.TabButton.ToolTip = GetEffectiveTitle(tab);
            }
            else
            {
                tab.TabButton.Width = double.NaN;
                tab.TitleTextBlock.Text = GetEffectiveTitle(tab);
                tab.TabButton.ToolTip = null;
            }
        }

        /// <summary>
        /// 重新排序标签页
        /// </summary>
        private void ReorderTabs()
        {
            if (TabsPanel == null) return;
            var pinned = _tabs.Where(t => t.IsPinned).ToList();
            var unpinned = _tabs.Where(t => !t.IsPinned).ToList();
            var ordered = new List<TabInfo>();
            if (_config.PinnedTabs != null && _config.PinnedTabs.Count > 0)
            {
                foreach (var k in _config.PinnedTabs)
                {
                    var found = pinned.FirstOrDefault(t => GetTabKey(t) == k);
                    if (found != null) ordered.Add(found);
                }
                foreach (var t in pinned)
                {
                    if (!ordered.Contains(t)) ordered.Add(t);
                }
            }
            else
            {
                ordered.AddRange(pinned);
            }
            ordered.AddRange(unpinned);
            TabsPanel.Children.Clear();
            foreach (var t in ordered)
            {
                if (t.TabContainer != null) TabsPanel.Children.Add(t.TabContainer);
            }
            TabsPanel.UpdateLayout();
            TabsBorder.UpdateLayout();
        }

        /// <summary>
        /// 重命名显示标题
        /// </summary>
        private void RenameDisplayTitle(TabInfo tab)
        {
            try
            {
                var dlg = new PathInputDialog("请输入新的显示标题：");
                dlg.Owner = _parentWindow ?? Application.Current.MainWindow;
                dlg.InputText = GetEffectiveTitle(tab);
                if (dlg.ShowDialog() == true)
                {
                    var newTitle = dlg.InputText?.Trim() ?? string.Empty;
                    var key = GetTabKey(tab);
                    if (string.IsNullOrWhiteSpace(newTitle))
                    {
                        tab.OverrideTitle = null;
                        if (_config.TabTitleOverrides != null) _config.TabTitleOverrides.Remove(key);
                    }
                    else
                    {
                        tab.OverrideTitle = newTitle;
                        if (_config.TabTitleOverrides == null) _config.TabTitleOverrides = new Dictionary<string, string>();
                        _config.TabTitleOverrides[key] = newTitle;
                    }
                    ConfigManager.Save(_config);
                    ApplyPinVisual(tab);
                    if (tab.TitleTextBlock != null) tab.TitleTextBlock.Text = GetEffectiveTitle(tab);
                    TabTitleChanged?.Invoke(this, tab);
                }
            }
            catch { }
        }

        /// <summary>
        /// 更新标签页样式
        /// </summary>
        private void UpdateTabStyles()
        {
            foreach (var tab in _tabs)
            {
                if (tab.TabButton == null) continue;
                if (tab == _activeTab)
                {
                    // 活动标签页样式
                    tab.TabButton.Background = new SolidColorBrush(Color.FromRgb(0x0D, 0x6E, 0xFD));
                    tab.TabButton.Foreground = Brushes.White;
                    if (tab.CloseButton != null && tab.CloseButton.Child is TextBlock closeText)
                        closeText.Foreground = Brushes.White;
                }
                else
                {
                    // 非活动标签页样式
                    tab.TabButton.Background = new SolidColorBrush(Color.FromRgb(0xE9, 0xEC, 0xEF));
                    tab.TabButton.Foreground = new SolidColorBrush(Color.FromRgb(0x21, 0x25, 0x29));
                    if (tab.CloseButton != null && tab.CloseButton.Child is TextBlock closeText)
                        closeText.Foreground = new SolidColorBrush(Color.FromRgb(0x6C, 0x75, 0x7D));
                }
            }
        }

        /// <summary>
        /// 初始化拖拽
        /// </summary>
        private void InitializeDragDrop()
        {
            if (TabsPanel == null) return;
            TabsPanel.AllowDrop = true;
            TabsPanel.DragOver += TabsPanel_DragOver;
            TabsPanel.Drop += TabsPanel_Drop;
        }

        private void TabsPanel_DragOver(object sender, DragEventArgs e)
        {
            if (!e.Data.GetDataPresent("OoiMRR_TabKey"))
            {
                e.Effects = DragDropEffects.None;
                return;
            }
            e.Effects = DragDropEffects.Move;
            e.Handled = true;
        }

        private void TabsPanel_Drop(object sender, DragEventArgs e)
        {
            try
            {
                if (!e.Data.GetDataPresent("OoiMRR_TabKey")) return;
                var key = e.Data.GetData("OoiMRR_TabKey") as string;
                if (string.IsNullOrEmpty(key) || TabsPanel == null) return;
                var tab = _tabs.FirstOrDefault(t => GetTabKey(t) == key);
                if (tab == null) return;

                var panel = TabsPanel;
                var mousePos = e.GetPosition(panel);
                var children = panel.Children.OfType<StackPanel>().ToList();
                int targetIndex = 0;
                for (int i = 0; i < children.Count; i++)
                {
                    var child = children[i] as FrameworkElement;
                    if (child == null) continue;
                    var pos = child.TransformToAncestor(panel).Transform(new System.Windows.Point(0, 0));
                    double mid = pos.X + child.ActualWidth / 2;
                    if (mousePos.X > mid) targetIndex = i + 1;
                }

                int pinnedCount = _tabs.Count(t => t.IsPinned);
                if (tab.IsPinned) targetIndex = Math.Min(targetIndex, pinnedCount);
                else targetIndex = Math.Max(targetIndex, pinnedCount);

                int currentIndex = children.IndexOf(tab.TabContainer);
                if (currentIndex == targetIndex) return;

                var pinned = _tabs.Where(t => t.IsPinned).ToList();
                var unpinned = _tabs.Where(t => !t.IsPinned).ToList();

                if (tab.IsPinned)
                {
                    pinned.Remove(tab);
                    pinned.Insert(targetIndex, tab);
                    _config.PinnedTabs = pinned.Select(t => GetTabKey(t)).ToList();
                    ConfigManager.Save(_config);
                    _tabs = new ObservableCollection<TabInfo>(pinned.Concat(unpinned));
                }
                else
                {
                    int unTarget = Math.Max(0, targetIndex - pinnedCount);
                    int unCurrent = unpinned.IndexOf(tab);
                    if (unCurrent == -1) return;
                    unpinned.Remove(tab);
                    if (unTarget > unpinned.Count) unTarget = unpinned.Count;
                    unpinned.Insert(unTarget, tab);
                    _tabs = new ObservableCollection<TabInfo>(pinned.Concat(unpinned));
                }

                ReorderTabs();
                UpdateTabStyles();
            }
            catch { }
        }
    }
}

