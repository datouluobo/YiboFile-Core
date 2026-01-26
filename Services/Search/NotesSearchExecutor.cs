using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace YiboFile.Services.Search
{
    /// <summary>
    /// 备注搜索执行器
    /// 负责执行备注搜索操作
    /// </summary>
    public class NotesSearchExecutor
    {
        /// <summary>
        /// 执行备注搜索
        /// </summary>
        /// <param name="keyword">搜索关键词</param>
        /// <param name="getNotesFromDb">从数据库获取备注搜索结果的函数</param>
        /// <param name="resultPaths">结果路径集合（用于去重和合并）</param>
        /// <returns>备注匹配的路径集合</returns>
        public HashSet<string> Execute(
            string keyword,
            Func<string, List<string>> getNotesFromDb,
            HashSet<string> resultPaths)
        {
            var notesResultPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (string.IsNullOrEmpty(keyword) || getNotesFromDb == null)
            {
                return notesResultPaths;
            }

            try
            {
                Debug.WriteLine($"开始备注搜索，关键词: '{keyword}'");
                var notesResults = getNotesFromDb(keyword);
                Debug.WriteLine($"备注搜索返回结果: {notesResults?.Count ?? 0} 个");

                if (notesResults != null && notesResults.Count > 0)
                {
                    Debug.WriteLine($"备注搜索完成，找到 {notesResults.Count} 个文件");

                    foreach (var path in notesResults)
                    {
                        if (!string.IsNullOrEmpty(path))
                        {
                            Debug.WriteLine($"备注搜索结果文件: {path}");
                            notesResultPaths.Add(path);
                            if (resultPaths != null)
                            {
                                resultPaths.Add(path);
                            }
                        }
                    }
                    Debug.WriteLine($"备注搜索后，总结果数: {resultPaths?.Count ?? 0}");
                }
                else
                {
                    Debug.WriteLine($"备注搜索未找到匹配结果（关键词: '{keyword}'）");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"备注搜索失败: {ex.Message}\n{ex.StackTrace}");
            }

            return notesResultPaths;
        }
    }
}















