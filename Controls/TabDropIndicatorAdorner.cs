using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;

namespace YiboFile.Controls
{
    /// <summary>
    /// 标签页拖拽位置指示器
    /// 用于在标签页之间显示一条竖线，提示放置位置
    /// </summary>
    public class TabDropIndicatorAdorner : Adorner
    {
        private readonly Pen _renderPen;
        private double _posX;
        private double _height;
        
        public TabDropIndicatorAdorner(UIElement adornedElement) : base(adornedElement)
        {
            IsHitTestVisible = false;
            
            // 使用应用的高亮颜色，如果找不到则回退到蓝色
            var highlightBrush = Application.Current.TryFindResource("PrimaryThemeBrush") as Brush 
                ?? new SolidColorBrush(Color.FromRgb(0, 120, 215));
            
            _renderPen = new Pen(highlightBrush, 2.0);
            _renderPen.Freeze();
        }

        public void UpdatePosition(double x, double height)
        {
            _posX = x;
            _height = height;
            InvalidateVisual();
        }

        protected override void OnRender(DrawingContext drawingContext)
        {
            if (_height > 0)
            {
                drawingContext.DrawLine(_renderPen, new Point(_posX, 0), new Point(_posX, _height));
            }
        }
    }
}
