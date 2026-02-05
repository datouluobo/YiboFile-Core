using System.Collections;
using System.Collections.Generic;
using YiboFile.Controls;
using YiboFile.Models;
using YiboFile.Services.Search;

namespace YiboFile.Services.Bridges
{
    /// <summary>
    /// Thin wrapper for FileBrowserControl to decouple navigation logic from UI access.
    /// </summary>
    public class FileBrowserBridge
    {
        private readonly FileBrowserControl _control;

        public FileBrowserBridge(FileBrowserControl control)
        {
            _control = control;
        }

        /// <summary>
        /// Set address text and editability.
        /// </summary>
        public void SetAddress(string text, bool isReadOnly)
        {
            if (_control == null) return;
            _control.AddressText = text ?? string.Empty;
            _control.IsAddressReadOnly = isReadOnly;
        }

        /// <summary>
        /// Show path breadcrumb.
        /// </summary>
        public void SetPathBreadcrumb(string path)
        {
            _control?.UpdateBreadcrumb(path ?? string.Empty);
        }

        /// <summary>
        /// Show library breadcrumb.
        /// </summary>
        public void SetLibraryBreadcrumb(string libraryName)
        {
            _control?.SetLibraryBreadcrumb(libraryName ?? string.Empty);
        }

        /// <summary>
        /// Show tag breadcrumb.
        /// </summary>
        public void SetTagBreadcrumb(string tagName)
        {
            _control?.SetTagBreadcrumb(tagName ?? string.Empty);
        }

        /// <summary>
        /// Show search breadcrumb.
        /// </summary>
        public void SetSearchBreadcrumb(string keyword)
        {
            _control?.SetSearchBreadcrumb(keyword ?? string.Empty);
        }

        /// <summary>
        /// Toggle tabs visibility.
        /// </summary>
        public void SetTabsVisible(bool visible)
        {
            if (_control == null) return;
            _control.TabsVisible = visible;
        }

        /// <summary>
        /// Toggle nav up button.
        /// </summary>
        public void SetNavUpEnabled(bool enabled)
        {
            if (_control == null) return;
            _control.NavUpEnabled = enabled;
        }

        /// <summary>
        /// Set file list items.
        /// </summary>
        public void SetFilesSource(IEnumerable itemsSource)
        {
            if (_control == null) return;

            // MVVM Adaptor: Update ViewModel
            if (_control.DataContext is ViewModels.PaneViewModel vm && vm.FileList != null)
            {
                var collection = itemsSource as System.Collections.ObjectModel.ObservableCollection<FileSystemItem>;
                if (collection == null && itemsSource is IEnumerable<FileSystemItem> list)
                {
                    collection = new System.Collections.ObjectModel.ObservableCollection<FileSystemItem>(list);
                }

                if (collection != null)
                {
                    vm.FileList.Files = collection;
                }
            }
        }

        /// <summary>
        /// Set grouped search results.
        /// </summary>
        public void SetGroupedSearchResults(Dictionary<SearchResultType, List<FileSystemItem>> groups)
        {
            if (_control == null) return;
            _control.SetGroupedSearchResults(groups);
        }

        /// <summary>
        /// Control load-more indicator.
        /// </summary>
        public void SetLoadMoreVisible(bool visible)
        {
            if (_control == null) return;
            _control.LoadMoreVisible = visible;
        }

        /// <summary>
        /// Show empty state with message.
        /// </summary>
        public void ShowEmptyState(string message)
        {
            _control?.ShowEmptyState(message);
        }

        /// <summary>
        /// Hide empty state.
        /// </summary>
        public void HideEmptyState()
        {
            _control?.HideEmptyState();
        }
    }
}




















