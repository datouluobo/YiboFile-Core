using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Controls;
using YiboFile.Controllers;
using YiboFile.ViewModels;
using YiboFile.ViewModels.Messaging;
using YiboFile.ViewModels.Messaging.Messages;

namespace YiboFile.Controls
{
    /// <summary>
    /// NavigationRailControl 的交互逻辑
    /// 导航工具栏 - 混合架构实现（保持向后兼容）
    /// </summary>
    public partial class NavigationRailControl : UserControl
    {
        // 向后兼容：保留事件供外部使用
        // public event EventHandler LayoutFocusRequested; // Unused
        // public event EventHandler LayoutWorkRequested; // Unused
        // public event EventHandler LayoutFullRequested; // Unused
        // public event EventHandler DualPaneToggleRequested; // Unused
        // public event EventHandler SettingsRequested; // Moved to MVVM Command
        // public event EventHandler AboutRequested;    // Moved to MVVM Command

        public Button NavPathButton => FindName("PathButton") as Button;
        public Button NavLibraryButton => FindName("LibraryButton") as Button;
        public Button NavTagButton => FindName("TagButton") as Button;

        private NavigationRailCoordinator _coordinator;

        /// <summary>
        /// 获取或设置 ViewModel
        /// </summary>
        public NavigationRailViewModel ViewModel
        {
            get => DataContext as NavigationRailViewModel;
            set => DataContext = value;
        }

        /// <summary>
        /// 获取或设置 Coordinator
        /// </summary>
        public NavigationRailCoordinator Coordinator
        {
            get => _coordinator;
            set => _coordinator = value;
        }

        public NavigationRailControl()
        {
            InitializeComponent();
        }

        /// <summary>
        /// 设置消息总线以桥接 ViewModel 消息到事件（向后兼容）
        /// </summary>
        public void SetupMessageBridge(IMessageBus messageBus)
        {
            /* 移除可能导致循环的订阅（改为由 Coordinator 直接处理）
            messageBus.Subscribe<NavigationModeChangedMessage>(msg =>
            {
                NavigationModeChanged?.Invoke(this, msg.Mode);
            });
            */

            /* 
             * 移除 LayoutModeChangedMessage 的事件桥接
             * 原因：MainWindow 可能监听这些事件并重新发布 Request 消息，导致死循环。
             * 现代架构中，UI 更新应由 LayoutEventHandler 处理，不再依赖这些事件。
            
            messageBus.Subscribe<LayoutModeChangedMessage>(msg =>
            {
                switch (msg.Mode)
                {
                    case "Focus":
                        LayoutFocusRequested?.Invoke(this, EventArgs.Empty);
                        break;
                    case "Work":
                        LayoutWorkRequested?.Invoke(this, EventArgs.Empty);
                        break;
                    case "Full":
                        LayoutFullRequested?.Invoke(this, EventArgs.Empty);
                        break;
                }
            });

            messageBus.Subscribe<DualPaneModeToggledMessage>(msg =>
            {
                DualPaneToggleRequested?.Invoke(this, EventArgs.Empty);
            });
            */

        }

        // 按钮字段由 XAML 生成代码自动提供 (Internal 访问)
        // 外部如需访问，可直接使用 XAML 生成的字段名 (因为它们是 internal 的，且在同一程序集)
        // 或者保留以下空行以清理冲突

        /// <summary>
        /// 外部设置导航模式（用于配置加载等场景）
        /// </summary>
        public void SetActiveMode(string mode)
        {
            _coordinator?.SetNavigationMode(mode);
        }

        /// <summary>
        /// 外部设置布局模式
        /// </summary>
        public void SetLayoutMode(string mode)
        {
            _coordinator?.SetLayoutMode(mode);
        }

        // --- Drag and Drop Logic ---
        
        private Point _dragStartPoint;

        private void RailItem_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _dragStartPoint = e.GetPosition(null);
        }

        private void RailItem_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                Point mousePos = e.GetPosition(null);
                Vector diff = _dragStartPoint - mousePos;

                if (Math.Abs(diff.X) > SystemParameters.MinimumHorizontalDragDistance ||
                    Math.Abs(diff.Y) > SystemParameters.MinimumVerticalDragDistance)
                {
                    if (sender is Button button && button.DataContext is NavigationRailItem railItem)
                    {
                        DragDrop.DoDragDrop(button, railItem, DragDropEffects.Move);
                    }
                }
            }
        }

        private void RailItem_DragOver(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(typeof(NavigationRailItem)))
            {
                e.Effects = DragDropEffects.Move;
                e.Handled = true;
            }
        }

        private void RailItem_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(typeof(NavigationRailItem)))
            {
                var droppedItem = e.Data.GetData(typeof(NavigationRailItem)) as NavigationRailItem;
                if (sender is Button targetButton && targetButton.DataContext is NavigationRailItem targetItem)
                {
                    if (droppedItem != targetItem && ViewModel != null)
                    {
                        bool insertAfter = e.GetPosition(targetButton).Y > targetButton.ActualHeight / 2;

                        if (ViewModel.TopItems.Contains(droppedItem)) ViewModel.TopItems.Remove(droppedItem);
                        if (ViewModel.BottomItems.Contains(droppedItem)) ViewModel.BottomItems.Remove(droppedItem);

                        if (ViewModel.TopItems.Contains(targetItem))
                        {
                            int index = ViewModel.TopItems.IndexOf(targetItem);
                            if (insertAfter) index++;
                            ViewModel.TopItems.Insert(index, droppedItem);
                        }
                        else if (ViewModel.BottomItems.Contains(targetItem))
                        {
                            int index = ViewModel.BottomItems.IndexOf(targetItem);
                            if (insertAfter) index++;
                            ViewModel.BottomItems.Insert(index, droppedItem);
                        }
                        
                        ViewModel.SaveSettings();
                    }
                }
                e.Handled = true;
            }
        }

        private void Spacer_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(typeof(NavigationRailItem)))
            {
                var droppedItem = e.Data.GetData(typeof(NavigationRailItem)) as NavigationRailItem;
                if (ViewModel != null && droppedItem != null)
                {
                    if (ViewModel.TopItems.Contains(droppedItem)) ViewModel.TopItems.Remove(droppedItem);
                    if (ViewModel.BottomItems.Contains(droppedItem)) ViewModel.BottomItems.Remove(droppedItem);

                    double dropY = e.GetPosition(this).Y;
                    double middleY = this.ActualHeight / 2;

                    if (dropY < middleY)
                    {
                        ViewModel.TopItems.Add(droppedItem);
                    }
                    else
                    {
                        ViewModel.BottomItems.Insert(0, droppedItem);
                    }

                    ViewModel.SaveSettings();
                }
                e.Handled = true;
            }
        }
    }
}
