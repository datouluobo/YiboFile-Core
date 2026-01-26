using System.Threading.Tasks;
using Library = YiboFile.Library;

namespace YiboFile.Services.Abstractions
{
    /// <summary>
    /// Library related operations consumed by tab navigation.
    /// </summary>
    public interface ILibraryLoader
    {
        /// <summary>
        /// Load files for the given library.
        /// </summary>
        void LoadLibraryFiles(Library library);

        /// <summary>
        /// Highlight library in UI.
        /// </summary>
        void HighlightLibrary(Library library);
    }

    /// <summary>
    /// Tag related operations consumed by tab navigation.
    /// </summary>
    public interface ITagLoader
    {
        /// <summary>
        /// Filter files by tag.
        /// </summary>

    }

    /// <summary>
    /// Path/search operations consumed by tab navigation.
    /// </summary>
    public interface IPathNavigator
    {
        /// <summary>
        /// Navigate to a filesystem path.
        /// </summary>
        void NavigateToPath(string path);

        /// <summary>
        /// Refresh search tab results.
        /// </summary>
        Task RefreshSearchTabAsync(string searchTabPath);

        /// <summary>
        /// Perform a search for the given keyword.
        /// </summary>
        Task PerformSearchAsync(string keyword);
    }
}


