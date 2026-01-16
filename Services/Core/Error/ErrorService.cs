using System;

namespace YiboFile.Services.Core.Error
{
    /// <summary>
    /// 错误严重程度
    /// </summary>
    public enum ErrorSeverity
    {
        Info,
        Warning,
        Error,
        Critical
    }

    /// <summary>
    /// 错误信息事件参数
    /// </summary>
    public class ErrorOccurredEventArgs : EventArgs
    {
        public string Message { get; }
        public Exception Exception { get; }
        public ErrorSeverity Severity { get; }

        public ErrorOccurredEventArgs(string message, Exception exception, ErrorSeverity severity)
        {
            Message = message;
            Exception = exception;
            Severity = severity;
        }
    }

    /// <summary>
    /// 统一错误处理服务
    /// </summary>
    public class ErrorService
    {
        /// <summary>
        /// 当错误发生时触发
        /// </summary>
        public event EventHandler<ErrorOccurredEventArgs> ErrorOccurred;

        /// <summary>
        /// 报告错误
        /// </summary>
        /// <param name="message">错误消息</param>
        /// <param name="severity">严重程度</param>
        /// <param name="ex">异常对象（可选）</param>
        public void ReportError(string message, ErrorSeverity severity = ErrorSeverity.Error, Exception ex = null)
        {
            // 1. 记录日志
            LogToDisk(message, severity, ex);

            // 2. 触发事件通知UI
            // 使用 Invoke 防止多线程问题，但事件订阅者需要在UI线程上处理UI更新
            ErrorOccurred?.Invoke(this, new ErrorOccurredEventArgs(message, ex, severity));
        }

        private void LogToDisk(string message, ErrorSeverity severity, Exception ex)
        {
            try
            {
                string logMessage = $"[{severity}] {message}";
                if (ex != null)
                {
                    FileLogger.LogException(logMessage, ex);
                }
                else
                {
                    FileLogger.Log(logMessage);
                }
            }
            catch
            {
                // 防止日志记录本身导致崩溃
            }
        }
    }
}

