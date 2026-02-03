namespace YiboFile.ViewModels.Messaging.Messages
{
    public class MainLayoutVisibilityChangedMessage
    {
        public bool IsVisible { get; }

        public MainLayoutVisibilityChangedMessage(bool isVisible)
        {
            IsVisible = isVisible;
        }
    }
}
