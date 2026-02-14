using System;
using System.Windows;
using YiboFile.ViewModels.Messaging;
using YiboFile.ViewModels.Messaging.Messages;
using YiboFile.Models;

namespace YiboFile.Services.UI
{
    /// <summary>
    /// 事件桥接服务
    /// 负责将 MainWindow 的 XAML 事件转换为 MessageBus 消息，
    /// 从而消除 Code-behind 中的业务逻辑处理。
    /// </summary>
    public class EventBridgeService : IDisposable
    {
        private readonly MainWindow _window;
        private readonly IMessageBus _messageBus;
        private bool _isDisposed;

        public EventBridgeService(MainWindow window, IMessageBus messageBus)
        {
            _window = window ?? throw new ArgumentNullException(nameof(window));
            _messageBus = messageBus ?? throw new ArgumentNullException(nameof(messageBus));

            HookEvents();
        }

        private void HookEvents()
        {
            // 窗口生命周期事件
            _window.Loaded += OnWindowLoaded;
            _window.Closing += OnWindowClosing;
            _window.LocationChanged += OnWindowLocationChanged;
            _window.StateChanged += OnWindowStateChanged;
            _window.SizeChanged += OnWindowSizeChanged;
            _window.ContentRendered += OnWindowContentRendered;

            // 分割器事件
            if (_window.SplitterLeft != null)
            {
                _window.SplitterLeft.DragCompleted += OnLeftSplitterDragCompleted;
            }

            if (_window.SplitterRight != null)
            {
                _window.SplitterRight.DragCompleted += OnRightSplitterDragCompleted;
            }

            // 侧边栏事件
            if (_window.NavigationRail != null)
            {
                _window.NavigationRail.Loaded += OnNavigationRailLoaded;
                // NavigationModeChanged 现在通过 MVVM Command 直接发布消息，不再需要事件桥接
                // _window.NavigationRail.NavigationModeChanged += OnRailNavigationModeChanged;

                _window.NavigationRail.SettingsRequested += OnRailSettingsRequested;
                _window.NavigationRail.AboutRequested += OnRailAboutRequested;
            }

            // 文件浏览器事件
            if (_window.FileBrowser != null)
            {
                _window.FileBrowser.ViewModeChanged += OnPrimaryViewModeChanged;
                _window.FileBrowser.InfoHeightChanged += OnPrimaryInfoHeightChanged;
                _window.FileBrowser.FilesSelectionChanged += OnPrimarySelectionChanged;
                _window.FileBrowser.TagClicked += OnPrimaryTagClicked;
                _window.FileBrowser.GridViewColumnHeaderClick += OnPrimaryColumnHeaderClick;
            }

            if (_window.SecondFileBrowser != null)
            {
                _window.SecondFileBrowser.ViewModeChanged += OnSecondaryViewModeChanged;
                _window.SecondFileBrowser.InfoHeightChanged += OnSecondaryInfoHeightChanged;
                _window.SecondFileBrowser.FilesSelectionChanged += OnSecondarySelectionChanged;
                _window.SecondFileBrowser.TagClicked += OnSecondaryTagClicked;
                _window.SecondFileBrowser.GridViewColumnHeaderClick += OnSecondaryColumnHeaderClick;
            }

            // 右侧面板事件
            if (_window.RightPanel != null)
            {
                _window.RightPanel.NotesHeightChanged += OnRightPanelNotesHeightChanged;
            }

            // 键盘事件
            _window.PreviewKeyDown += OnWindowPreviewKeyDown;
            _window.KeyDown += OnWindowKeyDown;
        }

