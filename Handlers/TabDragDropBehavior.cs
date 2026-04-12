using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using YiboFile.Models;
using YiboFile.Services.Tabs;

namespace YiboFile.Handlers
{
    /// <summary>
    /// 处理标签页拖拽的附加行为类
    /// 它挂载在 ItemsControl (或 TabManagerControl) 上，自动管理拖拽事件和排序/跨栏逻辑
    /// </summary>
    public static class TabDragDropBehavior
    {
        public static readonly DependencyProperty IsEnabledProperty =
            DependencyProperty.RegisterAttached(
                "IsEnabled", typeof(bool), typeof(TabDragDropBehavior),
                new PropertyMetadata(false, OnIsEnabledChanged));

        public static bool GetIsEnabled(DependencyObject obj) => (bool)obj.GetValue(IsEnabledProperty);
        public static void SetIsEnabled(DependencyObject obj, bool value) => obj.SetValue(IsEnabledProperty, value);

        private static void OnIsEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ItemsControl itemsControl)
            {
                if ((bool)e.NewValue)
                {
                    itemsControl.AllowDrop = true;
                    itemsControl.PreviewMouseLeftButtonDown += ItemsControl_PreviewMouseLeftButtonDown;
                    itemsControl.PreviewMouseMove += ItemsControl_PreviewMouseMove;
                    itemsControl.DragOver += ItemsControl_DragOver;
                    itemsControl.DragLeave += ItemsControl_DragLeave;
                    itemsControl.Drop += ItemsControl_Drop;
                }
                else
                {
                    itemsControl.AllowDrop = false;
                    itemsControl.PreviewMouseLeftButtonDown -= ItemsControl_PreviewMouseLeftButtonDown;
                    itemsControl.PreviewMouseMove -= ItemsControl_PreviewMouseMove;
                    itemsControl.DragOver -= ItemsControl_DragOver;
                    itemsControl.DragLeave -= ItemsControl_DragLeave;
                    itemsControl.Drop -= ItemsControl_Drop;
                }
            }
        }

        private static Point _dragStartPoint;
        private static FrameworkElement _draggedItemContainer;
        private static PathTab _draggedTab;
        private static Controls.TabDropIndicatorAdorner _dropIndicator;
        private static AdornerLayer _adornerLayer;

        private static void ItemsControl_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _dragStartPoint = e.GetPosition(null);
            _draggedItemContainer = GetItemContainerFromPoint((ItemsControl)sender, e.GetPosition((ItemsControl)sender));
            if (_draggedItemContainer != null)
            {
                _draggedTab = _draggedItemContainer.DataContext as PathTab;
            }
        }

        private static void ItemsControl_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton != MouseButtonState.Pressed || _draggedItemContainer == null || _draggedTab == null) return;

            Point currentPosition = e.GetPosition(null);
            Vector diff = _dragStartPoint - currentPosition;

            if (Math.Abs(diff.X) > SystemParameters.MinimumHorizontalDragDistance ||
                Math.Abs(diff.Y) > SystemParameters.MinimumVerticalDragDistance)
            {
                var currentTab = _draggedTab;
                var currentContainer = _draggedItemContainer;

                // 立即清空，防止 DoDragDrop 内部的消息泵触发由队列积压引发的重入（嵌套调用导致的 NullReferenceException）
                _draggedTab = null;
                _draggedItemContainer = null;

                var itemsControl = (ItemsControl)sender;
                var sourceService = FindTabService(itemsControl);

                var dragData = new TabDragData
                {
                    Tab = currentTab,
                    SourceService = sourceService,
                    IsPinned = currentTab.IsPinned
                };

                DataObject dataObject = new DataObject(TabDragDropFormats.TabData, dragData);
                
                // 也添加兼容的旧格式以便拖拽到其他不支持新格式的地方(可选)
                dataObject.SetData("YiboFile_TabKey", sourceService?.GetTabKey(currentTab));
                dataObject.SetData("YiboFile_TabPinned", currentTab.IsPinned);

                currentTab.IsDragging = true;
                DragDrop.DoDragDrop(currentContainer, dataObject, DragDropEffects.Move | DragDropEffects.Copy);
                
                // 拖拽完成
                currentTab.IsDragging = false;
                RemoveDropIndicator();
            }
        }

        private static void ItemsControl_DragOver(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(TabDragDropFormats.TabData))
            {
                e.Handled = true;
                var dragData = e.Data.GetData(TabDragDropFormats.TabData) as TabDragData;
                if (dragData == null)
                {
                    e.Effects = DragDropEffects.None;
                    RemoveDropIndicator();
                    return;
                }

                bool isCopy = (e.KeyStates & DragDropKeyStates.ControlKey) == DragDropKeyStates.ControlKey;
                e.Effects = isCopy ? DragDropEffects.Copy : DragDropEffects.Move;

                var itemsControl = (ItemsControl)sender;
                UpdateDropIndicator(itemsControl, e.GetPosition(itemsControl), dragData);
            }
            else
            {
                e.Effects = DragDropEffects.None;
                RemoveDropIndicator();
            }
        }

        private static void ItemsControl_DragLeave(object sender, DragEventArgs e)
        {
            RemoveDropIndicator();
        }

        private static void ItemsControl_Drop(object sender, DragEventArgs e)
        {
            RemoveDropIndicator();
            
            if (e.Data.GetDataPresent(TabDragDropFormats.TabData))
            {
                e.Handled = true;
                var dragData = e.Data.GetData(TabDragDropFormats.TabData) as TabDragData;
                if (dragData == null) return;

                var itemsControl = (ItemsControl)sender;
                var targetService = FindTabService(itemsControl);
                if (targetService == null) return;

                // 计算放置索引
                int targetIndex = CalculateDropIndex(itemsControl, e.GetPosition(itemsControl));
                bool isCopy = (e.KeyStates & DragDropKeyStates.ControlKey) == DragDropKeyStates.ControlKey;

                if (dragData.SourceService == targetService)
                {
                    // 同栏内拖拽排序
                    if (isCopy)
                    {
                        targetService.CreateDuplicateTab(dragData.Tab);
                        // 此时复制出的可能在末尾，可以选择进行 UpdateTabOrderAfterDrag
                    }
                    else
                    {
                        int pinnedCount = targetService.Tabs.Count(t => t.IsPinned);
                        targetService.UpdateTabOrderAfterDrag(dragData.Tab, targetIndex, pinnedCount);
                    }
                }
                else
                {
                    // 跨栏拖拽
                    if (!isCopy)
                    {
                        dragData.SourceService?.RemoveTab(dragData.Tab);
                    }

                    // 需要在新 Service 中创建标签
                    PathTab newTab = null;
                    if (dragData.Tab.ContentTypeId == TabContentTypes.Library && dragData.Tab.Library != null)
                    {
                        targetService.OpenLibraryTab(dragData.Tab.Library, forceNewTab: true, activate: true);
                        newTab = targetService.ActiveTab;
                    }
                    else if (dragData.Tab.ContentTypeId == TabContentTypes.Tag)
                    {
                         targetService.CreateTagTab(dragData.Tab.Title, forceNewTab: true, activate: true);
                         newTab = targetService.ActiveTab;
                    }
                    else
                    {
                        targetService.CreatePathTab(dragData.Tab.Path, forceNewTab: true, skipValidation: true, activate: true);
                        newTab = targetService.ActiveTab;
                    }

                    if (newTab != null)
                    {
                        if (dragData.IsPinned && !newTab.IsPinned)
                        {
                            targetService.TogglePinTab(newTab);
                        }

                        int pinnedCount = targetService.Tabs.Count(t => t.IsPinned);
                        if (newTab.IsPinned) targetIndex = Math.Min(targetIndex, pinnedCount);
                        else targetIndex = Math.Max(targetIndex, pinnedCount);

                        targetService.UpdateTabOrderAfterDrag(newTab, targetIndex, pinnedCount);
                    }
                }
            }
        }

        private static FrameworkElement GetItemContainerFromPoint(ItemsControl itemsControl, Point p)
        {
            HitTestResult hitTestResult = VisualTreeHelper.HitTest(itemsControl, p);
            DependencyObject element = hitTestResult?.VisualHit;

            while (element != null)
            {
                if (element == itemsControl) break;
                if (itemsControl.ItemContainerGenerator.ItemFromContainer(element) != DependencyProperty.UnsetValue)
                {
                    return element as FrameworkElement;
                }
                element = VisualTreeHelper.GetParent(element);
            }
            return null;
        }

        private static int CalculateDropIndex(ItemsControl itemsControl, Point p)
        {
            int index = 0;
            for (int i = 0; i < itemsControl.Items.Count; i++)
            {
                var container = itemsControl.ItemContainerGenerator.ContainerFromIndex(i) as FrameworkElement;
                if (container != null)
                {
                    // 转换为 ItemsControl 坐标系
                    Point containerPos = container.TransformToAncestor(itemsControl).Transform(new Point(0, 0));
                    double midX = containerPos.X + container.ActualWidth / 2;
                    if (p.X > midX)
                    {
                        index = i + 1;
                    }
                }
            }
            return index;
        }

        private static void UpdateDropIndicator(ItemsControl itemsControl, Point p, TabDragData dragData)
        {
            // 获取 ItemsPanel (StackPanel)
            if (VisualTreeHelper.GetChildrenCount(itemsControl) == 0) return;
            var border = VisualTreeHelper.GetChild(itemsControl, 0) as Border;
            if (border == null) return;
            var itemsPresenter = border.Child as ItemsPresenter;
            if (itemsPresenter == null) return;
            
            var panel = VisualTreeHelper.GetChild(itemsPresenter, 0) as Panel;
            if (panel == null) return;

            // 无论之前有没有，都尝试重新获取，因为这可能是个静态方法跨越了左/右两个不同的 ItemsControl
            _adornerLayer = AdornerLayer.GetAdornerLayer(panel);
            if (_adornerLayer == null) return;

            if (_dropIndicator == null)
            {
                _dropIndicator = new Controls.TabDropIndicatorAdorner(panel);
                _adornerLayer.Add(_dropIndicator);
            }
            else
            {
                // 如果 _dropIndicator 已经存在，确保它在当前的 AdornerLayer 中
                // (由于涉及到跨栏，左侧栏的 indicator 不能直接画在右侧，需要移除重建或切换)
                if (_dropIndicator.AdornedElement != panel)
                {
                    RemoveDropIndicator();
                    _adornerLayer = AdornerLayer.GetAdornerLayer(panel);
                    _dropIndicator = new Controls.TabDropIndicatorAdorner(panel);
                    _adornerLayer?.Add(_dropIndicator);
                }
            }

            int index = CalculateDropIndex(itemsControl, p);
            
            // 计算插入线的X坐标
            double xCoord = 0;
            double height = panel.ActualHeight;

            if (index == 0)
            {
                xCoord = 0;
            }
            else if (index >= itemsControl.Items.Count)
            {
                var lastItem = itemsControl.ItemContainerGenerator.ContainerFromIndex(itemsControl.Items.Count - 1) as FrameworkElement;
                if (lastItem != null)
                {
                    var pos = lastItem.TransformToAncestor(panel).Transform(new Point(0, 0));
                    xCoord = pos.X + lastItem.ActualWidth;
                }
            }
            else
            {
                var targetItem = itemsControl.ItemContainerGenerator.ContainerFromIndex(index) as FrameworkElement;
                if (targetItem != null)
                {
                    var pos = targetItem.TransformToAncestor(panel).Transform(new Point(0, 0));
                    xCoord = pos.X;
                }
            }

            _dropIndicator.UpdatePosition(xCoord, height);
        }

        private static void RemoveDropIndicator()
        {
            if (_dropIndicator != null && _adornerLayer != null)
            {
                _adornerLayer.Remove(_dropIndicator);
                _dropIndicator = null;
            }
        }

        private static TabService FindTabService(ItemsControl itemsControl)
        {
            // 通过导航上下文或 DataContext 查找所属的 TabService
            // 在 YiboFile 中，TabManagerControl 通常会将其放入 OwnerWindow
            var window = Window.GetWindow(itemsControl) as MainWindow;
            if (window != null)
            {
                // 可以通过对比 DataContext 或 Tag 等判断左栏还是右栏
                // 但更好的方式是通过 ItemsControl 所在的 Grid/PaneId 定位
                var tabManager = itemsControl.TemplatedParent as Controls.TabManagerControl 
                    ?? FindParent<Controls.TabManagerControl>(itemsControl);

                if (tabManager == window.TabManager) return window._tabService;
                if (tabManager == window.SecondTabManager) return window._secondTabService;
            }
            return null;
        }

        private static T FindParent<T>(DependencyObject child) where T : DependencyObject
        {
            DependencyObject parentObject = VisualTreeHelper.GetParent(child);

            if (parentObject == null) return null;

            T parent = parentObject as T;
            if (parent != null)
                return parent;
            else
                return FindParent<T>(parentObject);
        }
    }
}
