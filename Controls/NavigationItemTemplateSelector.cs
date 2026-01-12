using System.Windows;
using System.Windows.Controls;
using OoiMRR.Services.Navigation;

namespace OoiMRR.Controls
{
    public class NavigationItemTemplateSelector : DataTemplateSelector
    {
        public DataTemplate DriveTemplate { get; set; }
        public DataTemplate FolderTemplate { get; set; }

        public override DataTemplate SelectTemplate(object item, DependencyObject container)
        {
            if (item is NavigationItem navItem)
            {
                return navItem.IsDrive ? DriveTemplate : FolderTemplate;
            }

            return base.SelectTemplate(item, container);
        }
    }
}
