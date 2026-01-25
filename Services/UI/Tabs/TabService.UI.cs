using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using YiboFile.Controls;
using YiboFile.Dialogs;
using YiboFile.Models;
using YiboFile.Services.Config;
using YiboFile.Services.Search;

namespace YiboFile.Services.Tabs
{
    public class TabUiContext
    {
        public FileBrowserControl FileBrowser { get; init; }
        public TabManagerControl TabManager { get; init; }
        public Dispatcher Dispatcher { get; init; }
        public Window OwnerWindow { get; init; }
        public Func<AppConfig> GetConfig { get; init; }
        public Action<AppConfig> SaveConfig { get; init; }
        public Func<Library> GetCurrentLibrary { get; init; }
        public Action<Library> SetCurrentLibrary { get; init; }
        public Func<string> GetCurrentPath { get; init; }
        public Action<string> SetCurrentPath { get; init; }
        public Action<string> SetNavigationCurrentPath { get; init; }
        public Action<Library> LoadLibraryFiles { get; init; }
        public Action<string> NavigateToPathInternal { get; init; }
        public Action UpdateNavigationButtonsState { get; init; }
        public SearchService SearchService { get; init; }
        public Func<SearchCacheService> GetSearchCacheService { get; init; }
        public Func<SearchOptions> GetSearchOptions { get; init; }
        public Func<List<FileSystemItem>> GetCurrentFiles { get; init; }
        public Action<List<FileSystemItem>> SetCurrentFiles { get; init; }
        public Action ClearFilter { get; init; }
        public Func<string, Task> RefreshSearchTab { get; init; }
        public Func<string, object> FindResource { get; init; }
        public Services.Features.ITagService TagService { get; init; }
        public Func<string> GetCurrentNavigationMode { get; init; }
    }

    public partial class TabService
    {
        private void EnsureUi()
        {
            if (_config == null && _ui?.GetConfig != null)
            {
                _config = _ui.GetConfig();
            }
        }

        public void CreatePathTab(string path, bool forceNewTab = false, bool skipValidation = false, bool activate = true)
        {
            EnsureUi();
            if (_ui.TabManager?.TabsPanelControl == null) return;

            if (!skipValidation && !ValidatePath(path, out string errorMessage))
            {
                YiboFile.DialogService.Warning(errorMessage);
                return;
            }

            if (!forceNewTab)
            {
                var existingTab = FindTabByPath(path);
                if (existingTab != null)
                {
                    if (activate) SwitchToTab(existingTab);
                    return;
                }
            }

            var newTab = new PathTab
            {
                Type = TabType.Path,
                Path = path,
                Title = GetPathDisplayTitle(path)
            };

            CreateTabInternal(newTab, activate);
        }

        public PathTab CreateBlankTab()
        {
            var desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            CreatePathTab(desktopPath, forceNewTab: true);
            return ActiveTab as PathTab;
        }

        public void OpenLibraryTab(Library library, bool forceNewTab = false, bool activate = true)
        {
            EnsureUi();
            if (library == null) return;
            if (forceNewTab)
            {
                var tab = new PathTab
                {
                    Type = TabType.Library,
                    Path = library.Name,
                    Title = library.Name,
                    Library = library
                };
                CreateTabInternal(tab, activate);
                return;
            }

            var window = TimeSpan.FromSeconds(_config?.ReuseTabTimeWindow ?? 10);
            var recentTab = FindRecentTab(
                t => t.Type == TabType.Library && t.Library?.Id == library.Id,
                window
            );

            if (recentTab != null)
            {
                if (activate) SwitchToTab(recentTab);
                return;
            }

            var currentMode = _ui?.GetCurrentNavigationMode?.Invoke() ?? "Path";
            if (currentMode == "Library" && _activeTab != null && _activeTab.Type == TabType.Library)
            {
                _activeTab.Library = library;
                _activeTab.Path = library.Name;
                _activeTab.Title = library.Name;
                if (_activeTab.TitleTextBlock != null) _activeTab.TitleTextBlock.Text = library.Name;
                if (_activeTab.TabButton != null) _activeTab.TabButton.ToolTip = library.Name;
                if (activate) SwitchToTab(_activeTab);
                return;
            }

            var newTab = new PathTab
            {
                Type = TabType.Library,
                Path = library.Name,
                Title = library.Name,
                Library = library
            };

            CreateTabInternal(newTab, activate);
        }

