using System;
using System.Collections.Generic;
using System.Windows.Threading;

namespace YiboFile.ViewModels.Messaging
{
    /// <summary>
    /// 消息总线实现
    /// 线程安全，支持 UI 线程调度
    /// </summary>
    public class MessageBus : IMessageBus
    {
        private readonly Dictionary<Type, List<Delegate>> _subscribers = new();
        private readonly object _lock = new();
        private readonly Dispatcher _dispatcher;

        /// <summary>
        /// 单例实例（向后兼容）
        /// </summary>
        public static MessageBus Instance { get; } = new MessageBus();

        public MessageBus() : this(null) { }

        public MessageBus(Dispatcher dispatcher)
        {
            _dispatcher = dispatcher ?? Dispatcher.CurrentDispatcher;
        }

        public void Subscribe<TMessage>(Action<TMessage> handler) where TMessage : class
        {
            if (handler == null) throw new ArgumentNullException(nameof(handler));

            lock (_lock)
            {
                var type = typeof(TMessage);
                if (!_subscribers.ContainsKey(type))
                {
                    _subscribers[type] = new List<Delegate>();
                }
                _subscribers[type].Add(handler);
            }
        }

        public void Unsubscribe<TMessage>(Action<TMessage> handler) where TMessage : class
        {
            if (handler == null) return;

            lock (_lock)
            {
                var type = typeof(TMessage);
                if (_subscribers.ContainsKey(type))
                {
                    _subscribers[type].Remove(handler);
                }
            }
        }

        public void Publish<TMessage>(TMessage message) where TMessage : class
        {
            if (message == null) return;

            List<Delegate> handlers;
            lock (_lock)
            {
                var type = typeof(TMessage);
                if (!_subscribers.ContainsKey(type) || _subscribers[type].Count == 0)
                    return;
                handlers = new List<Delegate>(_subscribers[type]);
            }

            foreach (var handler in handlers)
            {
                try
                {
                    var action = (Action<TMessage>)handler;

                    // 如果在 UI 线程，直接执行；否则调度到 UI 线程
                    if (_dispatcher.CheckAccess())
                    {
                        action(message);
                    }
                    else
                    {
                        _dispatcher.BeginInvoke(action, message);
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[MessageBus] Error: {ex.Message}");
                }
            }
        }

        public IDisposable SubscribeWithToken<TMessage>(Action<TMessage> handler) where TMessage : class
        {
            Subscribe(handler);
            return new SubscriptionToken<TMessage>(this, handler);
        }

        /// <summary>
        /// 订阅令牌，用于自动取消订阅
        /// </summary>
        private class SubscriptionToken<TMessage> : IDisposable where TMessage : class
        {
            private readonly MessageBus _bus;
            private readonly Action<TMessage> _handler;
            private bool _disposed;

            public SubscriptionToken(MessageBus bus, Action<TMessage> handler)
            {
                _bus = bus;
                _handler = handler;
            }

            public void Dispose()
            {
                if (!_disposed)
                {
                    _bus.Unsubscribe(_handler);
                    _disposed = true;
                }
            }
        }
    }
}
