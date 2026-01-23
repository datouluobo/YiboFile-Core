using System;
using System.Collections.Generic;
using YiboFile.Services.Core;

namespace YiboFile.Services.Features
{
    public class TagService : ITagService
    {
        public IEnumerable<ITagGroup> GetTagGroups()
        {
            return DatabaseManager.GetTagGroups();
        }

        public IEnumerable<ITag> GetTagsByGroup(int groupId)
        {
            return DatabaseManager.GetTagsByGroup(groupId);
        }

        public IEnumerable<ITag> GetAllTags()
        {
            return DatabaseManager.GetAllTags();
        }

        public IEnumerable<ITag> GetUngroupedTags()
        {
            return DatabaseManager.GetUngroupedTags();
        }

        public IEnumerable<ITag> GetFileTags(string filePath)
        {
            return DatabaseManager.GetFileTags(filePath);
        }

        public void AddTagToFile(string filePath, int tagId)
        {
            DatabaseManager.AddTagToFile(filePath, tagId);
        }

        public void RemoveTagFromFile(string filePath, int tagId)
        {
            DatabaseManager.RemoveTagFromFile(filePath, tagId);
        }

        public int AddTag(int groupId, string name, string color = null)
        {
            return DatabaseManager.AddTag(groupId, name, color);
        }

        public IEnumerable<string> GetFilesByTag(int tagId)
        {
            return DatabaseManager.GetFilesByTag(tagId);
        }

        public ITag GetTag(int tagId)
        {
            return DatabaseManager.GetTag(tagId);
        }

        public void RenameTag(int tagId, string newName)
        {
            DatabaseManager.RenameTag(tagId, newName);
        }

        public event Action<int, string> TagUpdated;

        public void UpdateTagColor(int tagId, string color)
        {
            DatabaseManager.UpdateTagColor(tagId, color);
            TagUpdated?.Invoke(tagId, color);
        }

        public void DeleteTag(int tagId)
        {
            DatabaseManager.DeleteTag(tagId);
        }

        public void UpdateTagGroup(int tagId, int newGroupId)
        {
            DatabaseManager.UpdateTagGroup(tagId, newGroupId);
        }

        public int AddTagGroup(string name, string color = null)
        {
            return DatabaseManager.AddTagGroup(name, color);
        }

        public void RenameTagGroup(int groupId, string newName)
        {
            DatabaseManager.RenameTagGroup(groupId, newName);
        }

        public void DeleteTagGroup(int groupId)
        {
            DatabaseManager.DeleteTagGroup(groupId);
        }
    }
}