        private void UnhookEvents()
        {
            _window.Loaded -= OnWindowLoaded;
            _window.Closing -= OnWindowClosing;
            _window.LocationChanged -= OnWindowLocationChanged;
            _window.StateChanged -= OnWindowStateChanged;
            _window.SizeChanged -= OnWindowSizeChanged;
            _window.ContentRendered -= OnWindowContentRendered;

            if (_window.SplitterLeft != null)
            {
                _window.SplitterLeft.DragCompleted -= OnLeftSplitterDragCompleted;
            }

            if (_window.SplitterRight != null)
            {
                _window.SplitterRight.DragCompleted -= OnRightSplitterDragCompleted;
            }

            if (_window.NavigationRail != null)
            {
                _window.NavigationRail.Loaded -= OnNavigationRailLoaded;
                // _window.NavigationRail.NavigationModeChanged -= OnRailNavigationModeChanged;

                _window.NavigationRail.SettingsRequested -= OnRailSettingsRequested;
                _window.NavigationRail.AboutRequested -= OnRailAboutRequested;
            }

            if (_window.FileBrowser != null)
            {
                _window.FileBrowser.ViewModeChanged -= OnPrimaryViewModeChanged;
                _window.FileBrowser.InfoHeightChanged -= OnPrimaryInfoHeightChanged;
                _window.FileBrowser.FilesSelectionChanged -= OnPrimarySelectionChanged;
                _window.FileBrowser.TagClicked -= OnPrimaryTagClicked;
                _window.FileBrowser.GridViewColumnHeaderClick -= OnPrimaryColumnHeaderClick;
            }

            if (_window.SecondFileBrowser != null)
            {
                _window.SecondFileBrowser.ViewModeChanged -= OnSecondaryViewModeChanged;
                _window.SecondFileBrowser.InfoHeightChanged -= OnSecondaryInfoHeightChanged;
                _window.SecondFileBrowser.FilesSelectionChanged -= OnSecondarySelectionChanged;
                _window.SecondFileBrowser.TagClicked -= OnSecondaryTagClicked;
                _window.SecondFileBrowser.GridViewColumnHeaderClick -= OnSecondaryColumnHeaderClick;
            }

            if (_window.RightPanel != null)
            {
                _window.RightPanel.NotesHeightChanged -= OnRightPanelNotesHeightChanged;
            }

            _window.PreviewKeyDown -= OnWindowPreviewKeyDown;
            _window.KeyDown -= OnWindowKeyDown;
        }

        #region Event Handlers

        private void OnWindowLoaded(object sender, RoutedEventArgs e)
        {
            _messageBus.Publish(new WindowLoadedMessage
            {
                ActualWidth = _window.ActualWidth,
                ActualHeight = _window.ActualHeight,
                WindowState = _window.WindowState
            });
        }

        private void OnWindowClosing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            var message = new WindowClosingMessage { Cancel = false };
            _messageBus.Publish(message);

            if (message.Cancel)
            {
                e.Cancel = true;
            }
        }

        private void OnWindowLocationChanged(object sender, EventArgs e)
        {
            _messageBus.Publish(new WindowLocationChangedMessage
            {
                Left = _window.Left,
                Top = _window.Top
            });
        }

        private void OnWindowStateChanged(object sender, EventArgs e)
        {
            _messageBus.Publish(new WindowStateChangedMessage
            {
                NewState = _window.WindowState
            });
        }

        private void OnWindowSizeChanged(object sender, SizeChangedEventArgs e)
        {
            _messageBus.Publish(new WindowSizeChangedMessage
            {
                NewWidth = e.NewSize.Width,
                NewHeight = e.NewSize.Height
            });
        }

        private void OnWindowContentRendered(object sender, EventArgs e)
        {
            _messageBus.Publish(new WindowContentRenderedMessage
            {
                WindowState = _window.WindowState
            });
        }

        private void OnLeftSplitterDragCompleted(object sender, System.Windows.Controls.Primitives.DragCompletedEventArgs e)
        {
            _messageBus.Publish(new SplitterDragCompletedMessage
            {
                SplitterName = "Left",
                NewValue = _window.ColLeft.ActualWidth
            });
        }

