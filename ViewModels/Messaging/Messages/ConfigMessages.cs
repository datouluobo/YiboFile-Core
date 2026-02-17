namespace YiboFile.ViewModels.Messaging.Messages
{
    /// <summary>
    /// 配置设置项变更消息
    /// </summary>
    public class ConfigurationSettingChangedMessage
    {
        public string SettingName { get; }

        public ConfigurationSettingChangedMessage(string settingName)
        {
            SettingName = settingName;
        }
    }
}
