using System.Collections.Generic;
using YiboFile.Models;

namespace YiboFile.ViewModels.Messaging.Messages
{
    /// <summary>
    /// 请求添加项到收藏夹
    /// </summary>
    public class AddFavoriteRequestMessage
    {
        public List<FileSystemItem> Items { get; }
        public int GroupId { get; }
        public AddFavoriteRequestMessage(List<FileSystemItem> items, int groupId = 1)
        {
            Items = items;
            GroupId = groupId;
        }
    }

    /// <summary>
    /// 请求创建收藏分组
    /// </summary>
    public class CreateFavoriteGroupRequestMessage
    {
        public string Name { get; }
        public List<FileSystemItem> InitialItems { get; }
        public CreateFavoriteGroupRequestMessage(string name, List<FileSystemItem> initialItems = null) { Name = name; InitialItems = initialItems; }
    }

    /// <summary>
    /// 收藏夹更新通知
    /// </summary>
    public class FavoritesUpdatedMessage { }
}
