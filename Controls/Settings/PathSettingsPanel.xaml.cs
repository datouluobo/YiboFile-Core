using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using YiboFile.ViewModels;

namespace YiboFile.Controls.Settings
{
    public partial class PathSettingsPanel : UserControl, ISettingsPanel
    {
        public event EventHandler SettingsChanged;

        private SettingsViewModel _viewModel;

        public PathSettingsPanel()
        {
            InitializeComponent();
            _viewModel = new SettingsViewModel();
            this.DataContext = _viewModel;

            InitializeBindings();
        }

        private void InitializeComponent()
        {
            var stackPanel = new StackPanel { Margin = new Thickness(0) };

            var title = new TextBlock
            {
                Text = "导航栏显示顺序",
                FontSize = 18,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 0, 0, 16)
            };
            stackPanel.Children.Add(title);

            var hint = new TextBlock
            {
                Text = "拖拽或使用按钮调整侧边栏导航项的显示顺序。",
                FontSize = 12,
                Margin = new Thickness(0, 0, 0, 12)
            };
            hint.SetResourceReference(TextBlock.ForegroundProperty, "TextSecondaryBrush");
            stackPanel.Children.Add(hint);

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            // ListBox for sections
            var listBox = new ListBox
            {
                Name = "SectionsListBox",
                Margin = new Thickness(0, 0, 10, 0),
                MinHeight = 200,
                BorderThickness = new Thickness(1),
            };
            listBox.SelectionMode = SelectionMode.Single;

            // Create item template
            var itemTemplate = new DataTemplate();
            var factory = new FrameworkElementFactory(typeof(TextBlock));
            factory.SetBinding(TextBlock.TextProperty, new Binding("DisplayName"));
            factory.SetValue(TextBlock.PaddingProperty, new Thickness(5));
            factory.SetValue(TextBlock.FontSizeProperty, 14.0);
            itemTemplate.VisualTree = factory;
            listBox.ItemTemplate = itemTemplate;

            Grid.SetColumn(listBox, 0);
            grid.Children.Add(listBox);

            // Buttons
            var buttonPanel = new StackPanel
            {
                Orientation = Orientation.Vertical,
                VerticalAlignment = VerticalAlignment.Center
            };

            var moveUpButton = new Button
            {
                Content = "▲ 上移",
                Margin = new Thickness(0, 0, 0, 10),
                Padding = new Thickness(15, 8, 15, 8),
                Style = (Style)Application.Current.Resources["ModernButtonStyle"]
            };
            // Command binding handles click
            buttonPanel.Children.Add(moveUpButton);

            var moveDownButton = new Button
            {
                Content = "▼ 下移",
                Padding = new Thickness(15, 8, 15, 8),
                Style = (Style)Application.Current.Resources["ModernButtonStyle"]
            };
            // Command binding handles click
            buttonPanel.Children.Add(moveDownButton);

            Grid.SetColumn(buttonPanel, 1);
            grid.Children.Add(buttonPanel);

            stackPanel.Children.Add(grid);

            var scrollViewer = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Content = stackPanel
            };

            Content = scrollViewer;

            // Save reference for binding in InitializeBindings used by non-generated members
            // But since we are creating purely in code, we can bind directly here

            // Bind ListBox ItemsSource
            listBox.SetBinding(ListBox.ItemsSourceProperty, new Binding(nameof(SettingsViewModel.NavigationSections)));

            // Bind Buttons Command
            moveUpButton.SetBinding(Button.CommandProperty, new Binding(nameof(SettingsViewModel.MoveSectionUpCommand)));
            moveUpButton.SetBinding(Button.CommandParameterProperty, new Binding("SelectedItem") { Source = listBox });

            moveDownButton.SetBinding(Button.CommandProperty, new Binding(nameof(SettingsViewModel.MoveSectionDownCommand)));
            moveDownButton.SetBinding(Button.CommandParameterProperty, new Binding("SelectedItem") { Source = listBox });
        }

        private void InitializeBindings()
        {
            // Bindings handled in InitializeComponent for this simple panel
        }

        public void LoadSettings()
        {
            _viewModel?.LoadFromConfig();
        }

        public void SaveSettings()
        {
            // Auto-saved by ViewModel
        }
    }
}
