using System.Collections.Generic;
using System.Windows.Controls;

namespace YiboFile.Interfaces.Plugins
{
    /// <summary>
    /// Interface for plugins that extend the application menus.
    /// </summary>
    public interface IMenuExtension
    {
        /// <summary>
        /// Gets the menu items to be added to the specified location.
        /// </summary>
        /// <param name="location">The location where the menu items should be added (e.g., "FileContextMenu", "MainMenu").</param>
        /// <returns>A collection of MenuItems.</returns>
        IEnumerable<MenuItem> GetMenuItems(string location);
    }
}
