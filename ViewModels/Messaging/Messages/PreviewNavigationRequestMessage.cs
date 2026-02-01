using System;

namespace YiboFile.ViewModels.Messaging.Messages
{
    public class PreviewNavigationRequestMessage
    {
        public bool IsNext { get; }

        public PreviewNavigationRequestMessage(bool isNext)
        {
            IsNext = isNext;
        }
    }
}
