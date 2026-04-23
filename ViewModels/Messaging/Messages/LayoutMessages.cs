using YiboFile.Services.Navigation;

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
    /// 双面板模式变化消息
    /// </summary>
    public class DualPaneModeChangedMessage
    {
        public bool IsEnabled { get; }

        public DualPaneModeChangedMessage(bool isEnabled)
        {
            IsEnabled = isEnabled;
        }
    }

    /// <summary>
    /// 请求交换两个面板的内容
    /// </summary>
    public class RequestSwapPanesMessage { }

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
    /// 双面板模式切换消息
    /// </summary>
    public class DualPaneModeToggledMessage
    {
        public bool IsEnabled { get; }

        public DualPaneModeToggledMessage(bool isEnabled)
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

    /// <summary>
    /// 预览面板可见性变更消息（用于双栏模式下的跨面板预览协调）
    /// </summary>
    public class PreviewPaneVisibilityChangedMessage
    {
        public PaneId Pane { get; }
        public bool IsVisible { get; }

        public PreviewPaneVisibilityChangedMessage(PaneId pane, bool isVisible)
        {
            Pane = pane;
            IsVisible = isVisible;
        }
    }
}
