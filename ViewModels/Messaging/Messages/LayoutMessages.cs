namespace YiboFile.ViewModels.Messaging.Messages
{
    /// <summary>
    /// 布局模式变化消息
    /// </summary>
    public class LayoutModeChangedMessage
    {
        public string Mode { get; }

        public LayoutModeChangedMessage(string mode)
        {
            Mode = mode;
        }
    }

    /// <summary>
    /// 双列表模式变化消息
    /// </summary>
    public class DualListModeChangedMessage
    {
        public bool IsEnabled { get; }

        public DualListModeChangedMessage(bool isEnabled)
        {
            IsEnabled = isEnabled;
        }
    }

    /// <summary>
    /// 请求切换焦点面板
    /// </summary>
    public class SwitchFocusedPaneMessage { }

    /// <summary>
    /// 焦点面板已变更通知（用于 UI 更新）
    /// </summary>
    public class FocusedPaneChangedMessage
    {
        public bool IsSecondPaneFocused { get; }

        public FocusedPaneChangedMessage(bool isSecondPaneFocused)
        {
            IsSecondPaneFocused = isSecondPaneFocused;
        }
    }

    /// <summary>
    /// 请求设置焦点面板
    /// </summary>
    public class SetFocusedPaneMessage
    {
        public bool IsSecondPane { get; }

        public SetFocusedPaneMessage(bool isSecondPane)
        {
            IsSecondPane = isSecondPane;
        }
    }

    /// <summary>
    /// 双列表模式切换消息
    /// </summary>
    public class DualListModeToggledMessage
    {
        public bool IsEnabled { get; }

        public DualListModeToggledMessage(bool isEnabled)
        {
            IsEnabled = isEnabled;
        }
    }

    /// <summary>
    /// 请求显示设置面板
    /// </summary>
    public record ShowSettingsMessage();

    /// <summary>
    /// 请求显示关于面板
    /// </summary>
    public record ShowAboutMessage();
}
