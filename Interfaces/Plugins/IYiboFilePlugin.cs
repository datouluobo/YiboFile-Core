using System;
using YiboFile.ViewModels.Messaging;

namespace YiboFile.Interfaces.Plugins
{
    /// <summary>
    /// Base interface for all YiboFile plugins.
    /// </summary>
    public interface IYiboFilePlugin
    {
        /// <summary>
        /// Gets the unique name of the plugin.
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Gets the version of the plugin.
        /// </summary>
        string Version { get; }

        /// <summary>
        /// Gets the description of the plugin.
        /// </summary>
        string Description { get; }

        /// <summary>
        /// Initializes the plugin with the necessary services.
        /// </summary>
        /// <param name="messageBus">The application message bus for communication.</param>
        /// <param name="serviceProvider">The service provider for resolving dependencies.</param>
        void Initialize(IMessageBus messageBus, IServiceProvider serviceProvider);
    }
}
