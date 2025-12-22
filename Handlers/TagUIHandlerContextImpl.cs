using System;
using System.Collections.Generic;
using OoiMRR.Controls;
using OoiMRR.Services.Tag;
using OoiMRR.Services.FileList;

namespace OoiMRR.Handlers
{
    /// <summary>
    /// TagUIHandler 上下文实现类
    /// </summary>
    internal class TagUIHandlerContextImpl : ITagUIHandlerContext
    {
        private readonly MainWindow _mainWindow;

        public TagUIHandlerContextImpl(MainWindow mainWindow)
        {
            _mainWindow = mainWindow ?? throw new ArgumentNullException(nameof(mainWindow));
        }

        public FileBrowserControl FileBrowser => _mainWindow.FileBrowser;
        public System.Windows.Threading.Dispatcher Dispatcher => _mainWindow.Dispatcher;
        public System.Windows.Window OwnerWindow => _mainWindow;
        public Func<Tag> GetCurrentTagFilter => () => _mainWindow._currentTagFilter;
        public Action<Tag> SetCurrentTagFilter => tag => _mainWindow._currentTagFilter = tag;
        public Func<List<FileSystemItem>> GetCurrentFiles => () => _mainWindow._currentFiles;
        public Action<List<FileSystemItem>> SetCurrentFiles => files => _mainWindow._currentFiles = files;
        public Func<Library> GetCurrentLibrary => () => _mainWindow._currentLibrary;
        public Func<string> GetCurrentPath => () => _mainWindow._currentPath;
        public Func<bool> GetIsUpdatingTagSelection => () => _mainWindow._isUpdatingTagSelection;
        public Func<List<int>, List<string>> OrderTagNames => tagIds => _mainWindow.OrderTagNames(tagIds);
        public Action<Tag, List<FileSystemItem>> UpdateTagFilesUI => (tag, files) => _mainWindow.UpdateTagFilesUI(tag, files);
        public Action LoadFiles => () => _mainWindow.LoadFiles();
        public Action<Library> LoadLibraryFiles => library => _mainWindow.LoadLibraryFiles(library);
        public Action LoadCurrentDirectory => () => _mainWindow.LoadCurrentDirectory();
        public Action LoadTags => () => _mainWindow.LoadTags();
        public Func<System.Windows.Controls.Grid> GetNavTagContent => () => _mainWindow.NavTagContent;
        public Func<FileListService> GetFileListService => () => _mainWindow._fileListService;
    }
}
