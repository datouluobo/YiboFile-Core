using System;
using System.Windows;
using YiboFile.Handlers;
using YiboFile.Services.Config;

namespace YiboFile
{
    public partial class MainWindow
    {
        // 菜单事件桥接方法

        private void Exit_Click(object sender, RoutedEventArgs e) => Application.Current.Shutdown();
        private void SelectAll_Click(object sender, RoutedEventArgs e) => _viewModel?.ActivePane?.SelectAllCommand?.Execute(null);

        private void ViewLargeIcons_Click(object sender, RoutedEventArgs e) => _viewModel?.ActivePane?.SwitchViewModeCommand?.Execute("Thumbnail");
        private void ViewSmallIcons_Click(object sender, RoutedEventArgs e) => _viewModel?.ActivePane?.SwitchViewModeCommand?.Execute("List");
        private void ViewList_Click(object sender, RoutedEventArgs e) => _viewModel?.ActivePane?.SwitchViewModeCommand?.Execute("List");
        private void ViewDetails_Click(object sender, RoutedEventArgs e) => _viewModel?.ActivePane?.SwitchViewModeCommand?.Execute("Details");

        private void Settings_Click(object sender, RoutedEventArgs e) => _settingsOverlayController?.Toggle();
        private void ImportConfig_Click(object sender, RoutedEventArgs e) { /* Phase 2: Configuration Import */ }
        private void ExportConfig_Click(object sender, RoutedEventArgs e) { /* Phase 2: Configuration Export */ }
        internal void EditNotes_Click(object sender, RoutedEventArgs e) { /* Phase 2: Show via Property Panel */ }
        private void About_Click(object sender, RoutedEventArgs e) => OnRailAboutRequested(null, null);
    }
}
