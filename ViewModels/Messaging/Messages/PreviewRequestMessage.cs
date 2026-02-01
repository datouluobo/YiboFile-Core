namespace YiboFile.ViewModels.Messaging.Messages
{
    public class PreviewRequestMessage
    {
        public string FilePath { get; }

        public PreviewRequestMessage(string filePath)
        {
            FilePath = filePath;
        }
    }
}
