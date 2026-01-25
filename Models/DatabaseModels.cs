using System;
using System.Collections.Generic;
using System.Linq;

namespace YiboFile
{
    public class Library
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public List<string> Paths { get; set; } = new List<string>();
        public int DisplayOrder { get; set; }

        // 兼容旧代码
        [Obsolete("Use Paths property instead")]
        public string Path
        {
            get => Paths?.FirstOrDefault() ?? "";
            set
            {
                if (Paths == null) Paths = new List<string>();
                if (!Paths.Contains(value)) Paths.Add(value);
            }
        }
    }

    public class LibraryPath
    {
        public int Id { get; set; }
        public int LibraryId { get; set; }
        public string Path { get; set; }
        public string DisplayName { get; set; }
    }

    public class Favorite
    {
        public int Id { get; set; }
        public int GroupId { get; set; }
        public string Path { get; set; }
        public string DisplayName { get; set; }
        public bool IsDirectory { get; set; }
        public DateTime CreatedAt { get; set; }
        public int SortOrder { get; set; }
    }

    public class FavoriteGroup
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public int SortOrder { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
