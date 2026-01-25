using System.Collections.Generic;
using System.Threading.Tasks;

namespace YiboFile.Services.Data.Repositories
{
    /// <summary>
    /// 文件备注存储库接口
    /// 抽象化数据访问层，便于测试和替换实现
    /// </summary>
    public interface INotesRepository
    {
        /// <summary>
        /// 获取文件的备注内容
        /// </summary>
        /// <param name="filePath">文件路径</param>
        /// <returns>备注内容（如果不存在则返回空字符串）</returns>
        string GetNotes(string filePath);

        /// <summary>
        /// 异步获取文件的备注内容
        /// </summary>
        /// <param name="filePath">文件路径</param>
        /// <returns>备注内容</returns>
        Task<string> GetNotesAsync(string filePath);

        /// <summary>
        /// 设置/更新文件的备注
        /// </summary>
        /// <param name="filePath">文件路径</param>
        /// <param name="notes">备注内容</param>
        void SetNotes(string filePath, string notes);

        /// <summary>
        /// 异步设置/更新文件的备注
        /// </summary>
        /// <param name="filePath">文件路径</param>
        /// <param name="notes">备注内容</param>
        Task SetNotesAsync(string filePath, string notes);

        /// <summary>
        /// 删除文件的备注
        /// </summary>
        /// <param name="filePath">文件路径</param>
        void DeleteNotes(string filePath);

        /// <summary>
        /// 异步删除文件的备注
        /// </summary>
        /// <param name="filePath">文件路径</param>
        Task DeleteNotesAsync(string filePath);

        /// <summary>
        /// 搜索包含指定文本的备注
        /// </summary>
        /// <param name="searchText">搜索关键词</param>
        /// <returns>匹配的文件路径列表</returns>
        List<string> SearchByNotes(string searchText);

        /// <summary>
        /// 异步搜索包含指定文本的备注
        /// </summary>
        /// <param name="searchText">搜索关键词</param>
        /// <returns>匹配的文件路径列表</returns>
        Task<List<string>> SearchByNotesAsync(string searchText);

        /// <summary>
        /// 检查文件是否有备注
        /// </summary>
        /// <param name="filePath">文件路径</param>
        /// <returns>是否有备注</returns>
        bool HasNotes(string filePath);

        /// <summary>
        /// 获取所有有备注的文件路径
        /// </summary>
        /// <returns>文件路径列表</returns>
        List<string> GetAllNotedFiles();

        /// <summary>
        /// 批量获取多个文件的备注（优化性能）
        /// </summary>
        /// <param name="filePaths">文件路径列表</param>
        /// <returns>路径-备注的字典</returns>
        Dictionary<string, string> GetNotesBatch(IEnumerable<string> filePaths);

        /// <summary>
        /// 异步批量获取多个文件的备注
        /// </summary>
        /// <param name="filePaths">文件路径列表</param>
        /// <returns>路径-备注的字典</returns>
        Task<Dictionary<string, string>> GetNotesBatchAsync(IEnumerable<string> filePaths);
    }
}
