using System;
using System.Threading.Tasks;
using System.Linq;
using YiboFile.Services.Features;
using YiboFile.ViewModels.Messaging;
using YiboFile.ViewModels.Messaging.Messages;

namespace YiboFile.ViewModels.Modules
{
    public class TagsModule : ModuleBase
    {
        private readonly ITagService _tagService;

        public override string Name => "TagsModule";

        public TagsModule(IMessageBus messageBus, ITagService tagService) : base(messageBus)
        {
            _tagService = tagService ?? throw new ArgumentNullException(nameof(tagService));
        }

        protected override void OnInitialize()
        {
            Subscribe<AddTagRequestMessage>(OnAddTagRequest);
            Subscribe<DeleteTagRequestMessage>(OnDeleteTagRequest);
            Subscribe<UpdateTagColorRequestMessage>(OnUpdateTagColorRequest);
            Subscribe<RenameTagRequestMessage>(OnRenameTagRequest);
            Subscribe<AddTagToFileRequestMessage>(OnAddTagToFileRequest);
            Subscribe<RemoveTagFromFileRequestMessage>(OnRemoveTagFromFileRequest);
            Subscribe<ToggleTagRequestMessage>(OnToggleTagRequest);
            Subscribe<AddTagToFilesRequestMessage>(OnAddTagToFilesRequest);
        }

        private async void OnAddTagToFilesRequest(AddTagToFilesRequestMessage msg)
        {
            if (msg.FilePaths == null || msg.FilePaths.Count == 0) return;
            foreach (var path in msg.FilePaths)
            {
                await _tagService.AddTagToFileAsync(path, msg.TagId);
                Publish(new FileTagsChangedMessage(path));
            }
        }

        private async void OnToggleTagRequest(ToggleTagRequestMessage msg)
        {
            if (msg.FilePaths == null || msg.FilePaths.Count == 0) return;

            foreach (var path in msg.FilePaths)
            {
                var fileTags = await _tagService.GetFileTagsAsync(path);
                if (fileTags.Any(t => t.Id == msg.TagId))
                {
                    await _tagService.RemoveTagFromFileAsync(path, msg.TagId);
                }
                else
                {
                    await _tagService.AddTagToFileAsync(path, msg.TagId);
                }
                Publish(new FileTagsChangedMessage(path));
            }
        }

        private async void OnAddTagRequest(AddTagRequestMessage msg)
        {
            await _tagService.AddTagAsync(msg.GroupId, msg.Name, msg.Color);
            Publish(new TagListChangedMessage());
        }

        private async void OnDeleteTagRequest(DeleteTagRequestMessage msg)
        {
            await _tagService.DeleteTagAsync(msg.TagId);
            Publish(new TagListChangedMessage());
        }

        private async void OnUpdateTagColorRequest(UpdateTagColorRequestMessage msg)
        {
            await _tagService.UpdateTagColorAsync(msg.TagId, msg.Color);
            Publish(new TagListChangedMessage());
        }

        private async void OnRenameTagRequest(RenameTagRequestMessage msg)
        {
            await _tagService.RenameTagAsync(msg.TagId, msg.NewName);
            Publish(new TagListChangedMessage());
        }

        private async void OnAddTagToFileRequest(AddTagToFileRequestMessage msg)
        {
            await _tagService.AddTagToFileAsync(msg.FilePath, msg.TagId);
            Publish(new FileTagsChangedMessage(msg.FilePath));
        }

        private async void OnRemoveTagFromFileRequest(RemoveTagFromFileRequestMessage msg)
        {
            await _tagService.RemoveTagFromFileAsync(msg.FilePath, msg.TagId);
            Publish(new FileTagsChangedMessage(msg.FilePath));
        }

        // Public methods for direct invocation if needed (e.g. from ViewModel)
        public async Task AddTagAsync(int groupId, string name, string color = null)
        {
            await _tagService.AddTagAsync(groupId, name, color);
            Publish(new TagListChangedMessage());
        }
    }
}
