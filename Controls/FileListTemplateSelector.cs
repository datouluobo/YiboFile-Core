using System.Windows;
using System.Windows.Controls;
using YiboFile.Models;

namespace YiboFile.Controls
{
    public class FileListTemplateSelector : DataTemplateSelector
    {
        public DataTemplate LibraryTemplate { get; set; }
        public DataTemplate DefaultTemplate { get; set; }

        public override DataTemplate SelectTemplate(object item, DependencyObject container)
        {
            if (item is FileSystemItem fileItem)
            {
                if (fileItem.Type == "Lib")
                {
                    return LibraryTemplate;
                }
            }

            return DefaultTemplate;
        }
    }
}
