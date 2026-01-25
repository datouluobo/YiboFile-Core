using System;

namespace YiboFile.ViewModels.Messaging
{
    /// <summary>
    /// 消息总线接口
    /// 支持发布/订阅模式的模块间解耦通信
    /// </summary>
    public interface IMessageBus
    {
        /// <summary>
        /// 订阅指定类型的消息
        /// </summary>
        /// <typeparam name="TMessage">消息类型</typeparam>
        /// <param name="handler">消息处理器</param>
        void Subscribe<TMessage>(Action<TMessage> handler) where TMessage : class;

        /// <summary>
        /// 取消订阅
        /// </summary>
        void Unsubscribe<TMessage>(Action<TMessage> handler) where TMessage : class;

        /// <summary>
        /// 发布消息到所有订阅者
        /// </summary>
        void Publish<TMessage>(TMessage message) where TMessage : class;

        /// <summary>
        /// 订阅消息并返回可用于取消订阅的 IDisposable
        /// </summary>
        IDisposable SubscribeWithToken<TMessage>(Action<TMessage> handler) where TMessage : class;
    }
}