        public void CloseTab(PathTab tab)
        {
            EnsureUi();
            if (tab == null || tab.TabButton == null) return;
            if (!CanCloseTab(tab, _ui.GetCurrentLibrary?.Invoke() != null)) return;
            if (_ui.TabManager == null || _ui.TabManager.TabsPanelControl == null) return;

            bool wasActive = (tab == _activeTab);

            RemoveTab(tab);

            var container = tab.TabButton.Parent as StackPanel;
            if (container != null)
            {
                container.Children.Clear();
                _ui.TabManager.TabsPanelControl.Children.Remove(container);
                _ui.TabManager.EnsureNewTabButtonLast();
                _ui.TabManager.TabsPanelControl.UpdateLayout();
                _ui.TabManager.TabsBorderControl?.UpdateLayout();
            }

            tab.TabButton = null;
            tab.CloseButton = null;

            if (wasActive)
            {
                var remainingTabs = GetTabsInOrder();
                if (remainingTabs.Count > 0)
                {
                    SwitchToTab(remainingTabs[0]);
                }
                else
                {
                    var desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                    if (Directory.Exists(desktopPath))
                    {
                        CreatePathTab(desktopPath);
                    }
                }
            }
            else
            {
                UpdateTabStyles();
                UpdateTabWidths();
            }
        }

        public void ApplyPinVisual(PathTab tab)
        {
            if (tab == null || tab.TabButton == null || tab.TitleTextBlock == null) return;
            var effectiveTitle = GetEffectiveTitle(tab);
            if (string.IsNullOrWhiteSpace(effectiveTitle) && !string.IsNullOrWhiteSpace(tab.Path))
            {
                effectiveTitle = GetPathDisplayTitle(tab.Path);
            }
            if (tab.IsPinned)
            {
                tab.TabButton.Width = double.NaN;
                tab.TabButton.MinWidth = GetPinnedTabWidth();
                tab.TitleTextBlock.Text = "📌 " + effectiveTitle;
                tab.TabButton.ToolTip = effectiveTitle;
            }
            else
            {
                tab.TitleTextBlock.Text = effectiveTitle;
                tab.TabButton.ToolTip = effectiveTitle;
                tab.TabButton.MinWidth = 0;
            }
        }

        public void ReorderTabs()
        {
            EnsureUi();
            if (_ui.TabManager == null || _ui.TabManager.TabsPanelControl == null) return;
            var ordered = GetTabsInOrder();
            _ui.TabManager.TabsPanelControl.Children.Clear();
            foreach (var t in ordered)
            {
                if (t.TabContainer != null)
                {
                    _ui.TabManager.TabsPanelControl.Children.Add(t.TabContainer);
                }
            }
            _ui.TabManager.EnsureNewTabButtonLast();
            _ui.TabManager.TabsPanelControl.UpdateLayout();
            _ui.TabManager.TabsBorderControl?.UpdateLayout();
        }

        public void RenameDisplayTitle(PathTab tab)
        {
            EnsureUi();
            try
            {
                var newTitle = DialogService.ShowInput("请输入新的显示标题：", GetEffectiveTitle(tab), "输入", owner: _ui.OwnerWindow);
                if (newTitle != null)
                {
                    newTitle = newTitle.Trim();
                    SetTabOverrideTitle(tab, newTitle);
                    ApplyPinVisual(tab);
                    if (tab.TitleTextBlock != null) tab.TitleTextBlock.Text = GetEffectiveTitle(tab);
                }
            }
            catch { }
        }

        public void UpdateTabStyles()
        {
            EnsureUi();
            var activeTab = _activeTab;
            foreach (var tab in _tabs)
            {
                if (tab.TabButton != null)
                {
                    if (_ui.FindResource != null)
                    {
                        tab.TabButton.Style = (Style)_ui.FindResource(tab == activeTab ? "ActiveTabButtonStyle" : "TabButtonStyle");
                    }

                    if (tab.CloseButton is Border border && border.Child is TextBlock closeButtonText)
                    {
                        if (tab == activeTab)
                        {
                            closeButtonText.Foreground = Brushes.White;
                        }
                        else
                        {
                            closeButtonText.Foreground = new SolidColorBrush(Color.FromRgb(0x6C, 0x75, 0x7D));
                        }
                    }
                }
            }
        }

