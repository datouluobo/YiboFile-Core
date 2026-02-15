using System;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Collections.Generic;
using System.IO;
using YiboFile.ViewModels.Messaging;
using YiboFile.ViewModels.Messaging.Messages;
using YiboFile.Models;
using YiboFile.Services.Navigation;
using YiboFile.Services.Core;
using YiboFile.Services.Features;
using Microsoft.Extensions.DependencyInjection;

namespace YiboFile.ViewModels
{
    /// <summary>
    /// PaneViewModel 的命令集。将业务逻辑命令与状态容器分离。
    /// [Phase 3.5-C]
    /// </summary>
    public class PaneCommandSet
    {
        private readonly PaneViewModel _pane;
        private readonly IMessageBus _messageBus;

        public PaneCommandSet(PaneViewModel pane, IMessageBus messageBus)
        {
            _pane = pane ?? throw new ArgumentNullException(nameof(pane));
            _messageBus = messageBus ?? throw new ArgumentNullException(nameof(messageBus));

            InitializeCommands();
        }

        #region Commands

        public ICommand RefreshCommand { get; private set; }
        public ICommand NavigateBackCommand { get; private set; }
        public ICommand NavigateForwardCommand { get; private set; }
        public ICommand NavigateUpCommand { get; private set; }
        public ICommand NavigateHomeCommand { get; private set; }
        public ICommand OpenParentFolderCommand { get; private set; }
        public ICommand SwitchViewModeCommand { get; private set; }

        public ICommand SelectAllCommand { get; private set; }
        public ICommand PropertiesCommand { get; private set; }
        public ICommand NewFolderCommand { get; private set; }
        public ICommand NewFileCommand { get; private set; }
        public ICommand DeleteCommand { get; private set; }
        public ICommand CopyCommand { get; private set; }
        public ICommand CutCommand { get; private set; }
        public ICommand PasteCommand { get; private set; }
        public ICommand RenameCommand { get; private set; }
        public ICommand UndoCommand { get; private set; }
        public ICommand RedoCommand { get; private set; }

        public ICommand ToggleLibraryCommand { get; private set; }
        public ICommand AddToFavoriteCommand { get; private set; }
        public ICommand ToggleTagCommand { get; private set; }
        public ICommand NewLibraryCommand { get; private set; }
        public ICommand NewFavoriteGroupCommand { get; private set; }

        public ICommand NewTagCommand { get; private set; }
        public ICommand ManageTagsCommand { get; private set; }
        public ICommand BatchAddTagsCommand { get; private set; }
        public ICommand TagStatisticsCommand { get; private set; }

        public ICommand LoadMoreCommand { get; private set; }

        #endregion

        private void InitializeCommands()
        {
            RefreshCommand = new RelayCommand(() => _pane.Refresh());
            NavigateBackCommand = new RelayCommand(() => _pane.ExecuteNavigateBack(), () => _pane.CanNavigateBack);
            NavigateForwardCommand = new RelayCommand(() => _pane.ExecuteNavigateForward(), () => _pane.CanNavigateForward);
            NavigateUpCommand = new RelayCommand(() => _pane.ExecuteNavigateUp(), () => _pane.CanNavigateUp);
            NavigateHomeCommand = new RelayCommand(() => _pane.NavigateTo("Home"));
            OpenParentFolderCommand = NavigateUpCommand;

            SwitchViewModeCommand = new RelayCommand<string>(mode => _pane.ExecuteSwitchViewMode(mode));

            PropertiesCommand = new RelayCommand(() => _pane.ExecuteShowProperties(), () => _pane.Selection?.SelectedItem != null);
            NewFolderCommand = new RelayCommand(() => _pane.ExecuteNewFolder());
            NewFileCommand = new RelayCommand(() => _pane.ExecuteNewFile());
            DeleteCommand = new RelayCommand(() => _pane.ExecuteDelete(), () => (_pane.Selection?.SelectedItems?.Count ?? 0) > 0);
            CopyCommand = new RelayCommand(() => _pane.ExecuteCopy(), () => (_pane.Selection?.SelectedItems?.Count ?? 0) > 0);
            CutCommand = new RelayCommand(() => _pane.ExecuteCut(), () => (_pane.Selection?.SelectedItems?.Count ?? 0) > 0);
            PasteCommand = new RelayCommand(() => _pane.ExecutePaste());
            RenameCommand = new RelayCommand(() => _pane.ExecuteRename(), () => (_pane.Selection?.SelectedItems?.Count ?? 0) == 1);
            UndoCommand = new RelayCommand(() => _pane.ExecuteUndo());
            RedoCommand = new RelayCommand(() => _pane.ExecuteRedo());

            ToggleLibraryCommand = new RelayCommand<Library>(lib => _pane.ExecuteToggleLibrary(lib));
            AddToFavoriteCommand = new RelayCommand<int>(groupId => _pane.ExecuteAddToFavorite(groupId));
            ToggleTagCommand = new RelayCommand<ITag>(tag => _pane.ExecuteToggleTag(tag));
            NewLibraryCommand = new RelayCommand(() => _pane.ExecuteNewLibrary());
            NewFavoriteGroupCommand = new RelayCommand(() => _pane.ExecuteNewFavoriteGroup());

            NewTagCommand = new RelayCommand(() => _pane.ExecuteManageTags());
            ManageTagsCommand = new RelayCommand(() => _pane.ExecuteManageTags());
            BatchAddTagsCommand = new RelayCommand(() => _pane.ExecuteBatchAddTags(), () => (_pane.Selection?.SelectedItems?.Count ?? 0) > 0);
            TagStatisticsCommand = new RelayCommand(() => _pane.ExecuteTagStatistics());

            LoadMoreCommand = new RelayCommand(() => _pane.Filter?.LoadMoreCommand?.Execute(null));
            SelectAllCommand = new RelayCommand(() => _pane.ExecuteSelectAll());
        }

        public void NotifyCommandStatesChanged()
        {
            // 对于这种使用 CommandManager.RequerySuggested 的 RelayCommand 实现，
            // 调用 InvalidateRequerySuggested 会强制刷新所有命令的 CanExecute 状态。
            CommandManager.InvalidateRequerySuggested();
        }
    }
}
