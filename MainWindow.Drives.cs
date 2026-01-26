using System;
using System.Windows;
using System.Windows.Controls;

namespace YiboFile
{
    /// <summary>
    /// MainWindow 驱动器功能
    /// </summary>
    public partial class MainWindow
    {
        #region 驱动器功能

        internal void LoadDrives()
        {
            if (DrivesTreeView == null) return;
            // Use the new TreeView loading method
            _quickAccessService.LoadDriveTree(DrivesTreeView, _fileListService.FormatFileSize);
        }

        private void DrivesListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Note: This matches the event name wired in NavigationPanelControl.xaml.cs
            // We ignore 'e' because it might be null or not contain useful info for TreeView wrapper
            if (sender is TreeView treeView)
            {
                if (treeView.SelectedItem is YiboFile.Services.Navigation.NavigationItem selectedItem)
                {
                    if (!string.IsNullOrEmpty(selectedItem.Path))
                    {
                        NavigateToPath(selectedItem.Path);
                    }
                }
            }
            ClearOtherNavigationSelections("Drives");
        }

        internal void ClearDriveSelection()
        {
            if (DrivesTreeView?.ItemsSource is System.Collections.IEnumerable items)
            {
                foreach (var item in items)
                {
                    if (item is YiboFile.Services.Navigation.NavigationItem navItem)
                    {
                        RecursivelyClearSelection(navItem);
                    }
                }
            }
        }

        private void RecursivelyClearSelection(YiboFile.Services.Navigation.NavigationItem item)
        {
            if (item.IsSelected) item.IsSelected = false;
            foreach (var child in item.Children)
            {
                RecursivelyClearSelection(child);
            }
        }

        private void DrivesTreeViewItem_Click(object sender, RoutedEventArgs e)
        {
            // Handle direct click on TreeViewItem (or content)
            // 'sender' is the TreeViewItem from the EventSetter
            if (sender is TreeViewItem tvi && tvi.DataContext is YiboFile.Services.Navigation.NavigationItem navItem)
            {
                if (!string.IsNullOrEmpty(navItem.Path))
                {
                    NavigateToPath(navItem.Path);
                }

                // Clear selections in other lists
                ClearOtherNavigationSelections("Drives");

                // Do NOT set Handled = true.
                // If we set it, we block the default TreeView behavior (Selection change, Focus, DoubleClick expansion).
                // We want to Navigate AND let the TreeView behave normally.
                // e.Handled = true; 
            }
        }

        // Added handler for PreviewMouseDown if needed, though Navigate logic is now in SelectionChanged
        // The NavigationPanelControl wires PreviewMouseDown to DrivesListBoxPreviewMouseDown
        // We need to ensure MainWindow subscribes to it correctly in InitializeEvents() if it wasn't auto-wired.
        // Actually, MainWindow.xaml.cs InitializeEvents() likely subscribes to NavigationPanelControl events.
        // Let's check MainWindow.Events.cs or wherever InitializeEvents is.
        // Assuming the existing subscription "NavigationPanelControl.DrivesListBoxPreviewMouseDown" works
        // because we kept the event name in NavigationPanelControl.cs same.

        private void DrivesListBox_PreviewMouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            // Middle click logic is handled inside QuickAccessService or here?
            // ListBox logic was in QuickAccessService. 
            // For TreeView, QuickAccessService doesn't have the event handler wired in LoadDriveTree yet.
            // We should check if we need to port that logic here.

            if (e.ChangedButton == System.Windows.Input.MouseButton.Middle)
            {
                if (e.OriginalSource is DependencyObject obj)
                {
                    var item = FindAncestor<TreeViewItem>(obj);
                    if (item != null && item.DataContext is YiboFile.Services.Navigation.NavigationItem navItem)
                    {
                        CreateTab(navItem.Path);
                        e.Handled = true;
                    }
                }
            }
        }

        #endregion
    }
}

