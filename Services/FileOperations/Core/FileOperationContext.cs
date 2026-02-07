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
            string result = null;

            if (!string.IsNullOrEmpty(TargetPath) && Directory.Exists(TargetPath))
            {
                result = TargetPath;
            }
            else if (CurrentLibrary?.Paths?.Count > 0)
            {
                foreach (var path in CurrentLibrary.Paths)
                {
                    if (Directory.Exists(path))
                    {
                        result = path;
                        break;
                    }
                }
            }

            // 确保返回的路径是绝对路径，防止库路径是相对路径导致的文件未找到错误
            if (!string.IsNullOrEmpty(result))
            {
                try
                {
                    result = Path.GetFullPath(result);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[FileOperationContext] Path.GetFullPath failed for {result}: {ex.Message}");
                }
            }

            return result;
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

