using System;
using System.IO;

namespace YiboFile.Services.Core
{
    public enum ProtocolType
    {
        Local,          // Standard file path
        Archive,        // zip://
        Search,         // search://
        ContentSearch,  // content://
        Library,        // lib://
        Tag,            // tag://
        Unknown
    }

    public class ProtocolInfo
    {
        public ProtocolType Type { get; set; }
        public string OriginalPath { get; set; }
        public string TargetPath { get; set; } // The path stripped of protocol, or relevant content
        public string ExtraData { get; set; } // e.g. for zip://C:\a.zip|inner, Target=C:\a.zip, Extra=inner
    }

    public static class ProtocolManager
    {
        public const string ZipProtocol = "zip://";
        public const string SearchProtocol = "search://";
        public const string ContentSearchProtocol = "content://";
        public const string LibraryProtocol = "lib://";
        public const string TagProtocol = "tag://";
        public const string PathProtocol = "path:/"; // Note the single slash usually for path:/c:/... but let's support robustly

        public static bool IsVirtual(string path)
        {
            if (string.IsNullOrEmpty(path)) return false;
            var info = Parse(path);
            return info.Type != ProtocolType.Local;
        }

        public static ProtocolInfo Parse(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return new ProtocolInfo { Type = ProtocolType.Unknown, OriginalPath = path };
            }

            string trimmed = path.Trim();

            // Normalize slashes for check
            string normalizedStart = trimmed.Replace('\\', '/');

            if (normalizedStart.StartsWith(ZipProtocol, StringComparison.OrdinalIgnoreCase) ||
                trimmed.StartsWith("zip:", StringComparison.OrdinalIgnoreCase)) // Fallback for simple zip: check if slashes are messed up
            {
                // zip://C:\File.zip|Inner
                // Handle various prefix forms
                string raw;
                if (normalizedStart.StartsWith(ZipProtocol, StringComparison.OrdinalIgnoreCase))
                {
                    raw = trimmed.Substring(ZipProtocol.Length);
                }
                else
                {
                    // Case where it might be zip:\ or zip:/ or just zip:
                    int colonIndex = trimmed.IndexOf(':');
                    raw = trimmed.Substring(colonIndex + 1);
                    // Trim leading slashes
                    while (raw.Length > 0 && (raw[0] == '/' || raw[0] == '\\'))
                    {
                        raw = raw.Substring(1);
                    }
                }
                string target = raw;
                string extra = string.Empty;

                int pipeIndex = raw.IndexOf('|');
                if (pipeIndex >= 0)
                {
                    target = raw.Substring(0, pipeIndex);
                    extra = raw.Substring(pipeIndex + 1);
                }

                return new ProtocolInfo
                {
                    Type = ProtocolType.Archive,
                    OriginalPath = trimmed,
                    TargetPath = target,
                    ExtraData = extra
                };
            }

            if (trimmed.StartsWith(SearchProtocol, StringComparison.OrdinalIgnoreCase))
            {
                return new ProtocolInfo
                {
                    Type = ProtocolType.Search,
                    OriginalPath = trimmed,
                    TargetPath = trimmed.Substring(SearchProtocol.Length)
                };
            }

            if (trimmed.StartsWith(ContentSearchProtocol, StringComparison.OrdinalIgnoreCase))
            {
                return new ProtocolInfo
                {
                    Type = ProtocolType.ContentSearch,
                    OriginalPath = trimmed,
                    TargetPath = trimmed.Substring(ContentSearchProtocol.Length)
                };
            }

            if (trimmed.StartsWith(LibraryProtocol, StringComparison.OrdinalIgnoreCase))
            {
                return new ProtocolInfo
                {
                    Type = ProtocolType.Library,
                    OriginalPath = trimmed,
                    TargetPath = trimmed.Substring(LibraryProtocol.Length)
                };
            }

            if (trimmed.StartsWith(TagProtocol, StringComparison.OrdinalIgnoreCase))
            {
                return new ProtocolInfo
                {
                    Type = ProtocolType.Tag,
                    OriginalPath = trimmed,
                    TargetPath = trimmed.Substring(TagProtocol.Length)
                };
            }

            // Explicit path protocol: path:/C:/Windows
            if (trimmed.StartsWith(PathProtocol, StringComparison.OrdinalIgnoreCase) ||
                trimmed.StartsWith("path://", StringComparison.OrdinalIgnoreCase))
            {
                string raw = trimmed.StartsWith("path://", StringComparison.OrdinalIgnoreCase)
                    ? trimmed.Substring(7)
                    : trimmed.Substring(6); // path:/ is 6 chars

                // Normalize: if path is /C:/Windows -> C:/Windows
                if ((raw.StartsWith("/") || raw.StartsWith("\\")) && raw.Length > 2 && raw[2] == ':')
                {
                    raw = raw.Substring(1);
                }

                return new ProtocolInfo
                {
                    Type = ProtocolType.Local, // Treat as local
                    OriginalPath = trimmed,
                    TargetPath = raw
                };
            }

            // Standard local path
            return new ProtocolInfo
            {
                Type = ProtocolType.Local,
                OriginalPath = trimmed,
                TargetPath = trimmed
            };
        }
    }
}

