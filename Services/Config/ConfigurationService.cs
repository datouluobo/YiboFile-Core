using System;
using System.IO;
using System.Linq.Expressions;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Windows.Threading;
using Microsoft.Extensions.DependencyInjection;
using YiboFile.Models.Config;
using YiboFile.Services.Core;
using YiboFile.ViewModels.Messaging;
using YiboFile.ViewModels.Messaging.Messages;

namespace YiboFile.Services.Config
{
    /// <summary>
    /// 统一配置管理服务 - 单例模式
    /// 消除分散的ConfigManager.Save调用，避免配置互相覆盖的竞态条件
    /// </summary>
    public class ConfigurationService : IConfigurationService
    {
        private static ConfigurationService _instance;
        private static readonly object _instanceLock = new object();

        private AppConfig _config;
        private UserSettings _userSettings;
        private AppState _appState;

        private readonly object _configLock = new object();
        private readonly System.Threading.Timer _debounceTimer;
        private bool _isDirty = false;
        private IMessageBus _messageBus;
        private IConfigPathProvider _pathProvider;

        // 默认为 true，防止启动时的波动触发保存
        private bool _isSaveSuppressed = true;

        /// <summary>
        /// 启用配置保存（应在应用初始化完成后调用）
        /// </summary>
        public void EnableSaving()
        {
            lock (_configLock)
            {
                _isSaveSuppressed = false;
            }
        }

        /// <summary>
        /// 设置消息总线
        /// </summary>
        public void SetMessageBus(IMessageBus messageBus)
        {
            _messageBus = messageBus;
        }

        // 去抖时间（毫秒）
        private const int DebounceDelayMs = 500;

        // 性能监控
        private static int _totalSaveCount = 0;
        private static int _debouncedSaveCount = 0;
        private static DateTime _lastSaveTime = DateTime.MinValue;


