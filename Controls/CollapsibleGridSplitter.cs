using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media.Animation;

namespace YiboFile.Controls
{
    /// <summary>
    /// 可折叠的 GridSplitter 控件，支持拖动调整大小和折叠/展开功能
    /// </summary>
    public class CollapsibleGridSplitter : GridSplitter
    {
        #region Events

        /// <summary>
        /// 当折叠状态发生改变时触发
        /// </summary>
        public event EventHandler<RoutedEventArgs> CollapsedStateChanged;

        #endregion

        #region Dependency Properties

        /// <summary>
        /// 折叠模式：Previous (仅折叠前面的面板), Next (仅折叠后面的面板), Both (两侧都可折叠)
        /// </summary>
        public static readonly DependencyProperty CollapseModeProperty =
            DependencyProperty.Register(nameof(CollapseMode), typeof(CollapseMode), typeof(CollapsibleGridSplitter),
                new PropertyMetadata(CollapseMode.Both, OnCollapseModeChanged));

        /// <summary>
        /// 分割器方向：Vertical (垂直) 或 Horizontal (水平)
        /// </summary>
        public static readonly DependencyProperty OrientationProperty =
            DependencyProperty.Register(nameof(Orientation), typeof(Orientation), typeof(CollapsibleGridSplitter),
                new PropertyMetadata(Orientation.Vertical, OnOrientationChanged));

        /// <summary>
        /// 动画持续时间（毫秒）
        /// </summary>
        public static readonly DependencyProperty AnimationDurationProperty =
            DependencyProperty.Register(nameof(AnimationDuration), typeof(int), typeof(CollapsibleGridSplitter),
                new PropertyMetadata(250));

        /// <summary>
        /// 前一个面板是否已折叠
        /// </summary>
        public static readonly DependencyProperty IsPreviousCollapsedProperty =
            DependencyProperty.Register(nameof(IsPreviousCollapsed), typeof(bool), typeof(CollapsibleGridSplitter),
                new PropertyMetadata(false, OnIsCollapsedChanged));

        /// <summary>
        /// 后一个面板是否已折叠
        /// </summary>
        public static readonly DependencyProperty IsNextCollapsedProperty =
            DependencyProperty.Register(nameof(IsNextCollapsed), typeof(bool), typeof(CollapsibleGridSplitter),
                new PropertyMetadata(false, OnIsCollapsedChanged));

        /// <summary>
        /// 当折叠一侧面板时，是否自动让另一侧面板填满剩余空间（设置为 Star）
        /// </summary>
        public static readonly DependencyProperty AutoFillNeighborProperty =
            DependencyProperty.Register(nameof(AutoFillNeighbor), typeof(bool), typeof(CollapsibleGridSplitter),
                new PropertyMetadata(false));

        #endregion

        #region Properties

        public CollapseMode CollapseMode
        {
            get => (CollapseMode)GetValue(CollapseModeProperty);
            set => SetValue(CollapseModeProperty, value);
        }

        public Orientation Orientation
        {
            get => (Orientation)GetValue(OrientationProperty);
            set => SetValue(OrientationProperty, value);
        }

        public int AnimationDuration
        {
            get => (int)GetValue(AnimationDurationProperty);
            set => SetValue(AnimationDurationProperty, value);
        }

        public bool IsPreviousCollapsed
        {
            get => (bool)GetValue(IsPreviousCollapsedProperty);
            private set => SetValue(IsPreviousCollapsedProperty, value);
        }

        public bool IsNextCollapsed
        {
            get => (bool)GetValue(IsNextCollapsedProperty);
            private set => SetValue(IsNextCollapsedProperty, value);
        }

        public bool AutoFillNeighbor
        {
            get => (bool)GetValue(AutoFillNeighborProperty);
            set => SetValue(AutoFillNeighborProperty, value);
        }

        #endregion

        #region Private Fields

        private Button _collapsePreviousButton;
        private Button _collapseNextButton;
        private GridLength _previousSize;
        private GridLength _nextSize;
        private double _previousActualLength;
        private double _nextActualLength;
        private const double MinSize = 0;
        private double _savedPreviousMinSize;
        private double _savedNextMinSize;

        // Neighbor saving
        private GridLength _savedNeighborSize;
        private bool _hasSavedNeighborSize = false;

        #endregion

        #region Constructor

