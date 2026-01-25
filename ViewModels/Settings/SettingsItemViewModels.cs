using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows.Media;
using YiboFile.Models;

namespace YiboFile.ViewModels
{
    public class LibraryItemViewModel : BaseViewModel
    {
        public int Id { get; set; }

        private string _name;
        public string Name
        {
            get => _name;
            set => SetProperty(ref _name, value);
        }

        private ObservableCollection<string> _paths;
        public ObservableCollection<string> Paths
        {
            get => _paths;
            set => SetProperty(ref _paths, value);
        }

        public string DisplayPath => Paths != null && Paths.Count > 0 ? Paths[0] : "无路径";
        public string ToolTipText => Paths != null ? string.Join(Environment.NewLine, Paths) : "";

        public LibraryItemViewModel(Library lib)
        {
            Id = lib.Id;
            Name = lib.Name;
            Paths = new ObservableCollection<string>(lib.Paths ?? new List<string>());
        }
    }

    public class ThemeItemViewModel
    {
        public string Id { get; }
        public string Name { get; }
        public string Emoji { get; }
        public string DisplayName => $"{Emoji} {Name}";

        public ThemeItemViewModel(string id, string name, string emoji)
        {
            Id = id;
            Name = name;
            Emoji = emoji;
        }

        public override string ToString() => DisplayName;
    }

    public class IconStyleItemViewModel
    {
        public string Id { get; }
        public string Name { get; }

        public IconStyleItemViewModel(string id, string name)
        {
            Id = id;
            Name = name;
        }

        public override string ToString() => Name;
    }

    public class NavigationSectionItemViewModel
    {
        public string Key { get; }
        public string DisplayName { get; }

        public NavigationSectionItemViewModel(string key, string displayName)
        {
            Key = key;
            DisplayName = displayName;
        }

        public override string ToString() => DisplayName;
    }

    public class HotkeyItemViewModel : BaseViewModel
    {
        public string Description { get; }
        public string DefaultKey { get; }

        private string _keyCombination;
        public string KeyCombination
        {
            get => _keyCombination;
            set
            {
                if (SetProperty(ref _keyCombination, value))
                {
                    OnPropertyChanged(nameof(IsModified));
                }
            }
        }

        public bool IsModified => KeyCombination != DefaultKey;

        public HotkeyItemViewModel(string description, string defaultKey)
        {
            Description = description;
            DefaultKey = defaultKey;
            _keyCombination = defaultKey;
        }
    }

    public class TagGroupManageViewModel : BaseViewModel
    {
        public int Id { get; set; }
        private string _name;
        public string Name
        {
            get => _name;
            set => SetProperty(ref _name, value);
        }

        private string _color;
        public string Color
        {
            get => _color;
            set
            {
                if (SetProperty(ref _color, value))
                {
                    OnPropertyChanged(nameof(ColorBrush));
                }
            }
        }

        public Brush ColorBrush
        {
            get
            {
                try
                {
                    if (string.IsNullOrEmpty(Color)) return Brushes.Transparent;
                    return (Brush)new BrushConverter().ConvertFrom(Color);
                }
                catch
                {
                    return Brushes.Transparent;
                }
            }
        }

        public ObservableCollection<TagItemManageViewModel> Tags { get; set; } = new ObservableCollection<TagItemManageViewModel>();

        private string _newTagText;
        public string NewTagText
        {
            get => _newTagText;
            set => SetProperty(ref _newTagText, value);
        }
    }

    public class TagItemManageViewModel : BaseViewModel
    {
        public int Id { get; set; }
        private string _name;
        public string Name
        {
            get => _name;
            set => SetProperty(ref _name, value);
        }

        private string _color;
        public string Color
        {
            get => _color;
            set
            {
                if (SetProperty(ref _color, value))
                {
                    OnPropertyChanged(nameof(ColorBrush));
                }
            }
        }

        public Brush ColorBrush
        {
            get
            {
                return YiboFile.Models.TagViewModel.GetColorBrush(Name, Color);
            }
        }

        public int GroupId { get; set; }
    }
}
