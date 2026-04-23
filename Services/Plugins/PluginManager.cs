using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using YiboFile.Interfaces.Plugins;
using YiboFile.Services.Core;
using YiboFile.ViewModels.Messaging;

namespace YiboFile.Services.Plugins
{
    /// <summary>
    /// Service for discovering, loading, and managing application plugins.
    /// </summary>
    public class PluginManager : IPluginManager
    {
        private readonly List<IYiboFilePlugin> _plugins = new List<IYiboFilePlugin>();
        private readonly IMessageBus _messageBus;
        private readonly IServiceProvider _serviceProvider;
        private readonly YiboFile.Services.Tabs.TabContentRegistry _registry;

        public PluginManager(IMessageBus messageBus, IServiceProvider serviceProvider)
        {
            _messageBus = messageBus ?? throw new ArgumentNullException(nameof(messageBus));
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            
            // 可选：获取TabContentRegistry
            _registry = serviceProvider.GetService(typeof(YiboFile.Services.Tabs.TabContentRegistry)) as YiboFile.Services.Tabs.TabContentRegistry;
        }

        public IReadOnlyList<IYiboFilePlugin> Plugins => _plugins.AsReadOnly();

        public async Task LoadPluginsAsync(string pluginDirectory)
        {
            if (string.IsNullOrWhiteSpace(pluginDirectory))
                return;

            if (!Directory.Exists(pluginDirectory))
            {
                try
                {
                    Directory.CreateDirectory(pluginDirectory);
                }
                catch (Exception ex)
                {
                    FileLogger.LogException($"Failed to create plugin directory: {pluginDirectory}", ex);
                    return;
                }
            }

            FileLogger.Log($"Scanning for plugins in: {pluginDirectory}");

            // Find all DLLs in the plugin directory
            var dllFiles = Directory.GetFiles(pluginDirectory, "*.dll", SearchOption.TopDirectoryOnly);

            foreach (var dllPath in dllFiles)
            {
                try
                {
                    // Load the assembly
                    var assembly = Assembly.LoadFrom(dllPath);

                    // Find types implementing IYiboFilePlugin
                    var pluginTypes = assembly.GetTypes()
                        .Where(t => typeof(IYiboFilePlugin).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract);

                    foreach (var type in pluginTypes)
                    {
                        try
                        {
                            // Instantiate the plugin (assumes parameterless constructor)
                            if (Activator.CreateInstance(type) is IYiboFilePlugin plugin)
                            {
                                try
                                {
                                    // Initialize the plugin
                                    plugin.Initialize(_messageBus, _serviceProvider);
                                    _plugins.Add(plugin);
                                    FileLogger.Log($"Loaded plugin: {plugin.Name} (v{plugin.Version}) - {plugin.Description}");
                                }
                                catch (Exception ex)
                                {
                                    FileLogger.LogException($"Error initializing plugin {type.Name}", ex);
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            FileLogger.LogException($"Failed to instantiate plugin type {type.Name} from {Path.GetFileName(dllPath)}", ex);
                        }
                    }

                    // 新增: 扫描 ITabPageExtension (支持插件向Tab注册特殊内置页)
                    if (_registry != null)
                    {
                        var tabExtensions = assembly.GetTypes()
                            .Where(t => typeof(ITabPageExtension).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract);

                        foreach (var extType in tabExtensions)
                        {
                            try
                            {
                                if (Activator.CreateInstance(extType) is ITabPageExtension ext)
                                {
                                    _registry.Register(ext.ContentTypeId, () => ext.CreateContent());
                                    FileLogger.Log($"Registered tab page extension: {ext.ContentTypeId}");
                                }
                            }
                            catch (Exception ex)
                            {
                                FileLogger.LogException($"Failed to instantiate custom tab extension {extType.Name} from {Path.GetFileName(dllPath)}", ex);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    FileLogger.LogException($"Failed to load assembly: {Path.GetFileName(dllPath)}", ex);
                }
            }

            await Task.CompletedTask;
        }

        public IEnumerable<T> GetExtensions<T>() where T : class
        {
            return _plugins.OfType<T>();
        }
    }
}
