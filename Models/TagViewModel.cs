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

            // 2. No color set - return white for ungrouped tags
            return System.Windows.Media.Brushes.White;
        }

        public event System.ComponentModel.PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(propertyName));
        }
    }
}
