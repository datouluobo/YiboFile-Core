using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using YiboFile.Services.Features.FileNotes;
using YiboFile.ViewModels.Messaging;
using YiboFile.ViewModels.Messaging.Messages;

namespace YiboFile.ViewModels.Modules
{
    /// <summary>
    /// 备注模块 (MVVM 架构)
    /// 处理备注的获取、保存、删除和搜索
    /// 通过消息总线与 UI 层通信
    /// </summary>
    public class NotesModule : ModuleBase
    {
        private readonly INotesService _notesService;

        public override string Name => "NotesModule";

        /// <summary>
        /// 当前正在编辑的文件路径
        /// </summary>
        public string CurrentFilePath { get; private set; }

        /// <summary>
        /// 当前文件的备注内容
        /// </summary>
        public string CurrentNotes { get; private set; }

        public NotesModule(IMessageBus messageBus, INotesService notesService)
            : base(messageBus)
        {
            _notesService = notesService ?? throw new ArgumentNullException(nameof(notesService));
        }

        protected override void OnInitialize()
        {
            // 订阅消息
            Subscribe<GetNotesRequestMessage>(OnGetNotesRequest);
            Subscribe<SaveNotesRequestMessage>(OnSaveNotesRequest);
            Subscribe<DeleteNotesRequestMessage>(OnDeleteNotesRequest);
            Subscribe<SearchNotesRequestMessage>(OnSearchNotesRequest);

            // 订阅服务事件
            _notesService.NotesUpdated += OnServiceNotesUpdated;

            // 订阅文件选择变化
            Subscribe<FileSelectionChangedMessage>(OnFileSelectionChanged);
        }

        #region 消息处理

        private async void OnGetNotesRequest(GetNotesRequestMessage message)
        {
            if (string.IsNullOrEmpty(message.FilePath)) return;

            var notes = await _notesService.GetNotesAsync(message.FilePath);
            CurrentFilePath = message.FilePath;
            CurrentNotes = notes;

            Publish(new NotesLoadedMessage(message.FilePath, notes));
        }

        private async void OnSaveNotesRequest(SaveNotesRequestMessage message)
        {
            if (string.IsNullOrEmpty(message.FilePath)) return;

            await _notesService.SaveNotesAsync(message.FilePath, message.Notes);
            CurrentNotes = message.Notes;

            // 发布更新通知
            var summary = NotesService.GetSummary(message.Notes);
            Publish(new NotesUpdatedMessage(message.FilePath, message.Notes, summary));
        }

        private async void OnDeleteNotesRequest(DeleteNotesRequestMessage message)
        {
            if (string.IsNullOrEmpty(message.FilePath)) return;

            await _notesService.DeleteNotesAsync(message.FilePath);

            if (message.FilePath == CurrentFilePath)
            {
                CurrentNotes = null;
            }

            Publish(new NotesUpdatedMessage(message.FilePath, null, null));
        }

        private async void OnSearchNotesRequest(SearchNotesRequestMessage message)
        {
            if (string.IsNullOrEmpty(message.Keyword)) return;

            var results = await _notesService.SearchAsync(message.Keyword);
            // 搜索结果可以通过专门的消息发送，或结合现有的搜索架构
        }

        private async void OnFileSelectionChanged(FileSelectionChangedMessage message)
        {
            // 当文件选择变化时，自动加载备注
            if (message.SelectedItems != null && message.SelectedItems.Count == 1)
            {
                var item = message.SelectedItems[0] as Models.FileSystemItem;
                if (item != null && !string.IsNullOrEmpty(item.Path))
                {
                    var notes = await _notesService.GetNotesAsync(item.Path);
                    CurrentFilePath = item.Path;
                    CurrentNotes = notes;

                    Publish(new NotesLoadedMessage(item.Path, notes));
                }
            }
        }

        private void OnServiceNotesUpdated(object sender, NotesUpdatedEventArgs e)
        {
            // 服务层事件转发为消息
            var summary = e.IsDeleted ? null : NotesService.GetSummary(e.Notes);
            Publish(new NotesUpdatedMessage(e.FilePath, e.Notes, summary));
        }

        #endregion

        #region 公开方法 (供直接调用)

        /// <summary>
        /// 加载指定文件的备注
        /// </summary>
        public async Task<string> LoadNotesAsync(string filePath)
        {
            Publish(new GetNotesRequestMessage(filePath));
            return await _notesService.GetNotesAsync(filePath);
        }

        /// <summary>
        /// 保存备注
        /// </summary>
        public async Task SaveNotesAsync(string filePath, string notes)
        {
            await _notesService.SaveNotesAsync(filePath, notes);
        }

        /// <summary>
        /// 删除备注
        /// </summary>
        public async Task DeleteNotesAsync(string filePath)
        {
            await _notesService.DeleteNotesAsync(filePath);
        }

        /// <summary>
        /// 获取备注摘要
        /// </summary>
        public string GetSummary(string notes)
        {
            return NotesService.GetSummary(notes);
        }

        /// <summary>
        /// 批量获取备注（优化列表加载）
        /// </summary>
        public async Task<Dictionary<string, string>> GetNotesBatchAsync(IEnumerable<string> filePaths)
        {
            return await _notesService.GetNotesBatchAsync(filePaths);
        }

        #endregion
    }
}
