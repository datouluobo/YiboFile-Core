using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows.Media;

namespace YiboFile.Services.Features
{
    /// <summary>
    /// 标签服务接口
    /// 提供同步和异步的标签操作
    /// </summary>
    public interface ITagService
    {
        // 同步方法 (保留兼容性)
        IEnumerable<ITagGroup> GetTagGroups();
        IEnumerable<ITag> GetTagsByGroup(int groupId);
        IEnumerable<ITag> GetAllTags();
        IEnumerable<ITag> GetUngroupedTags();
        IEnumerable<ITag> GetFileTags(string filePath);
        void AddTagToFile(string filePath, int tagId);
        void RemoveTagFromFile(string filePath, int tagId);
        int AddTag(int groupId, string name, string color = null);
        IEnumerable<string> GetFilesByTag(int tagId);
        ITag GetTag(int tagId);
        void RenameTag(int tagId, string newName);
        void UpdateTagColor(int tagId, string color);
        void UpdateTagGroup(int tagId, int newGroupId);
        void DeleteTag(int tagId);
        int AddTagGroup(string name, string color = null);
        void RenameTagGroup(int groupId, string newName);
        void DeleteTagGroup(int groupId);

        event Action<int, string> TagUpdated;

        // 异步方法 (新架构)
        Task<IEnumerable<ITagGroup>> GetTagGroupsAsync();
        Task<IEnumerable<ITag>> GetTagsByGroupAsync(int groupId);
        Task<IEnumerable<ITag>> GetAllTagsAsync();
        Task<IEnumerable<ITag>> GetUngroupedTagsAsync();
        Task<IEnumerable<ITag>> GetFileTagsAsync(string filePath);
        Task AddTagToFileAsync(string filePath, int tagId);
        Task RemoveTagFromFileAsync(string filePath, int tagId);
        Task<int> AddTagAsync(int groupId, string name, string color = null);
        Task<IEnumerable<string>> GetFilesByTagAsync(int tagId);
        Task<IEnumerable<string>> GetFilesByTagNameAsync(string tagName);
        Task<ITag> GetTagAsync(int tagId);
        Task RenameTagAsync(int tagId, string newName);
        Task UpdateTagColorAsync(int tagId, string color);
        Task UpdateTagGroupAsync(int tagId, int newGroupId);
        Task DeleteTagAsync(int tagId);
        Task<int> AddTagGroupAsync(string name, string color = null);
        Task RenameTagGroupAsync(int groupId, string newName);
        Task DeleteTagGroupAsync(int groupId);
    }

    public interface ITagGroup
    {
        int Id { get; set; }
        string Name { get; set; }
        string Color { get; set; }
    }

    public interface ITag
    {
        int Id { get; set; }
        string Name { get; set; }
        string Color { get; set; }
        int GroupId { get; set; }
    }
}
