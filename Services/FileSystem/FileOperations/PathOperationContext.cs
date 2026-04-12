using System;
using Microsoft.Extensions.DependencyInjection;
using YiboFile.Models;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using YiboFile.Controls;
using YiboFile.Services;
using YiboFile.Dialogs;
using YiboFile.Services.UI;

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
            var dialogService = App.ServiceProvider?.GetService(typeof(IDialogService)) as IDialogService;
            if (dialogService == null) return MessageBoxResult.None;

            if (buttons == MessageBoxButton.YesNo || buttons == MessageBoxButton.YesNoCancel)
            {
                return dialogService.Confirm(message, title) ? MessageBoxResult.Yes : MessageBoxResult.No;
            }
            if (buttons == MessageBoxButton.OKCancel)
            {
                return dialogService.Confirm(message, title) ? MessageBoxResult.OK : MessageBoxResult.Cancel;
            }

            if (icon == MessageBoxImage.Error)
            {
                dialogService.ShowError(message, title);
            }
            else if (icon == MessageBoxImage.Warning)
            {
                dialogService.ShowWarning(message, title);
            }
            else
            {
                dialogService.ShowInfo(message, title);
            }
            return MessageBoxResult.OK;
        }

        public bool ShowConfirm(string message, string title)
        {
            var dialogService = App.ServiceProvider?.GetService(typeof(IDialogService)) as IDialogService;
            return dialogService?.Confirm(message, title) ?? false;
        }

        public string ShowInput(string prompt, string defaultText, string title, bool selectFileNameOnly = false)
        {
            var dialogService = App.ServiceProvider?.GetService<UI.IDialogService>();
            return dialogService?.ShowInput(prompt, defaultText, title, selectFileNameOnly);
        }
    }
}
