        private string GetTabTypePrefix(PathTab tab)
        {
            if (tab.Type == TabType.Path)
            {
                if (!string.IsNullOrEmpty(tab.Path))
                {
                    if (tab.Path.StartsWith("search://", StringComparison.OrdinalIgnoreCase)) return "🔍";
                    if (tab.Path.StartsWith("tag://", StringComparison.OrdinalIgnoreCase)) return "🏷️";
                    if (tab.Path.StartsWith("content://", StringComparison.OrdinalIgnoreCase)) return "📝";
                    if (tab.Path.StartsWith("lib://")) return "📚";
                }
                return "📁";
            }
            else if (tab.Type == TabType.Library) return "📚";
            return "📁";
        }

        private (SolidColorBrush bg, SolidColorBrush fg) GetTabTypeBadgeColors(string prefix)
        {
            return (Brushes.Transparent, Brushes.Black);
        }

        private TextBlock CreateTypeIcon(PathTab tab)
        {
            string icon = GetTabTypePrefix(tab);
            var iconText = new TextBlock
            {
                Text = icon,
                FontSize = 14,
                FontFamily = new FontFamily("Segoe UI Emoji, Segoe UI Symbol"),
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 4, 0),
                Opacity = 1.0
            };
            return iconText;
        }

        private void CreateTabInternal(PathTab tab, bool activate = true)
        {
            EnsureUi();
            if (_ui.TabManager == null || _ui.TabManager.TabsPanelControl == null) return;

            var tabContainer = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(0, 0, 2, 0)
            };

            var typeIcon = CreateTypeIcon(tab);

