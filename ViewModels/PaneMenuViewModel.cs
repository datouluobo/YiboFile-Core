using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using Microsoft.Extensions.DependencyInjection;
using YiboFile.Models;
using YiboFile.Services;
using YiboFile.Services.Core;
using YiboFile.Services.Favorite;
using YiboFile.Services.Navigation;
using YiboFile.Services.Features;
using YiboFile.ViewModels.Messaging;
using YiboFile.ViewModels.Messaging.Messages;

namespace YiboFile.ViewModels
{
    public class PaneMenuViewModel : BaseViewModel, IDisposable
    {
        private readonly IMessageBus _messageBus;
        private readonly Dispatcher _dispatcher;
        private readonly LibraryService _libraryService;
        private readonly ITagService _tagService;
        private readonly FavoriteService _favoriteService;
        private readonly Services.UI.IDialogService _dialogService;
        private readonly PaneViewModel _pane;

        public PaneMenuViewModel(PaneViewModel pane, IMessageBus messageBus, Services.UI.IDialogService dialogService = null)
        {
            _pane = pane ?? throw new ArgumentNullException(nameof(pane));
            _messageBus = messageBus ?? throw new ArgumentNullException(nameof(messageBus));
            _dialogService = dialogService ?? App.ServiceProvider?.GetService<Services.UI.IDialogService>();
            _dispatcher = Dispatcher.CurrentDispatcher;

            _libraryService = App.ServiceProvider?.GetService<LibraryService>();
            _tagService = App.ServiceProvider?.GetService<ITagService>();
            _favoriteService = App.ServiceProvider?.GetService<FavoriteService>();

            SubscribeEvents();
        }

        public ObservableCollection<ContextMenuItemViewModel> LibraryMenuItems { get; } = new ObservableCollection<ContextMenuItemViewModel>();
        public ObservableCollection<ContextMenuItemViewModel> TagMenuItems { get; } = new ObservableCollection<ContextMenuItemViewModel>();
        public ObservableCollection<ContextMenuItemViewModel> FavoriteMenuItems { get; } = new ObservableCollection<ContextMenuItemViewModel>();

        private void SubscribeEvents()
        {
            _messageBus.Subscribe<TagListChangedMessage>(OnTagListChanged);
            _messageBus.Subscribe<LibraryListChangedMessage>(OnLibraryListChanged);
            _messageBus.Subscribe<FavoritesUpdatedMessage>(OnFavoritesUpdated);
        }


