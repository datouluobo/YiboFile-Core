namespace YiboFile.ViewModels.Messaging.Messages
{
    public class PreviewRequestMessage
    {
        public string FilePath { get; }
        public YiboFile.Services.Navigation.PaneId TargetPane { get; }

        public PreviewRequestMessage(string filePath, YiboFile.Services.Navigation.PaneId targetPane)
        {
            FilePath = filePath;
            TargetPane = targetPane;
        }
    }
}
