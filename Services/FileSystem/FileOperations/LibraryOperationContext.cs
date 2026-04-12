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

            // 确保路径是绝对路径，防止库路径是相对路径导致的文件未找到错误
            try
            {
                firstPath = Path.GetFullPath(firstPath);
            }
            catch (Exception)
            {
            }

            if (!Directory.Exists(firstPath))
            {
                return null;
            }

            // 如果有多个位置，询问用户（在需要时）
            if (_currentLibrary.Paths.Count > 1)
            {
                var dialogService = App.ServiceProvider?.GetService(typeof(IDialogService)) as IDialogService;
                var paths = string.Join("\n", _currentLibrary.Paths.Select((p, i) => $"{i + 1}. {p}"));
                bool confirm = dialogService?.Confirm(
                    $"当前库有多个位置，将在第一个位置执行操作：\n\n{firstPath}\n\n是否继续？\n\n所有位置：\n{paths}",
                    "选择位置") ?? false;

                if (!confirm)
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
                    var dialogService = App.ServiceProvider?.GetService(typeof(IDialogService)) as IDialogService;
                    dialogService?.ShowInfo("当前库没有添加任何位置，请先在管理库中添加位置", "提示");
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
























