using System;
using YiboFile.Models;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using YiboFile.Controls;
using YiboFile.Services;

namespace YiboFile.Services.FileOperations
{
    /// <summary>
    /// 库模式操作上下文
    /// </summary>
    public class LibraryOperationContext : IFileOperationContext
    {
        private readonly Library _currentLibrary;
        private readonly FileBrowserControl _fileBrowser;
        private readonly Window _ownerWindow;
        private readonly Action _refreshCallback;

        public NavigationStateManager.NavigationMode Mode => NavigationStateManager.NavigationMode.Library;

        public LibraryOperationContext(Library currentLibrary, FileBrowserControl fileBrowser, Window ownerWindow, Action refreshCallback)
        {
            _currentLibrary = currentLibrary;
            _fileBrowser = fileBrowser;
            _ownerWindow = ownerWindow;
            _refreshCallback = refreshCallback;
        }

        public string GetTargetPath()
        {
            if (_currentLibrary == null || _currentLibrary.Paths == null || _currentLibrary.Paths.Count == 0)
            {
                return null;
            }

            // 库模式：返回第一个位置
            var firstPath = _currentLibrary.Paths[0];
            if (!Directory.Exists(firstPath))
            {
                return null;
            }

            // 如果有多个位置，询问用户（在需要时）
            if (_currentLibrary.Paths.Count > 1)
            {
                var paths = string.Join("\n", _currentLibrary.Paths.Select((p, i) => $"{i + 1}. {p}"));
                var result = MessageBox.Show(
                    _ownerWindow,
                    $"当前库有多个位置，将在第一个位置执行操作：\n\n{firstPath}\n\n是否继续？\n\n所有位置：\n{paths}",
                    "选择位置",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result != MessageBoxResult.Yes)
                {
                    return null;
                }
            }

            return firstPath;
        }

        public bool CanPerformOperation(string operation)
        {
            if (_currentLibrary == null || _currentLibrary.Paths == null || _currentLibrary.Paths.Count == 0)
            {
                if (operation == "NewFolder" || operation == "NewFile" || operation == "Paste")
                {
                    MessageBox.Show(_ownerWindow, "当前库没有添加任何位置，请先在管理库中添加位置", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                return false;
            }

            var targetPath = _currentLibrary.Paths[0];
            return Directory.Exists(targetPath);
        }

        public void RefreshAfterOperation()
        {
            _refreshCallback?.Invoke();
        }

        public List<FileSystemItem> GetSelectedItems()
        {
            var items = new List<FileSystemItem>();
            if (_fileBrowser?.FilesSelectedItems != null)
            {
                foreach (FileSystemItem item in _fileBrowser.FilesSelectedItems)
                {
                    items.Add(item);
                }
            }
            return items;
        }

        public MessageBoxResult ShowMessage(string message, string title, MessageBoxButton buttons, MessageBoxImage icon)
        {
            return MessageBox.Show(_ownerWindow, message, title, buttons, icon);
        }

        public bool ShowConfirm(string message, string title)
        {
            return ConfirmDialog.Show(message, title, ConfirmDialog.DialogType.Warning, _ownerWindow);
        }
    }
}



























