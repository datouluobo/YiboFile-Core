using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Forms;

namespace OoiMRR
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
            if (LibrariesListBox.SelectedItem is Library selectedLibrary)
            {
                LoadLibraryPaths(selectedLibrary.Id);
            }
            else
            {
                PathsListBox.ItemsSource = null;
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
            var dialog = new PathInputDialog("请输入库名称:");
            if (dialog.ShowDialog() == true)
            {
                var name = dialog.InputText.Trim();
                if (string.IsNullOrEmpty(name))
                {
                    System.Windows.MessageBox.Show("库名称不能为空", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                try
                {
                    var libraryId = DatabaseManager.AddLibrary(name);
                    if (libraryId > 0)
                    {
                        LoadLibraries();
                        // 选中新创建的库
                        var newLibrary = _libraries.FirstOrDefault(l => l.Id == libraryId);
                        if (newLibrary != null)
                        {
                            LibrariesListBox.SelectedItem = newLibrary;
                        }
                        
                        // 使用文件夹选择对话框添加初始位置（可选）
                        var result = System.Windows.MessageBox.Show(
                            "是否现在添加库的位置？\n\n您可以稍后在库位置列表中添加位置。",
                            "添加位置",
                            MessageBoxButton.YesNo,
                            MessageBoxImage.Question);
                        
                        if (result == MessageBoxResult.Yes)
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
                        var existingLibrary = _libraries.FirstOrDefault(l => l.Name == name);
                        if (existingLibrary != null)
                        {
                            LibrariesListBox.SelectedItem = existingLibrary;
                        }
                        System.Windows.MessageBox.Show("库名称已存在，已选中该库", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    else
                    {
                        System.Windows.MessageBox.Show("创建库失败", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
                catch (Exception ex)
                {
                    System.Windows.MessageBox.Show($"创建库失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                }
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
                        System.Windows.MessageBox.Show("库名称不能为空", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
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
                        System.Windows.MessageBox.Show($"重命名失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
            else
            {
                System.Windows.MessageBox.Show("请先选择一个库", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void DeleteLibrary_Click(object sender, RoutedEventArgs e)
        {
            if (LibrariesListBox.SelectedItem is Library selectedLibrary)
            {
                if (!ConfirmDialog.Show(
                    $"确定要删除库 \"{selectedLibrary.Name}\" 吗？\n这将删除库及其所有位置，但不会删除实际文件。",
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
                }
                catch (Exception ex)
                {
                    System.Windows.MessageBox.Show($"删除库失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            else
            {
                System.Windows.MessageBox.Show("请先选择一个库", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
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
                            System.Windows.MessageBox.Show("该路径已存在于库中", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                            return;
                        }

                        try
                        {
                            DatabaseManager.AddLibraryPath(selectedLibrary.Id, path);
                            LoadLibraryPaths(selectedLibrary.Id);
                        }
                        catch (Exception ex)
                        {
                            System.Windows.MessageBox.Show($"添加位置失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                        }
                    }
                }
            }
            else
            {
                System.Windows.MessageBox.Show("请先选择一个库", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void RemovePath_Click(object sender, RoutedEventArgs e)
        {
            if (PathsListBox.SelectedItem is LibraryPath selectedPath)
            {
                var result = System.Windows.MessageBox.Show(
                    $"确定要从库中移除位置 \"{selectedPath.Path}\" 吗？",
                    "确认删除",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    try
                    {
                        DatabaseManager.RemoveLibraryPath(selectedPath.LibraryId, selectedPath.Path);
                        LoadLibraryPaths(selectedPath.LibraryId);
                    }
                    catch (Exception ex)
                    {
                        System.Windows.MessageBox.Show($"移除位置失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
            else
            {
                System.Windows.MessageBox.Show("请先选择一个位置", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void PathsListBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            // 可以在这里添加位置选择改变的处理
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }
    }
}
