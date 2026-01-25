using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using YiboFile.Services.Core;
using YiboFile.Services.Data.Repositories;

namespace YiboFile.Services.Features.FileNotes
{
    /// <summary>
    /// 文件备注服务（业务逻辑层）
    /// 封装备注的业务规则，如自动保存、摘要生成等
    /// </summary>
    public class NotesService : INotesService
    {
        private readonly INotesRepository _repository;

        /// <summary>
        /// 备注更新事件
        /// </summary>
        public event EventHandler<NotesUpdatedEventArgs> NotesUpdated;

        public NotesService(INotesRepository repository)
        {
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        }

        #region 基本操作

        /// <summary>
        /// 获取文件备注
        /// </summary>
        public string GetNotes(string filePath)
        {
            return _repository.GetNotes(filePath);
        }

        /// <summary>
        /// 异步获取文件备注
        /// </summary>
        public async Task<string> GetNotesAsync(string filePath)
        {
            return await _repository.GetNotesAsync(filePath);
        }

        /// <summary>
        /// 保存文件备注
        /// </summary>
        public void SaveNotes(string filePath, string notes)
        {
            _repository.SetNotes(filePath, notes);
            OnNotesUpdated(filePath, notes);
        }

        /// <summary>
        /// 异步保存文件备注
        /// </summary>
        public async Task SaveNotesAsync(string filePath, string notes)
        {
            await _repository.SetNotesAsync(filePath, notes);
            OnNotesUpdated(filePath, notes);
        }

        /// <summary>
        /// 删除文件备注
        /// </summary>
        public void DeleteNotes(string filePath)
        {
            _repository.DeleteNotes(filePath);
            OnNotesUpdated(filePath, null);
        }

        /// <summary>
        /// 异步删除文件备注
        /// </summary>
        public async Task DeleteNotesAsync(string filePath)
        {
            await _repository.DeleteNotesAsync(filePath);
            OnNotesUpdated(filePath, null);
        }

        #endregion

        #region 搜索功能

        /// <summary>
        /// 搜索包含指定文本的备注
        /// </summary>
        public List<string> Search(string keyword)
        {
            return _repository.SearchByNotes(keyword);
        }

        /// <summary>
        /// 异步搜索
        /// </summary>
        public async Task<List<string>> SearchAsync(string keyword)
        {
            return await _repository.SearchByNotesAsync(keyword);
        }

        #endregion

        #region 业务辅助方法

        /// <summary>
        /// 获取备注摘要（用于列表显示）
        /// </summary>
        /// <param name="notes">完整备注内容</param>
        /// <param name="maxLength">最大长度</param>
        /// <returns>截断后的摘要</returns>
        public static string GetSummary(string notes, int maxLength = 100)
        {
            if (string.IsNullOrEmpty(notes)) return string.Empty;

            // 取第一行
            var lines = notes.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            var firstLine = lines.Length > 0 ? lines[0] : notes;

            if (firstLine.Length <= maxLength)
                return firstLine;

            return firstLine.Substring(0, maxLength) + "...";
        }

        /// <summary>
        /// 检查文件是否有备注
        /// </summary>
        public bool HasNotes(string filePath)
        {
            return _repository.HasNotes(filePath);
        }

        /// <summary>
        /// 批量获取备注（优化性能）
        /// </summary>
        public Dictionary<string, string> GetNotesBatch(IEnumerable<string> filePaths)
        {
            return _repository.GetNotesBatch(filePaths);
        }

        /// <summary>
        /// 异步批量获取备注
        /// </summary>
        public async Task<Dictionary<string, string>> GetNotesBatchAsync(IEnumerable<string> filePaths)
        {
            return await _repository.GetNotesBatchAsync(filePaths);
        }

        #endregion

        #region 事件触发

        protected virtual void OnNotesUpdated(string filePath, string notes)
        {
            NotesUpdated?.Invoke(this, new NotesUpdatedEventArgs(filePath, notes));
        }

        #endregion
    }

    /// <summary>
    /// 备注服务接口
    /// </summary>
    public interface INotesService
    {
        event EventHandler<NotesUpdatedEventArgs> NotesUpdated;

        string GetNotes(string filePath);
        Task<string> GetNotesAsync(string filePath);
        void SaveNotes(string filePath, string notes);
        Task SaveNotesAsync(string filePath, string notes);
        void DeleteNotes(string filePath);
        Task DeleteNotesAsync(string filePath);
        List<string> Search(string keyword);
        Task<List<string>> SearchAsync(string keyword);
        bool HasNotes(string filePath);
        Dictionary<string, string> GetNotesBatch(IEnumerable<string> filePaths);
        Task<Dictionary<string, string>> GetNotesBatchAsync(IEnumerable<string> filePaths);
    }

    /// <summary>
    /// 备注更新事件参数
    /// </summary>
    public class NotesUpdatedEventArgs : EventArgs
    {
        public string FilePath { get; }
        public string Notes { get; }
        public bool IsDeleted => Notes == null;

        public NotesUpdatedEventArgs(string filePath, string notes)
        {
            FilePath = filePath;
            Notes = notes;
        }
    }
}
