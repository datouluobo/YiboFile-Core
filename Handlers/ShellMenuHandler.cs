using System;
using System.Linq;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using YiboFile.Services.Shell;
using YiboFile.ViewModels.Messaging;
using YiboFile.ViewModels.Messaging.Messages;

namespace YiboFile.Handlers
{
    /// <summary>
    /// Shell 动词执行处理器
    /// </summary>
    public class ShellMenuHandler : IDisposable
    {
        private readonly IMessageBus _messageBus;
        private readonly IShellVerbExecutor _verbExecutor;

        public ShellMenuHandler(IMessageBus messageBus)
        {
            _messageBus = messageBus ?? throw new ArgumentNullException(nameof(messageBus));
            _verbExecutor = App.ServiceProvider?.GetService<IShellVerbExecutor>();

            SubscribeMessages();
        }

        private void SubscribeMessages()
        {
            _messageBus.Subscribe<ExecuteShellVerbRequestMessage>(OnExecuteShellVerb);
        }

        private void OnExecuteShellVerb(ExecuteShellVerbRequestMessage msg)
        {
            if (_verbExecutor == null || string.IsNullOrEmpty(msg.Verb) || msg.Paths == null || msg.Paths.Count == 0)
                return;

            try
            {
                _verbExecutor.Execute(msg.Verb, msg.Paths);
            }
            catch (Exception)
            {
            }
        }

        public void Dispose()
        {
            _messageBus.Unsubscribe<ExecuteShellVerbRequestMessage>(OnExecuteShellVerb);
        }
    }
}
