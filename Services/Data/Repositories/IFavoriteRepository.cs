using System.Collections.Generic;
using System.Threading.Tasks;
using YiboFile.Models;

namespace YiboFile.Services.Data.Repositories
{
    /// <summary>
    /// 收藏存储库接口
    /// 定义收藏项及分组的数据访问契约
    /// </summary>
    public interface IFavoriteRepository
    {
        // 收藏项管理
        List<YiboFile.Favorite> GetAllFavorites();
        Task<List<YiboFile.Favorite>> GetAllFavoritesAsync();

        void AddFavorite(string path, bool isDirectory, string displayName = null, int groupId = 1);
        Task AddFavoriteAsync(string path, bool isDirectory, string displayName = null, int groupId = 1);

        void RemoveFavorite(string path);
        Task RemoveFavoriteAsync(string path);

        bool IsFavorite(string path);
        Task<bool> IsFavoriteAsync(string path);

        void UpdateSortOrder(int favoriteId, int newSortOrder);
        Task UpdateSortOrderAsync(int favoriteId, int newSortOrder);

        // 分组管理
        List<YiboFile.FavoriteGroup> GetAllGroups();
        Task<List<YiboFile.FavoriteGroup>> GetAllGroupsAsync();

        int CreateGroup(string name);
        Task<int> CreateGroupAsync(string name);

        void RenameGroup(int id, string name);
        Task RenameGroupAsync(int id, string name);

        void DeleteGroup(int id);
        Task DeleteGroupAsync(int id);

        void UpdateGroupSortOrder(int id, int order);
        Task UpdateGroupSortOrderAsync(int id, int order);
    }
}
