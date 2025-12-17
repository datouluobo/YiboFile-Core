using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Threading;
using System.Diagnostics;
using System.IO;
using OoiMRR.Handlers;
using OoiMRR.Services;
using OoiMRR.Services.FileNotes;
using OoiMRR.Services.FileOperations;
using OoiMRR.Services.Navigation;
using OoiMRR.Services.Search;
using OoiMRR.Services.Tabs;
using OoiMRR.Services.Settings;
using OoiMRR.Services.TagTrain;
using OoiMRR.Services.ColumnManagement;
using OoiMRR.Services.Config;
using TagTrain.UI;

namespace OoiMRR
{
    public partial class MainWindow
    {
        private void InitializeHandlers()
        {
            // 初始化 FileBrowserEventHandler
            _fileBrowserEventHandler = new FileBrowserEventHandler(
                FileBrowser,
                _navigationCoordinator,
                _tabService,
                _searchService, // searchService
                _searchCacheService,
                NavigateToPath,
                (query) => PerformSearch(query, _searchOptions?.SearchNames ?? true, _searchOptions?.SearchNotes ?? true),
                SwitchNavigationMode,
                LoadCurrentDirectory,
                () => // ClearFilter
                {
                     _currentTagFilter = null;
                     FileBrowser.IsAddressReadOnly = false;
                     FileBrowser.SetTagBreadcrumb(null);
                     LoadCurrentDirectory();
                     // Also hide empty state
                     HideEmptyStateMessage();
                }, 
                HideEmptyStateMessage,
                GridViewColumnHeaderClickedHandler, // Action<GridViewColumnHeader>
                FileBrowser_FilesSizeChanged, // Action<SizeChangedEventArgs>
                FileBrowser_GridSplitterDragDelta, // Action<DragDeltaEventArgs>
                () => _currentPath,
                () => _configService?.Config,
                () => _currentTagFilter,
                (tag) => _currentTagFilter = tag,
                () => _currentFiles,
                (files) => _currentFiles = files,
                () => _searchOptions,
                FilesListView_SelectionChanged,
                FilesListView_MouseDoubleClick,
                FilesListView_PreviewMouseDoubleClick,
                FilesListView_PreviewKeyDown,
                FilesListView_PreviewMouseLeftButtonDown,
                FilesListView_MouseLeftButtonUp,
                FilesListView_PreviewMouseDown,
                FilesListView_PreviewMouseDoubleClickForBlank,
                () => ColLeft // Func<ColumnDefinition>
            );
            _fileBrowserEventHandler.Initialize();

            // 初始化 MenuEventHandler
            _menuEventHandler = new MenuEventHandler(
                FileBrowser,
                _libraryService,
                RefreshFileList,
                LoadCurrentDirectory,
                () => // ClearFilter
                {
                     _currentTagFilter = null;
                     FileBrowser.IsAddressReadOnly = false;
                     FileBrowser.SetTagBreadcrumb(null);
                     LoadCurrentDirectory();
                     HideEmptyStateMessage();
                },
                () => Close(),
                () => _settingsOverlayController?.Show(),
                () => MessageBox.Show("关于窗口功能待修复", "关于", MessageBoxButton.OK, MessageBoxImage.Information), // new AboutWindow { Owner = this }.ShowDialog(),
                EditNotes_Click_Logic, // Action editNotes
                BatchAddTags_Click_Logic, // Action batchAddTags
                () => _tagTrainEventHandler?.TagTrainTrainingStatus_Click(null, null), // showTagStatistics
                ImportLibrary_Click_Logic, // importLibrary
                ExportLibrary_Click_Logic, // exportLibrary
                () => {}, // addFileToLibrary - Implement logic if needed
                () => // Copy
                {
                     if (FileBrowser?.FilesSelectedItems != null)
                     {
                         var paths = FileBrowser.FilesSelectedItems.Cast<FileSystemItem>().Select(i => i.Path).ToList();
                         FileClipboardManager.SetCopyPaths(paths);
                     }
                },
                () => // Cut
                {
                     if (FileBrowser?.FilesSelectedItems != null)
                     {
                         var paths = FileBrowser.FilesSelectedItems.Cast<FileSystemItem>().Select(i => i.Path).ToList();
                         FileClipboardManager.SetCutPaths(paths);
                     }
                },
                () => // Paste
                {
                    IFileOperationContext context = null;
                    if (_currentLibrary != null)
                    {
                        context = new LibraryOperationContext(_currentLibrary, FileBrowser, this, RefreshFileList);
                    }
                    else
                    {
                        context = new PathOperationContext(_currentPath, FileBrowser, this, RefreshFileList);
                    }
                    var op = new PasteOperation(context);
                    op.Execute(FileClipboardManager.GetCopiedPaths(), FileClipboardManager.IsCutOperation);
                    if (FileClipboardManager.IsCutOperation) FileClipboardManager.ClearCutOperation();
                },
                () => // Delete
                {
                    IFileOperationContext context = null;
                    if (_currentLibrary != null)
                    {
                        context = new LibraryOperationContext(_currentLibrary, FileBrowser, this, RefreshFileList);
                    }
                    else
                    {
                        context = new PathOperationContext(_currentPath, FileBrowser, this, RefreshFileList);
                    }
                    var op = new DeleteOperation(context);
                    // Usually DeleteOperation gets selection from context/FileBrowser internally if null passed?
                    // But here we pass null. Let's assume operation handles it.
                    // Wait, looking at DeleteOperation previously viewed: Execute(List<FileSystemItem> items).
                    // We need to pass items!
                    var items = FileBrowser?.FilesSelectedItems?.Cast<FileSystemItem>().ToList();
                    op.Execute(items); 
                },
                () => // Rename
                {
                    IFileOperationContext context = null;
                    if (_currentLibrary != null)
                    {
                        context = new LibraryOperationContext(_currentLibrary, FileBrowser, this, RefreshFileList);
                    }
                    else
                    {
                        context = new PathOperationContext(_currentPath, FileBrowser, this, RefreshFileList);
                    }
                     var op = new RenameOperation(context, this);
                     // Rename usually needs specific item.
                     // The logic should probably get selected item.
                     var item = FileBrowser?.FilesSelectedItem as FileSystemItem;
                     if (item != null) op.Execute(item); 
                },
                () => {}, // ShowProperties - Implement if needed
                NavigateToPath,
                SwitchNavigationMode,
                () => _currentPath,
                () => _currentLibrary,
                () => _currentFiles,
                (files) => _currentFiles = files,
                () => this,
                (lib) => _tabService.OpenLibraryTab(lib),
                (lib) => {}, // HighlightMatchingLibrary
                () => _libraryService.LoadLibraries(), // LoadLibraries
                () => LibrariesListBox, // Func<ListBox>
                () => LibraryContextMenu, // Func<ContextMenu>
                (ext) => // CreateNewFileWithExtension
                {
                     IFileOperationContext context = new PathOperationContext(_currentPath, FileBrowser, this, RefreshFileList);
                     var op = new NewFileOperation(context, this);
                     op.Execute(ext);
                },
                (name) => // CreateNewFolder
                {
                     // This is handled inside MenuEventHandler's NewFolder_Click usually
                },
                 () => _configService?.Config,
                 (cfg) => _configService?.ApplyConfig(cfg),
                 () => _configService?.SaveCurrentConfig()
            );

            // 初始化 KeyboardEventHandler
            _keyboardEventHandler = new OoiMRR.Handlers.KeyboardEventHandler(
                FileBrowser,
                _tabService,
                (tab) => _tabService.RemoveTab(tab),
                (path) => _tabService.CreatePathTab(path),
                (tab) => _tabService.SwitchToTab(tab),
                () => _menuEventHandler.NewFolder_Click(null, null), // NewFolderClick
                RefreshFileList,
                () => _menuEventHandler.Copy_Click(null, null),
                () => _menuEventHandler.Paste_Click(null, null),
                () => _menuEventHandler.Cut_Click(null, null),
                () => _menuEventHandler.Delete_Click(null, null),
                () => _menuEventHandler.Rename_Click(null, null),
                NavigateToPath,
                SwitchNavigationMode,
                () => _currentLibrary != null,
                Back_Click_Logic // navigateBack
            );

            // 初始化 MouseEventHandler
            _mouseEventHandler = new OoiMRR.Handlers.MouseEventHandler(
                () => WindowMaximize_Click(null, null),
                () => DragMove(),
                () => FavoritesListBox,
                () => DrivesListBox,
                () => QuickAccessListBox,
                _navigationCoordinator,
                (fav) => _navigationCoordinator.HandleFavoriteNavigation(fav, NavigationCoordinator.ClickType.LeftClick),
                (drivePath) => _navigationCoordinator.HandlePathNavigation(drivePath, NavigationCoordinator.NavigationSource.Drive, NavigationCoordinator.ClickType.LeftClick), // drivePath is string
                (path) => _navigationCoordinator.HandlePathNavigation(path, NavigationCoordinator.NavigationSource.QuickAccess, NavigationCoordinator.ClickType.LeftClick) // Map to HandlePathNavigation
            );

            // 初始化 TagTrainEventHandler
            _tagTrainEventHandler = new TagTrainEventHandler(
                 TagBrowsePanel,
                 TagEditPanel,
                 FileBrowser,
                 this,
                 Dispatcher,
                 () => TagBrowsePanel.Mode == TagPanel.DisplayMode.Browse ? TagTrainEventHandler.TagClickMode.Browse : TagTrainEventHandler.TagClickMode.Edit,
                 (mode) => {
                     if (TagBrowsePanel != null) TagBrowsePanel.Mode = mode == TagTrainEventHandler.TagClickMode.Browse ? TagPanel.DisplayMode.Browse : TagPanel.DisplayMode.Edit;
                 },
                 () => _tagTrainIsTraining, // Use instance field
                 (val) => {}, // setTagTrainIsTraining
                 () => null, // cancellationTokenSource getter
                 (token) => {}, // cancellationTokenSource setter
                 () => _currentTagFilter,
                 () => TagBrowsePanel?.LoadExistingTags(),
                 () => {}, // updateTagTrainModelStatus
                 () => { _ = LoadFilesAsync(); },
                 (tag) => {
                     _currentTagFilter = tag;
                     _isUpdatingTagSelection = true;
                     RefreshFileList();
                     _isUpdatingTagSelection = false;
                 },
                 (paths) => {
                     if (FileBrowser?.FilesList != null)
                     {
                         // Implementation of restoring selection
                     }
                 }
            );
        }

