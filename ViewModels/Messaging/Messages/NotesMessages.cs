namespace YiboFile.ViewModels.Messaging.Messages
{
    /// <summary>
    /// 获取备注请求
    /// </summary>
    public class GetNotesRequestMessage
    {
        public string FilePath { get; }

        public GetNotesRequestMessage(string filePath)
        {
            FilePath = filePath;
        }
    }

    /// <summary>
    /// 保存备注请求
    /// </summary>
    public class SaveNotesRequestMessage
    {
        public string FilePath { get; }
        public string Notes { get; }

        public SaveNotesRequestMessage(string filePath, string notes)
        {
            FilePath = filePath;
            Notes = notes;
        }
    }

    /// <summary>
    /// 删除备注请求
    /// </summary>
    public class DeleteNotesRequestMessage
    {
        public string FilePath { get; }

        public DeleteNotesRequestMessage(string filePath)
        {
            FilePath = filePath;
        }
    }

    /// <summary>
    /// 搜索备注请求
    /// </summary>
    public class SearchNotesRequestMessage
    {
        public string Keyword { get; }

        public SearchNotesRequestMessage(string keyword)
        {
            Keyword = keyword;
        }
    }

    /// <summary>
    /// 备注已更新通知
    /// </summary>
    public class NotesUpdatedMessage
    {
        public string FilePath { get; }
        public string Notes { get; }
        public string Summary { get; }  // 摘要，用于列表显示

        public NotesUpdatedMessage(string filePath, string notes, string summary = null)
        {
            FilePath = filePath;
            Notes = notes;
            Summary = summary ?? (notes?.Length > 100 ? notes.Substring(0, 100) + "..." : notes);
        }
    }

    /// <summary>
    /// 备注加载完成通知
    /// </summary>
    public class NotesLoadedMessage
    {
        public string FilePath { get; }
        public string Notes { get; }

        public NotesLoadedMessage(string filePath, string notes)
        {
            FilePath = filePath;
            Notes = notes;
        }
    }
}
