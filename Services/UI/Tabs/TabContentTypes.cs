namespace YiboFile.Services.Tabs
{
    /// <summary>
    /// Core 内置标签页内容类型 ID 常量。
    /// Pro/Ultra/第三方插件可自由定义新 ID，无需修改此文件。
    /// 
    /// 命名规范：
    /// - Core 内置类型使用小写字母（如 "settings", "about"）
    /// - 插件类型建议使用 "vendor.name" 格式（如 "yibofile.fts-results"）
    /// </summary>
    public static class TabContentTypes
    {
        // ── 文件浏览类 ──

        /// <summary>文件系统路径浏览</summary>
        public const string Path = "path";

        /// <summary>库浏览</summary>
        public const string Library = "library";

        /// <summary>标签浏览</summary>
        public const string Tag = "tag";

        /// <summary>搜索结果</summary>
        public const string Search = "search";

        // ── 功能面板类 ──

        /// <summary>设置页</summary>
        public const string Settings = "settings";

        /// <summary>关于页</summary>
        public const string About = "about";

        /// <summary>路径/库/标签管理</summary>
        public const string Management = "management";

        /// <summary>任务队列</summary>
        public const string Tasks = "tasks";

        /// <summary>备份管理</summary>
        public const string Backup = "backup";

        /// <summary>剪切板历史</summary>
        public const string Clipboard = "clipboard";

        /// <summary>
        /// 判断指定内容类型是否为文件浏览类（使用 FileBrowserControl 显示）。
        /// </summary>
        /// <param name="contentTypeId">内容类型 ID</param>
        /// <returns>如果是文件浏览类返回 true。</returns>
        public static bool IsFileBrowserType(string contentTypeId)
        {
            return contentTypeId == Path
                || contentTypeId == Library
                || contentTypeId == Tag
                || contentTypeId == Search;
        }
    }
}
