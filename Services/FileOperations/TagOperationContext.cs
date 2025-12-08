using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using OoiMRR.Controls;
using OoiMRR.Services;

namespace OoiMRR.Services.FileOperations
{
    /// <summary>
    /// 标签模式操作上下文
    /// 标签模式下，文件操作通常不可用或需要特殊处理
    /// </summary>
    public class TagOperationContext : IFileOperationContext
    {
        private readonly OoiMRR.Tag _currentTagFilter;
        private readonly FileBrowserControl _fileBrowser;
        private readonly Window _ownerWindow;
        private readonly Action _refreshCallback;

        public NavigationStateManager.NavigationMode Mode => NavigationStateManager.NavigationMode.Tag;

        public TagOperationContext(OoiMRR.Tag currentTagFilter, FileBrowserControl fileBrowser, Window ownerWindow, Action refreshCallback)
        {
            _currentTagFilter = currentTagFilter;
            _fileBrowser = fileBrowser;
            _ownerWindow = ownerWindow;
            _refreshCallback = refreshCallback;
        }

        public string GetTargetPath()
        {
            // 标签模式下，通常无法确定单一的目标路径
            // 因为标签可能关联多个文件，分布在不同的路径中
            // 如果需要执行操作，可能需要让用户选择目标路径
            return null;
        }

        public bool CanPerformOperation(string operation)
        {
            // 标签模式下，大部分文件操作不可用
            // 只有查看、打开等操作可用
            switch (operation)
            {
                case "Copy":
                case "Cut":
                case "Delete":
                case "Rename":
                case "NewFolder":
                case "NewFile":
                case "Paste":
                    return false; // 标签模式下不支持这些操作
                default:
                    return false;
            }
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