            var titleText = new TextBlock
            {
                Text = tab.Title,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Center,
                TextAlignment = TextAlignment.Left,
                TextTrimming = TextTrimming.CharacterEllipsis,
                TextWrapping = TextWrapping.NoWrap,
                Margin = new Thickness(0, 0, 8, 0)
            };

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
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0)
            };

            closeButton.MouseLeftButtonDown += (s, e) =>
            {
                e.Handled = true;
                if (s is Border border && border.Tag is PathTab tabToClose)
                {
                    CloseTab(tabToClose);
                }
            };

            closeButton.MouseEnter += (s, e) =>
            {
                if (s is Border border && border.Child is TextBlock textBlock)
                {
                    textBlock.Foreground = new SolidColorBrush(Color.FromRgb(0xDC, 0x35, 0x45));
                }
            };

            closeButton.MouseLeave += (s, e) =>
            {
                if (s is Border border && border.Child is TextBlock textBlock)
                {
                    var tabToCheck = border.Tag as PathTab;
                    if (tabToCheck != null && tabToCheck == _activeTab)
                    {
                        textBlock.Foreground = Brushes.White;
                    }
                    else
                    {
                        textBlock.Foreground = new SolidColorBrush(Color.FromRgb(0x6C, 0x75, 0x7D));
                    }
                }
            };

            var iconCloseContainer = new Grid
            {
                Width = 24,
                Height = 24,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 4, 0)
            };
            iconCloseContainer.Children.Add(typeIcon);
            iconCloseContainer.Children.Add(closeButton);

            var buttonContent = new Grid
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch
            };
            buttonContent.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            buttonContent.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            Grid.SetColumn(iconCloseContainer, 0);
            Grid.SetColumn(titleText, 1);
            buttonContent.Children.Add(iconCloseContainer);
            buttonContent.Children.Add(titleText);

            var button = new Button
            {
                Content = buttonContent,
                Style = (Style)_ui.FindResource?.Invoke("TabButtonStyle"),
                Tag = tab,
                Margin = new Thickness(0)
            };

            System.Windows.Shell.WindowChrome.SetIsHitTestVisibleInChrome(button, true);

            button.PreviewMouseLeftButtonDown += (s, e) =>
            {
                if (e.OriginalSource is Border border && border.Tag == tab) return;
                _tabDragStartPoint = e.GetPosition(null);
                _draggingTab = tab;
                _isDragging = false;
                if (button.IsMouseCaptured) button.ReleaseMouseCapture();
            };

            button.PreviewMouseMove += (s, e) =>
            {
                if (_draggingTab == tab && e.LeftButton == MouseButtonState.Pressed)
                {
                    var pos = e.GetPosition(null);
                    var deltaX = Math.Abs(pos.X - _tabDragStartPoint.X);
                    var deltaY = Math.Abs(pos.Y - _tabDragStartPoint.Y);
                    if (deltaX > 4 || deltaY > 4)
                    {
                        _isDragging = true;
                        button.CaptureMouse();
                        var data = new DataObject();
                        data.SetData("YiboFile_TabKey", GetTabKey(tab));
                        data.SetData("YiboFile_TabPinned", tab.IsPinned);
                        data.SetData("YiboFile_SourceServiceHash", this.GetHashCode());
                        DragDrop.DoDragDrop(button, data, DragDropEffects.Move);
                        if (button.IsMouseCaptured) button.ReleaseMouseCapture();
                        _draggingTab = null;
                        _isDragging = false;
                        e.Handled = true;
                    }
                }
            };

            button.PreviewMouseLeftButtonUp += (s, e) =>
            {
                if (e.OriginalSource is Border border && border.Tag == tab)
                {
                    _draggingTab = null;
                    _isDragging = false;
                    return;
                }

                bool shouldPreventClick = false;
                if (_draggingTab == tab && _isDragging) shouldPreventClick = true;
                else if (_draggingTab == tab)
                {
                    var pos = e.GetPosition(null);
                    var deltaX = Math.Abs(pos.X - _tabDragStartPoint.X);
                    var deltaY = Math.Abs(pos.Y - _tabDragStartPoint.Y);

                    if (deltaX <= 4 && deltaY <= 4)
                    {
                        _ui.TabManager?.RaiseCloseOverlayRequested();
                        SwitchToTab(tab);
                        e.Handled = true;
                    }
                }

                if (shouldPreventClick) e.Handled = true;
                if (button.IsMouseCaptured) button.ReleaseMouseCapture();
                _draggingTab = null;
                _isDragging = false;
            };

            var cm = new ContextMenu();
            var closeItem = new MenuItem { Header = "关闭标签页" };
            closeItem.Click += (s, e) => CloseTab(tab);
            var closeOthersItem = new MenuItem { Header = "关闭其他标签页" };
            closeOthersItem.Click += (s, e) => CloseOtherTabs(tab);
            var closeAllItem = new MenuItem { Header = "关闭全部标签页" };
            closeAllItem.Click += (s, e) => CloseAllTabs();
            var openInExplorerItem = new MenuItem { Header = "在资源管理器中打开" };
            openInExplorerItem.Click += (s, e) => OpenTabInExplorer(tab);
            var pinItem = new MenuItem { Header = "固定此标签页" };
            pinItem.Click += (s, e) => TogglePinTab(tab);
            var renameItem = new MenuItem { Header = "重命名显示标题" };
            renameItem.Click += (s, e) => RenameDisplayTitle(tab);
            cm.Items.Add(closeItem);
            cm.Items.Add(closeOthersItem);
            cm.Items.Add(closeAllItem);
            cm.Items.Add(new Separator());
            cm.Items.Add(openInExplorerItem);
            cm.Items.Add(new Separator());
            cm.Items.Add(pinItem);
            cm.Items.Add(renameItem);
            cm.Opened += (s, e) =>
            {
                pinItem.Header = tab.IsPinned ? "取消固定此标签页" : "固定此标签页";
                openInExplorerItem.IsEnabled = !string.IsNullOrWhiteSpace(GetTabOpenPath(tab));
            };
            button.ContextMenu = cm;

            button.PreviewMouseDown += (s, e) =>
            {
                if (e.ChangedButton == MouseButton.Middle)
                {
                    if (s is Button btn && btn.Tag is PathTab tabToClose)
                    {
                        CloseTab(tabToClose);
                        e.Handled = true;
                    }
                }
            };

            if (tab == _activeTab) closeButtonText.Foreground = Brushes.White;
            else closeButtonText.Foreground = new SolidColorBrush(Color.FromRgb(0x6C, 0x75, 0x7D));

            var fadeInAnimation = new DoubleAnimation
            {
                To = 1.0,
                Duration = TimeSpan.FromMilliseconds(200),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseInOut }
            };

            var fadeOutAnimation = new DoubleAnimation
            {
                To = 0.0,
                Duration = TimeSpan.FromMilliseconds(200),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseInOut }
            };

            button.MouseEnter += (s, e) =>
            {
                typeIcon.BeginAnimation(UIElement.OpacityProperty, fadeOutAnimation);
                closeButtonText.BeginAnimation(UIElement.OpacityProperty, fadeInAnimation);
            };

            button.MouseLeave += (s, e) =>
            {
                var btn = button;
                var mousePos = Mouse.GetPosition(btn);
                if (mousePos.X < 0 || mousePos.Y < 0 || mousePos.X > btn.ActualWidth || mousePos.Y > btn.ActualHeight)
                {
                    typeIcon.BeginAnimation(UIElement.OpacityProperty, fadeInAnimation);
                    closeButtonText.BeginAnimation(UIElement.OpacityProperty, fadeOutAnimation);
                }
            };

            closeButton.MouseEnter += (s, e) => closeButtonText.BeginAnimation(UIElement.OpacityProperty, fadeInAnimation);
            closeButton.MouseLeave += (s, e) =>
            {
                var btn = button;
                var mousePos = Mouse.GetPosition(btn);
                if (mousePos.X < 0 || mousePos.Y < 0 || mousePos.X > btn.ActualWidth || mousePos.Y > btn.ActualHeight)
                {
                    typeIcon.BeginAnimation(UIElement.OpacityProperty, fadeInAnimation);
                    closeButtonText.BeginAnimation(UIElement.OpacityProperty, fadeOutAnimation);
                }
            };

            tabContainer.Children.Add(button);

            tab.CloseButton = closeButton;
            tab.TitleTextBlock = titleText;
            tab.IconTextBlock = typeIcon;
            tab.TabContainer = tabContainer;
            tab.TabButton = button;

            AddTab(tab);

            if (_ui.TabManager?.TabsPanelControl != null)
            {
                _ui.TabManager.TabsPanelControl.Children.Add(tabContainer);
                _ui.TabManager.EnsureNewTabButtonLast();
                InitializeTabsDragDrop();
            }

            ApplyTabOverrides(tab);
            ApplyPinVisual(tab);
            ReorderTabs();

            _ui.Dispatcher?.InvokeAsync(() =>
            {
                UpdateTabWidths();
                var border = _ui.TabManager?.TabsBorderControl;
                var scrollViewer = FindScrollViewer(border);
                if (scrollViewer != null && scrollViewer.ExtentWidth > scrollViewer.ViewportWidth)
                {
                    scrollViewer.ScrollToRightEnd();
                }
            }, DispatcherPriority.Loaded);

            if (activate) SwitchToTab(tab);
        }

        public void InitializeTabSizeHandler()
        {
            EnsureUi();
            try
            {
                var border = _ui.TabManager?.TabsBorderControl;
                if (border != null)
                {
                    border.SizeChanged -= TabsBorder_SizeChanged;
                    border.SizeChanged += TabsBorder_SizeChanged;
                }
            }
            catch { }
        }

        private void TabsBorder_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            UpdateTabWidths();
            var border = _ui.TabManager?.TabsBorderControl;
            var scrollViewer = FindScrollViewer(border);
            if (scrollViewer != null && scrollViewer.ExtentWidth > scrollViewer.ViewportWidth)
            {
                scrollViewer.ScrollToRightEnd();
            }
        }

        private ScrollViewer FindScrollViewer(DependencyObject parent)
        {
            if (parent == null) return null;
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is ScrollViewer sv) return sv;
                var result = FindScrollViewer(child);
                if (result != null) return result;
            }
            return null;
        }

        private void UpdateTabWidths()
        {
            EnsureUi();
            if (_tabs.Count == 0) return;
            if (_ui.TabManager?.TabsBorderControl == null) return;

            var border = _ui.TabManager.TabsBorderControl;
            _widthCalculator?.UpdateTabWidths(border, _tabs);
        }
    }
}