        // Helper methods for event handlers
        private void GridViewColumnHeaderClickedHandler(GridViewColumnHeader header)
        {
             _columnService?.HandleColumnHeaderClick(header, _currentFiles, (files) => 
             {
                 // Update files source
                 _currentFiles = files; // Should update field too?
                 if (FileBrowser != null)
                 {
                     FileBrowser.FilesItemsSource = files;
                 }
             },
             FileBrowser?.FilesList?.View as GridView); 
        }

        private void FileBrowser_FilesSizeChanged(SizeChangedEventArgs e)
        {
             _columnService?.AdjustListViewColumnWidths(FileBrowser);
        }

        private void FileBrowser_GridSplitterDragDelta(DragDeltaEventArgs e)
        {
             if (ColLeft != null)
             {
                 double newWidth = ColLeft.Width.Value + e.HorizontalChange;
                 if (newWidth < 150) newWidth = 150; // Minimum width
                 ColLeft.Width = new GridLength(newWidth);
             }
        }

        private void FilesListView_SelectionChanged(SelectionChangedEventArgs e)
        {
            // 1. Update Preview
            if (FileBrowser?.FilesSelectedItems != null && FileBrowser.FilesSelectedItems.Count == 1)
            {
                if (FileBrowser.FilesSelectedItem is FileSystemItem item)
                {
                    _fileInfoService?.ShowFileInfo(item);
                    _fileNotesUIHandler?.LoadFileNotes(item);
                    LoadFilePreview(item);
                }
            }
            else
            {
                ClearPreviewAndInfo();
            }

            // 2. Update Tag Selection
            if (!_isUpdatingTagSelection && App.IsTagTrainAvailable)
            {
                _tagUIHandler?.UpdateTagSelectionState(FileBrowser?.FilesSelectedItems?.Cast<FileSystemItem>().ToList());
            }
        }

