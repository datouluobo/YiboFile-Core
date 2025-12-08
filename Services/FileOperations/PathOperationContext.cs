using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using OoiMRR.Controls;
using OoiMRR.Services;

namespace OoiMRR.Services.FileOperations
{
    /// <summary>
    /// 路径模式操作上下文
    /// </summary>
    public class PathOperationContext : IFileOperationContext
    {
        private readonly string _currentPath;
        private readonly FileBrowserControl _fileBrowser;
        private readonly Window _ownerWindow;
        private readonly Action _refreshCallback;

        public NavigationStateManager.NavigationMode Mode => NavigationStateManager.NavigationMode.Path;

        public PathOperationContext(string currentPath, FileBrowserControl fileBrowser, Window ownerWindow, Action refreshCallback)
        {
            _currentPath = currentPath;
            _fileBrowser = fileBrowser;
            _ownerWindow = ownerWindow;
            _refreshCallback = refreshCallback;
        }

        public string GetTargetPath()
        {
            if (string.IsNullOrEmpty(_currentPath) || !Directory.Exists(_currentPath))
            {
                return null;
            }
            return _currentPath;
        }

        public bool CanPerformOperation(string operation)
        {
            // 路径模式下，所有操作都可以执行（只要有有效的路径）
            return !string.IsNullOrEmpty(_currentPath) && Directory.Exists(_currentPath);
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






