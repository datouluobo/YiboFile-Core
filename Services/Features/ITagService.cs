using System;
using System.Collections.Generic;
using System.Windows.Media;

namespace YiboFile.Services.Features
{
    /// <summary>
    /// 标签服务接口 (由 Pro 版本实现)
    /// </summary>
    public interface ITagService
    {
        IEnumerable<ITagGroup> GetTagGroups();
        IEnumerable<ITag> GetTagsByGroup(int groupId);
        IEnumerable<ITag> GetAllTags();
        IEnumerable<ITag> GetUngroupedTags();
        event Action<int, string> TagUpdated; // int: TagId, string: NewColorHex (or null if irrelevant)
        IEnumerable<ITag> GetFileTags(string filePath);
        void AddTagToFile(string filePath, int tagId);
        void RemoveTagFromFile(string filePath, int tagId);
        int AddTag(int groupId, string name, string color = null);
        IEnumerable<string> GetFilesByTag(int tagId);
        ITag GetTag(int tagId);

        // Management Methods
        void RenameTag(int tagId, string newName);
        void UpdateTagColor(int tagId, string color);
        void UpdateTagGroup(int tagId, int newGroupId);
        void DeleteTag(int tagId);
        int AddTagGroup(string name, string color = null);
        void RenameTagGroup(int groupId, string newName);
        void DeleteTagGroup(int groupId);
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
