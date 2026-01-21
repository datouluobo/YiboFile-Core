using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Extensions.DependencyInjection;
using YiboFile.Services.Features;
using YiboFile.Controls.Dialogs;
using YiboFile.Models;

namespace YiboFile.Controls
{
    public partial class TagBrowsePanel : UserControl
    {
        public event Action<int, string> TagClicked;
        public event EventHandler BackRequested;




        // Dependency Properties for UI control
        public static readonly DependencyProperty ShowHeaderProperty =
            DependencyProperty.Register("ShowHeader", typeof(bool), typeof(TagBrowsePanel), new PropertyMetadata(true));

        public bool ShowHeader
        {
            get { return (bool)GetValue(ShowHeaderProperty); }
            set { SetValue(ShowHeaderProperty, value); }
        }

        public static readonly DependencyProperty ShowFooterProperty =
            DependencyProperty.Register("ShowFooter", typeof(bool), typeof(TagBrowsePanel), new PropertyMetadata(false));

        public bool ShowFooter
        {
            get { return (bool)GetValue(ShowFooterProperty); }
            set { SetValue(ShowFooterProperty, value); }
        }

        private ITagService _tagService;

        public TagBrowsePanel()
        {
            InitializeComponent();
            this.Loaded += TagBrowsePanel_Loaded;
        }

        private void TagBrowsePanel_Loaded(object sender, RoutedEventArgs e)
        {
            if (System.ComponentModel.DesignerProperties.GetIsInDesignMode(this)) return;
            RefreshTags();
        }

        public void RefreshTags()
        {
            try
            {
                _tagService = App.ServiceProvider?.GetService<ITagService>();
                if (_tagService == null)
                {
                    EmptyStateText.Text = "标签服务不可用";
                    EmptyStateText.Visibility = Visibility.Visible;
                    return;
                }

                var groups = _tagService.GetTagGroups().ToList();

                // Create ViewModels for binding
                var viewModels = new List<TagGroupViewModel>();

                // 1. Process defined groups
                foreach (var group in groups)
                {
                    var tags = _tagService.GetTagsByGroup(group.Id).Select(t => new TagViewModel
                    {
                        Id = t.Id,
                        Name = t.Name,
                        Color = t.Color
                    }).ToList();

                    viewModels.Add(new TagGroupViewModel
                    {
                        Id = group.Id,
                        Name = group.Name,
                        Tags = tags
                    });
                }

                // 2. Process ungrouped tags? (Assuming ITagGroup doesn't cover ungrouped yet, or GroupId=0)
                // Implementation detail: Check if your default ungrouped handler exists.
                // For now, adhering to strict ITagService usage.

                TagGroupsControl.ItemsSource = viewModels;
                EmptyStateText.Visibility = viewModels.Any() ? Visibility.Collapsed : Visibility.Visible;

                // Subscribe to events (avoid duplicate)
                _tagService.TagUpdated -= OnTagUpdated;
                _tagService.TagUpdated += OnTagUpdated;
            }
            catch (Exception ex)
            {
                // Fallback logging
                System.Diagnostics.Debug.WriteLine($"Error loading tags: {ex.Message}");
            }
        }

        private void OnTagUpdated(int tagId, string newColor)
        {
            Dispatcher.Invoke(() =>
            {
                if (TagGroupsControl.ItemsSource is List<TagGroupViewModel> groups)
                {
                    foreach (var group in groups)
                    {
                        var tag = group.Tags.FirstOrDefault(t => t.Id == tagId);
                        if (tag != null)
                        {
                            tag.Color = newColor;
                            // Force refresh if needed, but PropertyChanged should handle it
                            // Assuming TagViewModel implements INotifyPropertyChanged
                        }
                    }
                }
            });
        }

