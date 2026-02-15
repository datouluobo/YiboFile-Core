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

            PropertiesCommand = new RelayCommand(() =>
            {
                if (_pane.SelectedItem != null) _messageBus.Publish(new ShowPropertiesRequestMessage(_pane.SelectedItem, _pane.CurrentPath));
                else if (!string.IsNullOrEmpty(_pane.CurrentPath)) _messageBus.Publish(new ShowPropertiesRequestMessage(null, _pane.CurrentPath));
            }, () => true);

            NewFolderCommand = new RelayCommand(() => _messageBus.Publish(new CreateFolderRequestMessage(_pane.CurrentPath)));
            NewFileCommand = new RelayCommand(() => _messageBus.Publish(new CreateFileRequestMessage(_pane.CurrentPath)));

            DeleteCommand = new RelayCommand(() =>
                _messageBus.Publish(new DeleteItemsRequestMessage(_pane.Selection?.SelectedItems?.ToList())),
                () => (_pane.Selection?.SelectedItems?.Count ?? 0) > 0);

            CopyCommand = new RelayCommand(() =>
                _messageBus.Publish(new CopyItemsRequestMessage(_pane.Selection?.SelectedItems?.ToList())),
                () => (_pane.Selection?.SelectedItems?.Count ?? 0) > 0);

            CutCommand = new RelayCommand(() =>
                _messageBus.Publish(new CutItemsRequestMessage(_pane.Selection?.SelectedItems?.ToList())),
                () => (_pane.Selection?.SelectedItems?.Count ?? 0) > 0);

            PasteCommand = new RelayCommand(() =>
                _messageBus.Publish(new PasteItemsRequestMessage(_pane.CurrentPath)));

            RenameCommand = new RelayCommand(() =>
                _messageBus.Publish(new RenameItemRequestMessage(_pane.Selection?.SelectedItem)),
                () => (_pane.Selection?.SelectedItems?.Count ?? 0) == 1);

            UndoCommand = new RelayCommand(() => _messageBus.Publish(new UndoRequestMessage()));
            RedoCommand = new RelayCommand(() => _messageBus.Publish(new RedoRequestMessage()));

            ToggleLibraryCommand = new RelayCommand<Library>(lib =>
            {
                if (lib != null && _pane.Selection?.SelectedItems?.Count > 0)
                    _messageBus.Publish(new ToggleLibraryPathRequestMessage(lib, _pane.Selection.SelectedItems.Select(i => i.Path).ToList()));
            });

            AddToFavoriteCommand = new RelayCommand<int>(groupId =>
            {
                if (_pane.Selection?.SelectedItems?.Count > 0)
                    _messageBus.Publish(new AddFavoriteRequestMessage(_pane.Selection.SelectedItems.ToList(), groupId));
            });

            ToggleTagCommand = new RelayCommand<ITag>(tag =>
            {
                if (tag != null && _pane.Selection?.SelectedItems?.Count > 0)
                    _messageBus.Publish(new ToggleTagRequestMessage(tag.Id, _pane.Selection.SelectedItems.Select(i => i.Path).ToList()));
            });

            NewLibraryCommand = new RelayCommand(() =>
            {
                var dialog = new YiboFile.Controls.Dialogs.InputDialog("新建库", "请输入库名称:");
                if (dialog.ShowDialog() == true && !string.IsNullOrWhiteSpace(dialog.InputText))
                {
                    var paths = _pane.Selection?.SelectedItems?.Where(i => i.IsDirectory).Select(i => i.Path).ToList();
                    _messageBus.Publish(new CreateLibraryRequestMessage(dialog.InputText, paths));
                }
            });

            NewFavoriteGroupCommand = new RelayCommand(() =>
            {
                var inputName = YiboFile.DialogService.ShowInput("请输入新分组名称：", "新分组", "新建分组");
                if (!string.IsNullOrEmpty(inputName))
                {
                    _messageBus.Publish(new CreateFavoriteGroupRequestMessage(inputName.Trim(), _pane.Selection?.SelectedItems?.ToList()));
                }
            });

            ManageTagsCommand = new RelayCommand(() =>
            {
                var dialog = new YiboFile.Controls.Dialogs.TagManagementDialog();
                if (Application.Current?.MainWindow != null) dialog.Owner = Application.Current.MainWindow;
                dialog.ShowDialog();
                _messageBus.Publish(new TagListChangedMessage());
            });
            NewTagCommand = ManageTagsCommand;

            BatchAddTagsCommand = new RelayCommand(() =>
            {
                if ((_pane.Selection?.SelectedItems?.Count ?? 0) == 0) return;
                var dialog = new YiboFile.Controls.Dialogs.TagSelectionDialog();
                if (Application.Current?.MainWindow != null) dialog.Owner = Application.Current.MainWindow;
                if (dialog.ShowDialog() == true)
                    _messageBus.Publish(new AddTagToFilesRequestMessage(_pane.Selection.SelectedItems.Select(i => i.Path).ToList(), dialog.SelectedTagId));
            }, () => (_pane.Selection?.SelectedItems?.Count ?? 0) > 0);

            TagStatisticsCommand = new RelayCommand(() => _pane.ExecuteTagStatistics()); // Keep this as it involves UI/Service interaction that might be complex to inline immediately without more context

            LoadMoreCommand = new RelayCommand(() => _pane.Filter?.LoadMoreCommand?.Execute(_pane.CurrentPath));
            SelectAllCommand = new RelayCommand(() => _messageBus.Publish(new SelectAllRequestMessage(_pane.IsSecondary ? PaneId.Second : PaneId.Main)));
        }

        public void NotifyCommandStatesChanged()
        {
            // 对于这种使用 CommandManager.RequerySuggested 的 RelayCommand 实现，
            // 调用 InvalidateRequerySuggested 会强制刷新所有命令的 CanExecute 状态。
            CommandManager.InvalidateRequerySuggested();
        }
    }
}