        public void UpdateDynamicMenuItems()
        {
            if (_dispatcher == null) return;

            _dispatcher.Invoke(() =>
           {
               var selectedItems = _pane.Selection?.SelectedItems ?? new ObservableCollection<FileSystemItem>();
               var hasSelection = selectedItems.Count > 0;

               // 1. Libraries
               var libraries = _libraryService?.GetAllLibraries() ?? new List<Library>();
               LibraryMenuItems.Clear();
               foreach (var lib in libraries)
               {
                   bool isChecked = hasSelection && selectedItems.All(i => lib.Paths != null && lib.Paths.Contains(i.Path));

                   LibraryMenuItems.Add(new ContextMenuItemViewModel
                   {
                       Header = lib.Name,
                       Command = new RelayCommand<Library>(l => ToggleLibrary(l, selectedItems)),
                       CommandParameter = lib,
                       IsCheckable = true,
                       IsChecked = isChecked,
                       Icon = Application.Current.TryFindResource("Icon_Library")
                   });
               }
               if (libraries.Count > 0) LibraryMenuItems.Add(new ContextMenuItemViewModel { IsSeparator = true });
               LibraryMenuItems.Add(new ContextMenuItemViewModel
               {
                   Header = "新建库...",
                   Command = new RelayCommand(NewLibrary)
               });

               // 2. Tags
               TagMenuItems.Clear();
               if (App.IsTagTrainAvailable)
               {
                   var tags = _tagService?.GetAllTags() ?? new List<ITag>();
                   foreach (var tag in tags)
                   {
                       bool isChecked = hasSelection && selectedItems.All(i => i.TagList != null && i.TagList.Any(t => t.Id == tag.Id));
                       TagMenuItems.Add(new ContextMenuItemViewModel
                       {
                           Header = tag.Name,
                           Command = new RelayCommand<ITag>(t => ToggleTag(t, selectedItems)),
                           CommandParameter = tag,
                           IsCheckable = true,
                           IsChecked = isChecked,
                           IconBrush = tag.Color ?? "#808080"
                       });
                   }
               }

               // 3. Favorites
               var groups = _favoriteService?.GetAllGroups() ?? new List<FavoriteGroup>();
               FavoriteMenuItems.Clear();
               foreach (var group in groups)
               {
                   FavoriteMenuItems.Add(new ContextMenuItemViewModel
                   {
                       Header = group.Name,
                       Command = new RelayCommand<int>(gid => AddToFavorite(gid, selectedItems)),
                       CommandParameter = group.Id,
                       Icon = Application.Current.TryFindResource("Icon_Favorite")
                   });
               }
               if (groups.Count > 0) FavoriteMenuItems.Add(new ContextMenuItemViewModel { IsSeparator = true });
               FavoriteMenuItems.Add(new ContextMenuItemViewModel
               {
                   Header = "+ 新建分组...",
                   Command = new RelayCommand(NewFavoriteGroup)
               });
           });
        }

        #region Actions

        private void ToggleLibrary(Library library, IEnumerable<FileSystemItem> items)
        {
            if (library == null || items == null || !items.Any()) return;
            _messageBus.Publish(new ToggleLibraryPathRequestMessage(library, items.Select(i => i.Path).ToList()));
        }

        private void NewLibrary()
        {
            var selectedItems = _pane.Selection?.SelectedItems;

            var libName = _dialogService?.ShowInput("请输入库名称:", "", "新建库");
            if (!string.IsNullOrWhiteSpace(libName))
            {
                var paths = selectedItems?.Where(i => i.IsDirectory).Select(i => i.Path).ToList() ?? new List<string>();
                _messageBus.Publish(new CreateLibraryRequestMessage(libName, paths));
            }
        }

        private void ToggleTag(ITag tag, IEnumerable<FileSystemItem> items)
        {
            if (tag == null || items == null || !items.Any()) return;
            _messageBus.Publish(new ToggleTagRequestMessage(tag.Id, items.Select(i => i.Path).ToList()));
        }

        private void AddToFavorite(int groupId, IEnumerable<FileSystemItem> items)
        {
            if (items == null || !items.Any()) return;
            _messageBus.Publish(new AddFavoriteRequestMessage(items.ToList(), groupId));
        }

        private void NewFavoriteGroup()
        {
            var selectedItems = _pane.Selection?.SelectedItems?.ToList();
            var inputName = _dialogService?.ShowInput("请输入新分组名称：", "新分组", "新建分组");
            if (!string.IsNullOrEmpty(inputName))
            {
                _messageBus.Publish(new CreateFavoriteGroupRequestMessage(inputName.Trim(), selectedItems));
            }
        }

        #endregion

        #region Event Handlers

        private void OnTagListChanged(TagListChangedMessage msg) => UpdateDynamicMenuItems();
        private void OnLibraryListChanged(LibraryListChangedMessage msg) => UpdateDynamicMenuItems();
        private void OnFavoritesUpdated(FavoritesUpdatedMessage msg) => UpdateDynamicMenuItems();

        #endregion

        public void Dispose()
        {
            _messageBus.Unsubscribe<TagListChangedMessage>(OnTagListChanged);
            _messageBus.Unsubscribe<LibraryListChangedMessage>(OnLibraryListChanged);
            _messageBus.Unsubscribe<FavoritesUpdatedMessage>(OnFavoritesUpdated);
        }
    }
}
