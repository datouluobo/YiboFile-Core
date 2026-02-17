using System;
using YiboFile.ViewModels.Messaging;
using YiboFile.ViewModels.Messaging.Messages;
using Microsoft.Extensions.DependencyInjection;

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
    /// 统一错误处理服务
    /// </summary>
    public class ErrorService
    {
        private readonly IMessageBus _messageBus;

        public ErrorService(IMessageBus messageBus = null)
        {
            _messageBus = messageBus ?? App.ServiceProvider?.GetService<IMessageBus>();
        }

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

            // 2. 触发消息通知UI
            _messageBus?.Publish(new ErrorOccurredMessage(message, ex, severity));
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

