using System.Windows.Controls;

namespace YiboFile.Interfaces.Plugins
{
    /// <summary>
    /// Interface for plugins that provide file preview capabilities.
    /// </summary>
    public interface IPreviewExtension
    {
        /// <summary>
        /// Determines whether this extension can preview the specified file.
        /// </summary>
        /// <param name="filePath">The path to the file.</param>
        /// <returns>True if the extension can preview the file; otherwise, false.</returns>
        bool CanPreview(string filePath);

        /// <summary>
        /// Creates a preview control for the specified file.
        /// </summary>
        /// <param name="filePath">The path to the file.</param>
        /// <returns>A WPF Control representing the preview.</returns>
        Control CreatePreview(string filePath);
    }
}
