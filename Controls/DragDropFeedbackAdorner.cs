using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;

namespace YiboFile.Controls
{
    /// <summary>
    /// 拖放反馈装饰器 - 在鼠标旁边显示操作类型提示
    /// </summary>
    public class DragDropFeedbackAdorner : Adorner
    {
        private string _text = "";
        public string Text => _text;
        private Point _position;
        private readonly Typeface _typeface = new Typeface("Segoe UI");
        private readonly Brush _background;
        private readonly Brush _foreground;
        private readonly Brush _border;

        public DragDropFeedbackAdorner(UIElement adornedElement) : base(adornedElement)
        {
            IsHitTestVisible = false;
            _background = new SolidColorBrush(Color.FromArgb(230, 40, 40, 40));
            _foreground = Brushes.White;
            _border = new SolidColorBrush(Color.FromArgb(255, 100, 100, 100));
        }

        private FormattedText _formattedText;

        public void UpdateFeedback(string text, Point position)
        {
            if (_text != text)
            {
                _text = text;
                _formattedText = null; // Invalidate cache
            }
            _position = position;
            InvalidateVisual();
        }

        protected override void OnRender(DrawingContext drawingContext)
        {
            if (string.IsNullOrEmpty(_text)) return;

            if (_formattedText == null)
            {
                _formattedText = new FormattedText(
                    _text,
                    System.Globalization.CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight,
                    _typeface,
                    12,
                    _foreground,
                    VisualTreeHelper.GetDpi(this).PixelsPerDip);
            }

            double padding = 6;
            double cornerRadius = 4;
            double offsetX = 16; // 在鼠标右下方
            double offsetY = 18;

            var rect = new Rect(
                _position.X + offsetX,
                _position.Y + offsetY,
                _formattedText.Width + padding * 2,
                _formattedText.Height + padding * 2);

            // 边框和背景
            drawingContext.DrawRoundedRectangle(_background, new Pen(_border, 1), rect, cornerRadius, cornerRadius);

            // 文本
            drawingContext.DrawText(_formattedText, new Point(rect.X + padding, rect.Y + padding));
        }
    }
}

