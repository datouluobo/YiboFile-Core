using System.Windows;
using System.Windows.Controls;

namespace YiboFile.Models.UI
{
    internal class DraggableButton
    {
        public Button Button { get; set; }
        public string ActionName { get; set; }
        public RoutedEventHandler ClickHandler { get; set; }
    }
}

