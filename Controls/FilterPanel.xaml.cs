using System;
using System.Windows;
using System.Windows.Controls;
using YiboFile.Services.Search;

namespace YiboFile.Controls
{
    public partial class FilterPanel : UserControl
    {
        public FilterPanel()
        {
            InitializeComponent();
        }

        // Keep the Options property for compatibility if needed, but it's not strictly necessary for internal bindings anymore if DataContext is set correctly.
        public static readonly DependencyProperty OptionsProperty =
            DependencyProperty.Register(nameof(Options), typeof(SearchOptions), typeof(FilterPanel),
                new PropertyMetadata(null));

        public SearchOptions Options
        {
            get => (SearchOptions)GetValue(OptionsProperty);
            set => SetValue(OptionsProperty, value);
        }
    }
}