        private void FilesListView_MouseDoubleClick(MouseButtonEventArgs e)
        {
            // Logic for opening item
            if (FileBrowser?.FilesSelectedItem is FileSystemItem item)
            {
                if (item.IsDirectory)
                {
                    NavigateToPath(item.Path);
                }
                else
                {
                    // Open file
                    try
                    {
                        Process.Start(new ProcessStartInfo
                        {
                            FileName = item.Path,
                            UseShellExecute = true
                        });
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"无法打开文件: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
        }

         private void FilesListView_PreviewMouseDoubleClick(MouseButtonEventArgs e) 
         {
             // Prevent default behavior if needed, or handle specific cases
         }

         private void FilesListView_PreviewKeyDown(KeyEventArgs e) 
         {
             // Handle keyboard navigation if special handling needed
         }

         private void FilesListView_PreviewMouseLeftButtonDown(MouseButtonEventArgs e) 
         {
             // Drag start logic potentially
             if (e.OriginalSource is DependencyObject source)
             {
                 // Drag logic usually handled by DragDropManager or similar
             }
         }

         private void FilesListView_MouseLeftButtonUp(MouseButtonEventArgs e) 
         {
             // Drag end logic
         }

         private void FilesListView_PreviewMouseDown(MouseButtonEventArgs e) 
         {
             // Focus handling
         }

         private void FilesListView_PreviewMouseDoubleClickForBlank(MouseButtonEventArgs e) 
         {
             // Double click on blank area -> Go up?
             if (e.ChangedButton == MouseButton.Left)
             {
                 // Go up logic
                 var parent = Directory.GetParent(_currentPath);
                 if (parent != null)
                 {
                     NavigateToPath(parent.FullName);
                 }
             }
         }

         private void EditNotes_Click_Logic() 
         {
             _fileNotesUIHandler?.ToggleNotesPanel();
         }

         private void BatchAddTags_Click_Logic() 
         {
             if (!App.IsTagTrainAvailable) return;
             _tagUIHandler?.ShowBatchTaggingDialog(_currentFiles); // This will open dialog for selection
         }

         private void ImportLibrary_Click_Logic() 
         {
            var dialog = new System.Windows.Forms.FolderBrowserDialog();
            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                 // Import library
                 _libraryService.ImportLibrary(dialog.SelectedPath);
                 _libraryService.LoadLibraries(); // Refresh
            }
         }
         
         private void ExportLibrary_Click_Logic() 
         {
             if (_currentLibrary == null) return;
             // Logic to export
         }
         
         private void Back_Click_Logic() 
         {
             if (_navigationService != null && _navigationService.CanNavigateBack) 
             {
                 _navigationService.NavigateBack();
             }
         }
         


         // Required for MenuEventHandler
         private void CreateNewFolderLogic(string name) 
         { 
             // Logic handled by event handler itself usually via NewFolderOperation
         }

    }
}