        /// <summary>
        /// 单例实例
        /// </summary>
        public static ConfigurationService Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_instanceLock)
                    {
                        if (_instance == null)
                        {
                            _instance = new ConfigurationService();
                        }
                    }
                }
                return _instance;
            }
        }

        private ConfigurationService()
        {
            // 获取路径提供者
            _pathProvider = App.ServiceProvider?.GetService<IConfigPathProvider>();

            // 如果 DI 尚未就绪，手动实例化 ConfigPathProvider (用于启动早期)
            if (_pathProvider == null)
            {
                _pathProvider = new ConfigPathProvider();
            }

            // 加载初始配置
            LoadFromStorage();

            // 尝试获取 MessageBus (延迟绑定)
            _messageBus = App.ServiceProvider?.GetService<IMessageBus>();

            // 创建去抖定时器（500ms）并使用 ThreadPool，避免 DispatcherTimer 跨线程无消息循环挂起
            _debounceTimer = new System.Threading.Timer(OnDebounceTick, null, Timeout.Infinite, Timeout.Infinite);
        }

        private void LoadFromStorage()
        {
            // 默认初始化
            _userSettings = new UserSettings();
            _appState = new AppState();

            bool loadedNew = false;

            // 1. 尝试加载新格式
            if (_pathProvider != null)
            {
                if (File.Exists(_pathProvider.SettingsFilePath))
                {
                    try
                    {
                        var json = File.ReadAllText(_pathProvider.SettingsFilePath);
                        _userSettings = JsonSerializer.Deserialize<UserSettings>(json, GetJsonOptions()) ?? new UserSettings();
                        loadedNew = true;
                    }
                    catch { }
                }

                if (File.Exists(_pathProvider.StateFilePath))
                {
                    try
                    {
                        var json = File.ReadAllText(_pathProvider.StateFilePath);
                        _appState = JsonSerializer.Deserialize<AppState>(json, GetJsonOptions()) ?? new AppState();
                        loadedNew = true;
                    }
                    catch { }
                }
            }

            // 2. 如果新格式未加载（或部分缺失），尝试迁移旧格式
            // 仅当settings.json不存在时才尝试迁移，避免覆盖
            if (!loadedNew && File.Exists(ConfigManager.GetConfigFilePath()))
            {
                try
                {
                    var legacyConfig = ConfigManager.LoadLegacy();
                    if (legacyConfig != null)
                    {
                        // 映射到新模型
                        ConfigMapper.MapToModels(legacyConfig, _userSettings, _appState);

                        // 保存新格式
                        SaveModelsToDisk();

                        // 重命名旧文件 (Migrate logic)
                        try
                        {
                            File.Move(ConfigManager.GetConfigFilePath(), ConfigManager.GetConfigFilePath() + ".bak", true);
                        }
                        catch { }
                    }
                }
                catch { }
            }

            // 3. 构建 AppConfig Facade
            _config = ConfigMapper.MapToAppConfig(_userSettings, _appState);
        }

        private void SaveModelsToDisk()
        {
            if (_pathProvider == null) return;

            var options = GetJsonOptions();

            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(_pathProvider.SettingsFilePath));
                var settingsJson = JsonSerializer.Serialize(_userSettings, options);
                File.WriteAllText(_pathProvider.SettingsFilePath, settingsJson);
            }
            catch { }

            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(_pathProvider.StateFilePath));
                var stateJson = JsonSerializer.Serialize(_appState, options);
                File.WriteAllText(_pathProvider.StateFilePath, stateJson);
            }
            catch { }
        }

        private JsonSerializerOptions GetJsonOptions()
        {
            return new JsonSerializerOptions
            {
                WriteIndented = true,
                Converters = { new JsonStringEnumConverter() },
                PropertyNameCaseInsensitive = true,
                NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };
        }

        /// <summary>
        /// 获取配置的只读快照
        /// </summary>
        public AppConfig GetSnapshot()
        {
            lock (_configLock)
            {
                // 返回深拷贝，防止外部直接修改
                return DeepCopyConfig(_config);
            }
        }

        /// <summary>
        /// 获取单个配置值
        /// </summary>
        public T Get<T>(Expression<Func<AppConfig, T>> propertyExpression)
        {
            lock (_configLock)
            {
                var func = propertyExpression.Compile();
                return func(_config);
            }
        }

        private int _recursionDepth = 0;
        private const int MaxRecursionDepth = 10;

        /// <summary>
        /// 设置单个配置值（原子操作）
        /// </summary>
        public void Set<T>(Expression<Func<AppConfig, T>> propertyExpression, T value)
        {
            if (_recursionDepth > MaxRecursionDepth) return;

            lock (_configLock)
            {
                var memberExpr = propertyExpression.Body as MemberExpression;
                if (memberExpr == null)
                    throw new ArgumentException("Expression must be a property access");

                var propInfo = memberExpr.Member as PropertyInfo;
                if (propInfo == null)
                    throw new ArgumentException("Expression must be a property access");

                // Check for equality to prevent infinite loops and unnecessary updates
                var currentValue = propInfo.GetValue(_config);
                if (Equals(currentValue, value)) return;

                _recursionDepth++;
                try
                {
                    propInfo.SetValue(_config, value);
                    _isDirty = true;

                    // 触发变更消
                    _messageBus?.Publish(new ConfigurationSettingChangedMessage(propInfo.Name));

                    TriggerDebouncedSave();
                }
                finally
                {
                    _recursionDepth--;
                }
            }
        }

        /// <summary>
        /// 批量更新配置（事务性操作）
        /// </summary>
        public void Update(Action<AppConfig> updateAction)
        {
            if (_recursionDepth > MaxRecursionDepth) return;

            lock (_configLock)
            {
                _recursionDepth++;
                try
                {
                    updateAction(_config);
                    _isDirty = true;

                    // 批量更新时触发通配符事件或特定事件(这里简化为null或"All")
                    _messageBus?.Publish(new ConfigurationSettingChangedMessage("All"));

                    TriggerDebouncedSave();
                }
                finally
                {
                    _recursionDepth--;
                }
            }
        }

        /// <summary>
        /// 立即保存配置（跳过去抖，用于程序关闭等关键时刻）
        /// </summary>
        public void SaveNow()
        {
            lock (_configLock)
            {
                _debounceTimer?.Change(Timeout.Infinite, Timeout.Infinite);

                if (_isDirty)
                {
                    PerformSaveWithMonitoring();
                    _isDirty = false;
                }
            }
        }

        /// <summary>
        /// 重新加载配置（从磁盘）
        /// 警告：这会丢失未保存的修改！
        /// </summary>
        public void Reload()
        {
            lock (_configLock)
            {
                LoadFromStorage();
                _isDirty = false;
            }
        }

        /// <summary>
        /// 手动保存外部配置对象（兼容 ConfigManager.Save）
        /// 将外部对象的属性应用到当前服务，并触发保存
        /// </summary>
        public void ManualSave(AppConfig externalConfig)
        {
            if (externalConfig == null) return;

            lock (_configLock)
            {
                // 如果传入的对象不是当前的配置对象，则需要复制属性
                // 注意：这里简单假设可以直接替换引用，或者如果引用不同则手动同步
                // 由于 AppConfig 是引用类型，且 _config 被广泛引用，我们应该尽量保持 _config 引用不变
                // 但如果外部传入了一个全新的 AppConfig 对象... 这是一个棘手的情况

                if (!ReferenceEquals(_config, externalConfig))
                {
                    // 深度复制属性 externalConfig -> _config
                    // 暂时通过序列化/反序列化实现属性复制，或者使用 ConfigMapper
                    // 为了简单起见，且 AppConfig 是 POCO，我们使用 ConfigMapper 的反向逻辑
                    // 但 ConfigMapper 是 Models <-> AppConfig

                    // 既然我们有 JSON 序列化，我们可以用它来复制属性
                    var json = JsonSerializer.Serialize(externalConfig, GetJsonOptions());
                    var newConfig = JsonSerializer.Deserialize<AppConfig>(json, GetJsonOptions());

                    // 我们不能替换 _config 引用，因为可能有其他地方持有它（虽然不推荐）
                    // 但最重要的是 _config 是 facade。
                    // 正确做法是：直接用 externalConfig 更新 Models，再重新生成 _config? 
                    // 或者将 externalConfig 视为新的 Source of Truth。

                    _config = newConfig; // 替换引用。如果有其他服务持有旧引用的 _config，它们将过时。这是不可避免的代价。
                }

                // 立即保存
                PerformSaveWithMonitoring();
                _isDirty = false;
            }
        }

        /// <summary>
        /// 提供对当前配置对象的直接访问
        /// 警告：直接修改此对象不会自动触发去抖保存，请优先使用 Set 或 Update 方法
        /// </summary>
        public AppConfig Config => _config;

        #region 辅助方法

        private AppConfig DeepCopyConfig(AppConfig source)
        {
            if (source == null) return null;

            var options = new JsonSerializerOptions
            {
                Converters = { new JsonStringEnumConverter() },
                PropertyNameCaseInsensitive = true,
                NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals
            };

            return JsonSerializer.Deserialize<AppConfig>(
                JsonSerializer.Serialize(source, options),
                options);
        }

        /// <summary>
        /// 执行保存并记录性能指标
        /// </summary>
        private void PerformSaveWithMonitoring()
        {
            var startTime = DateTime.Now;

            // Sync AppConfig back to models
            ConfigMapper.MapToModels(_config, _userSettings, _appState);

            // Save models to disk
            SaveModelsToDisk();

            var duration = (DateTime.Now - startTime).TotalMilliseconds;

            _totalSaveCount++;
            var timeSinceLastSave = _lastSaveTime == DateTime.MinValue
                ? 0
                : (startTime - _lastSaveTime).TotalSeconds;
            _lastSaveTime = startTime;
        }

        /// <summary>
        /// 获取性能统计信息
        /// </summary>
        public static PerformanceStats GetPerformanceStats()
        {
            return new PerformanceStats
            {
                TotalSaves = _totalSaveCount,
                DebouncedSaves = _debouncedSaveCount,
                DebounceHitRate = _totalSaveCount > 0 ? (double)_debouncedSaveCount / _totalSaveCount : 0
            };
        }

        #endregion

        /// <summary>
        /// 触发去抖保存
        /// </summary>
        private void TriggerDebouncedSave()
        {
            if (_isSaveSuppressed) return;

            // 重启去抖定时器
            _debounceTimer.Change(DebounceDelayMs, Timeout.Infinite);
        }

        /// <summary>
        /// 去抖定时器到期，执行保存
        /// </summary>
        private void OnDebounceTick(object state)
        {

            lock (_configLock)
            {
                if (_isDirty)
                {
                    try
                    {
                        _debouncedSaveCount++;
                        PerformSaveWithMonitoring();
                        _isDirty = false;
                    }
                    catch (Exception)
                    {
                        // 记录错误但不抛出，避免影响应用运行
                    }
                }
            }
        }

        /// <summary>
        /// 停止所有定时器（用于应用关闭）
        /// </summary>
        public void Shutdown()
        {
            _debounceTimer?.Change(Timeout.Infinite, Timeout.Infinite);

            // 强制保存未保存的更改
            SaveNow();
        }
    }

    /// <summary>
    /// 性能统计数据
    /// </summary>
    public class PerformanceStats
    {
        public int TotalSaves { get; set; }
        public int DebouncedSaves { get; set; }
        public double DebounceHitRate { get; set; }
    }
}
