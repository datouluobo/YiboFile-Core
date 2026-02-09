using System;
using System.Windows.Controls;
using YiboFile.Controls;
using YiboFile.Services.Navigation;
using YiboFile.Models;
using YiboFile.Services.Tabs;

namespace YiboFile.Services.UI.Adapters
{
    public class NavigationModeUIAdapter : INavigationModeUIHelper
    {
        private readonly MainWindow _window;

        public NavigationModeUIAdapter(MainWindow window)
        {
            _window = window ?? throw new ArgumentNullException(nameof(window));
        }

        public System.Windows.Threading.Dispatcher Dispatcher => _window.Dispatcher;

        public Library CurrentLibrary
        {
            get => _window._libraryModule?.SelectedLibrary ?? _window._currentLibrary;
            set
            {
                _window._currentLibrary = value;
                if (_window._libraryModule != null && _window._libraryModule.SelectedLibrary != value)
                {
                    _window._libraryModule.SelectedLibrary = value;
                }
            }
        }

        public string CurrentPath
        {
            get => _window._currentPath;
            set => _window._currentPath = value;
        }

        public FileBrowserControl FileBrowser => _window.FileBrowser;
        public ListBox LibrariesListBox => _window.LibrariesListBox;
        public NavigationPanelControl NavigationPanelControl => _window.NavigationPanelControl;

        // NavigationRail is internal in MainWindow, accessing internal fields of NavigationRail
        public Button NavPathButton => _window.NavigationRail?.NavPathButton;
        public Button NavLibraryButton => _window.NavigationRail?.NavLibraryButton;
        public Button NavTagButton => _window.NavigationRail?.NavTagButton;

        public void SwitchToTab(PathTab tab) => _window.SwitchToTab(tab);
        public void CreateTab(string path) => _window.CreateTab(path);
        public void HighlightMatchingLibrary(Library library) => _window.HighlightMatchingLibrary(library);
        public void EnsureSelectedItemVisible(ListBox listBox, object selectedItem) => _window._uiHelperService?.EnsureSelectedItemVisible(listBox, selectedItem);
        public void LoadLibraryFiles(Library library) => _window.LoadLibraryFiles(library);
        public void InitializeNavigationPanelDragDrop() => _window.InitializeNavigationPanelDragDrop();
        public void ApplyVisibleColumnsForCurrentMode() => _window.ApplyVisibleColumnsForCurrentMode();
        public void EnsureHeaderContextMenuHook() => _window.EnsureHeaderContextMenuHook();
        public void RefreshFileList() => _window.RefreshFileList();
        public void RefreshTagList() => _window.NavigationPanelControl?.TagBrowsePanelControl?.RefreshTags();
    }
}
