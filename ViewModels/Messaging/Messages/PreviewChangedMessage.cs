using YiboFile.ViewModels.Previews;

namespace YiboFile.ViewModels.Messaging.Messages
{
    public class PreviewChangedMessage
    {
        public IPreviewViewModel Preview { get; }

        public PreviewChangedMessage(IPreviewViewModel preview)
        {
            Preview = preview;
        }
    }
}
