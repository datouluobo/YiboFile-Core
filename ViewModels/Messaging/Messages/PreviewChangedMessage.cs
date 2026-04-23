using YiboFile.ViewModels.Previews;

namespace YiboFile.ViewModels.Messaging.Messages
{
    public class PreviewChangedMessage
    {
        public IPreviewViewModel Preview { get; }
        public YiboFile.Services.Navigation.PaneId TargetPane { get; }

        public PreviewChangedMessage(IPreviewViewModel preview, YiboFile.Services.Navigation.PaneId targetPane)
        {
            Preview = preview;
            TargetPane = targetPane;
        }
    }
}
