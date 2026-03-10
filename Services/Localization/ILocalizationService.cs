using System.Collections.Generic;
using System.ComponentModel;

namespace YiboFile.Services.Localization
{
    public interface ILocalizationService : INotifyPropertyChanged
    {
        /// <summary>当前语言代码 (如 "zh-CN", "en-US")</summary>
        string CurrentLanguage { get; }

        /// <summary>可用语言列表</summary>
        IReadOnlyList<LanguageInfo> AvailableLanguages { get; }

        /// <summary>索引器：通过 Key 获取翻译文本</summary>
        string this[string key] { get; }

        /// <summary>带参数的格式化翻译</summary>
        string Get(string key, params object[] args);

        /// <summary>切换语言</summary>
        void SetLanguage(string cultureCode);
    }

    public record LanguageInfo(string Code, string DisplayName, string NativeName);
}
