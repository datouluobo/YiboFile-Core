using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace YiboFile.Controls
{
    /// <summary>
    /// 颜色选择器控件
    /// 支持RGB、HSV和十六进制颜色输入
    /// </summary>
    public partial class ColorPickerControl : UserControl
    {
        private bool _isUpdating;

        public static readonly DependencyProperty SelectedColorProperty =
            DependencyProperty.Register(
                nameof(SelectedColor),
                typeof(Color),
                typeof(ColorPickerControl),
                new PropertyMetadata(Colors.Blue, OnSelectedColorChanged));

        public Color SelectedColor
        {
            get => (Color)GetValue(SelectedColorProperty);
            set => SetValue(SelectedColorProperty, value);
        }

        public event EventHandler<Color> ColorChanged;

        public ColorPickerControl()
        {
            InitializeComponent();
            InitializeColor(Colors.Blue);
        }

        private static void OnSelectedColorChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ColorPickerControl picker && e.NewValue is Color color)
            {
                picker.UpdateFromColor(color);
            }
        }

        private void InitializeColor(Color color)
        {
            _isUpdating = true;
            UpdateFromColor(color);
            _isUpdating = false;
        }

        private void UpdateFromColor(Color color)
        {
            _isUpdating = true;

            // 更新预览
            PreviewBrush.Color = color;

            // 更新RGB滑块
            RedSlider.Value = color.R;
            GreenSlider.Value = color.G;
            BlueSlider.Value = color.B;

            // 转换到HSV并更新HSV滑块
            ColorToHsv(color, out double h, out double s, out double v);
            HueSlider.Value = h;
            SaturationSlider.Value = s * 100;
            ValueSlider.Value = v * 100;

            // 更新十六进制输入
            HexTextBox.Text = $"#{color.R:X2}{color.G:X2}{color.B:X2}";

            _isUpdating = false;
        }

        private void OnRgbSliderChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_isUpdating) return;

            var color = Color.FromRgb(
                (byte)RedSlider.Value,
                (byte)GreenSlider.Value,
                (byte)BlueSlider.Value
            );

            UpdateFromRgb(color);
        }

        private void OnHsvSliderChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_isUpdating) return;

            var color = HsvToColor(
                HueSlider.Value,
                SaturationSlider.Value / 100.0,
                ValueSlider.Value / 100.0
            );

            UpdateFromHsv(color);
        }

        private void OnHexTextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isUpdating) return;

            try
            {
                var hex = HexTextBox.Text.Trim();
                if (!hex.StartsWith("#"))
                {
                    hex = "#" + hex;
                }

                if (hex.Length == 7) // #RRGGBB
                {
                    var color = (Color)ColorConverter.ConvertFromString(hex);
                    UpdateFromHex(color);
                }
            }
            catch
            {
                // 忽略无效输入
            }
        }

        private void UpdateFromRgb(Color color)
        {
            _isUpdating = true;

            SelectedColor = color;
            PreviewBrush.Color = color;

            // 转换到HSV
            ColorToHsv(color, out double h, out double s, out double v);
            HueSlider.Value = h;
            SaturationSlider.Value = s * 100;
            ValueSlider.Value = v * 100;

            // 更新十六进制
            HexTextBox.Text = $"#{color.R:X2}{color.G:X2}{color.B:X2}";

            _isUpdating = false;

            ColorChanged?.Invoke(this, color);
        }

        private void UpdateFromHsv(Color color)
        {
            _isUpdating = true;

            SelectedColor = color;
            PreviewBrush.Color = color;

            // 更新RGB
            RedSlider.Value = color.R;
            GreenSlider.Value = color.G;
            BlueSlider.Value = color.B;

            // 更新十六进制
            HexTextBox.Text = $"#{color.R:X2}{color.G:X2}{color.B:X2}";

            _isUpdating = false;

            ColorChanged?.Invoke(this, color);
        }

        private void UpdateFromHex(Color color)
        {
            _isUpdating = true;

            SelectedColor = color;
            PreviewBrush.Color = color;

            // 更新RGB
            RedSlider.Value = color.R;
            GreenSlider.Value = color.G;
            BlueSlider.Value = color.B;

            // 更新HSV
            ColorToHsv(color, out double h, out double s, out double v);
            HueSlider.Value = h;
            SaturationSlider.Value = s * 100;
            ValueSlider.Value = v * 100;

            _isUpdating = false;

            ColorChanged?.Invoke(this, color);
        }

        #region HSV转换算法

        /// <summary>
        /// RGB颜色转HSV
        /// </summary>
        private static void ColorToHsv(Color color, out double hue, out double saturation, out double value)
        {
            double r = color.R / 255.0;
            double g = color.G / 255.0;
            double b = color.B / 255.0;

            double max = Math.Max(r, Math.Max(g, b));
            double min = Math.Min(r, Math.Min(g, b));
            double delta = max - min;

            // Hue
            if (delta == 0)
            {
                hue = 0;
            }
            else if (max == r)
            {
                hue = 60 * (((g - b) / delta) % 6);
            }
            else if (max == g)
            {
                hue = 60 * (((b - r) / delta) + 2);
            }
            else
            {
                hue = 60 * (((r - g) / delta) + 4);
            }

            if (hue < 0)
                hue += 360;

            // Saturation
            saturation = max == 0 ? 0 : delta / max;

            // Value
            value = max;
        }

        /// <summary>
        /// HSV转RGB颜色
        /// </summary>
        private static Color HsvToColor(double hue, double saturation, double value)
        {
            double c = value * saturation;
            double x = c * (1 - Math.Abs((hue / 60) % 2 - 1));
            double m = value - c;

            double r, g, b;

            if (hue < 60)
            {
                r = c; g = x; b = 0;
            }
            else if (hue < 120)
            {
                r = x; g = c; b = 0;
            }
            else if (hue < 180)
            {
                r = 0; g = c; b = x;
            }
            else if (hue < 240)
            {
                r = 0; g = x; b = c;
            }
            else if (hue < 300)
            {
                r = x; g = 0; b = c;
            }
            else
            {
                r = c; g = 0; b = x;
            }

            return Color.FromRgb(
                (byte)((r + m) * 255),
                (byte)((g + m) * 255),
                (byte)((b + m) * 255)
            );
        }

        #endregion
    }
}

