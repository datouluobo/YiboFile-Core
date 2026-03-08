using System;
using System.Collections.ObjectModel;
using System.Windows.Input;
using System.Threading;
using YiboFile.Models;
using YiboFile.Services.Config;
using YiboFile.Services.Navigation;
using YiboFile.ViewModels.Messaging;
using YiboFile.ViewModels.Messaging.Messages;
using YiboFile.ViewModels.Previews;

namespace YiboFile.ViewModels.Previews
{
    public class PanePreviewViewModel : BaseViewModel
    {
        private readonly IMessageBus _messageBus;
        private readonly ConfigurationService _configService;
        private readonly PaneId _paneId;
        
        private Timer _debounceTimer;
        private string _pendingPreviewPath;

        private bool _isVisible = false;
        public bool IsVisible
        {
            get => _isVisible;
            set
            {
                if (SetProperty(ref _isVisible, value))
                {
                    OnPropertyChanged(nameof(EffectiveVisibility));
                    OnPropertyChanged(nameof(IsCollapsed));
                    _messageBus.Publish(new PreviewPaneVisibilityChangedMessage(_paneId, value));
                }
            }
        }

        public bool EffectiveVisibility => IsVisible;

        public bool IsCollapsed
        {
            get => !IsVisible;
            set
            {
                if (IsVisible != !value)
                {
                    IsVisible = !value;
                    // persist to config so the user's choice is saved and loaded next time
                    YiboFile.Services.Config.ConfigurationService.Instance.Set(c => c.IsPreviewCollapsed, value);
                    YiboFile.Services.Config.ConfigurationService.Instance.SaveNow();
                }
            }
        }

        private double _notesHeight = 200;
        public double NotesHeight
        {
            get => _notesHeight;
            set => SetProperty(ref _notesHeight, value);
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
            set
            {
                if (_activePreview != value)
                {
                    _activePreview?.Dispose();
                    SetProperty(ref _activePreview, value);
                }
            }
        }

        private FileSystemItem _selectedItem;
        public FileSystemItem SelectedItem
        {
            get => _selectedItem;
            set => SetProperty(ref _selectedItem, value);
        }

        public PanePreviewViewModel(IMessageBus messageBus, ConfigurationService configService, PaneId paneId)
        {
            _messageBus = messageBus ?? throw new ArgumentNullException(nameof(messageBus));
            _configService = configService ?? throw new ArgumentNullException(nameof(configService));
            _paneId = paneId;

            // default to true only if the user specifically expanded it last time (or previously RightPanel was used)
            _isVisible = !(_configService.Config?.IsPreviewCollapsed ?? true);
            
            if (_configService.Config != null && _configService.Config.RightPanelNotesHeight > 0)
            {
                _notesHeight = _configService.Config.RightPanelNotesHeight;
            }

            _messageBus.Subscribe<FileSelectionChangedMessage>(m =>
            {
                if (m.Pane != _paneId) return;

                System.Windows.Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                {
                    if (m.SelectedItems?.Count > 0)
                    {
                        SelectedItem = m.SelectedItems[0] as FileSystemItem;
                        if (m.RequestPreview)
                        {
                            UpdatePreview(SelectedItem?.Path);
                        }
                        else
                        {
                            ActivePreview = null;
                        }
                    }
                    else
                    {
                        SelectedItem = null;
                        ActivePreview = null;
                    }
                }));
            });

            _messageBus.Subscribe<ShowFileInfoMessage>(m =>
            {
                if (m.Pane != _paneId) return;

                System.Windows.Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                {
                    if (SelectedItem == null || m.Item?.Path == SelectedItem?.Path)
                    {
                        SelectedItem = m.Item;
                    }
                }));
            });

            _messageBus.Subscribe<PreviewChangedMessage>(m =>
            {
                if (m.TargetPane != _paneId) return;

                System.Windows.Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                {
                    ActivePreview = m.Preview;
                }));
            });

            /* 移除全局 IsRightPanelVisible 联动，让各个面板的预览状态彼此独立。在双列表模式下可以分别开关。
            _messageBus.Subscribe<ConfigurationSettingChangedMessage>(m =>
            {
                if (m.SettingName == nameof(AppConfig.IsRightPanelVisible))
                {
                    System.Windows.Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                    {
                        if (IsVisible != _configService.Config.IsRightPanelVisible)
                        {
                            IsCollapsed = !_configService.Config.IsRightPanelVisible;
                        }
                    }));
                }
            });
            */

            _debounceTimer = new Timer(OnDebounceTick, null, Timeout.Infinite, Timeout.Infinite);
        }

        private void UpdatePreview(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                _pendingPreviewPath = null;
                _debounceTimer.Change(Timeout.Infinite, Timeout.Infinite);
                ActivePreview = null;
                return;
            }

            _pendingPreviewPath = path;
            _debounceTimer.Change(250, Timeout.Infinite);
        }

        private void OnDebounceTick(object state)
        {
            var path = _pendingPreviewPath;
            if (!string.IsNullOrEmpty(path))
            {
                _messageBus.Publish(new PreviewRequestMessage(path, _paneId));
            }
        }
    }
}
