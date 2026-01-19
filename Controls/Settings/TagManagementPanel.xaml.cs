using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using Microsoft.Extensions.DependencyInjection;
using YiboFile.Services.Features;
using YiboFile.Controls.Dialogs; // Shared InputDialog

namespace YiboFile.Controls.Settings
{
    public partial class TagManagementPanel : UserControl
    {
        private readonly ITagService _tagService;
        public ObservableCollection<TagGroupManageViewModel> Groups { get; set; } = new();

        public TagManagementPanel()
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
                // Find sibling TextBox
                var parent = System.Windows.Media.VisualTreeHelper.GetParent(btn);
                // Grid -> Grid (Container) contains the TextBox
                while (parent != null && !(parent is Grid)) parent = System.Windows.Media.VisualTreeHelper.GetParent(parent);

                if (parent is Grid grid)
                {
                    // This grid contains two columns: Grid (with TextBox) and Button
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
            if (e.Key == Key.Enter && sender is TextBox tb)
            {
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
                // Handle Enter press: call AddTagToGroup logic or duplicate it
                // We duplicated it above because getting reference to button is annoying.
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

        private void DeleteGroup_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement fe && fe.DataContext is TagGroupManageViewModel vm)
            {
                try
                {
                    if (MessageBox.Show(Window.GetWindow(this), $"确定要删除分组“{vm.Name}”及其所有标签吗？", "确认删除", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
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
                input.Owner = Window.GetWindow(this);
                if (input.ShowDialog() == true)
                {
                    try
                    {
                        _tagService?.RenameTagGroup(vm.Id, input.InputText);
                        RefreshGroups();
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
                    if (MessageBox.Show(Window.GetWindow(this), $"确定要删除标签“{vm.Name}”吗？", "确认删除", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
                    {
                        _tagService?.DeleteTag(vm.Id);
                        RefreshGroups();
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
                input.Owner = Window.GetWindow(this);
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
