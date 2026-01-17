using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using Microsoft.Extensions.DependencyInjection;
using YiboFile.Services.Features;

namespace YiboFile.Controls.Dialogs
{
    public partial class TagManagementDialog : Window
    {
        private readonly ITagService _tagService;
        public ObservableCollection<TagGroupManageViewModel> Groups { get; set; } = new();
        public ObservableCollection<TagManageViewModel> CurrentTags { get; set; } = new();

        public TagManagementDialog()
        {
            InitializeComponent();
            _tagService = App.ServiceProvider?.GetService<ITagService>();
            GroupsList.ItemsSource = Groups;
            TagsList.ItemsSource = CurrentTags;

            Loaded += (s, e) => RefreshGroups();
        }

        private void RefreshGroups()
        {
            if (_tagService == null) return;
            Groups.Clear();
            var groups = _tagService.GetTagGroups();
            foreach (var g in groups)
            {
                Groups.Add(new TagGroupManageViewModel { Id = g.Id, Name = g.Name, Color = g.Color });
            }
        }

        private void GroupsList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var selectedGroup = GroupsList.SelectedItem as TagGroupManageViewModel;
            if (selectedGroup != null)
            {
                CurrentGroupNameText.Text = selectedGroup.Name;
                LoadTagsForGroup(selectedGroup.Id);
                NoGroupSelectedHint.Visibility = Visibility.Collapsed;
                AddTagArea.Visibility = Visibility.Visible;
                NewTagNameBox.Focus();
            }
            else
            {
                CurrentGroupNameText.Text = "";
                CurrentTags.Clear();
                NoGroupSelectedHint.Visibility = Visibility.Visible;
                AddTagArea.Visibility = Visibility.Collapsed;
            }
        }

        private void LoadTagsForGroup(int groupId)
        {
            if (_tagService == null) return;
            CurrentTags.Clear();
            var tags = _tagService.GetTagsByGroup(groupId);
            foreach (var t in tags)
            {
                CurrentTags.Add(new TagManageViewModel { Id = t.Id, Name = t.Name, Color = t.Color ?? "#2E8B57", GroupId = t.GroupId });
            }
        }

        private void AddGroupBtn_Click(object sender, RoutedEventArgs e)
        {
            var name = NewGroupNameBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(name)) return;