        private void OnRightSplitterDragCompleted(object sender, System.Windows.Controls.Primitives.DragCompletedEventArgs e)
        {
            _messageBus.Publish(new SplitterDragCompletedMessage
            {
                SplitterName = "Right",
                NewValue = _window.ColRight.ActualWidth
            });
        }

        private void OnNavigationRailLoaded(object sender, RoutedEventArgs e)
        {
            _messageBus.Publish(new NavigationRailLoadedMessage());
        }

        private void OnRailNavigationModeChanged(object sender, string mode)
        {
            _messageBus.Publish(new NavigationModeChangedMessage(mode));
        }


        private void OnRailSettingsRequested(object sender, EventArgs e) => _messageBus.Publish(new ShowSettingsMessage());
        private void OnRailAboutRequested(object sender, EventArgs e) => _messageBus.Publish(new ShowAboutMessage());

        private void OnPrimaryViewModeChanged(object sender, string mode)
        {
            _messageBus.Publish(new ViewModeChangedMessage(mode, YiboFile.Services.Navigation.PaneId.Main));
        }

        private void OnSecondaryViewModeChanged(object sender, string mode)
        {
            _messageBus.Publish(new ViewModeChangedMessage(mode, YiboFile.Services.Navigation.PaneId.Second));
        }

        private void OnPrimaryInfoHeightChanged(object sender, double height)
        {
            _messageBus.Publish(new InfoHeightChangedMessage { NewHeight = height, TargetPane = YiboFile.Services.Navigation.PaneId.Main });
        }

        private void OnSecondaryInfoHeightChanged(object sender, double height)
        {
            _messageBus.Publish(new InfoHeightChangedMessage { NewHeight = height, TargetPane = YiboFile.Services.Navigation.PaneId.Second });
        }

        private void OnRightPanelNotesHeightChanged(object sender, double height)
        {
            _messageBus.Publish(new NotesHeightChangedMessage { NewHeight = height });
        }

        private void OnWindowPreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            _messageBus.Publish(new WindowPreviewKeyDownMessage { EventArgs = e });
        }

        private void OnWindowKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            _messageBus.Publish(new WindowKeyDownMessage { EventArgs = e });
        }

        private void OnPrimarySelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            _messageBus.Publish(new SelectionChangedMessage { SelectedItems = _window.FileBrowser.FilesSelectedItems, TargetPane = YiboFile.Services.Navigation.PaneId.Main });
        }

        private void OnSecondarySelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            _messageBus.Publish(new SelectionChangedMessage { SelectedItems = _window.SecondFileBrowser.FilesSelectedItems, TargetPane = YiboFile.Services.Navigation.PaneId.Second });
        }

        private void OnPrimaryTagClicked(object sender, TagViewModel tag)
        {
            _messageBus.Publish(new TagClickedMessage { Tag = tag, TargetPane = YiboFile.Services.Navigation.PaneId.Main });
        }

        private void OnSecondaryTagClicked(object sender, TagViewModel tag)
        {
            _messageBus.Publish(new TagClickedMessage { Tag = tag, TargetPane = YiboFile.Services.Navigation.PaneId.Second });
        }

        private void OnPrimaryColumnHeaderClick(object sender, RoutedEventArgs e)
        {
            if (e.OriginalSource is System.Windows.Controls.GridViewColumnHeader header)
            {
                _messageBus.Publish(new GridViewColumnHeaderClickedMessage { Header = header, TargetPane = YiboFile.Services.Navigation.PaneId.Main });
            }
        }

        private void OnSecondaryColumnHeaderClick(object sender, RoutedEventArgs e)
        {
            if (e.OriginalSource is System.Windows.Controls.GridViewColumnHeader header)
            {
                _messageBus.Publish(new GridViewColumnHeaderClickedMessage { Header = header, TargetPane = YiboFile.Services.Navigation.PaneId.Second });
            }
        }

        #endregion

        public void Dispose()
        {
            if (!_isDisposed)
            {
                UnhookEvents();
                _isDisposed = true;
            }
        }
    }
}
