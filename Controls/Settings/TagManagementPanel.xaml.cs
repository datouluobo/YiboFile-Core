using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Input;
using YiboFile.ViewModels;
using YiboFile.ViewModels.Settings;

namespace YiboFile.Controls.Settings
{
    public partial class TagManagementPanel : UserControl, ISettingsPanel
    {
        // Event reserved for future use
#pragma warning disable CS0067
        public event EventHandler SettingsChanged;
#pragma warning restore CS0067
        private TagSettingsViewModel _viewModel;

        // UI 元素字段 - 用于水印状态同步
        private TextBox _newGroupTextBox;
        private TextBlock _newGroupWatermark;
        private TextBox _newTagTextBox;
        private TextBlock _newTagWatermark;
        private ListBox _groupList;

        public TagManagementPanel()
        {
            InitializeUI();
            this.DataContextChanged += OnDataContextChanged;

            // Auto-initialize if DataContext is not set externally (Common for independent panels)
            if (this.DataContext == null)
            {
                // Create new view model
                var vm = new TagSettingsViewModel();
                this.DataContext = vm;
                _viewModel = vm;
                SubscribeEvents();
                // Refresh handled by constructor
            }
        }

        private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (e.NewValue is TagSettingsViewModel vm)
            {
                _viewModel = vm;
                SubscribeEvents();
                // Ensure data is refreshed
                _viewModel.RefreshTagGroups();
            }
        }