            try
            {
                _tagService?.AddTagGroup(name);
                NewGroupNameBox.Text = "";
                RefreshGroups();
                // Select the new group
                var newGroup = Groups.FirstOrDefault(g => g.Name == name);
                if (newGroup != null) GroupsList.SelectedItem = newGroup;
            }
            catch (Exception ex)
            {
                ShowError($"添加分组失败: {ex.Message} (可能是名称重复)");
            }
        }

        private void NewGroupNameBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter) AddGroupBtn_Click(sender, e);
        }

        private void AddTagBtn_Click(object sender, RoutedEventArgs e)
        {
            var selectedGroup = GroupsList.SelectedItem as TagGroupManageViewModel;
            if (selectedGroup == null) return;

            var name = NewTagNameBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(name)) return;

            try
            {
                _tagService?.AddTag(selectedGroup.Id, name);
                NewTagNameBox.Text = "";
                LoadTagsForGroup(selectedGroup.Id);
            }
            catch (Exception ex)
            {
                ShowError($"添加标签失败: {ex.Message}");
            }
        }

        private void NewTagNameBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter) AddTagBtn_Click(sender, e);
        }

        private void DeleteGroup_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement fe && fe.DataContext is TagGroupManageViewModel vm)
            {
                try
                {
                    if (MessageBox.Show($"确定要删除分组“{vm.Name}”及其所有标签吗？", "确认删除", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
                    {
                        _tagService?.DeleteTagGroup(vm.Id);
                        RefreshGroups();
                    }
                }
                catch (Exception ex)
                {
                    ShowError($"删除失败: {ex.Message}");
                }
            }
        }

        private void RenameGroup_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement fe && fe.DataContext is TagGroupManageViewModel vm)
            {
                var input = new InputDialog("重命名分组", "请输入新的分组名称:", vm.Name);
                if (input.ShowDialog() == true)
                {
                    try
                    {
                        _tagService?.RenameTagGroup(vm.Id, input.InputText);
                        RefreshGroups();
                        // Restore selection
                        var updated = Groups.FirstOrDefault(g => g.Id == vm.Id);
                        if (updated != null) GroupsList.SelectedItem = updated;
                    }
                    catch (Exception ex)
                    {
                        ShowError($"重命名失败: {ex.Message}");
                    }
                }
            }
        }

        private void DeleteTag_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement fe && fe.DataContext is TagManageViewModel vm)
            {
                try
                {
                    if (MessageBox.Show($"确定要删除标签“{vm.Name}”吗？", "确认删除", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
                    {
                        _tagService?.DeleteTag(vm.Id);
                        LoadTagsForGroup(vm.GroupId);
                    }
                }
                catch (Exception ex)
                {
                    ShowError($"删除失败: {ex.Message}");
                }
            }
        }

        private void RenameTag_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement fe && fe.DataContext is TagManageViewModel vm)
            {
                var input = new InputDialog("重命名标签", "请输入新的标签名称:", vm.Name);
                if (input.ShowDialog() == true)
                {
                    try
                    {
                        _tagService?.RenameTag(vm.Id, input.InputText);
                        LoadTagsForGroup(vm.GroupId);
                    }
                    catch (Exception ex)
                    {
                        ShowError($"重命名失败: {ex.Message}");
                    }
                }
            }
        }

        // Inline Edit Stubs - for now using Dialogs for Rename
        private void EditNameBox_KeyDown(object sender, KeyEventArgs e) { }
        private void EditNameBox_LostFocus(object sender, RoutedEventArgs e) { }


        private void ShowError(string msg)
        {
            if (ErrorText == null || ErrorOverlay == null) return;
            ErrorText.Text = msg;
            ErrorOverlay.Visibility = Visibility.Visible;

            // Auto hide after 3 seconds
            var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
            timer.Tick += (s, e) =>
            {
                if (ErrorOverlay != null) ErrorOverlay.Visibility = Visibility.Collapsed;
                timer.Stop();
            };
            timer.Start();
        }
    }

    public class TagGroupManageViewModel
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Color { get; set; }
    }

    public class TagManageViewModel
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Color { get; set; }
        public int GroupId { get; set; }
    }

    public class InputDialog : Window
    {
        public string InputText { get; private set; }
        private TextBox _textBox;

        public InputDialog(string title, string prompt, string defaultValue = "")
        {
            Title = title;
            Width = 350;
            Height = 160;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            ResizeMode = ResizeMode.NoResize;
            Background = System.Windows.Media.Brushes.White;

            var grid = new Grid { Margin = new Thickness(20) };
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            grid.Children.Add(new TextBlock { Text = prompt, Margin = new Thickness(0, 0, 0, 10), FontWeight = FontWeights.SemiBold });

            _textBox = new TextBox { Text = defaultValue, Margin = new Thickness(0, 0, 0, 15), Padding = new Thickness(5) };
            Grid.SetRow(_textBox, 1);
            grid.Children.Add(_textBox);

            var btnPanel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            var okBtn = new Button { Content = "确定", Width = 80, Height = 30, IsDefault = true, Margin = new Thickness(0, 0, 10, 0) };
            okBtn.Click += (s, e) => { InputText = _textBox.Text; DialogResult = true; };

            var cancelBtn = new Button { Content = "取消", Width = 80, Height = 30, IsCancel = true };

            btnPanel.Children.Add(okBtn);
            btnPanel.Children.Add(cancelBtn);
            Grid.SetRow(btnPanel, 2);
            grid.Children.Add(btnPanel);

            Content = grid;

            _textBox.Focus();
            _textBox.SelectAll();

            // Handle Enter key
            KeyDown += (s, e) =>
            {
                if (e.Key == Key.Enter) { InputText = _textBox.Text; DialogResult = true; }
                if (e.Key == Key.Escape) { DialogResult = false; }
            };
        }
    }
}
