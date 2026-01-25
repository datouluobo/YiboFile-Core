using System.Collections.Generic;
using System.Threading.Tasks;
using YiboFile.Services.Features;

namespace YiboFile.Services.Data.Repositories
{
    /// <summary>
    /// 标签存储库接口
    /// 定义标签系统的数据访问契约
    /// </summary>
    public interface ITagsRepository
    {
        // Tag Groups
        int AddTagGroup(string name, string color = null);
        Task<int> AddTagGroupAsync(string name, string color = null);

        void RenameTagGroup(int groupId, string newName);
        Task RenameTagGroupAsync(int groupId, string newName);

        void DeleteTagGroup(int groupId);
        Task DeleteTagGroupAsync(int groupId);

        List<ITagGroup> GetTagGroups();
        Task<List<ITagGroup>> GetTagGroupsAsync();

        // Tags
        int AddTag(int groupId, string name, string color = null);
        Task<int> AddTagAsync(int groupId, string name, string color = null);

        void RenameTag(int tagId, string newName);
        Task RenameTagAsync(int tagId, string newName);

        void UpdateTagColor(int tagId, string color);
        Task UpdateTagColorAsync(int tagId, string color);

        void DeleteTag(int tagId);
        Task DeleteTagAsync(int tagId);

        void UpdateTagGroup(int tagId, int newGroupId);
        Task UpdateTagGroupAsync(int tagId, int newGroupId);

        ITag GetTag(int tagId);
        Task<ITag> GetTagAsync(int tagId);

        List<ITag> GetTagsByGroup(int groupId);
        Task<List<ITag>> GetTagsByGroupAsync(int groupId);

        List<ITag> GetAllTags();
        Task<List<ITag>> GetAllTagsAsync();

        List<ITag> GetUngroupedTags();
        Task<List<ITag>> GetUngroupedTagsAsync();

        // File Tags
        void AddTagToFile(string filePath, int tagId);
        Task AddTagToFileAsync(string filePath, int tagId);

        void RemoveTagFromFile(string filePath, int tagId);
        Task RemoveTagFromFileAsync(string filePath, int tagId);

        List<ITag> GetFileTags(string filePath);
        Task<List<ITag>> GetFileTagsAsync(string filePath);

        List<string> GetFilesByTag(int tagId);
        Task<List<string>> GetFilesByTagAsync(int tagId);

        List<string> GetFilesByTagName(string tagName);
        Task<List<string>> GetFilesByTagNameAsync(string tagName);
    }
}
