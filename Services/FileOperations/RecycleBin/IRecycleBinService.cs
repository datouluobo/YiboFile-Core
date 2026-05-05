using System;
using System.Collections.Generic;

namespace YiboFile.Services.FileOperations.RecycleBin
{
    public interface IRecycleBinService
    {
        /// <summary>将文件/目录发送到回收站</summary>
        bool Send(string path);

        /// <summary>从回收站还原文件/目录（按原始路径匹配）</summary>
        /// <returns>还原后的路径，失败返回 null</returns>
        string Restore(string originalPath);

        /// <summary>枚举回收站中的所有项目</summary>
        List<RecycleBinItem> ListItems();

        /// <summary>清空回收站</summary>
        bool Empty();
    }
}
