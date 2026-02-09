using System.Windows;

namespace YiboFile.ViewModels.Messaging.Messages
{
    /// <summary>
    /// 窗口加载完成消息
    /// </summary>
    public class WindowLoadedMessage
    {
        public double ActualWidth { get; set; }
        public double ActualHeight { get; set; }
        public WindowState WindowState { get; set; }
    }

    /// <summary>
    /// 窗口正在关闭消息
    /// </summary>
    public class WindowClosingMessage
    {
        public bool Cancel { get; set; }
    }

    /// <summary>
    /// 窗口位置发生变更消息
    /// </summary>
    public class WindowLocationChangedMessage
    {
        public double Left { get; set; }
        public double Top { get; set; }
    }

    /// <summary>
    /// 窗口状态变更消息 (最大化/最小化/还原)
    /// </summary>
    public class WindowStateChangedMessage
    {
        public WindowState NewState { get; set; }
    }

    /// <summary>
    /// 窗口大小变更消息
    /// </summary>
    public class WindowSizeChangedMessage
    {
        public double NewWidth { get; set; }
        public double NewHeight { get; set; }
    }

    /// <summary>
    /// 窗口内容渲染完成消息 (ContentRendered)
    /// </summary>
    public class WindowContentRenderedMessage
    {
        public WindowState WindowState { get; set; }
    }

    /// <summary>
    /// 窗口预览按键消息
    /// </summary>
    public class WindowPreviewKeyDownMessage
    {
        public System.Windows.Input.KeyEventArgs EventArgs { get; set; }
    }

    /// <summary>
    /// 窗口按键消息
    /// </summary>
    public class WindowKeyDownMessage
    {
        public System.Windows.Input.KeyEventArgs EventArgs { get; set; }
    }
}
