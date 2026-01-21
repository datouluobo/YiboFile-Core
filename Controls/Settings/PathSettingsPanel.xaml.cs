using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Shapes;
using YiboFile.ViewModels;

namespace YiboFile.Controls.Settings
{
    public partial class PathSettingsPanel : UserControl, ISettingsPanel
    {
        // Event reserved for future use
#pragma warning disable CS0067
        public event EventHandler SettingsChanged;
#pragma warning restore CS0067

        private SettingsViewModel _viewModel;

        public PathSettingsPanel()
        {
            InitializeComponent();
            _viewModel = new SettingsViewModel();
            this.DataContext = _viewModel;
        }

        private void InitializeComponent()
        {
            this.SetResourceReference(Panel.BackgroundProperty, "PanelBackgroundBrush");

            var mainGrid = new Grid();
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Title
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Description
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // List

            // Title
            var titleText = new TextBlock
            {
                Text = "导航栏显示顺序",
                FontSize = 18,
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(0, 0, 0, 8),
            };
            titleText.SetResourceReference(TextBlock.ForegroundProperty, "TextPrimaryBrush");
            mainGrid.Children.Add(titleText);

            // Description
            var descText = new TextBlock
            {
                Text = "您可以调整侧边栏导航项的显示顺序，将常用的项排在前面。",
                FontSize = 13,
                TextWrapping = TextWrapping.Wrap,
            };
            descText.SetResourceReference(TextBlock.ForegroundProperty, "TextSecondaryBrush");
            Grid.SetRow(descText, 1);
            mainGrid.Children.Add(descText);

            // Items Container (ScrollViewer)
            var scrollViewer = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                Padding = new Thickness(0, 0, 20, 0)
            };
            Grid.SetRow(scrollViewer, 2);

            // ItemsControl
            var itemsControl = new ItemsControl();
            itemsControl.SetBinding(ItemsControl.ItemsSourceProperty, new Binding(nameof(SettingsViewModel.NavigationSections)));

            // Item Template
            string itemTemplateXaml = @"
<DataTemplate xmlns=""http://schemas.microsoft.com/winfx/2006/xaml/presentation"">
    <Border Margin=""0,0,0,10"" Background=""{DynamicResource CardBackgroundBrush}"" BorderBrush=""{DynamicResource BorderBrush}"" BorderThickness=""1"" CornerRadius=""6"">
        <Grid Margin=""12,8"">
            <Grid.ColumnDefinitions>
                <ColumnDefinition Width=""Auto""/> <!-- Icon placeholder -->
                <ColumnDefinition Width=""*""/>    <!-- Name -->
                <ColumnDefinition Width=""Auto""/> <!-- Up -->
                <ColumnDefinition Width=""Auto""/> <!-- Down -->
            </Grid.ColumnDefinitions>

            <!-- Icon Placeholder (Optional) -->
            <Ellipse Grid.Column=""0"" Width=""6"" Height=""6"" Fill=""#CCCCCC"" VerticalAlignment=""Center"" Margin=""0,0,12,0""/>

            <!-- Name -->
            <TextBlock Grid.Column=""1"" Text=""{Binding DisplayName}"" VerticalAlignment=""Center"" FontSize=""14"" FontWeight=""Medium"" Foreground=""{DynamicResource TextPrimaryBrush}""/>

            <!-- Buttons -->
            <Button Grid.Column=""2"" Content=""▲"" 
                    Command=""{Binding DataContext.MoveSectionUpCommand, RelativeSource={RelativeSource AncestorType=UserControl}}"" 
                    CommandParameter=""{Binding}""
                    Width=""28"" Height=""28"" Margin=""0,0,4,0""
                    Background=""Transparent"" BorderThickness=""0"" Foreground=""{DynamicResource TextSecondaryBrush}"" Cursor=""Hand"" ToolTip=""上移"">
                <Button.Template>
                    <ControlTemplate TargetType=""Button"">
                         <Border Background=""{TemplateBinding Background}"" CornerRadius=""4"">
                             <ContentPresenter HorizontalAlignment=""Center"" VerticalAlignment=""Center""/>
                         </Border>
                    </ControlTemplate>
                </Button.Template>
                <Button.Style>
                    <Style TargetType=""Button"">
                        <Style.Triggers>
                            <Trigger Property=""IsMouseOver"" Value=""True"">
                                <Setter Property=""Background"" Value=""#E0E0E0""/>
                            </Trigger>
                        </Style.Triggers>
                    </Style>
                </Button.Style>
            </Button>

            <Button Grid.Column=""3"" Content=""▼"" 
                    Command=""{Binding DataContext.MoveSectionDownCommand, RelativeSource={RelativeSource AncestorType=UserControl}}"" 
                    CommandParameter=""{Binding}""
                    Width=""28"" Height=""28""
                    Background=""Transparent"" BorderThickness=""0"" Foreground=""{DynamicResource TextSecondaryBrush}"" Cursor=""Hand"" ToolTip=""下移"">
                <Button.Template>
                    <ControlTemplate TargetType=""Button"">
                         <Border Background=""{TemplateBinding Background}"" CornerRadius=""4"">
                             <ContentPresenter HorizontalAlignment=""Center"" VerticalAlignment=""Center""/>
                         </Border>
                    </ControlTemplate>
                </Button.Template>
                <Button.Style>
                    <Style TargetType=""Button"">
                        <Style.Triggers>
                            <Trigger Property=""IsMouseOver"" Value=""True"">
                                <Setter Property=""Background"" Value=""#E0E0E0""/>
                            </Trigger>
                        </Style.Triggers>
                    </Style>
                </Button.Style>
            </Button>
        </Grid>
    </Border>
</DataTemplate>";

            try
            {
                itemsControl.ItemTemplate = (DataTemplate)System.Windows.Markup.XamlReader.Parse(itemTemplateXaml);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading template: {ex.Message}");
            }

            scrollViewer.Content = itemsControl;
            mainGrid.Children.Add(scrollViewer);

            // Container Wrapper with Padding
            var container = new Border
            {
                Padding = new Thickness(20),
                Child = mainGrid
            };

            this.Content = container;
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
