using System.Collections.Generic;
using System.Threading.Tasks;
using YiboFile.Interfaces.Plugins;

namespace YiboFile.Services.Plugins
{
    /// <summary>
    /// Manages the lifecycle of plugins.
    /// </summary>
    public interface IPluginManager
    {
        /// <summary>
        /// Gets the collection of loaded plugins.
        /// </summary>
        IReadOnlyList<IYiboFilePlugin> Plugins { get; }

        /// <summary>
        /// Loads plugins from the specified directory.
        /// </summary>
        /// <param name="pluginDirectory">The directory containing plugin assemblies.</param>
        Task LoadPluginsAsync(string pluginDirectory);

        /// <summary>
        /// Gets all loaded extensions of a specific type.
        /// </summary>
        /// <typeparam name="T">The extension interface type.</typeparam>
        /// <returns>A collection of extensions implementing the specified type.</returns>
        IEnumerable<T> GetExtensions<T>() where T : class;
    }
}
