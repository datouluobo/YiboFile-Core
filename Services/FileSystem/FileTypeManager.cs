using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Text;

namespace YiboFile
{
    /// <summary>
    /// 文件类型管理器
    /// </summary>
    public static class FileTypeManager
    {
        private static readonly Dictionary<string, FileTypeInfo> _fileTypes = new Dictionary<string, FileTypeInfo>
        {
            // 图片文件
            { ".jpg", new FileTypeInfo { Category = "图片", CanPreview = true, PreviewType = PreviewType.Image } },
            { ".jpeg", new FileTypeInfo { Category = "图片", CanPreview = true, PreviewType = PreviewType.Image } },
            { ".png", new FileTypeInfo { Category = "图片", CanPreview = true, PreviewType = PreviewType.Image } },
            { ".gif", new FileTypeInfo { Category = "图片", CanPreview = true, PreviewType = PreviewType.Image } },
            { ".bmp", new FileTypeInfo { Category = "图片", CanPreview = true, PreviewType = PreviewType.Image } },
            { ".tiff", new FileTypeInfo { Category = "图片", CanPreview = true, PreviewType = PreviewType.Image } },
            { ".tif", new FileTypeInfo { Category = "图片", CanPreview = true, PreviewType = PreviewType.Image } },
            { ".ico", new FileTypeInfo { Category = "图片", CanPreview = true, PreviewType = PreviewType.Image } },
            { ".webp", new FileTypeInfo { Category = "图片", CanPreview = true, PreviewType = PreviewType.Image } },
            { ".svg", new FileTypeInfo { Category = "图片", CanPreview = true, PreviewType = PreviewType.Image } },
            { ".psd", new FileTypeInfo { Category = "图片", CanPreview = true, PreviewType = PreviewType.Image } },
            { ".tga", new FileTypeInfo { Category = "图片", CanPreview = true, PreviewType = PreviewType.Image } },
            { ".blp", new FileTypeInfo { Category = "图片", CanPreview = true, PreviewType = PreviewType.Image } },
            { ".heic", new FileTypeInfo { Category = "图片", CanPreview = true, PreviewType = PreviewType.Image } },
            { ".heif", new FileTypeInfo { Category = "图片", CanPreview = true, PreviewType = PreviewType.Image } },
            { ".ai", new FileTypeInfo { Category = "图片", CanPreview = false, PreviewType = PreviewType.None } },

            // 文本文件
            { ".txt", new FileTypeInfo { Category = "文本", CanPreview = true, PreviewType = PreviewType.Text } },
            { ".log", new FileTypeInfo { Category = "文本", CanPreview = true, PreviewType = PreviewType.Text } },
            
            // 配置文件
            { ".ini", new FileTypeInfo { Category = "配置", CanPreview = true, PreviewType = PreviewType.Text } },
            { ".reg", new FileTypeInfo { Category = "配置", CanPreview = true, PreviewType = PreviewType.Text } },
            { ".cfg", new FileTypeInfo { Category = "配置", CanPreview = true, PreviewType = PreviewType.Text } },
            { ".conf", new FileTypeInfo { Category = "配置", CanPreview = true, PreviewType = PreviewType.Text } },
            { ".config", new FileTypeInfo { Category = "配置", CanPreview = true, PreviewType = PreviewType.Text } },
            { ".properties", new FileTypeInfo { Category = "配置", CanPreview = true, PreviewType = PreviewType.Text } },
            { ".env", new FileTypeInfo { Category = "配置", CanPreview = true, PreviewType = PreviewType.Text } },
            { ".yaml", new FileTypeInfo { Category = "配置", CanPreview = true, PreviewType = PreviewType.Text } },
            { ".yml", new FileTypeInfo { Category = "配置", CanPreview = true, PreviewType = PreviewType.Text } },
            { ".toml", new FileTypeInfo { Category = "配置", CanPreview = true, PreviewType = PreviewType.Text } },
            { ".toc", new FileTypeInfo { Category = "配置", CanPreview = true, PreviewType = PreviewType.Text } },
            
            // 数据文件
            { ".xml", new FileTypeInfo { Category = "数据", CanPreview = true, PreviewType = PreviewType.Text } },
            { ".json", new FileTypeInfo { Category = "数据", CanPreview = true, PreviewType = PreviewType.Text } },
            { ".csv", new FileTypeInfo { Category = "数据", CanPreview = true, PreviewType = PreviewType.Text } },
            { ".tsv", new FileTypeInfo { Category = "数据", CanPreview = true, PreviewType = PreviewType.Text } },
            
            // 文档文件
            { ".md", new FileTypeInfo { Category = "文档", CanPreview = true, PreviewType = PreviewType.Text } },
            { ".markdown", new FileTypeInfo { Category = "文档", CanPreview = true, PreviewType = PreviewType.Text } },
            { ".rst", new FileTypeInfo { Category = "文档", CanPreview = true, PreviewType = PreviewType.Text } },
            
            // 网页文件
            { ".html", new FileTypeInfo { Category = "网页", CanPreview = true, PreviewType = PreviewType.Text } },
            { ".htm", new FileTypeInfo { Category = "网页", CanPreview = true, PreviewType = PreviewType.Text } },
            { ".xhtml", new FileTypeInfo { Category = "网页", CanPreview = true, PreviewType = PreviewType.Text } },
            { ".css", new FileTypeInfo { Category = "样式", CanPreview = true, PreviewType = PreviewType.Text } },
            { ".scss", new FileTypeInfo { Category = "样式", CanPreview = true, PreviewType = PreviewType.Text } },
            { ".sass", new FileTypeInfo { Category = "样式", CanPreview = true, PreviewType = PreviewType.Text } },
            { ".less", new FileTypeInfo { Category = "样式", CanPreview = true, PreviewType = PreviewType.Text } },
            
            // 脚本文件
            { ".js", new FileTypeInfo { Category = "脚本", CanPreview = true, PreviewType = PreviewType.Text } },
            { ".jsx", new FileTypeInfo { Category = "脚本", CanPreview = true, PreviewType = PreviewType.Text } },
            { ".ts", new FileTypeInfo { Category = "脚本", CanPreview = true, PreviewType = PreviewType.Text } },
            { ".tsx", new FileTypeInfo { Category = "脚本", CanPreview = true, PreviewType = PreviewType.Text } },
            { ".vue", new FileTypeInfo { Category = "脚本", CanPreview = true, PreviewType = PreviewType.Text } },
            { ".sh", new FileTypeInfo { Category = "脚本", CanPreview = true, PreviewType = PreviewType.Text } },
            { ".bash", new FileTypeInfo { Category = "脚本", CanPreview = true, PreviewType = PreviewType.Text } },
            { ".ps1", new FileTypeInfo { Category = "脚本", CanPreview = true, PreviewType = PreviewType.Text } },
            { ".bat", new FileTypeInfo { Category = "脚本", CanPreview = true, PreviewType = PreviewType.Text } },
            { ".cmd", new FileTypeInfo { Category = "脚本", CanPreview = true, PreviewType = PreviewType.Text } },
            { ".vbs", new FileTypeInfo { Category = "脚本", CanPreview = true, PreviewType = PreviewType.Text } },
            
            // 代码文件
            { ".cs", new FileTypeInfo { Category = "代码", CanPreview = true, PreviewType = PreviewType.Text } },
            { ".cpp", new FileTypeInfo { Category = "代码", CanPreview = true, PreviewType = PreviewType.Text } },
            { ".cxx", new FileTypeInfo { Category = "代码", CanPreview = true, PreviewType = PreviewType.Text } },
            { ".cc", new FileTypeInfo { Category = "代码", CanPreview = true, PreviewType = PreviewType.Text } },
            { ".c", new FileTypeInfo { Category = "代码", CanPreview = true, PreviewType = PreviewType.Text } },
            { ".h", new FileTypeInfo { Category = "代码", CanPreview = true, PreviewType = PreviewType.Text } },
            { ".hpp", new FileTypeInfo { Category = "代码", CanPreview = true, PreviewType = PreviewType.Text } },
            { ".hxx", new FileTypeInfo { Category = "代码", CanPreview = true, PreviewType = PreviewType.Text } },
            { ".py", new FileTypeInfo { Category = "代码", CanPreview = true, PreviewType = PreviewType.Text } },
            { ".pyw", new FileTypeInfo { Category = "代码", CanPreview = true, PreviewType = PreviewType.Text } },
            { ".java", new FileTypeInfo { Category = "代码", CanPreview = true, PreviewType = PreviewType.Text } },
            { ".kt", new FileTypeInfo { Category = "代码", CanPreview = true, PreviewType = PreviewType.Text } },
            { ".kts", new FileTypeInfo { Category = "代码", CanPreview = true, PreviewType = PreviewType.Text } },
            { ".php", new FileTypeInfo { Category = "代码", CanPreview = true, PreviewType = PreviewType.Text } },
            { ".rb", new FileTypeInfo { Category = "代码", CanPreview = true, PreviewType = PreviewType.Text } },
            { ".go", new FileTypeInfo { Category = "代码", CanPreview = true, PreviewType = PreviewType.Text } },
            { ".rs", new FileTypeInfo { Category = "代码", CanPreview = true, PreviewType = PreviewType.Text } },
            { ".swift", new FileTypeInfo { Category = "代码", CanPreview = true, PreviewType = PreviewType.Text } },
            { ".dart", new FileTypeInfo { Category = "代码", CanPreview = true, PreviewType = PreviewType.Text } },
            { ".lua", new FileTypeInfo { Category = "代码", CanPreview = true, PreviewType = PreviewType.Text } },
            { ".pl", new FileTypeInfo { Category = "代码", CanPreview = true, PreviewType = PreviewType.Text } },
            { ".pm", new FileTypeInfo { Category = "代码", CanPreview = true, PreviewType = PreviewType.Text } },
            { ".r", new FileTypeInfo { Category = "代码", CanPreview = true, PreviewType = PreviewType.Text } },
            { ".m", new FileTypeInfo { Category = "代码", CanPreview = true, PreviewType = PreviewType.Text } },
            { ".mm", new FileTypeInfo { Category = "代码", CanPreview = true, PreviewType = PreviewType.Text } },
            { ".scala", new FileTypeInfo { Category = "代码", CanPreview = true, PreviewType = PreviewType.Text } },
            { ".clj", new FileTypeInfo { Category = "代码", CanPreview = true, PreviewType = PreviewType.Text } },
            { ".cljs", new FileTypeInfo { Category = "代码", CanPreview = true, PreviewType = PreviewType.Text } },
            { ".fs", new FileTypeInfo { Category = "代码", CanPreview = true, PreviewType = PreviewType.Text } },
            { ".fsx", new FileTypeInfo { Category = "代码", CanPreview = true, PreviewType = PreviewType.Text } },
            { ".ml", new FileTypeInfo { Category = "代码", CanPreview = true, PreviewType = PreviewType.Text } },
            { ".mli", new FileTypeInfo { Category = "代码", CanPreview = true, PreviewType = PreviewType.Text } },
            { ".sql", new FileTypeInfo { Category = "代码", CanPreview = true, PreviewType = PreviewType.Text } },
            { ".asm", new FileTypeInfo { Category = "代码", CanPreview = true, PreviewType = PreviewType.Text } },
            { ".s", new FileTypeInfo { Category = "代码", CanPreview = true, PreviewType = PreviewType.Text } },

            // 视频文件
            { ".mp4", new FileTypeInfo { Category = "视频", CanPreview = true, PreviewType = PreviewType.Video } },
            { ".avi", new FileTypeInfo { Category = "视频", CanPreview = true, PreviewType = PreviewType.Video } },
            { ".mkv", new FileTypeInfo { Category = "视频", CanPreview = true, PreviewType = PreviewType.Video } },
            { ".mov", new FileTypeInfo { Category = "视频", CanPreview = true, PreviewType = PreviewType.Video } },
            { ".wmv", new FileTypeInfo { Category = "视频", CanPreview = true, PreviewType = PreviewType.Video } },
            { ".flv", new FileTypeInfo { Category = "视频", CanPreview = true, PreviewType = PreviewType.Video } },
            { ".webm", new FileTypeInfo { Category = "视频", CanPreview = true, PreviewType = PreviewType.Video } },
            { ".m4v", new FileTypeInfo { Category = "视频", CanPreview = true, PreviewType = PreviewType.Video } },
            { ".mpg", new FileTypeInfo { Category = "视频", CanPreview = true, PreviewType = PreviewType.Video } },
            { ".mpeg", new FileTypeInfo { Category = "视频", CanPreview = true, PreviewType = PreviewType.Video } },
            { ".3gp", new FileTypeInfo { Category = "视频", CanPreview = false, PreviewType = PreviewType.Video } },
            { ".3g2", new FileTypeInfo { Category = "视频", CanPreview = false, PreviewType = PreviewType.Video } },
            { ".rm", new FileTypeInfo { Category = "视频", CanPreview = true, PreviewType = PreviewType.Video } },
            { ".rmvb", new FileTypeInfo { Category = "视频", CanPreview = true, PreviewType = PreviewType.Video } },
            { ".vob", new FileTypeInfo { Category = "视频", CanPreview = false, PreviewType = PreviewType.Video } },
            { ".asf", new FileTypeInfo { Category = "视频", CanPreview = false, PreviewType = PreviewType.Video } },

            // 音频文件
            { ".mp3", new FileTypeInfo { Category = "音频", CanPreview = true, PreviewType = PreviewType.Audio } },
            { ".wav", new FileTypeInfo { Category = "音频", CanPreview = true, PreviewType = PreviewType.Audio } },
            { ".flac", new FileTypeInfo { Category = "音频", CanPreview = true, PreviewType = PreviewType.Audio } },
            { ".aac", new FileTypeInfo { Category = "音频", CanPreview = true, PreviewType = PreviewType.Audio } },
            { ".ogg", new FileTypeInfo { Category = "音频", CanPreview = true, PreviewType = PreviewType.Audio } },
            { ".wma", new FileTypeInfo { Category = "音频", CanPreview = true, PreviewType = PreviewType.Audio } },
            { ".m4a", new FileTypeInfo { Category = "音频", CanPreview = true, PreviewType = PreviewType.Audio } },
            { ".opus", new FileTypeInfo { Category = "音频", CanPreview = true, PreviewType = PreviewType.Audio } },
            { ".ape", new FileTypeInfo { Category = "音频", CanPreview = true, PreviewType = PreviewType.Audio } },
            { ".wv", new FileTypeInfo { Category = "音频", CanPreview = true, PreviewType = PreviewType.Audio } },
            { ".ac3", new FileTypeInfo { Category = "音频", CanPreview = true, PreviewType = PreviewType.Audio } },
            { ".dts", new FileTypeInfo { Category = "音频", CanPreview = true, PreviewType = PreviewType.Audio } },
            { ".amr", new FileTypeInfo { Category = "音频", CanPreview = true, PreviewType = PreviewType.Audio } },
            { ".au", new FileTypeInfo { Category = "音频", CanPreview = true, PreviewType = PreviewType.Audio } },
            { ".ra", new FileTypeInfo { Category = "音频", CanPreview = true, PreviewType = PreviewType.Audio } },
            { ".mid", new FileTypeInfo { Category = "音频", CanPreview = true, PreviewType = PreviewType.Audio } },
            { ".midi", new FileTypeInfo { Category = "音频", CanPreview = true, PreviewType = PreviewType.Audio } },

            // 文档文件
            { ".pdf", new FileTypeInfo { Category = "文档", CanPreview = true, PreviewType = PreviewType.Document } },
            { ".doc", new FileTypeInfo { Category = "文档", CanPreview = true, PreviewType = PreviewType.Document } },
            { ".docx", new FileTypeInfo { Category = "文档", CanPreview = true, PreviewType = PreviewType.Document } },
            { ".docm", new FileTypeInfo { Category = "文档", CanPreview = true, PreviewType = PreviewType.Document } },
            { ".rtf", new FileTypeInfo { Category = "文档", CanPreview = true, PreviewType = PreviewType.Document } },
            { ".chm", new FileTypeInfo { Category = "帮助", CanPreview = true, PreviewType = PreviewType.Document } },  // CHM帮助文件
            { ".odt", new FileTypeInfo { Category = "文档", CanPreview = false, PreviewType = PreviewType.Document } },
            { ".xls", new FileTypeInfo { Category = "表格", CanPreview = true, PreviewType = PreviewType.Document } },
            { ".xlsx", new FileTypeInfo { Category = "表格", CanPreview = true, PreviewType = PreviewType.Document } },
            { ".xlsm", new FileTypeInfo { Category = "表格", CanPreview = true, PreviewType = PreviewType.Document } },
            { ".ods", new FileTypeInfo { Category = "表格", CanPreview = false, PreviewType = PreviewType.Document } },
            { ".ppt", new FileTypeInfo { Category = "演示", CanPreview = true, PreviewType = PreviewType.Document } },
            { ".pptx", new FileTypeInfo { Category = "演示", CanPreview = true, PreviewType = PreviewType.Document } },
            { ".pptm", new FileTypeInfo { Category = "演示", CanPreview = true, PreviewType = PreviewType.Document } },
            { ".odp", new FileTypeInfo { Category = "演示", CanPreview = false, PreviewType = PreviewType.Document } },
            { ".pages", new FileTypeInfo { Category = "文档", CanPreview = false, PreviewType = PreviewType.Document } },
            { ".numbers", new FileTypeInfo { Category = "表格", CanPreview = false, PreviewType = PreviewType.Document } },
            { ".key", new FileTypeInfo { Category = "演示", CanPreview = false, PreviewType = PreviewType.Document } },

            // 压缩文件
            { ".zip", new FileTypeInfo { Category = "压缩", CanPreview = true, PreviewType = PreviewType.Archive } },
            { ".rar", new FileTypeInfo { Category = "压缩", CanPreview = true, PreviewType = PreviewType.Archive } },
            { ".7z", new FileTypeInfo { Category = "压缩", CanPreview = true, PreviewType = PreviewType.Archive } },
            { ".tar", new FileTypeInfo { Category = "压缩", CanPreview = false, PreviewType = PreviewType.Archive } },
            { ".gz", new FileTypeInfo { Category = "压缩", CanPreview = false, PreviewType = PreviewType.Archive } },
            { ".bz2", new FileTypeInfo { Category = "压缩", CanPreview = false, PreviewType = PreviewType.Archive } },
            { ".xz", new FileTypeInfo { Category = "压缩", CanPreview = false, PreviewType = PreviewType.Archive } },
            { ".lz", new FileTypeInfo { Category = "压缩", CanPreview = false, PreviewType = PreviewType.Archive } },
            { ".lzma", new FileTypeInfo { Category = "压缩", CanPreview = false, PreviewType = PreviewType.Archive } },
            { ".cab", new FileTypeInfo { Category = "压缩", CanPreview = false, PreviewType = PreviewType.Archive } },
            { ".arj", new FileTypeInfo { Category = "压缩", CanPreview = false, PreviewType = PreviewType.Archive } },
            { ".z", new FileTypeInfo { Category = "压缩", CanPreview = false, PreviewType = PreviewType.Archive } },
            { ".lzh", new FileTypeInfo { Category = "压缩", CanPreview = false, PreviewType = PreviewType.Archive } },
            { ".ace", new FileTypeInfo { Category = "压缩", CanPreview = false, PreviewType = PreviewType.Archive } },
            { ".iso", new FileTypeInfo { Category = "镜像", CanPreview = false, PreviewType = PreviewType.Archive } },
            { ".dmg", new FileTypeInfo { Category = "镜像", CanPreview = false, PreviewType = PreviewType.Archive } },
            { ".img", new FileTypeInfo { Category = "镜像", CanPreview = false, PreviewType = PreviewType.Archive } },

            // 可执行文件
            { ".exe", new FileTypeInfo { Category = "程序", CanPreview = false, PreviewType = PreviewType.Executable } },
            { ".msi", new FileTypeInfo { Category = "程序", CanPreview = false, PreviewType = PreviewType.Executable } },
            { ".dll", new FileTypeInfo { Category = "程序", CanPreview = false, PreviewType = PreviewType.Executable } },
            { ".sys", new FileTypeInfo { Category = "系统", CanPreview = false, PreviewType = PreviewType.Executable } },
            { ".drv", new FileTypeInfo { Category = "系统", CanPreview = false, PreviewType = PreviewType.Executable } },
            { ".app", new FileTypeInfo { Category = "程序", CanPreview = false, PreviewType = PreviewType.Executable } },
            { ".appx", new FileTypeInfo { Category = "程序", CanPreview = false, PreviewType = PreviewType.Executable } },
            { ".apk", new FileTypeInfo { Category = "程序", CanPreview = false, PreviewType = PreviewType.Executable } },
            { ".deb", new FileTypeInfo { Category = "程序", CanPreview = false, PreviewType = PreviewType.Executable } },
            { ".rpm", new FileTypeInfo { Category = "程序", CanPreview = false, PreviewType = PreviewType.Executable } },
            
            // 快捷方式文件
            { ".lnk", new FileTypeInfo { Category = "快捷方式", CanPreview = true, PreviewType = PreviewType.Shortcut } },
            
            // CAD 文件
            { ".dwg", new FileTypeInfo { Category = "CAD", CanPreview = true, PreviewType = PreviewType.Document } },
            { ".dxf", new FileTypeInfo { Category = "CAD", CanPreview = true, PreviewType = PreviewType.Document } },
        };

        public static FileTypeInfo GetFileTypeInfo(string filePath)
        {
            var extension = Path.GetExtension(filePath).ToLower();
            if (_fileTypes.TryGetValue(extension, out var info))
            {
                return info;
            }
            return new FileTypeInfo { Category = "未知", CanPreview = false, PreviewType = PreviewType.None };
        }

        public static bool CanPreview(string filePath)
        {
            return GetFileTypeInfo(filePath).CanPreview;
        }

        public static PreviewType GetPreviewType(string filePath)
        {
            return GetFileTypeInfo(filePath).PreviewType;
        }

        public static string GetFileCategory(string filePath)
        {
            return GetFileTypeInfo(filePath).Category;
        }


        // 所有预览方法已移至Previews目录
    }

    public class FileTypeInfo
    {
        public string Category { get; set; }
        public bool CanPreview { get; set; }
        public PreviewType PreviewType { get; set; }
    }

    public enum PreviewType
    {
        None,
        Image,
        Text,
        Video,
        Audio,
        Document,
        Archive,
        Executable,
        Shortcut
    }
}

