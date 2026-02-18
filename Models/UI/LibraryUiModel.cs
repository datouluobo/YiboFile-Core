using System.Collections.Generic;
using YiboFile;
using YiboFile.Models;

namespace YiboFile.Models.UI
{
    public class LibraryUiModel
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public int DisplayOrder { get; set; }
        public List<LibraryPath> Paths { get; set; }
    }
}
