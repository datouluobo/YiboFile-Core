using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Forms;
using YiboFile.Controls;
using YiboFile.Services.Core;

namespace YiboFile
{
    public partial class LibraryManagementWindow : Window
    {
        private List<Library> _libraries;

        public LibraryManagementWindow()
        {
            InitializeComponent();
            LoadLibraries();
            this.KeyDown += LibraryManagementWindow_KeyDown;
        }

        private void LibraryManagementWindow_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Escape)
            {
                DialogResult = false;
                Close();
            }
        }

        private void LoadLibraries()
        {
            _libraries = DatabaseManager.GetAllLibraries();
            LibrariesListBox.ItemsSource = _libraries;
        }

        private void LibrariesListBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            bool hasSelection = LibrariesListBox.SelectedItem != null;
            RenameLibraryBtn.IsEnabled = hasSelection;
            DeleteLibraryBtn.IsEnabled = hasSelection;

            // 更新上/下移按钮状态
            int selectedIndex = LibrariesListBox.SelectedIndex;
            int itemCount = LibrariesListBox.Items.Count;
            MoveUpBtn.IsEnabled = hasSelection && selectedIndex > 0;
            MoveDownBtn.IsEnabled = hasSelection && selectedIndex < itemCount - 1;

            // 更新库位置按钮状态
            AddPathBtn.IsEnabled = hasSelection;

            if (hasSelection && LibrariesListBox.SelectedItem is Library selectedLibrary)
            {
                LoadLibraryPaths(selectedLibrary.Id);
            }
            else
            {
                PathsListBox.ItemsSource = null;
                EditPathBtn.IsEnabled = false;
                RemovePathBtn.IsEnabled = false;
            }
        }

        private void LoadLibraryPaths(int libraryId)
        {
            var paths = DatabaseManager.GetLibraryPaths(libraryId);
            foreach (var path in paths)
            {
                if (string.IsNullOrEmpty(path.DisplayName))
                {
                    path.DisplayName = Path.GetFileName(path.Path) ?? path.Path;
                }
            }
            PathsListBox.ItemsSource = paths;
        }

        private void NewLibrary_Click(object sender, RoutedEventArgs e)
        {
            CreateNewLibrary();
        }

        private void NewLibraryNameTextBox_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Enter)
            {
                CreateNewLibrary();
            }
        }

        private void CreateNewLibrary()
        {
            var categoryName = NewLibraryNameTextBox.Text?.Trim();
            if (string.IsNullOrWhiteSpace(categoryName))
            {
                DialogService.Warning("请输入库名称", "提示", this);
                NewLibraryNameTextBox.Focus();
                return;
            }

            try
            {
                var libraryId = DatabaseManager.AddLibrary(categoryName);
                if (libraryId > 0)
                {
                    // 清空输入框
                    NewLibraryNameTextBox.Text = "";

                    LoadLibraries();

                    // 选中新创建的库
                    var newLibrary = _libraries.FirstOrDefault(l => l.Id == libraryId);
                    if (newLibrary != null)
                    {
                        LibrariesListBox.SelectedItem = newLibrary;
                    }

                    // 使用文件夹选择对话框添加初始位置（可选）
                    if (DialogService.Ask("是否现在添加库的位置？\n\n您可以稍后在库位置列表中添加位置。", "添加位置", this))
                    {
                        using (var folderDialog = new FolderBrowserDialog())
                        {
                            folderDialog.Description = "选择库的初始文件夹:";
                            folderDialog.ShowNewFolderButton = false;
                            folderDialog.SelectedPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);

                            if (folderDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                            {
                                var path = folderDialog.SelectedPath;
                                DatabaseManager.AddLibraryPath(libraryId, path);
                                LoadLibraryPaths(libraryId);
                            }
                        }
                    }
                }
                else if (libraryId < 0)
                {
                    // 库已存在，刷新列表并选中它
                    LoadLibraries();
                    var existingLibrary = _libraries.FirstOrDefault(l => l.Name == categoryName);
                    if (existingLibrary != null)
                    {
                        LibrariesListBox.SelectedItem = existingLibrary;
                    }
                    DialogService.Info("库名称已存在，已选中该库", "提示", this);
                    NewLibraryNameTextBox.Text = "";
                }
                else
                {
                    DialogService.Error("创建库失败", "错误", this);
                }
            }
            catch (Exception ex)
            {
                DialogService.Error($"创建库失败: {ex.Message}", "错误", this);
            }
        }

        private void RenameLibrary_Click(object sender, RoutedEventArgs e)
        {
            if (LibrariesListBox.SelectedItem is Library selectedLibrary)
            {
                var dialog = new PathInputDialog("请输入新的库名称:");
                dialog.InputText = selectedLibrary.Name;
                if (dialog.ShowDialog() == true)
                {
                    var newName = dialog.InputText.Trim();
                    if (string.IsNullOrEmpty(newName))
                    {
                        DialogService.Warning("库名称不能为空", "提示", this);
                        return;
                    }

                    try
                    {
                        DatabaseManager.UpdateLibraryName(selectedLibrary.Id, newName);
                        LoadLibraries();
                        LibrariesListBox.SelectedItem = _libraries.FirstOrDefault(l => l.Id == selectedLibrary.Id);
                    }
                    catch (Exception ex)
                    {
                        DialogService.Error($"编辑库失败: {ex.Message}", "错误", this);
                    }
                }
            }
        }

        private void DeleteLibrary_Click(object sender, RoutedEventArgs e)
        {
            if (LibrariesListBox.SelectedItem is Library selectedLibrary)
            {
                if (!ConfirmDialog.Show(
                    $"确定要删除库 \"{selectedLibrary.Name}\" 吗？\n\n删除后，该库的所有位置将被移除，但不会删除实际文件。",
                    "确认删除",
                    ConfirmDialog.DialogType.Question,
                    this))
                {
                    return;
                }

                try
                {
                    DatabaseManager.DeleteLibrary(selectedLibrary.Id);
                    LoadLibraries();
                    PathsListBox.ItemsSource = null;
                    NotificationService.Show($"已删除库 \"{selectedLibrary.Name}\"", NotificationType.Success);
                }
                catch (Exception ex)
                {
                    DialogService.Error($"删除库失败: {ex.Message}", "错误", this);
                }
            }
        }

        private void AddPath_Click(object sender, RoutedEventArgs e)
        {
            if (LibrariesListBox.SelectedItem is Library selectedLibrary)
            {
                // 使用文件夹选择对话框
                using (var dialog = new FolderBrowserDialog())
                {
                    dialog.Description = "选择要添加到库的文件夹:";
                    dialog.ShowNewFolderButton = false;

                    // 如果库已有位置，从第一个位置开始浏览
                    if (selectedLibrary.Paths != null && selectedLibrary.Paths.Count > 0)
                    {
                        var firstPath = selectedLibrary.Paths[0];
                        if (Directory.Exists(firstPath))
                        {
                            dialog.SelectedPath = firstPath;
                        }
                    }
                    else
                    {
                        dialog.SelectedPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                    }

                    if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                    {
                        var path = dialog.SelectedPath;

                        // 检查是否已存在
                        var existingPaths = DatabaseManager.GetLibraryPaths(selectedLibrary.Id);
                        if (existingPaths.Any(p => p.Path.Equals(path, StringComparison.OrdinalIgnoreCase)))
                        {
                            DialogService.Info("该路径已存在于库中", "提示", this);
                            return;
                        }

                        try
                        {
                            DatabaseManager.AddLibraryPath(selectedLibrary.Id, path);
                            LoadLibraryPaths(selectedLibrary.Id);
                            NotificationService.Show("库位置添加成功", NotificationType.Success);
                        }
                        catch (Exception ex)
                        {
                            DialogService.Error($"添加位置失败: {ex.Message}", "错误", this);
                        }
                    }
                }
            }
        }

        private void EditPath_Click(object sender, RoutedEventArgs e)
        {
            if (PathsListBox.SelectedItem is LibraryPath selectedPath)
            {
                var dialog = new PathInputDialog("请输入显示名称:");
                dialog.InputText = selectedPath.DisplayName ?? Path.GetFileName(selectedPath.Path) ?? selectedPath.Path;
                if (dialog.ShowDialog() == true)
                {
                    var newDisplayName = dialog.InputText.Trim();
                    if (string.IsNullOrEmpty(newDisplayName))
                    {
                        newDisplayName = null;
                    }

                    try
                    {
                        DatabaseManager.UpdateLibraryPathDisplayName(selectedPath.LibraryId, selectedPath.Path, newDisplayName);
                        LoadLibraryPaths(selectedPath.LibraryId);
                    }
                    catch (Exception ex)
                    {
                        DialogService.Error($"更新显示名称失败: {ex.Message}", "错误", this);
                    }
                }
            }
        }

        private void RemovePath_Click(object sender, RoutedEventArgs e)
        {
            if (PathsListBox.SelectedItem is LibraryPath selectedPath)
            {
                if (DialogService.Ask($"确定要从库中移除位置 \"{selectedPath.Path}\" 吗？", "确认删除", this))
                {
                    try
                    {
                        DatabaseManager.RemoveLibraryPath(selectedPath.LibraryId, selectedPath.Path);
                        LoadLibraryPaths(selectedPath.LibraryId);
                        NotificationService.Show("库位置已移除", NotificationType.Success);
                    }
                    catch (Exception ex)
                    {
                        DialogService.Error($"删除位置失败: {ex.Message}", "错误", this);
                    }
                }
            }
        }

        private void PathsListBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            bool hasSelection = PathsListBox.SelectedItem != null;
            EditPathBtn.IsEnabled = hasSelection;
            RemovePathBtn.IsEnabled = hasSelection;
        }

        private void MoveLibraryUp_Click(object sender, RoutedEventArgs e)
        {
            MoveLibrary(-1);
        }

        private void MoveLibraryDown_Click(object sender, RoutedEventArgs e)
        {
            MoveLibrary(1);
        }

        private void MoveLibrary(int direction)
        {
            if (LibrariesListBox.SelectedItem == null) return;

            int selectedIndex = LibrariesListBox.SelectedIndex;
            int newIndex = selectedIndex + direction;

            if (newIndex < 0 || newIndex >= _libraries.Count) return;

            try
            {
                var currentLibrary = _libraries[selectedIndex];

                if (direction < 0)
                {
                    DatabaseManager.MoveLibraryUp(currentLibrary.Id);
                }
                else
                {
                    DatabaseManager.MoveLibraryDown(currentLibrary.Id);
                }

                // 重新加载并恢复选中
                LoadLibraries();

                if (newIndex >= 0 && newIndex < LibrariesListBox.Items.Count)
                {
                    LibrariesListBox.SelectedIndex = newIndex;
                }
            }
            catch (Exception ex)
            {
                DialogService.Error($"移动库失败: {ex.Message}", "错误", this);
            }
        }

        private void OK_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}

