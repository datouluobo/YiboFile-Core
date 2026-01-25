using System;
using System.Collections.Generic;
using YiboFile.ViewModels.Messaging;

namespace YiboFile.ViewModels.Modules
{
    /// <summary>
    /// 模块基类
    /// 提供消息订阅管理和生命周期支持
    /// </summary>
    public abstract class ModuleBase : IModule
    {
        private readonly List<IDisposable> _subscriptions = new();
        private bool _disposed;

        protected IMessageBus MessageBus { get; }

        public abstract string Name { get; }
        public bool IsInitialized { get; private set; }

        protected ModuleBase(IMessageBus messageBus)
        {
            MessageBus = messageBus ?? throw new ArgumentNullException(nameof(messageBus));
        }

        /// <summary>
        /// 初始化模块，订阅消息
        /// </summary>
        public void Initialize()
        {
            if (IsInitialized) return;

            OnInitialize();
            IsInitialized = true;
        }

        /// <summary>
        /// 关闭模块，取消所有订阅
        /// </summary>
        public void Shutdown()
        {
            if (!IsInitialized) return;

            OnShutdown();
            UnsubscribeAll();
            IsInitialized = false;
        }

        /// <summary>
        /// 子类重写以执行初始化逻辑
        /// </summary>
        protected virtual void OnInitialize() { }

        /// <summary>
        /// 子类重写以执行关闭逻辑
        /// </summary>
        protected virtual void OnShutdown() { }

        /// <summary>
        /// 订阅消息（自动管理取消订阅）
        /// </summary>
        protected void Subscribe<TMessage>(Action<TMessage> handler) where TMessage : class
        {
            var token = MessageBus.SubscribeWithToken(handler);
            _subscriptions.Add(token);
        }

        /// <summary>
        /// 发布消息
        /// </summary>
        protected void Publish<TMessage>(TMessage message) where TMessage : class
        {
            MessageBus.Publish(message);
        }

        /// <summary>
        /// 取消所有订阅
        /// </summary>
        private void UnsubscribeAll()
        {
            foreach (var subscription in _subscriptions)
            {
                subscription.Dispose();
            }
            _subscriptions.Clear();
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_disposed) return;

            if (disposing)
            {
                Shutdown();
            }

            _disposed = true;
        }
    }
}