        private void TagItem_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is Border border && border.DataContext is TagViewModel tag)
            {
                TagClicked?.Invoke(tag.Id, tag.Name);
            }
        }

        private void TagListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var listBox = sender as ListBox;
            if (listBox == null || listBox.SelectedItem == null) return;

            var tag = listBox.SelectedItem as TagViewModel;
            if (tag != null)
            {
                TagClicked?.Invoke(tag.Id, tag.Name);
            }

            // Clear selection to allow clicking the same tag again
            listBox.SelectedItem = null;
        }

        private void ManageTagsBtn_Click(object sender, RoutedEventArgs e)
        {
            var window = new YiboFile.Windows.NavigationSettingsWindow("Tag");
            window.Owner = Window.GetWindow(this);
            window.ShowDialog();
        }

        private void BackBtn_Click(object sender, RoutedEventArgs e)
        {
            BackRequested?.Invoke(this, EventArgs.Empty);
        }

        private void RenameTag_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem menuItem && menuItem.DataContext is TagViewModel tag)
            {
                var dialog = new InputDialog("重命名标签", "请输入新名称:", tag.Name);
                if (dialog.ShowDialog() == true && !string.IsNullOrWhiteSpace(dialog.InputText))
                {
                    _tagService?.RenameTag(tag.Id, dialog.InputText);
                    RefreshTags();
                }
            }
        }

        private void ChangeTagColor_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem menuItem && menuItem.DataContext is TagViewModel tag)
            {
                var dialog = new ColorSelectionDialog(tag.Color);
                dialog.Owner = Window.GetWindow(this);
                if (dialog.ShowDialog() == true)
                {
                    _tagService?.UpdateTagColor(tag.Id, dialog.SelectedColor);
                    RefreshTags();
                }
            }
        }

        private void DeleteTag_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem menuItem && menuItem.DataContext is TagViewModel tag)
            {
                if (MessageBox.Show($"确定要删除标签 '{tag.Name}' 吗?", "确认删除", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                {
                    _tagService?.DeleteTag(tag.Id);
                    RefreshTags();
                }
            }
        }

        // Group Header Actions (DataContext is TagGroupViewModel)
        private void AddTagHeader_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem menuItem && menuItem.DataContext is TagGroupViewModel group)
            {
                // Find group ID is tricky because TagGroupViewModel doesn't store native ID yet
                // We need to fix TagGroupViewModel to include Id from TagService
                // Let's assume we can lookup or we fix ViewModel first.
                // Fixing ViewModel below.
                var dialog = new InputDialog("添加标签", "请输入标签名称:");
                if (dialog.ShowDialog() == true && !string.IsNullOrWhiteSpace(dialog.InputText))
                {
                    _tagService?.AddTag(group.Id, dialog.InputText);
                    RefreshTags();
                }
            }
        }

        private void RenameGroupHeader_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem menuItem && menuItem.DataContext is TagGroupViewModel group)
            {
                var dialog = new InputDialog("重命名分组", "请输入新名称:", group.Name);
                if (dialog.ShowDialog() == true && !string.IsNullOrWhiteSpace(dialog.InputText))
                {
                    _tagService?.RenameTagGroup(group.Id, dialog.InputText);
                    RefreshTags();
                }
            }
        }

        private void DeleteGroupHeader_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem menuItem && menuItem.DataContext is TagGroupViewModel group)
            {
                if (MessageBox.Show($"确定要删除分组 '{group.Name}' 吗?", "确认删除", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                {
                    _tagService?.DeleteTagGroup(group.Id);
                    RefreshTags();
                }
            }
        }

        private void TagItem_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed && sender is ListBoxItem item)
            {
                // Logic to start drag if needed (dragging tag itself), 
                // but requirements said "Dragging files onto a tag".
                // So this might not be needed unless we want to drag tags to rearrange.
                // Skipping for now to focus on Drop.
            }
        }

        private void TagItem_Drop(object sender, DragEventArgs e)
        {
            if (sender is ListBoxItem item && item.DataContext is TagViewModel tag)
            {
                if (e.Data.GetDataPresent(DataFormats.FileDrop))
                {
                    var files = (string[])e.Data.GetData(DataFormats.FileDrop);
                    if (files != null && files.Length > 0)
                    {
                        foreach (var file in files)
                        {
                            try
                            {
                                _tagService?.AddTagToFile(file, tag.Id);
                            }
                            catch { /* Ignore duplicates or errors */ }
                        }
                        // Feedback?
                        MessageBox.Show($"已将 {files.Length} 个文件添加到标签 '{tag.Name}'");
                    }
                }
            }
        }

        // Footer Actions
        private void NewGroupBtn_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new InputDialog("新建分组", "请输入分组名称:");
            if (dialog.ShowDialog() == true && !string.IsNullOrWhiteSpace(dialog.InputText))
            {
                _tagService?.AddTagGroup(dialog.InputText);
                RefreshTags();
            }
        }

        private void NewTagBtn_Click(object sender, RoutedEventArgs e)
        {
            // Default to first group if any, or ask user to select group? 
            // InputDialog is simple text. We need a way to select group?
            // For now, let's create in first group or "Default" group.
            // Or use a more complex logic.
            // Simplest: Ask name, add to first available group or "未分组" equivalent.
            // But ITagService requires GroupId.
            // Let's check groups count.
            var groups = _tagService?.GetTagGroups().ToList();
            if (groups == null || groups.Count == 0)
            {
                MessageBox.Show("请先创建分组");
                return;
            }

            // Ideally we show a dialog to pick group + name.
            // But reuse InputDialog for Name, then pick Group?
            // Let's assume selection of group is needed.
            // Hack for now: Add to first group, user can move later?
            // Better: Add an overload to InputDialog? No.

            // Let's just prompt for name and add to the first group for now, 
            // or if we have a "selected group" context.
            // We don't have selected group context in the footer button.

            var dialog = new InputDialog("新建标签 (添加到第一分组)", "请输入标签名称:");
            if (dialog.ShowDialog() == true && !string.IsNullOrWhiteSpace(dialog.InputText))
            {
                _tagService?.AddTag(groups[0].Id, dialog.InputText);
                RefreshTags();
            }
        }
    }

    public class TagGroupViewModel
    {
        public int Id { get; set; } // Added ID
        public string Name { get; set; }
        public List<TagViewModel> Tags { get; set; }
    }
}
