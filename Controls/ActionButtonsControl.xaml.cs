using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace YiboFile.Controls
{
    /// <summary>
    /// 动态操作按钮控件
    /// 根据当前导航模式显示不同的操作按钮，支持拖拽排序
    /// </summary>
    public partial class ActionButtonsControl : UserControl
    {
        // 内部类：可拖动按钮
        private class DraggableButton
        {
            public Button Button { get; set; }
            public string ActionName { get; set; }
            public RoutedEventHandler ClickHandler { get; set; }
        }

        // 内部类：操作项（按钮或分隔符）
        private class ActionItem
        {
            public DraggableButton Button { get; set; }
            public Separator Separator { get; set; }
            public bool IsSeparator => Separator != null;
        }

        private List<DraggableButton> _currentActionButtons = new List<DraggableButton>();
        private List<ActionItem> _actionItems = new List<ActionItem>();
        private DraggableButton _draggingButton = null;
        private System.Windows.Point _buttonDragStartPoint;
        private bool _isDragging = false;

        // 事件：按钮点击
        public event RoutedEventHandler ButtonClick;

        public ActionButtonsControl()
        {
            InitializeComponent();
        }

        /// <summary>
        /// 更新操作按钮（根据模式）
        /// </summary>
        public void UpdateActionButtons(StackPanel sourcePanel, Func<string, RoutedEventHandler> getClickHandler)
        {
            if (sourcePanel == null || ActionButtonsContainer == null)
                return;

            // 清空现有按钮
            ActionButtonsContainer.Children.Clear();
            _currentActionButtons.Clear();
            _actionItems.Clear();

            // 从源面板中提取按钮和分隔符
            foreach (var child in sourcePanel.Children)
            {
                if (child is Button btn)
                {
                    var newBtn = new Button
                    {
                        Content = btn.Content,
                        Style = btn.Style,
                        Margin = new Thickness(0, 0, 4, 0)
                    };

                    // 获取点击处理程序
                    string content = btn.Content?.ToString() ?? "";
                    RoutedEventHandler handler = getClickHandler?.Invoke(content);
                    if (handler != null)
                    {
                        newBtn.Click += (s, e) =>
                        {
                            handler(s, e);
                            ButtonClick?.Invoke(s, e);
                        };
                    }

                    var draggableBtn = new DraggableButton
                    {
                        Button = newBtn,
                        ActionName = content,
                        ClickHandler = handler
                    };

                    // 添加拖动支持
                    AddDragSupport(draggableBtn);

                    _currentActionButtons.Add(draggableBtn);
                    _actionItems.Add(new ActionItem { Button = draggableBtn });
                }
                else if (child is Separator sep)
                {
                    var newSeparator = new Separator { Margin = new Thickness(16, 0, 16, 0) };
                    _actionItems.Add(new ActionItem { Separator = newSeparator });
                }
            }

            RefreshActionButtons();
        }

        private void AddDragSupport(DraggableButton draggableBtn)
        {
            draggableBtn.Button.PreviewMouseLeftButtonDown += (s, e) =>
            {
                if (Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl))
                {
                    _draggingButton = draggableBtn;
                    _buttonDragStartPoint = e.GetPosition(ActionButtonsContainer);
                    _isDragging = false;
                    draggableBtn.Button.CaptureMouse();
                    e.Handled = false;
                }
            };

            draggableBtn.Button.PreviewMouseMove += (s, e) =>
            {
                if (_draggingButton == draggableBtn && draggableBtn.Button.IsMouseCaptured)
                {
                    var currentPoint = e.GetPosition(ActionButtonsContainer);
                    var delta = currentPoint - _buttonDragStartPoint;

                    if (!_isDragging && (Math.Abs(delta.X) > 5 || Math.Abs(delta.Y) > 5))
                    {
                        _isDragging = true;
                        draggableBtn.Button.Cursor = Cursors.Hand;
                    }

                    if (_isDragging)
                    {
                        var insertIndex = GetInsertIndex(currentPoint, draggableBtn);
                        if (insertIndex >= 0)
                        {
                            var currentItemIndex = _actionItems.FindIndex(item => item.Button == draggableBtn);
                            if (currentItemIndex != insertIndex && insertIndex <= _actionItems.Count)
                            {
                                var item = _actionItems[currentItemIndex];
                                _actionItems.RemoveAt(currentItemIndex);
                                _actionItems.Insert(insertIndex, item);
                                RefreshActionButtons();
                                _buttonDragStartPoint = currentPoint;
                            }
                        }
                    }
                }
            };

            draggableBtn.Button.PreviewMouseLeftButtonUp += (s, e) =>
            {
                if (_draggingButton == draggableBtn)
                {
                    if (draggableBtn.Button.IsMouseCaptured)
                    {
                        draggableBtn.Button.ReleaseMouseCapture();
                    }
                    _draggingButton = null;
                    _isDragging = false;
                    draggableBtn.Button.Cursor = Cursors.Arrow;
                }
            };
        }

        private int GetInsertIndex(System.Windows.Point point, DraggableButton draggingBtn)
        {
            if (ActionButtonsContainer == null) return -1;

            double accumulatedX = 0;
            int itemIndex = 0;

            foreach (var element in ActionButtonsContainer.Children.OfType<FrameworkElement>())
            {
                var width = element.ActualWidth > 0 ? element.ActualWidth : (element is Separator ? 16 : 80);

                if (point.X < accumulatedX + width / 2)
                {
                    return itemIndex;
                }

                accumulatedX += width;
                itemIndex++;
            }

            return itemIndex;
        }

        private void RefreshActionButtons()
        {
            if (ActionButtonsContainer == null)
                return;

            ActionButtonsContainer.Children.Clear();

            foreach (var item in _actionItems)
            {
                if (item == null) continue;

                UIElement elementToAdd = null;

                if (item.IsSeparator)
                {
                    if (item.Separator != null)
                    {
                        elementToAdd = item.Separator;
                    }
                }
                else
                {
                    if (item.Button != null && item.Button.Button != null)
                    {
                        elementToAdd = item.Button.Button;
                    }
                }

                if (elementToAdd != null)
                {
                    ActionButtonsContainer.Children.Add(elementToAdd);
                }
            }
        }

        /// <summary>
        /// 清空所有按钮
        /// </summary>
        public void Clear()
        {
            if (ActionButtonsContainer != null)
            {
                ActionButtonsContainer.Children.Clear();
            }
            _currentActionButtons.Clear();
            _actionItems.Clear();
        }
    }
}





























