using System;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading;
using System.Windows.Threading;
using YiboFile.Services.Core;

namespace YiboFile.Services.Config
{
    /// <summary>
    /// 统一配置管理服务 - 单例模式
    /// 消除分散的ConfigManager.Save调用，避免配置互相覆盖的竞态条件
    /// </summary>
    public class ConfigurationService
    {
        private static ConfigurationService _instance;
        private static readonly object _instanceLock = new object();

        private AppConfig _config;
        private readonly object _configLock = new object();
        private readonly DispatcherTimer _debounceTimer;
        private bool _isDirty = false;

        /// <summary>
        /// 当配置项变更时触发
        /// </summary>
        public event EventHandler<string> SettingChanged;

        // 去抖时间（毫秒）
        private const int DebounceDelayMs = 500;

        // 性能监控
        private static int _totalSaveCount = 0;
        private static int _debouncedSaveCount = 0;
        private static DateTime _lastSaveTime = DateTime.MinValue;
        private static readonly string _perfLogPath = @"f:\Download\GitHub\YiboFile\.cursor\config_perf.log";

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
            // 加载初始配置
            _config = ConfigManager.Load();

            // 创建去抖定时器（500ms）
            _debounceTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(DebounceDelayMs)
            };
            _debounceTimer.Tick += OnDebounceTick;
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

        /// <summary>
        /// 设置单个配置值（原子操作）
        /// </summary>
        public void Set<T>(Expression<Func<AppConfig, T>> propertyExpression, T value)
        {
            lock (_configLock)
            {
                var memberExpr = propertyExpression.Body as MemberExpression;
                if (memberExpr == null)
                    throw new ArgumentException("Expression must be a property access");

                var propInfo = memberExpr.Member as PropertyInfo;
                if (propInfo == null)
                    throw new ArgumentException("Expression must be a property access");

                propInfo.SetValue(_config, value);
                _isDirty = true;

                // 触发变更事件
                SettingChanged?.Invoke(this, propInfo.Name);

                TriggerDebouncedSave();
            }
        }

        /// <summary>
        /// 批量更新配置（事务性操作）
        /// </summary>
        public void Update(Action<AppConfig> updateAction)
        {
            lock (_configLock)
            {
                updateAction(_config);
                _isDirty = true;

                // 批量更新时触发通配符事件或特定事件(这里简化为null或"All")
                SettingChanged?.Invoke(this, "All");

                TriggerDebouncedSave();
            }
        }

        /// <summary>
        /// 立即保存配置（跳过去抖，用于程序关闭等关键时刻）
        /// </summary>
        public void SaveNow()
        {
            lock (_configLock)
            {
                if (_debounceTimer.IsEnabled)
                {
                    _debounceTimer.Stop();
                }

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
                _config = ConfigManager.Load();
                _isDirty = false;
            }
        }

        #region 辅助方法

        private AppConfig DeepCopyConfig(AppConfig source)
        {
            if (source == null) return null;

            return System.Text.Json.JsonSerializer.Deserialize<AppConfig>(
                System.Text.Json.JsonSerializer.Serialize(source),
                new System.Text.Json.JsonSerializerOptions
                {
                    Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() },
                    PropertyNameCaseInsensitive = true
                });
        }

        /// <summary>
        /// 执行保存并记录性能指标
        /// </summary>
        private void PerformSaveWithMonitoring()
        {
            var startTime = DateTime.Now;
            ConfigManager.Save(_config);
            var duration = (DateTime.Now - startTime).TotalMilliseconds;

            _totalSaveCount++;
            var timeSinceLastSave = _lastSaveTime == DateTime.MinValue
                ? 0
                : (startTime - _lastSaveTime).TotalSeconds;
            _lastSaveTime = startTime;

            // 记录性能日志（只在DEBUG模式下）
#if DEBUG
            try
            {
                var logMsg = $"[{startTime:HH:mm:ss.fff}] Save #{_totalSaveCount} | Duration: {duration:F2}ms | Since Last: {timeSinceLastSave:F1}s\n";
                System.IO.File.AppendAllText(_perfLogPath, logMsg);
            }
            catch { /* 忽略日志错误 */ }
#endif
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
            // 重启定时器
            _debounceTimer.Stop();
            _debounceTimer.Start();
        }

        /// <summary>
        /// 去抖定时器到期，执行保存
        /// </summary>
        private void OnDebounceTick(object sender, EventArgs e)
        {
            _debounceTimer.Stop();

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
            _debounceTimer?.Stop();

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

