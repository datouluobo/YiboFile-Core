using System.Collections.Generic;
using System.Threading.Tasks;
using YiboFile.Models;

namespace YiboFile.Services.Data.Repositories
{
    /// <summary>
    /// 库存储库接口
    /// 定义库和库路径的数据访问契约
    /// </summary>
    public interface ILibraryRepository
    {
        // 库管理
        int AddLibrary(string name);
        Task<int> AddLibraryAsync(string name);

        void UpdateLibraryName(int libraryId, string newName);
        Task UpdateLibraryNameAsync(int libraryId, string newName);

        void DeleteLibrary(int libraryId);
        Task DeleteLibraryAsync(int libraryId);

        List<Library> GetAllLibraries();
        Task<List<Library>> GetAllLibrariesAsync();

        Library GetLibrary(int libraryId);
        Task<Library> GetLibraryAsync(int libraryId);

        void MoveLibraryUp(int libraryId);
        Task MoveLibraryUpAsync(int libraryId);

        void MoveLibraryDown(int libraryId);
        Task MoveLibraryDownAsync(int libraryId);

        // 库路径管理
        void AddLibraryPath(int libraryId, string path, string displayName = null);
        Task AddLibraryPathAsync(int libraryId, string path, string displayName = null);

        void RemoveLibraryPath(int libraryId, string path);
        Task RemoveLibraryPathAsync(int libraryId, string path);

        void UpdateLibraryPathDisplayName(int libraryId, string path, string displayName);
        Task UpdateLibraryPathDisplayNameAsync(int libraryId, string path, string displayName);

        List<LibraryPath> GetLibraryPaths(int libraryId);
        Task<List<LibraryPath>> GetLibraryPathsAsync(int libraryId);
    }
}
