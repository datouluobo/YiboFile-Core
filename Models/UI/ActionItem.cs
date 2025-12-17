using System.Windows.Controls;

namespace OoiMRR.Models.UI
{
    internal class ActionItem
    {
        public DraggableButton Button { get; set; }
        public Separator Separator { get; set; }
        public bool IsSeparator => Separator != null;
    }
}
