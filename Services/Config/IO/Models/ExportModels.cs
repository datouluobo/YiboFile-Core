using System.Collections.Generic;

namespace YiboFile.Services.Config.IO.Models
{
    public class StructureExportDto
    {
        public List<TagGroupDto> TagGroups { get; set; } = new List<TagGroupDto>();
        public List<LibraryDto> Libraries { get; set; } = new List<LibraryDto>();
        public List<FavoriteGroupDto> FavoriteGroups { get; set; } = new List<FavoriteGroupDto>();
    }

    public class TagGroupDto
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Color { get; set; }
        public List<TagDto> Tags { get; set; } = new List<TagDto>();
    }

    public class TagDto
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Color { get; set; }
        public int GroupId { get; set; }
    }

    public class LibraryDto
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public List<LibraryPathDto> Paths { get; set; } = new List<LibraryPathDto>();
    }

    public class LibraryPathDto
    {
        public string Path { get; set; }
        public string DisplayName { get; set; }
    }

    public class FavoriteGroupDto
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public int SortOrder { get; set; }
        public List<FavoriteDto> Favorites { get; set; } = new List<FavoriteDto>();
    }

    public class FavoriteDto
    {
        public string Path { get; set; }
        public bool IsDirectory { get; set; }
        public string DisplayName { get; set; }
        public int SortOrder { get; set; }
        public int GroupId { get; set; }
    }

    public class FileDataExportDto
    {
        public Dictionary<string, List<int>> FileTags { get; set; } = new Dictionary<string, List<int>>();
        public Dictionary<string, string> FileNotes { get; set; } = new Dictionary<string, string>();
    }
}
