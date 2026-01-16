using System;
using System.Collections.Generic;
using System.Windows;
using YiboFile.Services;

namespace YiboFile.Services.FileOperations
{
    /// <summary>
    /// 文件操作上下文接口
    /// 提供不同导航模式下的操作上下文，统一操作接口
    /// </summary>
    public interface IFileOperationContext
    {
        /// <summary>
        /// 当前导航模式
        /// </summary>
        NavigationStateManager.NavigationMode Mode { get; }

        /// <summary>
        /// 获取操作目标路径
        /// </summary>
        /// <returns>目标路径，如果无法确定则返回null</returns>
        string GetTargetPath();

        /// <summary>
        /// 判断是否可以执行指定操作
        /// </summary>
        /// <param name="operation">操作名称（如 "Paste", "NewFolder" 等）</param>
        /// <returns>是否可以执行</returns>
        bool CanPerformOperation(string operation);

        /// <summary>
        /// 操作后刷新显示
        /// </summary>
        void RefreshAfterOperation();

        /// <summary>
        /// 获取选中的文件项列表
        /// </summary>
        /// <returns>选中的文件项列表</returns>
        List<FileSystemItem> GetSelectedItems();

        /// <summary>
        /// 显示消息框（用于操作中的用户交互）
        /// </summary>
        MessageBoxResult ShowMessage(string message, string title, MessageBoxButton buttons, MessageBoxImage icon);

        /// <summary>
        /// 显示确认对话框
        /// </summary>
        bool ShowConfirm(string message, string title);
    }
}



























