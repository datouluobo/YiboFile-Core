namespace YiboFile.ViewModels.Messaging.Messages
{
    public class OpenFileRequestMessage
    {
        public string FilePath { get; }

        public OpenFileRequestMessage(string filePath)
        {
            FilePath = filePath;
        }
    }
}
