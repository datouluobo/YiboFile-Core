using System.Windows.Controls;

namespace YiboFile.Models.UI
{
    internal class ActionItem
    {
        public DraggableButton Button { get; set; }
        public Separator Separator { get; set; }
        public bool IsSeparator => Separator != null;
    }
}

