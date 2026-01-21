using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Input;
using YiboFile.ViewModels;

namespace YiboFile.Controls.Settings
{
    public partial class LibrarySettingsPanel : UserControl, ISettingsPanel
    {
        public event EventHandler SettingsChanged;

        private SettingsViewModel _viewModel;

        public LibrarySettingsPanel()
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
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Toolbar
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // List

            // Title
            var titleText = new TextBlock
            {
                Text = "库管理",
                FontSize = 18,
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(0, 0, 0, 8)
            };
            titleText.SetResourceReference(TextBlock.ForegroundProperty, "TextPrimaryBrush");
            mainGrid.Children.Add(titleText);

            // Description
            var descText = new TextBlock
            {
                Text = "管理您的文件库。您可以添加文件夹作为库，以便快速访问和索引。",
                FontSize = 13,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 15)
            };
            descText.SetResourceReference(TextBlock.ForegroundProperty, "TextSecondaryBrush");
            Grid.SetRow(descText, 1);
            mainGrid.Children.Add(descText);

            // Toolbar
            // var toolbarColors = new SolidColorBrush(Color.FromRgb(0x21, 0x96, 0xF3)); // Blue
            var toolbarPanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 15) };

            // Add Library Button
            var addBtn = CreateButton("➕ 添加库", "AddLibraryCommand", null, null);
            addBtn.SetResourceReference(Control.BackgroundProperty, "AccentDefaultBrush");
            addBtn.SetResourceReference(Control.ForegroundProperty, "ForegroundOnAccentBrush");
            toolbarPanel.Children.Add(addBtn);

            // Import Button
            var importBtn = CreateButton("📂 导入配置", null, Brushes.Transparent, null);
            importBtn.SetResourceReference(Control.ForegroundProperty, "AccentDefaultBrush");
            importBtn.Click += ImportBtn_Click;
            importBtn.BorderThickness = new Thickness(1);
            importBtn.SetResourceReference(Control.BorderBrushProperty, "AccentDefaultBrush");
            importBtn.Margin = new Thickness(10, 0, 0, 0);
            toolbarPanel.Children.Add(importBtn);

            // Export Button
            var exportBtn = CreateButton("📤 导出配置", null, Brushes.Transparent, null);
            exportBtn.SetResourceReference(Control.ForegroundProperty, "AccentDefaultBrush");
            exportBtn.Click += ExportBtn_Click;
            exportBtn.BorderThickness = new Thickness(1);
            exportBtn.SetResourceReference(Control.BorderBrushProperty, "AccentDefaultBrush");
            exportBtn.Margin = new Thickness(10, 0, 0, 0);
            toolbarPanel.Children.Add(exportBtn);

            Grid.SetRow(toolbarPanel, 2);
            mainGrid.Children.Add(toolbarPanel);


            // Items Container (ScrollViewer)
            var scrollViewer = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                Padding = new Thickness(0, 0, 20, 0)
            };
            Grid.SetRow(scrollViewer, 3);

            // ItemsControl
            var itemsControl = new ItemsControl();
            itemsControl.SetBinding(ItemsControl.ItemsSourceProperty, new Binding("Libraries"));

            // Item Template
            string itemTemplateXaml = @"
