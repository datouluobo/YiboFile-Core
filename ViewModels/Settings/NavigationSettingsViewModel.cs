using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using Microsoft.Extensions.DependencyInjection;
using YiboFile.Services.Config;
using YiboFile.ViewModels;

namespace YiboFile.ViewModels.Settings
{
    public class NavigationSettingsViewModel : BaseViewModel
    {
        private ObservableCollection<NavigationSectionItemViewModel> _navigationSections;
        public ObservableCollection<NavigationSectionItemViewModel> NavigationSections
        {
            get => _navigationSections;
            set => SetProperty(ref _navigationSections, value);
        }

        public ICommand MoveSectionUpCommand { get; }
        public ICommand MoveSectionDownCommand { get; }

        public NavigationSettingsViewModel()
        {
            MoveSectionUpCommand = new RelayCommand<NavigationSectionItemViewModel>(MoveSectionUp);
            MoveSectionDownCommand = new RelayCommand<NavigationSectionItemViewModel>(MoveSectionDown);
            LoadFromConfig();
        }

        public void LoadFromConfig()
        {
            var config = ConfigurationService.Instance.GetSnapshot();
            InitializePathSettings(config);
        }

        private void InitializePathSettings(AppConfig config)
        {
            var order = config.NavigationSectionsOrder;
            if (order == null || order.Count == 0)
            {
                order = new List<string> { "Drives", "QuickAccess" };
            }

            var sections = new ObservableCollection<NavigationSectionItemViewModel>();
            var fixedSections = new HashSet<string> { "QuickAccess", "Drives" };

            foreach (var key in order)
            {
                if (fixedSections.Contains(key))
                {
                    sections.Add(new NavigationSectionItemViewModel(key, GetSectionDisplayName(key)));
                }
                else if (key.StartsWith("FavoriteGroup_"))
                {
                    sections.Add(new NavigationSectionItemViewModel(key, GetSectionDisplayName(key)));
                }
            }

            foreach (var key in fixedSections)
            {
                if (!sections.Any(s => s.Key == key))
                {
                    sections.Add(new NavigationSectionItemViewModel(key, GetSectionDisplayName(key)));
                }
            }

            try
            {
                var favoriteRepo = App.ServiceProvider.GetRequiredService<YiboFile.Services.Data.Repositories.IFavoriteRepository>();
                var groups = favoriteRepo.GetAllGroups();
                foreach (var group in groups)
                {
                    string key = $"FavoriteGroup_{group.Id}";
                    if (!sections.Any(s => s.Key == key))
                    {
                        sections.Add(new NavigationSectionItemViewModel(key, group.Name));
                    }
                }
            }
            catch { }

            NavigationSections = sections;
        }

        private string GetSectionDisplayName(string key)
        {
            if (key == "QuickAccess") return "快速访问";
            if (key == "Drives") return "此电脑 (驱动器)";
            if (key.StartsWith("FavoriteGroup_"))
            {
                if (int.TryParse(key.Substring("FavoriteGroup_".Length), out int groupId))
                {
                    try
                    {
                        var favoriteRepo = App.ServiceProvider.GetRequiredService<YiboFile.Services.Data.Repositories.IFavoriteRepository>();
                        var group = favoriteRepo.GetAllGroups().FirstOrDefault(g => g.Id == groupId);
                        if (group != null) return group.Name;
                    }
                    catch { }
                }
                return "收藏项";
            }
            if (key == "FolderFavorites") return "收藏夹 (文件夹)";
            if (key == "FileFavorites") return "收藏夹 (文件)";
            return key;
        }

        private void MoveSectionUp(NavigationSectionItemViewModel item)
        {
            if (item == null) return;
            var index = NavigationSections.IndexOf(item);
            if (index > 0)
            {
                NavigationSections.Move(index, index - 1);
                SavePathSettings();
            }
        }

        private void MoveSectionDown(NavigationSectionItemViewModel item)
        {
            if (item == null) return;
            var index = NavigationSections.IndexOf(item);
            if (index >= 0 && index < NavigationSections.Count - 1)
            {
                NavigationSections.Move(index, index + 1);
                SavePathSettings();
            }
        }

        private void SavePathSettings()
        {
            var newOrder = NavigationSections.Select(s => s.Key).ToList();
            ConfigurationService.Instance.Update(c => c.NavigationSectionsOrder = newOrder);
        }
    }
}
