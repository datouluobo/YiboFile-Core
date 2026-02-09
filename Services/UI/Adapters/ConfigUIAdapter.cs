using System;
using System.Windows;
using System.Windows.Controls;
using YiboFile.Controls;
using YiboFile.Services.Config;
using YiboFile;

namespace YiboFile.Services.UI.Adapters
{
    public class ConfigUIAdapter : IConfigUIHelper
    {
        private readonly MainWindow _window;

        public ConfigUIAdapter(MainWindow window)
        {
            _window = window ?? throw new ArgumentNullException(nameof(window));
        }

        public System.Windows.Window Window => _window;
        public Grid RootGrid => _window.RootGrid;
        public ColumnDefinition ColLeft => _window.ColLeft;
        public ColumnDefinition ColCenter => _window.ColCenter;
        public ColumnDefinition ColRight => _window.ColRight;
        public TitleActionBar TitleActionBar => _window.FileBrowser?.ActionBar;
        public RightPanelControl RightPanelControl => _window.RightPanel;
        public FileBrowserControl FileBrowser => _window.FileBrowser;

        public string CurrentPath
        {
            get => _window._currentPath;
            set => _window._currentPath = value;
        }

        public object CurrentLibrary => _window._currentLibrary;

        public void AdjustColumnWidths() => _window._windowLifecycleHandler?.AdjustColumnWidths();
        public void EnsureColumnMinWidths() => _window._windowLifecycleHandler?.EnsureColumnMinWidths();

        public System.Windows.Threading.Dispatcher Dispatcher => _window.Dispatcher;

        public void UpdateWindowStateUI() => _window._windowLifecycleHandler?.UpdateWindowStateUI();
    }
}