        static CollapsibleGridSplitter()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(CollapsibleGridSplitter),
                new FrameworkPropertyMetadata(typeof(CollapsibleGridSplitter)));
        }

        #endregion

        #region Template Override

        public override void OnApplyTemplate()
        {
            base.OnApplyTemplate();

            // 解除之前的事件绑定
            if (_collapsePreviousButton != null)
                _collapsePreviousButton.Click -= CollapsePreviousButton_Click;
            if (_collapseNextButton != null)
                _collapseNextButton.Click -= CollapseNextButton_Click;

            // 获取模板元素
            _collapsePreviousButton = GetTemplateChild("PART_CollapsePreviousButton") as Button;
            _collapseNextButton = GetTemplateChild("PART_CollapseNextButton") as Button;

            // 绑定事件
            if (_collapsePreviousButton != null)
                _collapsePreviousButton.Click += CollapsePreviousButton_Click;
            if (_collapseNextButton != null)
                _collapseNextButton.Click += CollapseNextButton_Click;

            // 更新按钮可见性
            UpdateButtonVisibility();
        }

        #endregion

        #region Event Handlers

        private void CollapsePreviousButton_Click(object sender, RoutedEventArgs e)
        {
            if (IsPreviousCollapsed)
                ExpandPanel(PanelDirection.Previous);
            else
                CollapsePanel(PanelDirection.Previous);
        }

        private void CollapseNextButton_Click(object sender, RoutedEventArgs e)
        {
            if (IsNextCollapsed)
                ExpandPanel(PanelDirection.Next);
            else
                CollapsePanel(PanelDirection.Next);
        }

        private static void OnCollapseModeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is CollapsibleGridSplitter splitter)
                splitter.UpdateButtonVisibility();
        }

        private static void OnOrientationChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is CollapsibleGridSplitter splitter)
                splitter.UpdateButtonVisibility();
        }

        private static void OnIsCollapsedChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is CollapsibleGridSplitter splitter)
            {
                splitter.UpdateButtonVisibility();
                splitter.CollapsedStateChanged?.Invoke(splitter, new RoutedEventArgs());
            }
        }

        #endregion

        #region Collapse/Expand Methods

        private void CollapsePanel(PanelDirection direction)
        {
            var parentGrid = Parent as Grid;
            if (parentGrid == null) return;

            var currentColumn = Grid.GetColumn(this);
            var currentRow = Grid.GetRow(this);

            if (Orientation == Orientation.Vertical)
            {
                // 垂直分割器：折叠列
                var targetColumnIndex = direction == PanelDirection.Previous ? currentColumn - 1 : currentColumn + 1;
                if (targetColumnIndex < 0 || targetColumnIndex >= parentGrid.ColumnDefinitions.Count) return;

                var columnDef = parentGrid.ColumnDefinitions[targetColumnIndex];

                // 保存当前大小
                if (direction == PanelDirection.Previous)
                {
                    _previousSize = columnDef.Width;
                    _previousActualLength = columnDef.ActualWidth;
                    _savedPreviousMinSize = columnDef.MinWidth;
                    columnDef.MinWidth = 0;
                    IsPreviousCollapsed = true;
                }
                else
                {
                    _nextSize = columnDef.Width;
                    _nextActualLength = columnDef.ActualWidth;
                    _savedNextMinSize = columnDef.MinWidth;
                    columnDef.MinWidth = 0;
                    IsNextCollapsed = true;
                }

                // 处理 AutoFillNeighbor
                if (AutoFillNeighbor)
                {
                    var neighborIndex = direction == PanelDirection.Previous ? currentColumn + 1 : currentColumn - 1;
                    if (neighborIndex >= 0 && neighborIndex < parentGrid.ColumnDefinitions.Count)
                    {
                        var neighborCol = parentGrid.ColumnDefinitions[neighborIndex];
                        if (!neighborCol.Width.IsStar)
                        {
                            _savedNeighborSize = neighborCol.Width;
                            _hasSavedNeighborSize = true;
                            neighborCol.Width = new GridLength(1, GridUnitType.Star);
                        }
                        else
                        {
                            _hasSavedNeighborSize = false;
                        }
                    }
                }

                // 执行动画
                AnimateColumnWidth(columnDef, columnDef.ActualWidth, MinSize);
            }
            else
            {
                // 水平分割器：折叠行
                var targetRowIndex = direction == PanelDirection.Previous ? currentRow - 1 : currentRow + 1;
                if (targetRowIndex < 0 || targetRowIndex >= parentGrid.RowDefinitions.Count) return;

                var rowDef = parentGrid.RowDefinitions[targetRowIndex];

                // 保存当前大小
                if (direction == PanelDirection.Previous)
                {
                    _previousSize = rowDef.Height;
                    _previousActualLength = rowDef.ActualHeight;
                    _savedPreviousMinSize = rowDef.MinHeight;
                    rowDef.MinHeight = 0;
                    IsPreviousCollapsed = true;
                }
                else
                {
                    _nextSize = rowDef.Height;
                    _nextActualLength = rowDef.ActualHeight;
                    _savedNextMinSize = rowDef.MinHeight;
                    rowDef.MinHeight = 0;
                    IsNextCollapsed = true;
                }

                // 处理 AutoFillNeighbor
                if (AutoFillNeighbor)
                {
                    var neighborIndex = direction == PanelDirection.Previous ? currentRow + 1 : currentRow - 1;
                    if (neighborIndex >= 0 && neighborIndex < parentGrid.RowDefinitions.Count)
                    {
                        var neighborRow = parentGrid.RowDefinitions[neighborIndex];
                        if (!neighborRow.Height.IsStar)
                        {
                            _savedNeighborSize = neighborRow.Height;
                            _hasSavedNeighborSize = true;
                            neighborRow.Height = new GridLength(1, GridUnitType.Star);
                        }
                        else
                        {
                            _hasSavedNeighborSize = false;
                        }
                    }
                }

                // 执行动画
                AnimateRowHeight(rowDef, rowDef.ActualHeight, MinSize);
            }
        }

        private void ExpandPanel(PanelDirection direction)
        {
            var parentGrid = Parent as Grid;
            if (parentGrid == null) return;

            var currentColumn = Grid.GetColumn(this);
            var currentRow = Grid.GetRow(this);

            if (Orientation == Orientation.Vertical)
            {
                // 垂直分割器：展开列
                var targetColumnIndex = direction == PanelDirection.Previous ? currentColumn - 1 : currentColumn + 1;
                if (targetColumnIndex < 0 || targetColumnIndex >= parentGrid.ColumnDefinitions.Count) return;

                var columnDef = parentGrid.ColumnDefinitions[targetColumnIndex];
                var targetPixelSize = direction == PanelDirection.Previous ? _previousActualLength : _nextActualLength;
                var originalSize = direction == PanelDirection.Previous ? _previousSize : _nextSize;

                if (direction == PanelDirection.Previous)
                    IsPreviousCollapsed = false;
                else
                    IsNextCollapsed = false;

                // 恢复 AutoFillNeighbor
                if (AutoFillNeighbor && _hasSavedNeighborSize)
                {
                    var neighborIndex = direction == PanelDirection.Previous ? currentColumn + 1 : currentColumn - 1;
                    if (neighborIndex >= 0 && neighborIndex < parentGrid.ColumnDefinitions.Count)
                    {
                        parentGrid.ColumnDefinitions[neighborIndex].Width = _savedNeighborSize;
                        _hasSavedNeighborSize = false;
                    }
                }

                // 执行动画，完成后恢复 MinWidth 和 GridUnitType
                AnimateColumnWidth(columnDef, MinSize, targetPixelSize, () =>
                {
                    columnDef.Width = originalSize; // 恢复原始 GridLength（包括 Star 类型）
                    if (direction == PanelDirection.Previous)
                        columnDef.MinWidth = _savedPreviousMinSize;
                    else
                        columnDef.MinWidth = _savedNextMinSize;

                    // 强制更新按钮可见性，确保状态正确
                    UpdateButtonVisibility();
                });
            }
            else
            {
                // 水平分割器：展开行
                var targetRowIndex = direction == PanelDirection.Previous ? currentRow - 1 : currentRow + 1;
                if (targetRowIndex < 0 || targetRowIndex >= parentGrid.RowDefinitions.Count) return;

                var rowDef = parentGrid.RowDefinitions[targetRowIndex];
                var targetPixelSize = direction == PanelDirection.Previous ? _previousActualLength : _nextActualLength;
                var originalSize = direction == PanelDirection.Previous ? _previousSize : _nextSize;

                if (direction == PanelDirection.Previous)
                    IsPreviousCollapsed = false;
                else
                    IsNextCollapsed = false;

                // 恢复 AutoFillNeighbor
                if (AutoFillNeighbor && _hasSavedNeighborSize)
                {
                    var neighborIndex = direction == PanelDirection.Previous ? currentRow + 1 : currentRow - 1;
                    if (neighborIndex >= 0 && neighborIndex < parentGrid.RowDefinitions.Count)
                    {
                        parentGrid.RowDefinitions[neighborIndex].Height = _savedNeighborSize;
                        _hasSavedNeighborSize = false;
                    }
                }

                // 执行动画，完成后恢复 MinHeight 和 GridUnitType
                AnimateRowHeight(rowDef, MinSize, targetPixelSize, () =>
                {
                    rowDef.Height = originalSize; // 恢复原始 GridLength（包括 Star 类型）
                    if (direction == PanelDirection.Previous)
                        rowDef.MinHeight = _savedPreviousMinSize;
                    else
                        rowDef.MinHeight = _savedNextMinSize;

                    // 强制更新按钮可见性，确保状态正确
                    UpdateButtonVisibility();
                });
            }
        }

        #endregion

        #region Animation Methods

        private void AnimateColumnWidth(ColumnDefinition column, double from, double to, Action onCompleted = null)
        {
            var animation = new DoubleAnimation
            {
                From = from,
                To = to,
                Duration = TimeSpan.FromMilliseconds(AnimationDuration),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut }
            };

            Timeline.SetDesiredFrameRate(animation, 60);

            animation.Completed += (s, e) =>
            {
                column.Width = new GridLength(to, GridUnitType.Pixel);
                onCompleted?.Invoke();
            };

            column.BeginAnimation(ColumnDefinitionWidthAnimation.WidthProperty, animation);
        }

        private void AnimateRowHeight(RowDefinition row, double from, double to, Action onCompleted = null)
        {
            var animation = new DoubleAnimation
            {
                From = from,
                To = to,
                Duration = TimeSpan.FromMilliseconds(AnimationDuration),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut }
            };

            Timeline.SetDesiredFrameRate(animation, 60);

            animation.Completed += (s, e) =>
            {
                row.Height = new GridLength(to, GridUnitType.Pixel);
                onCompleted?.Invoke();
            };

            row.BeginAnimation(RowDefinitionHeightAnimation.HeightProperty, animation);
        }

        #endregion

        #region Helper Methods

        private void UpdateButtonVisibility()
        {
            var showPrev = (CollapseMode == CollapseMode.Previous || CollapseMode == CollapseMode.Both);
            var showNext = (CollapseMode == CollapseMode.Next || CollapseMode == CollapseMode.Both);

            // 互斥逻辑：如果一侧已折叠，则隐藏另一侧按钮
            if (IsPreviousCollapsed) showNext = false;
            if (IsNextCollapsed) showPrev = false;

            if (_collapsePreviousButton != null)
                _collapsePreviousButton.Visibility = showPrev ? Visibility.Visible : Visibility.Collapsed;

            if (_collapseNextButton != null)
                _collapseNextButton.Visibility = showNext ? Visibility.Visible : Visibility.Collapsed;
        }

        #endregion
    }

    #region Animation Helpers

    /// <summary>
    /// 用于列宽动画的附加属性
    /// </summary>
    public static class ColumnDefinitionWidthAnimation
    {
        public static readonly DependencyProperty WidthProperty =
            DependencyProperty.RegisterAttached("Width", typeof(double), typeof(ColumnDefinitionWidthAnimation),
                new PropertyMetadata(0.0, OnWidthChanged));

        public static double GetWidth(DependencyObject obj) => (double)obj.GetValue(WidthProperty);
        public static void SetWidth(DependencyObject obj, double value) => obj.SetValue(WidthProperty, value);

        private static void OnWidthChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ColumnDefinition column)
                column.Width = new GridLength((double)e.NewValue, GridUnitType.Pixel);
        }
    }

    /// <summary>
    /// 用于行高动画的附加属性
    /// </summary>
    public static class RowDefinitionHeightAnimation
    {
        public static readonly DependencyProperty HeightProperty =
            DependencyProperty.RegisterAttached("Height", typeof(double), typeof(RowDefinitionHeightAnimation),
                new PropertyMetadata(0.0, OnHeightChanged));

        public static double GetHeight(DependencyObject obj) => (double)obj.GetValue(HeightProperty);
        public static void SetHeight(DependencyObject obj, double value) => obj.SetValue(HeightProperty, value);

        private static void OnHeightChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is RowDefinition row)
                row.Height = new GridLength((double)e.NewValue, GridUnitType.Pixel);
        }
    }

    #endregion

    #region Enums

    /// <summary>
    /// 折叠模式
    /// </summary>
    public enum CollapseMode
    {
        /// <summary>只能折叠前一个面板</summary>
        Previous,
        /// <summary>只能折叠后一个面板</summary>
        Next,
        /// <summary>可以折叠前后两个面板</summary>
        Both
    }

    /// <summary>
    /// 面板方向
    /// </summary>
    internal enum PanelDirection
    {
        Previous,
        Next
    }

    #endregion
}