        private void InitializeUI()
        {
            this.SetResourceReference(Panel.BackgroundProperty, "PanelBackgroundBrush");

            // Main Layout
            var mainGrid = new Grid();
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Header
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // Content

            // Header
            var headerBlock = new TextBlock
            {
                Text = "标签管理",
                FontSize = 18,
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(0, 0, 0, 10)
            };
            headerBlock.SetResourceReference(TextBlock.ForegroundProperty, "TextPrimaryBrush");
            mainGrid.Children.Add(headerBlock);

            // Master-Detail Grid
            var contentGrid = new Grid();
            contentGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(220) }); // Groups (Master)
            contentGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); // Tags (Detail)
            contentGrid.Margin = new Thickness(0, 10, 0, 0);

            Grid.SetRow(contentGrid, 1);
            mainGrid.Children.Add(contentGrid);

            // ==================== LEFT COLUMN: GROUPS ====================
            var leftPanel = new Grid();
            leftPanel.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // List
            leftPanel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Add Group Area
            leftPanel.Margin = new Thickness(0, 0, 20, 0);

            // Group List
            _groupList = new ListBox
            {
                Name = "GroupList",
                BorderThickness = new Thickness(1),
                ItemContainerStyle = CreateGroupItemStyle()
            };
            var groupList = _groupList;
            groupList.SetResourceReference(Control.BorderBrushProperty, "BorderBrush");
            groupList.SetResourceReference(Panel.BackgroundProperty, "InputBackgroundBrush");
            ScrollViewer.SetHorizontalScrollBarVisibility(groupList, ScrollBarVisibility.Disabled);
            groupList.SetBinding(ListBox.ItemsSourceProperty, new Binding("TagGroups"));
            groupList.ItemTemplate = CreateGroupTemplate();

            leftPanel.Children.Add(groupList);

            // Add Group Area
            var addGroupPanel = new Border
            {
                Padding = new Thickness(10),
                Margin = new Thickness(0, 10, 0, 0),
                CornerRadius = new CornerRadius(4)
            };

            var addGroupStack = new StackPanel();

            // New Group TextBox with Watermark
            _newGroupTextBox = new TextBox
            {
                Margin = new Thickness(0, 0, 0, 5),
                Padding = new Thickness(5),
                BorderThickness = new Thickness(1)
            };
            var newGroupTb = _newGroupTextBox;
            addGroupPanel.SetResourceReference(Border.BackgroundProperty, "BackgroundSecondaryBrush");
            newGroupTb.SetResourceReference(Control.BorderBrushProperty, "InputBorderBrush");
            newGroupTb.SetResourceReference(Control.BackgroundProperty, "InputBackgroundBrush");
            newGroupTb.SetResourceReference(Control.ForegroundProperty, "TextPrimaryBrush");
            newGroupTb.SetBinding(TextBox.TextProperty, new Binding("NewGroupName") { UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged });

            _newGroupWatermark = new TextBlock
            {
                Text = "新分组名称...",
                Margin = new Thickness(8, 0, 0, 5), // Adjust margin to match TextBox
                VerticalAlignment = VerticalAlignment.Center,
                IsHitTestVisible = false,
                Visibility = Visibility.Visible
            };
            var newGroupWatermark = _newGroupWatermark;
            newGroupWatermark.SetResourceReference(TextBlock.ForegroundProperty, "TextSecondaryBrush");
            newGroupTb.TextChanged += (s, e) => UpdateGroupWatermarkVisibility();
            newGroupTb.GotFocus += (s, e) => UpdateGroupWatermarkVisibility();
            newGroupTb.LostFocus += (s, e) => UpdateGroupWatermarkVisibility();

            var newGroupGrid = new Grid { Margin = new Thickness(0, 0, 0, 5) };
            newGroupGrid.Children.Add(newGroupTb);
            newGroupGrid.Children.Add(newGroupWatermark);

            var addGroupBtn = new Button
            {
                Content = "新建分组",
                Padding = new Thickness(0, 6, 0, 6),
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand
            };
            addGroupBtn.SetResourceReference(Control.BackgroundProperty, "ButtonBackgroundBrush");
            addGroupBtn.SetResourceReference(Control.ForegroundProperty, "TextPrimaryBrush");
            addGroupBtn.SetBinding(Button.CommandProperty, new Binding("AddTagGroupCommand"));

            addGroupStack.Children.Add(newGroupGrid);
            addGroupStack.Children.Add(addGroupBtn);
            addGroupPanel.Child = addGroupStack;

            Grid.SetRow(addGroupPanel, 1);
            leftPanel.Children.Add(addGroupPanel);

            contentGrid.Children.Add(leftPanel);

            // ==================== RIGHT COLUMN: TAGS ====================
            var rightPanel = new Grid();
            rightPanel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Header
            rightPanel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Add Tag Area
            rightPanel.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // Tag Cloud

            // Bind Right Panel Visibility
            var visibilityBinding = new Binding("SelectedItem") { Source = groupList, Converter = new NullToVisibilityConverter() };
            rightPanel.SetBinding(UIElement.VisibilityProperty, visibilityBinding);

            // Header
            var headerStack = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 15) };

            var groupTitle = new TextBlock
            {
                FontSize = 16,
                FontWeight = FontWeights.Bold,
                VerticalAlignment = VerticalAlignment.Center
            };
            groupTitle.SetBinding(TextBlock.TextProperty, new Binding("SelectedItem.Name") { Source = groupList, StringFormat = "分组: {0}" });
            headerStack.Children.Add(groupTitle);

            // Delete Group Button
            var delGroupBtn = new Button
            {
                Content = "删除分组",
                Margin = new Thickness(15, 0, 0, 0),
                Padding = new Thickness(10, 4, 10, 4),
                Background = Brushes.Transparent,
                Foreground = Brushes.Red,
                BorderThickness = new Thickness(1),
                BorderBrush = Brushes.Red,
                FontSize = 12,
                Cursor = Cursors.Hand
            };
            delGroupBtn.SetBinding(Button.CommandProperty, new Binding("DataContext.DeleteTagGroupCommand") { Source = this });
            delGroupBtn.SetBinding(Button.CommandParameterProperty, new Binding("SelectedItem") { Source = groupList });
            headerStack.Children.Add(delGroupBtn);

            rightPanel.Children.Add(headerStack);

            // Add Tag Area
            var addTagPanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 20) };

            // New Tag TextBox with Watermark
            _newTagTextBox = new TextBox
            {
                Padding = new Thickness(5),
                VerticalContentAlignment = VerticalAlignment.Center
            };
            var newTagTb = _newTagTextBox;
            newTagTb.SetBinding(TextBox.TextProperty, new Binding("SelectedItem.NewTagText") { Source = groupList, UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged });

            _newTagWatermark = new TextBlock
            {
                Text = "输入标签名称...",
                Foreground = Brushes.Gray,
                Margin = new Thickness(8, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center,
                IsHitTestVisible = false,
                Visibility = Visibility.Visible
            };
            var newTagWatermark = _newTagWatermark;
            newTagTb.TextChanged += (s, e) => UpdateTagWatermarkVisibility();
            newTagTb.GotFocus += (s, e) => UpdateTagWatermarkVisibility();
            newTagTb.LostFocus += (s, e) => UpdateTagWatermarkVisibility();

            var tagInputGrid = new Grid { Margin = new Thickness(0, 0, 10, 0), Width = 200 };
            tagInputGrid.Children.Add(newTagTb);
            tagInputGrid.Children.Add(newTagWatermark);

            var addTagBtn = new Button
            {
                Content = "添加标签",
                Padding = new Thickness(15, 5, 15, 5),
                Background = new SolidColorBrush(Color.FromRgb(0x21, 0x96, 0xF3)),
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand
            };
            addTagBtn.SetBinding(Button.CommandProperty, new Binding("DataContext.AddTagCommand") { Source = this });
            addTagBtn.SetBinding(Button.CommandParameterProperty, new Binding("SelectedItem") { Source = groupList });

            addTagPanel.Children.Add(tagInputGrid);
            addTagPanel.Children.Add(addTagBtn);

            Grid.SetRow(addTagPanel, 1);
            rightPanel.Children.Add(addTagPanel);

            // Tag Cloud
            var tagScroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            var tagItemsControl = new ItemsControl();

            var itemsPanelTemplate = new ItemsPanelTemplate();
            var wrapPanelFactory = new FrameworkElementFactory(typeof(WrapPanel));
            wrapPanelFactory.SetValue(WrapPanel.OrientationProperty, Orientation.Horizontal);
            itemsPanelTemplate.VisualTree = wrapPanelFactory;
            tagItemsControl.ItemsPanel = itemsPanelTemplate;

            tagItemsControl.SetBinding(ItemsControl.ItemsSourceProperty, new Binding("SelectedItem.Tags") { Source = groupList });
            tagItemsControl.ItemTemplate = CreateTagTemplate();

            tagScroll.Content = tagItemsControl;
            Grid.SetRow(tagScroll, 2);
            rightPanel.Children.Add(tagScroll);

            Grid.SetColumn(rightPanel, 1);
            contentGrid.Children.Add(rightPanel);

            // Empty State
            var emptyText = new TextBlock
            {
                Text = "← 请从左侧选择一个分组以管理标签",
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            emptyText.SetResourceReference(TextBlock.ForegroundProperty, "TextSecondaryBrush");
            var emptyVisibility = new Binding("SelectedItem") { Source = groupList, Converter = new NullToVisibilityConverter { Invert = true } };
            emptyText.SetBinding(UIElement.VisibilityProperty, emptyVisibility);
            Grid.SetColumn(emptyText, 1);
            contentGrid.Children.Add(emptyText);

            var container = new Border { Padding = new Thickness(20), Child = mainGrid };
            this.Content = container;
        }

        private DataTemplate CreateGroupTemplate()
        {
            // Simple: [Color] [Name]
            string xaml = @"
<DataTemplate xmlns=""http://schemas.microsoft.com/winfx/2006/xaml/presentation"">
    <Grid Margin=""0,8"">
        <Grid.ColumnDefinitions>
            <ColumnDefinition Width=""Auto""/>
            <ColumnDefinition Width=""*""/>
        </Grid.ColumnDefinitions>
        <Border Width=""12"" Height=""12"" CornerRadius=""6"" Background=""{Binding ColorBrush}"" Margin=""0,0,10,0"" VerticalAlignment=""Center""/>
        <TextBlock Grid.Column=""1"" Text=""{Binding Name}"" FontSize=""14"" VerticalAlignment=""Center"" Foreground=""{DynamicResource TextPrimaryBrush}""/>
    </Grid>
</DataTemplate>";
            return (DataTemplate)System.Windows.Markup.XamlReader.Parse(xaml);
        }

        private DataTemplate CreateTagTemplate()
        {
            // Tag Pill: Border -> [Name] [x]
            string xaml = @"
<DataTemplate xmlns=""http://schemas.microsoft.com/winfx/2006/xaml/presentation"">
    <Border Background=""{Binding ColorBrush}"" CornerRadius=""4"" Padding=""10,4,8,4"" Margin=""0,0,8,8""
            Tag=""{Binding DataContext, RelativeSource={RelativeSource AncestorType=UserControl}}"">
        <Border.ContextMenu>
            <ContextMenu>
                <MenuItem Header=""修改颜色"" Command=""{Binding PlacementTarget.Tag.UpdateTagColorCommand, RelativeSource={RelativeSource AncestorType=ContextMenu}}"" CommandParameter=""{Binding}"" />
                <MenuItem Header=""重命名"" Command=""{Binding PlacementTarget.Tag.RenameTagCommand, RelativeSource={RelativeSource AncestorType=ContextMenu}}"" CommandParameter=""{Binding}"" />
            </ContextMenu>
        </Border.ContextMenu>
        <StackPanel Orientation=""Horizontal"">
            <TextBlock Text=""{Binding Name}"" Foreground=""#FFFFFF"" FontSize=""13"" VerticalAlignment=""Center"" Margin=""0,0,8,0""/>
            <Button Content=""✕"" 
                    Command=""{Binding DataContext.DeleteTagCommand, RelativeSource={RelativeSource AncestorType=UserControl}}""
                    CommandParameter=""{Binding}""
                    Width=""16"" Height=""16"" 
                    Background=""Transparent"" BorderThickness=""0"" Foreground=""#FFFFFF"" Opacity=""0.7"" Cursor=""Hand"" ToolTip=""删除标签"">
                <Button.Template>
                     <ControlTemplate TargetType=""Button"">
                         <Border Background=""transparent"">
                             <TextBlock Text=""✕"" HorizontalAlignment=""Center"" VerticalAlignment=""Center"" FontSize=""10"" FontWeight=""Bold""/>
                         </Border>
                     </ControlTemplate>
                </Button.Template>
                <Button.Style>
                    <Style TargetType=""Button"">
                         <Style.Triggers>
                            <Trigger Property=""IsMouseOver"" Value=""True"">
                                <Setter Property=""Opacity"" Value=""1""/>
                            </Trigger>
                        </Style.Triggers>
                    </Style>
                </Button.Style>
            </Button>
        </StackPanel>
    </Border>
</DataTemplate>";
            return (DataTemplate)System.Windows.Markup.XamlReader.Parse(xaml);
        }

        private Style CreateGroupItemStyle()
        {
            // Using XamlReader is more robust for ControlTemplates with Triggers and TargetName
            string xaml = @"
<Style xmlns=""http://schemas.microsoft.com/winfx/2006/xaml/presentation"" 
       xmlns:x=""http://schemas.microsoft.com/winfx/2006/xaml""
       TargetType=""ListBoxItem"">
    <Setter Property=""Template"">
        <Setter.Value>
            <ControlTemplate TargetType=""ListBoxItem"">
                <Border x:Name=""Bd""
                        Padding=""10,4,10,4""
                        CornerRadius=""4""
                        SnapsToDevicePixels=""True""
                        Margin=""0,0,0,2""
                        Background=""Transparent""
                        BorderBrush=""Transparent""
                        BorderThickness=""0"">
                    <ContentPresenter/>
                </Border>
                <ControlTemplate.Triggers>
                    <Trigger Property=""IsSelected"" Value=""True"">
                        <Setter TargetName=""Bd"" Property=""Background"" Value=""{DynamicResource AccentLightBrush}""/>
                    </Trigger>
                    <Trigger Property=""IsMouseOver"" Value=""True"">
                        <Setter TargetName=""Bd"" Property=""Background"" Value=""{DynamicResource ControlHoverBrush}""/>
                    </Trigger>
                </ControlTemplate.Triggers>
            </ControlTemplate>
        </Setter.Value>
    </Setter>
</Style>";
            return (Style)System.Windows.Markup.XamlReader.Parse(xaml);
        }

        private void SubscribeEvents()
        {
            if (_viewModel == null) return;
            // Unsubscribe first to avoid duplicates
            _viewModel.RenameTagGroupRequested -= ViewModel_RenameTagGroupRequested;
            _viewModel.RenameTagRequested -= ViewModel_RenameTagRequested;
            _viewModel.UpdateTagColorRequested -= ViewModel_UpdateTagColorRequested;

            _viewModel.RenameTagGroupRequested += ViewModel_RenameTagGroupRequested;
            _viewModel.RenameTagRequested += ViewModel_RenameTagRequested;
            _viewModel.UpdateTagColorRequested += ViewModel_UpdateTagColorRequested;
        }

        private void ViewModel_RenameTagRequested(object sender, YiboFile.ViewModels.TagItemManageViewModel e)
        {
            var input = new YiboFile.Controls.Dialogs.InputDialog("重命名标签", "请输入新的标签名称:", e.Name);
            input.Owner = Window.GetWindow(this);
            if (input.ShowDialog() == true)
            {
                _viewModel.RenameTag(e, input.InputText);
            }
        }

        private void ViewModel_UpdateTagColorRequested(object sender, YiboFile.ViewModels.TagItemManageViewModel e)
        {
            var dialog = new YiboFile.Controls.Dialogs.ColorSelectionDialog(e.Color);
            dialog.Owner = Window.GetWindow(this);
            if (dialog.ShowDialog() == true)
            {
                _viewModel.UpdateTagColor(e, dialog.SelectedColor);
            }
        }

        private void ViewModel_RenameTagGroupRequested(object sender, YiboFile.ViewModels.TagGroupManageViewModel e)
        {
            var input = new YiboFile.Controls.Dialogs.InputDialog("重命名分组", "请输入新的分组名称:", e.Name);
            input.Owner = Window.GetWindow(this);
            if (input.ShowDialog() == true)
            {
                _viewModel.RenameTagGroup(e, input.InputText);
            }
        }

        public void LoadSettings()
        {
            _viewModel?.RefreshTagGroups();
        }

        public void SaveSettings() { }

        /// <summary>
        /// 更新分组输入框水印可见性
        /// </summary>
        private void UpdateGroupWatermarkVisibility()
        {
            if (_newGroupWatermark != null && _newGroupTextBox != null)
                _newGroupWatermark.Visibility = string.IsNullOrEmpty(_newGroupTextBox.Text)
                    ? Visibility.Visible : Visibility.Collapsed;
        }

        /// <summary>
        /// 更新标签输入框水印可见性
        /// </summary>
        private void UpdateTagWatermarkVisibility()
        {
            if (_newTagWatermark != null && _newTagTextBox != null)
                _newTagWatermark.Visibility = string.IsNullOrEmpty(_newTagTextBox.Text)
                    ? Visibility.Visible : Visibility.Collapsed;
        }
    }

    public class NullToVisibilityConverter : IValueConverter
    {
        public bool Invert { get; set; }
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            bool isNull = value == null;
            if (Invert) return isNull ? Visibility.Visible : Visibility.Collapsed;
            return isNull ? Visibility.Collapsed : Visibility.Visible;
        }
        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture) => throw new NotImplementedException();
    }
}
