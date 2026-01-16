using System;
using System.Windows;
using YiboFile.Handlers;

namespace YiboFile
{
    public partial class MainWindow
    {
        // 菜单事件桥接方法 - 委托给 MenuEventHandler 处理
        
        private void Exit_Click(object sender, RoutedEventArgs e) => _menuEventHandler?.Exit_Click(sender, e);
        private void SelectAll_Click(object sender, RoutedEventArgs e) => _menuEventHandler?.SelectAll_Click(sender, e);
        private void ViewLargeIcons_Click(object sender, RoutedEventArgs e) => _menuEventHandler?.ViewLargeIcons_Click(sender, e);
        private void ViewSmallIcons_Click(object sender, RoutedEventArgs e) => _menuEventHandler?.ViewSmallIcons_Click(sender, e);
        private void ViewList_Click(object sender, RoutedEventArgs e) => _menuEventHandler?.ViewList_Click(sender, e);
        private void ViewDetails_Click(object sender, RoutedEventArgs e) => _menuEventHandler?.ViewDetails_Click(sender, e);
        private void Settings_Click(object sender, RoutedEventArgs e) => _menuEventHandler?.Settings_Click(sender, e);
        private void ImportConfig_Click(object sender, RoutedEventArgs e) => _menuEventHandler?.ImportConfig_Click(sender, e);
        private void ExportConfig_Click(object sender, RoutedEventArgs e) => _menuEventHandler?.ExportConfig_Click(sender, e);
        internal void EditNotes_Click(object sender, RoutedEventArgs e) => _menuEventHandler?.EditNotes_Click(sender, e);
        private void About_Click(object sender, RoutedEventArgs e) => _menuEventHandler?.About_Click(sender, e);
    }
}

