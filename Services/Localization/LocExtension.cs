using System;
using System.Windows.Data;
using System.Windows.Markup;
using Microsoft.Extensions.DependencyInjection;

namespace YiboFile.Services.Localization
{
    [MarkupExtensionReturnType(typeof(string))]
    public class LocExtension : MarkupExtension
    {
        public string Key { get; set; }

        public LocExtension() { }

        public LocExtension(string key)
        {
            Key = key;
        }

        public override object ProvideValue(IServiceProvider serviceProvider)
        {
            if (string.IsNullOrEmpty(Key))
                return string.Empty;

            var locService = App.ServiceProvider?.GetService<ILocalizationService>();
            
            // 如果服务不可用（例如设计器模式或提早加载），返回占位符
            if (locService == null)
            {
                return $"[{Key}]";
            }

            // 创建绑定：绑定到 locService 的索引器 [Key] 上
            var binding = new Binding($"[{Key}]")
            {
                Source = locService,
                Mode = BindingMode.OneWay
            };

            return binding.ProvideValue(serviceProvider);
        }
    }
}
