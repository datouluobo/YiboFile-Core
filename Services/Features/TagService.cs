using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using YiboFile.Services.Core;
using YiboFile.Services.Data.Repositories;
using YiboFile.ViewModels.Messaging;
using YiboFile.ViewModels.Messaging.Messages;
using Microsoft.Extensions.DependencyInjection;
using System.Linq;

namespace YiboFile.Services.Features
{
    /// <summary>
    /// 标签服务实现
    /// 封装业务逻辑，调用 ITagsRepository 进行数据访问
    /// </summary>
    public class TagService : ITagService
    {
        private readonly ITagsRepository _repository;
        private readonly IMessageBus _messageBus;

        /// <summary>
        /// 依赖注入构造函数
        /// </summary>
        public TagService(ITagsRepository repository, IMessageBus messageBus = null)
        {
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
            _messageBus = messageBus ?? App.ServiceProvider?.GetService<IMessageBus>();
        }

        public TagService()
        {
            _repository = new SqliteTagsRepository();
            _messageBus = App.ServiceProvider?.GetService<IMessageBus>();
        }

        #region 同步方法实现 (调用 Repository)

        public IEnumerable<ITagGroup> GetTagGroups() => _repository.GetTagGroups();
        public IEnumerable<ITag> GetTagsByGroup(int groupId) => _repository.GetTagsByGroup(groupId);
        public IEnumerable<ITag> GetAllTags() => _repository.GetAllTags();
        public IEnumerable<ITag> GetUngroupedTags() => _repository.GetUngroupedTags();
        public IEnumerable<ITag> GetFileTags(string filePath) => _repository.GetFileTags(filePath);

        public void AddTagToFile(string filePath, int tagId)
        {
            _repository.AddTagToFile(filePath, tagId);
            _messageBus?.Publish(new FileTagsChangedMessage(filePath));
        }

        public void RemoveTagFromFile(string filePath, int tagId)
        {
            _repository.RemoveTagFromFile(filePath, tagId);
            _messageBus?.Publish(new FileTagsChangedMessage(filePath));
        }

        public int AddTag(int groupId, string name, string color = null)
        {
            var id = _repository.AddTag(groupId, name, color);
            _messageBus?.Publish(new TagListChangedMessage());
            return id;
        }

        public IEnumerable<string> GetFilesByTag(int tagId) => _repository.GetFilesByTag(tagId);
        public IEnumerable<string> GetFilesByTagName(string tagName) => _repository.GetFilesByTagName(tagName);
        public ITag GetTag(int tagId) => _repository.GetTag(tagId);

        public void RenameTag(int tagId, string newName)
        {
            _repository.RenameTag(tagId, newName);
            _messageBus?.Publish(new TagListChangedMessage());
        }

        public void UpdateTagColor(int tagId, string color)
        {
            _repository.UpdateTagColor(tagId, color);
            _messageBus?.Publish(new TagListChangedMessage());
        }

        public void UpdateTagGroup(int tagId, int newGroupId)
        {
            _repository.UpdateTagGroup(tagId, newGroupId);
            _messageBus?.Publish(new TagListChangedMessage());
        }

        public void DeleteTag(int tagId)
        {
            _repository.DeleteTag(tagId);
            _messageBus?.Publish(new TagListChangedMessage());
        }

        public int AddTagGroup(string name, string color = null)
        {
            var id = _repository.AddTagGroup(name, color);
            _messageBus?.Publish(new TagListChangedMessage());
            return id;
        }

        public void RenameTagGroup(int groupId, string newName)
        {
            _repository.RenameTagGroup(groupId, newName);
            _messageBus?.Publish(new TagListChangedMessage());
        }

        public void DeleteTagGroup(int groupId)
        {
            _repository.DeleteTagGroup(groupId);
            _messageBus?.Publish(new TagListChangedMessage());
        }

        public string GetTagColorByName(string tagName) => _repository.GetTagColorByName(tagName);

        #endregion

        #region 异步方法实现 (调用 Repository)

        public async Task<IEnumerable<ITagGroup>> GetTagGroupsAsync() => await _repository.GetTagGroupsAsync();
        public async Task<IEnumerable<ITag>> GetTagsByGroupAsync(int groupId) => await _repository.GetTagsByGroupAsync(groupId);
        public async Task<IEnumerable<ITag>> GetAllTagsAsync() => await _repository.GetAllTagsAsync();
        public async Task<IEnumerable<ITag>> GetUngroupedTagsAsync() => await _repository.GetUngroupedTagsAsync();
        public async Task<IEnumerable<ITag>> GetFileTagsAsync(string filePath) => await _repository.GetFileTagsAsync(filePath);

        public async Task AddTagToFileAsync(string filePath, int tagId)
        {
            await _repository.AddTagToFileAsync(filePath, tagId);
            _messageBus?.Publish(new FileTagsChangedMessage(filePath));
        }

        public async Task RemoveTagFromFileAsync(string filePath, int tagId)
        {
            await _repository.RemoveTagFromFileAsync(filePath, tagId);
            _messageBus?.Publish(new FileTagsChangedMessage(filePath));
        }

        public async Task<int> AddTagAsync(int groupId, string name, string color = null)
        {
            var id = await _repository.AddTagAsync(groupId, name, color);
            _messageBus?.Publish(new TagListChangedMessage());
            return id;
        }

        public async Task<IEnumerable<string>> GetFilesByTagAsync(int tagId) => await _repository.GetFilesByTagAsync(tagId);
        public async Task<IEnumerable<string>> GetFilesByTagNameAsync(string tagName) => await _repository.GetFilesByTagNameAsync(tagName);
        public async Task<ITag> GetTagAsync(int tagId) => await _repository.GetTagAsync(tagId);

        public async Task RenameTagAsync(int tagId, string newName)
        {
            await _repository.RenameTagAsync(tagId, newName);
            _messageBus?.Publish(new TagListChangedMessage());
        }

        public async Task UpdateTagColorAsync(int tagId, string color)
        {
            await _repository.UpdateTagColorAsync(tagId, color);
            _messageBus?.Publish(new TagListChangedMessage());
        }

        public async Task UpdateTagGroupAsync(int tagId, int newGroupId)
        {
            await _repository.UpdateTagGroupAsync(tagId, newGroupId);
            _messageBus?.Publish(new TagListChangedMessage());
        }

        public async Task DeleteTagAsync(int tagId)
        {
            await _repository.DeleteTagAsync(tagId);
            _messageBus?.Publish(new TagListChangedMessage());
        }

        public async Task<int> AddTagGroupAsync(string name, string color = null)
        {
            var id = await _repository.AddTagGroupAsync(name, color);
            _messageBus?.Publish(new TagListChangedMessage());
            return id;
        }

        public async Task RenameTagGroupAsync(int groupId, string newName)
        {
            await _repository.RenameTagGroupAsync(groupId, newName);
            _messageBus?.Publish(new TagListChangedMessage());
        }

        public async Task DeleteTagGroupAsync(int groupId)
        {
            await _repository.DeleteTagGroupAsync(groupId);
            _messageBus?.Publish(new TagListChangedMessage());
        }

        public async Task<string> GetTagColorByNameAsync(string tagName) => await _repository.GetTagColorByNameAsync(tagName);

        #endregion
    }
}
