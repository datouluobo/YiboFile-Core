using System;

namespace YiboFile.Models
{
    public class TagViewModel : System.ComponentModel.INotifyPropertyChanged
    {
        public int Id { get; set; }
        public string Name { get; set; }

        private string _color;
        public string Color
        {
            get => _color;
            set
            {
                if (_color != value)
                {
                    _color = value;
                    OnPropertyChanged(nameof(Color));
                    OnPropertyChanged(nameof(ColorBrush));
                }
            }
        }


        public System.Windows.Media.Brush ColorBrush
        {
            get
            {
                return GetColorBrush(Name, Color);
            }
        }

        public static System.Windows.Media.Brush GetColorBrush(string name, string colorCode)
        {
            // 1. Try Explicit Color via Converter (User provided)
            if (!string.IsNullOrEmpty(colorCode))
            {
                try
                {
                    var color = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(colorCode);
                    var brush = new System.Windows.Media.SolidColorBrush(color);
                    if (brush.CanFreeze) brush.Freeze();
                    return brush;
                }
                catch { }
            }

            // 2. Fallback to consistent pastel color based on name (Manual Parse)
            if (!string.IsNullOrEmpty(name))
            {
                var colors = new[]
                {
                     "#FFCDD2", "#F8BBD0", "#E1BEE7", "#D1C4E9", "#C5CAE9",
                     "#BBDEFB", "#B3E5FC", "#B2EBF2", "#B2DFDB", "#C8E6C9",
                     "#DCEDC8", "#F0F4C3", "#FFF9C4", "#FFECB3", "#FFE0B2",
                     "#FFCCBC", "#D7CCC8", "#F5F5F5", "#CFD8DC"
                 };

                try
                {
                    // Use bitmask for safe positive integer, avoiding Math.Abs(int.MinValue) overflow
                    int hash = name.GetHashCode() & 0x7FFFFFFF;
                    int index = hash % colors.Length;
                    string hex = colors[index];

                    // Manual parsing to avoid TypeConverter dependencies/issues
                    // Format is always #RRGGBB (7 chars)
                    byte r = Convert.ToByte(hex.Substring(1, 2), 16);
                    byte g = Convert.ToByte(hex.Substring(3, 2), 16);
                    byte b = Convert.ToByte(hex.Substring(5, 2), 16);

                    var color = System.Windows.Media.Color.FromRgb(r, g, b);
                    var brush = new System.Windows.Media.SolidColorBrush(color);
                    if (brush.CanFreeze) brush.Freeze();
                    return brush;
                }
                catch { }
            }

            return System.Windows.Media.Brushes.LightGray;
        }

        public event System.ComponentModel.PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(propertyName));
        }
    }
}
