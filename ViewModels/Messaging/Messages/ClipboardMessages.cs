using System.Collections.Generic;

namespace YiboFile.ViewModels.Messaging.Messages
{
    /// <summary>
    /// 剪贴板剪切状态变更消息
    /// </summary>
    public class ClipboardCutStateChangedMessage
    {
        public IReadOnlyList<string> CutPaths { get; }

        public ClipboardCutStateChangedMessage(IReadOnlyList<string> cutPaths)
        {
            CutPaths = cutPaths;
        }
    }
}
