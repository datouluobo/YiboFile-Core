using System;
using System.Linq.Expressions;
using YiboFile;

namespace YiboFile.Services.Config
{
    public interface IConfigurationService
    {
        AppConfig Config { get; }

        AppConfig GetSnapshot();

        T Get<T>(Expression<Func<AppConfig, T>> propertyExpression);

        void Set<T>(Expression<Func<AppConfig, T>> propertyExpression, T value);

        void Update(Action<AppConfig> updateAction);

        void SaveNow();

        void Reload();

        void EnableSaving();

        void ManualSave(AppConfig externalConfig);
    }
}
