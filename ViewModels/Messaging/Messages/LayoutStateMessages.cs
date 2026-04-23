namespace YiboFile.ViewModels.Messaging.Messages
{
    /// <summary>
    /// 布局验证请求消息
    /// 触发系统检查当前分栏比例是否合法（如防止右侧空白）
    /// </summary>
    public class LayoutValidationRequestMessage { }

    /// <summary>
    /// Tab 管理器边距更新请求消息
    /// 当窗口控制按钮宽度变化或面板折叠时触发
    /// </summary>
    public class TabManagerMarginUpdateRequestMessage
    {
        public bool ForceImmediate { get; set; }
    }


}
