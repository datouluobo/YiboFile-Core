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

        public TagManagementDialog()
        {
            InitializeComponent();
            _tagService = App.ServiceProvider?.GetService<ITagService>();
            GroupsList.ItemsSource = Groups;

            Loaded += (s, e) => RefreshGroups();
        }

        private void RefreshGroups()
        {
            if (_tagService == null) return;
            Groups.Clear();
            var groups = _tagService.GetTagGroups();
            foreach (var g in groups)
            {
                var groupVm = new TagGroupManageViewModel
                {
                    Id = g.Id,
                    Name = g.Name,
                    Color = g.Color
                };

                // Load tags immediately
                var tags = _tagService.GetTagsByGroup(g.Id);
                foreach (var t in tags)
                {
                    groupVm.Tags.Add(new TagManageViewModel
                    {
                        Id = t.Id,
                        Name = t.Name,
                        Color = t.Color ?? "#2E8B57",
                        GroupId = t.GroupId
                    });
                }

                Groups.Add(groupVm);
            }
        }

        // Logic for Add Tag from within Group
        private void AddTagToGroup_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement btn && btn.DataContext is TagGroupManageViewModel groupVm)
            {
                // Find sibling TextBox - simplistic approach or binding approach?
                // VisualTreeHelper search is reliable but tedious. 
                // Better: The TextBox is updated in UI, but how do we get its text?
                // Let's rely on finding the TextBox via name in the visual tree relative to the button? 
                // OR: Binding? But TagGroupManageViewModel doesn't have "NewTagText" property.
                // Let's traverse up to Grid, then find TextBox "NewTagBox".

                var parent = System.Windows.Media.VisualTreeHelper.GetParent(btn);
                while (parent != null && !(parent is Grid)) parent = System.Windows.Media.VisualTreeHelper.GetParent(parent);

                if (parent is Grid grid)
                {
                    // This grid contains NewTagBox
                    foreach (var child in grid.Children)
                    {
                        if (child is Grid innerGrid) // TextBox is wrapped in Grid for Watermark
                        {
                            foreach (var innerChild in innerGrid.Children)
                            {
                                if (innerChild is TextBox tb && tb.Name == "NewTagBox")
                                {
                                    string name = tb.Text.Trim();
                                    if (!string.IsNullOrEmpty(name))
                                    {
                                        try
                                        {
                                            _tagService?.AddTag(groupVm.Id, name);
                                            tb.Text = "";
                                            // Refresh just this group's tags? Or all? easier to refresh all for now or modify collection locally.
                                            // Refreshing all ensures ID sync.
                                            RefreshGroups();
                                        }
                                        catch (Exception ex)
                                        {
                                            ShowError($"添加标签失败: {ex.Message}");
                                        }
                                    }
                                    return;
                                }
                            }
                        }
                    }
                }
            }
        }

        private void NewTagBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                // Find the button and mock click?
                // Or navigate tree to find Button?
                // Let's traverse up to Grid (same container) and find button.
                if (sender is TextBox tb)
                {
                    var parent = System.Windows.Media.VisualTreeHelper.GetParent(tb.Parent as FrameworkElement); // tb -> Grid -> Grid (Container)
                    if (parent is Grid grid)
                    {
                        foreach (var child in grid.Children)
                        {
                            if (child is Button btn && btn.Name == "") // The button doesn't have x:Name in template?
                            {
                                // We didn't give button x:Name. 
                                // Let's rely on AddTagToGroup_Click logic but call it directly if we can context.
                                // Instead of complex traversal, let's execute logic directly if DataContext matches.
                                if (tb.DataContext is TagGroupManageViewModel groupVm)
                                {
                                    string name = tb.Text.Trim();
                                    if (!string.IsNullOrEmpty(name))
                                    {
                                        try
                                        {
                                            _tagService?.AddTag(groupVm.Id, name);
                                            tb.Text = "";
                                            RefreshGroups();
                                        }
                                        catch (Exception ex) { ShowError($"添加标签失败: {ex.Message}"); }
                                    }
                                }
                            }
                        }
                    }
                }
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
                // Select the new group - Not supported in ItemsControl
                // var newGroup = Groups.FirstOrDefault(g => g.Name == name);
                // if (newGroup != null) GroupsList.SelectedItem = newGroup;
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

        // Removed Old AddTagBtn_Click and NewTagNameBox_KeyDown as they are now inline


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
                        // Restore selection - Not supported in ItemsControl, maybe scroll to it later?
                        // var updated = Groups.FirstOrDefault(g => g.Id == vm.Id);
                        // if (updated != null) GroupsList.SelectedItem = updated;
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
                        RefreshGroups(); // Refresh all to simplest update
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
                        RefreshGroups();
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
        public ObservableCollection<TagManageViewModel> Tags { get; set; } = new();
    }

    public class TagManageViewModel
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Color { get; set; }
        public int GroupId { get; set; }
    }


}
