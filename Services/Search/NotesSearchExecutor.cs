using System;
using System.Collections.Generic;

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
                var notesResults = getNotesFromDb(keyword);

                if (notesResults != null && notesResults.Count > 0)
                {
                    foreach (var path in notesResults)
                    {
                        if (!string.IsNullOrEmpty(path))
                        {
                            notesResultPaths.Add(path);
                            if (resultPaths != null)
                            {
                                resultPaths.Add(path);
                            }
                        }
                    }
                }
            }
            catch (Exception)
            {
            }

            return notesResultPaths;
        }
    }
}















