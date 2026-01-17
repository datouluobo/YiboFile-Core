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
            // 确保在UI线程上刷新
            if (Application.Current?.Dispatcher?.CheckAccess() == false)
            {
                Application.Current.Dispatcher.Invoke(_refreshCallback);
            }
            else
            {
                _refreshCallback?.Invoke();
            }
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
            // 确保在UI线程上显示对话框
            if (Application.Current?.Dispatcher?.CheckAccess() == false)
            {
                return (bool)Application.Current.Dispatcher.Invoke(() =>
                    ConfirmDialog.Show(message, title, ConfirmDialog.DialogType.Warning, _ownerWindow));
            }
            return ConfirmDialog.Show(message, title, ConfirmDialog.DialogType.Warning, _ownerWindow);
        }
    }
}



























