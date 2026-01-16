using System;
using System.Collections.Generic;

namespace YiboFile.Services.FileOperations
{
    /// <summary>
    /// 文件操作结果封装
    /// </summary>
    public class FileOperationResult
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public Exception Exception { get; set; }
        public int ProcessedCount { get; set; }
        public int FailedCount { get; set; }
        public List<string> FailedItems { get; set; } = new List<string>();

        public static FileOperationResult Succeeded(int processedCount = 0) => new FileOperationResult
        {
            Success = true,
            ProcessedCount = processedCount
        };

        public static FileOperationResult Failed(string message, Exception ex = null) => new FileOperationResult
        {
            Success = false,
            Message = message,
            Exception = ex
        };

        public static FileOperationResult Cancelled() => new FileOperationResult
        {
            Success = false,
            Message = "操作已取消"
        };
    }
}

