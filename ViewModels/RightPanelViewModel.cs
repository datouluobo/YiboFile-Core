using System;
using System.Collections.ObjectModel;
using System.Windows.Input;
using YiboFile.Models;
using YiboFile.Services.Config;
using YiboFile.Services.FileList;
using YiboFile.ViewModels.Messaging;
using YiboFile.ViewModels.Messaging.Messages;
using YiboFile.ViewModels.Previews;

namespace YiboFile.ViewModels
{
    public class RightPanelViewModel : BaseViewModel
    {
        private readonly IMessageBus _messageBus;
        private readonly ConfigService _configService;
        private readonly FileListService _fileListService;

        private bool _isVisible;
        public bool IsVisible
        {
            get => _isVisible;
            set
            {
                if (SetProperty(ref _isVisible, value))
                {
                    var cfg = _configService.Config;
                    if (cfg.IsRightPanelVisible != value)
                    {
                        cfg.IsRightPanelVisible = value;
                        _configService.StartDelayedSave();
                    }
                }
            }
        }

        private double _notesHeight;
        public double NotesHeight
        {
            get => _notesHeight;
            set
            {
                if (SetProperty(ref _notesHeight, value))
                {
                    var cfg = _configService.Config;
                    if (Math.Abs(cfg.RightPanelNotesHeight - value) > 0.1)
                    {
                        cfg.RightPanelNotesHeight = value;
                        _configService.StartDelayedSave();
                    }
                }
            }
        }

        private string _currentNotes;
        public string CurrentNotes
        {
            get => _currentNotes;
            set => SetProperty(ref _currentNotes, value);
        }

        private IPreviewViewModel _activePreview;
        public IPreviewViewModel ActivePreview
        {
            get => _activePreview;
            set => SetProperty(ref _activePreview, value);
        }

        private FileSystemItem _selectedItem;
        public FileSystemItem SelectedItem
        {
            get => _selectedItem;
            set
            {
                if (SetProperty(ref _selectedItem, value))
                {
                    UpdatePreview(value?.Path);
                }
            }
        }

        public RightPanelViewModel(IMessageBus messageBus, ConfigService configService, FileListService fileListService)
        {
            _messageBus = messageBus ?? throw new ArgumentNullException(nameof(messageBus));
            _configService = configService ?? throw new ArgumentNullException(nameof(configService));
            _fileListService = fileListService ?? throw new ArgumentNullException(nameof(fileListService));

            var cfg = _configService.Config;
            _isVisible = cfg.IsRightPanelVisible;
            _notesHeight = cfg.RightPanelNotesHeight;

            _messageBus.Subscribe<FileSelectionChangedMessage>(m =>
            {
                if (m.SelectedItems?.Count > 0)
                    SelectedItem = m.SelectedItems[0] as FileSystemItem;
                else
                    SelectedItem = null;
            });

            _messageBus.Subscribe<PreviewChangedMessage>(m =>
            {
                ActivePreview = m.Preview;
            });
        }

        private void UpdatePreview(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                ActivePreview = new ErrorPreviewViewModel { ErrorMessage = "选择文件以预览" };
                return;
            }

            // Set loading state
            ActivePreview = new ErrorPreviewViewModel { ErrorMessage = "正在加载预览...", IsLoading = true };

            // Request preview from service
            _messageBus.Publish(new PreviewRequestMessage(path));
        }
    }
}
