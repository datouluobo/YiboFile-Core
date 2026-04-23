using System;
using YiboFile.Services.Core.Error;

namespace YiboFile.ViewModels.Messaging.Messages
{
    /// <summary>
    /// 错误发生消息
    /// </summary>
    public class ErrorOccurredMessage
    {
        public string Message { get; }
        public Exception Exception { get; }
        public ErrorSeverity Severity { get; }

        public ErrorOccurredMessage(string message, Exception exception, ErrorSeverity severity)
        {
            Message = message;
            Exception = exception;
            Severity = severity;
        }
    }
}
