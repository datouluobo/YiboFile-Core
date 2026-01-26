using System;
using System.IO;
using System.Windows;

namespace YiboFile.Services.FileOperations
{
    /// <summary>
    /// 统一的文件操作上下文
    /// 提供操作所需的目标路径、UI 回调等
    /// </summary>
    public class FileOperationContext
    {
        /// <summary>
        /// 目标路径（粘贴/移动的目标目录）
        /// </summary>
        public string TargetPath { get; set; }

        /// <summary>
        /// 当前库（库模式下使用）
        /// </summary>
        public Library CurrentLibrary { get; set; }

        /// <summary>
        /// 所属窗口（用于对话框显示）
        /// </summary>
        public Window OwnerWindow { get; set; }

        /// <summary>
        /// 操作完成后的刷新回调
        /// </summary>
        public Action RefreshCallback { get; set; }

        /// <summary>
        /// 获取有效的目标路径
        /// </summary>
        public string GetEffectiveTargetPath()
        {
            if (!string.IsNullOrEmpty(TargetPath) && Directory.Exists(TargetPath))
            {
                return TargetPath;
            }

            if (CurrentLibrary?.Paths?.Count > 0)
            {
                foreach (var path in CurrentLibrary.Paths)
                {
                    if (Directory.Exists(path))
                        return path;
                }
            }

            return null;
        }

        /// <summary>
        /// 是否可执行操作
        /// </summary>
        public bool CanPerformOperation()
        {
            return !string.IsNullOrEmpty(GetEffectiveTargetPath());
        }

        /// <summary>
        /// 执行刷新
        /// </summary>
        public void Refresh()
        {
            RefreshCallback?.Invoke();
        }
    }
}

