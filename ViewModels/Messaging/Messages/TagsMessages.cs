using System;
using System.Collections.Generic;
using YiboFile.Services.Features;

namespace YiboFile.ViewModels.Messaging.Messages
{
    // Tag Operation Requests
    public class AddTagRequestMessage
    {
        public int GroupId { get; }
        public string Name { get; }
        public string Color { get; }
        public AddTagRequestMessage(int groupId, string name, string color = null) { GroupId = groupId; Name = name; Color = color; }
    }

    public class DeleteTagRequestMessage
    {
        public int TagId { get; }
        public DeleteTagRequestMessage(int tagId) { TagId = tagId; }
    }

    public class UpdateTagColorRequestMessage
    {
        public int TagId { get; }
        public string Color { get; }
        public UpdateTagColorRequestMessage(int tagId, string color) { TagId = tagId; Color = color; }
    }

    public class RenameTagRequestMessage
    {
        public int TagId { get; }
        public string NewName { get; }
        public RenameTagRequestMessage(int tagId, string newName) { TagId = tagId; NewName = newName; }
    }

    // File Tag Operations
    public class AddTagToFileRequestMessage
    {
        public string FilePath { get; }
        public int TagId { get; }
        public AddTagToFileRequestMessage(string filePath, int tagId) { FilePath = filePath; TagId = tagId; }
    }

    public class RemoveTagFromFileRequestMessage
    {
        public string FilePath { get; }
        public int TagId { get; }
        public RemoveTagFromFileRequestMessage(string filePath, int tagId) { FilePath = filePath; TagId = tagId; }
    }

    // Notifications (Events)
    public class TagListChangedMessage { } // 标签列表发生变化的通用通知（添加、删除、重命名等）

    public class FileTagsChangedMessage
    {
        public string FilePath { get; }
        public FileTagsChangedMessage(string filePath) { FilePath = filePath; }
    }
}
