using System;
using System.Collections.Generic;
using System.Windows.Media;

namespace YiboFile.Models.Shell
{
    public class ShellMenuItem
    {
        public string Text { get; set; }
        public string Verb { get; set; }
        public int CommandId { get; set; }
        public ImageSource Icon { get; set; }
        public bool IsSeparator { get; set; }
        public bool IsPinned { get; set; }
        public bool IsHidden { get; set; }
        public List<ShellMenuItem> Children { get; set; } = new();

        public string UniqueKey => !string.IsNullOrEmpty(Verb) ? Verb : $"_text_{Text}";
    }
}
