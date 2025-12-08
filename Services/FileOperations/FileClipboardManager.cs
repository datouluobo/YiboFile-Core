using System;
using System.Collections.Generic;
using System.Linq;

namespace OoiMRR.Services.FileOperations
{
    /// <summary>
    /// 文件操作剪贴板管理器
    /// 统一管理复制/剪切的文件路径和操作类型
    /// </summary>
    public static class FileClipboardManager
    {
        private static List<string> _copiedPaths = new List<string>();
        private static bool _isCutOperation = false;

        /// <summary>
        /// 是否有剪贴板内容
        /// </summary>
        public static bool HasContent => _copiedPaths.Count > 0;

        /// <summary>
        /// 是否为剪切操作
        /// </summary>
        public static bool IsCutOperation => _isCutOperation;

        /// <summary>
        /// 获取复制的路径列表
        /// </summary>
        public static List<string> GetCopiedPaths()
        {
            return new List<string>(_copiedPaths);
        }

        /// <summary>
        /// 设置复制路径（复制操作）
        /// </summary>
        public static void SetCopyPaths(List<string> paths)
        {
            _copiedPaths = paths != null ? new List<string>(paths) : new List<string>();
            _isCutOperation = false;
        }

        /// <summary>
        /// 设置剪切路径（剪切操作）
        /// </summary>
        public static void SetCutPaths(List<string> paths)
        {
            _copiedPaths = paths != null ? new List<string>(paths) : new List<string>();
            _isCutOperation = true;
        }

        /// <summary>
        /// 清除剪贴板内容
        /// </summary>
        public static void Clear()
        {
            _copiedPaths.Clear();
            _isCutOperation = false;
        }

        /// <summary>
        /// 清除剪切操作标记（粘贴后调用）
        /// </summary>
        public static void ClearCutOperation()
        {
            if (_isCutOperation)
            {
                _copiedPaths.Clear();
                _isCutOperation = false;
            }
        }
    }
}