<DataTemplate xmlns=""http://schemas.microsoft.com/winfx/2006/xaml/presentation"">
    <Border Margin=""0,0,0,10"" Background=""{DynamicResource CardBackgroundBrush}"" BorderBrush=""{DynamicResource BorderBrush}"" BorderThickness=""1"" CornerRadius=""6"" Padding=""12"">
        <Grid>
            <Grid.ColumnDefinitions>
                <ColumnDefinition Width=""Auto""/> <!-- Icon -->
                <ColumnDefinition Width=""*""/>    <!-- Info -->
                <ColumnDefinition Width=""Auto""/> <!-- Actions -->
            </Grid.ColumnDefinitions>

            <!-- Icon -->
            <Border Grid.Column=""0"" Width=""40"" Height=""40"" CornerRadius=""20"" Background=""{DynamicResource ControlDefaultBrush}"" Margin=""0,0,15,0"" VerticalAlignment=""Center"">
                 <TextBlock Text=""📚"" HorizontalAlignment=""Center"" VerticalAlignment=""Center"" FontSize=""18""/>
            </Border>

            <!-- Info -->
            <StackPanel Grid.Column=""1"" VerticalAlignment=""Center"">
                <TextBlock Text=""{Binding Name}"" FontSize=""15"" FontWeight=""SemiBold"" Margin=""0,0,0,4"" Foreground=""{DynamicResource TextPrimaryBrush}""/>
                <TextBlock Text=""{Binding DisplayPath}"" FontSize=""12"" Foreground=""{DynamicResource TextSecondaryBrush}"" TextTrimming=""CharacterEllipsis"" ToolTip=""{Binding ToolTipText}""/>
            </StackPanel>

            <!-- Actions -->
            <Button Grid.Column=""2"" Content=""✕"" 
                    Command=""{Binding DataContext.RemoveLibraryCommand, RelativeSource={RelativeSource AncestorType=UserControl}}"" 
                    CommandParameter=""{Binding}""
                    Width=""32"" Height=""32"" 
                    Background=""Transparent"" BorderThickness=""0"" Foreground=""#EF5350"" Cursor=""Hand"" ToolTip=""移除库"">
                <Button.Template>
                    <ControlTemplate TargetType=""Button"">
                         <Border Background=""{TemplateBinding Background}"" CornerRadius=""16"">
                             <ContentPresenter HorizontalAlignment=""Center"" VerticalAlignment=""Center""/>
                         </Border>
                    </ControlTemplate>
                </Button.Template>
                <Button.Style>
                    <Style TargetType=""Button"">
                        <Style.Triggers>
                            <Trigger Property=""IsMouseOver"" Value=""True"">
                                <Setter Property=""Background"" Value=""{DynamicResource ControlHoverBrush}""/>
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

        private Button CreateButton(string content, string commandName, Brush background, Brush foreground)
        {
            var btn = new Button
            {
                Content = content,
                Padding = new Thickness(15, 8, 15, 8),
                Background = background,
                Foreground = foreground,
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand,
                FontSize = 13
            };

            // Simple style for radius
            var style = new Style(typeof(Button));
            var template = new ControlTemplate(typeof(Button));
            var borderFactory = new FrameworkElementFactory(typeof(Border));
            borderFactory.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(Button.BackgroundProperty));
            borderFactory.SetValue(Border.BorderBrushProperty, new TemplateBindingExtension(Button.BorderBrushProperty));
            borderFactory.SetValue(Border.BorderThicknessProperty, new TemplateBindingExtension(Button.BorderThicknessProperty));
            borderFactory.SetValue(Border.CornerRadiusProperty, new CornerRadius(4));
            var contentPresenter = new FrameworkElementFactory(typeof(ContentPresenter));
            contentPresenter.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            contentPresenter.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
            borderFactory.AppendChild(contentPresenter);
            template.VisualTree = borderFactory;
            style.Setters.Add(new Setter(Button.TemplateProperty, template));
            btn.Style = style;

            if (commandName != null)
            {
                btn.SetBinding(Button.CommandProperty, new Binding(commandName));
            }

            return btn;
        }

        private void ImportBtn_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "JSON 文件 (*.json)|*.json|所有文件 (*.*)|*.*",
                Title = "导入库配置"
            };

            if (dialog.ShowDialog() == true)
            {
                if (_viewModel.ImportLibrariesCommand.CanExecute(dialog.FileName))
                {
                    _viewModel.ImportLibrariesCommand.Execute(dialog.FileName);
                    // Refresh view handled by Command logic ideally, but let's force refresh via Reload if needed 
                    // ViewModel 'ImportLibraries' logic should refresh 'Libraries' collection.
                }
            }
        }

        private void ExportBtn_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "JSON 文件 (*.json)|*.json|所有文件 (*.*)|*.*",
                FileName = $"Libraries_Backup_{DateTime.Now:yyyyMMdd}.json",
                Title = "导出库配置"
            };

            if (dialog.ShowDialog() == true)
            {
                if (_viewModel.ExportLibrariesCommand.CanExecute(dialog.FileName))
                {
                    _viewModel.ExportLibrariesCommand.Execute(dialog.FileName);
                    MessageBox.Show("库配置已导出", "成功", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
        }

        public void LoadSettings()
        {
            _viewModel?.LoadFromConfig();
            _viewModel?.RefreshLibraries();
        }

        public void SaveSettings()
        {
            // Auto-saved
        }
    }
}
