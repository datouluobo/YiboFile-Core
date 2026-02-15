using System.Collections.Generic;
using YiboFile.Models;

namespace YiboFile.ViewModels.Messaging.Messages
{
    /// <summary>
    /// 创建文件夹请求
    /// </summary>
    public class CreateFolderRequestMessage
    {
        public string ParentPath { get; }
        public string FolderName { get; }

        public CreateFolderRequestMessage(string parentPath, string folderName = null)
        {
            ParentPath = parentPath;
            FolderName = folderName;
        }
    }

    /// <summary>
    /// 删除项目请求
    /// </summary>
    public class DeleteItemsRequestMessage
    {
        public List<FileSystemItem> Items { get; }
        public bool Permanent { get; }

        public DeleteItemsRequestMessage(List<FileSystemItem> items, bool permanent = false)
        {
            Items = items;
            Permanent = permanent;
        }
    }

    /// <summary>
    /// 复制项目请求
    /// </summary>
    public class CopyItemsRequestMessage
    {
        public List<FileSystemItem> Items { get; }

        public CopyItemsRequestMessage(List<FileSystemItem> items)
        {
            Items = items;
        }
    }

    /// <summary>
    /// 剪切项目请求
    /// </summary>
    public class CutItemsRequestMessage
    {
        public List<FileSystemItem> Items { get; }

        public CutItemsRequestMessage(List<FileSystemItem> items)
        {
            Items = items;
        }
    }

    /// <summary>
    /// 粘贴项目请求
    /// </summary>
    public class PasteItemsRequestMessage
    {
        public string TargetPath { get; }

        public PasteItemsRequestMessage(string targetPath)
        {
            TargetPath = targetPath;
        }
    }

    /// <summary>
    /// 重命名项目请求
    /// </summary>
    public class RenameItemRequestMessage
    {
        public FileSystemItem Item { get; }
        public string NewName { get; }

        public RenameItemRequestMessage(FileSystemItem item, string newName = null)
        {
            Item = item;
            NewName = newName;
        }
    }

    /// <summary>
    /// 撤销请求
    /// </summary>
    public class UndoRequestMessage { }

    /// <summary>
    /// 重做请求
    /// </summary>
    public class RedoRequestMessage { }

    /// <summary>
    /// 显示属性请求
    /// </summary>
    public class ShowPropertiesRequestMessage
    {
        public FileSystemItem Item { get; }
        public string CurrentPath { get; }

        public ShowPropertiesRequestMessage(FileSystemItem item, string currentPath)
        {
            Item = item;
            CurrentPath = currentPath;
        }
    }
    /// <summary>
    /// 创建文件请求
    /// </summary>
    public class CreateFileRequestMessage
    {
        public string ParentPath { get; }
        public string FileName { get; }
        public string Extension { get; }

        public CreateFileRequestMessage(string parentPath, string fileName = null, string extension = ".txt")
        {
            ParentPath = parentPath;
            FileName = fileName;
            Extension = extension;
        }
    }

    /// <summary>
    /// 通用文件操作请求（用于后台任务）
    /// </summary>
    public class FileOperationRequestMessage
    {
        public string OperationId { get; } = System.Guid.NewGuid().ToString();
        public string OperationType { get; } // "Copy", "Move", "Delete", "Recycle", "Rename"
        public List<string> SourcePaths { get; }
        public string DestinationPath { get; }

        public FileOperationRequestMessage(string operationType, List<string> sourcePaths, string destinationPath = null)
        {
            OperationType = operationType;
            SourcePaths = sourcePaths;
            DestinationPath = destinationPath;
        }
    }

    /// <summary>
    /// 文件操作完成消息
    /// </summary>
    public class FileOperationCompleteMessage
    {
        public string OperationId { get; }
        public bool Success { get; }
        public string ErrorMessage { get; }
        public List<string> ResultPaths { get; }

        public FileOperationCompleteMessage(string operationId, bool success, string errorMessage = null, List<string> resultPaths = null)
        {
            OperationId = operationId;
            Success = success;
            ErrorMessage = errorMessage;
            ResultPaths = resultPaths;
        }
    }
}
