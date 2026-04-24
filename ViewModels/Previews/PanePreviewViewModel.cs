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
        private Timer _notesSaveTimer;
        private string _pendingPreviewPath;
        private string _lastSavedNotes;

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
            set
            {
                if (SetProperty(ref _currentNotes, value))
                {
                    // 延迟保存笔记，避免频繁保存
                    ScheduleNotesSave();
                }
            }
        }

        private void ScheduleNotesSave()
        {
            if (_notesSaveTimer == null)
            {
                _notesSaveTimer = new Timer(OnNotesSaveTimerTick, null, Timeout.Infinite, Timeout.Infinite);
            }
            // 延迟 500ms 保存笔记
            _notesSaveTimer.Change(500, Timeout.Infinite);
        }

        private void OnNotesSaveTimerTick(object state)
        {
            System.Windows.Application.Current.Dispatcher.BeginInvoke(new Action(() =>
            {
                if (SelectedItem != null && !string.IsNullOrEmpty(SelectedItem.Path))
                {
                    // 检查笔记是否真的改变了
                    if (CurrentNotes != _lastSavedNotes)
                    {
                        _lastSavedNotes = CurrentNotes;
                        // 发布保存笔记请求消息
                        _messageBus.Publish(new SaveNotesRequestMessage(SelectedItem.Path, CurrentNotes));
                    }
                }
            }));
        }

        private bool _isNotesVisible = true;
        public bool IsNotesVisible
        {
            get => _isNotesVisible;
            set => SetProperty(ref _isNotesVisible, value);
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
                    IsNotesVisible = m.ShowNotes;
                    if (m.SelectedItems?.Count > 0)
                    {
                        SelectedItem = m.SelectedItems[0] as FileSystemItem;
                        // 更新当前笔记：如果 SelectedItem 有 Notes 属性，直接使用；否则从数据库加载
                        if (SelectedItem != null && !string.IsNullOrEmpty(SelectedItem.Notes))
                        {
                            CurrentNotes = SelectedItem.Notes;
                        }
                        else if (SelectedItem != null)
                        {
                            // 从数据库加载笔记
                            CurrentNotes = YiboFile.Services.FileNotes.FileNotesService.GetFileNotes(SelectedItem.Path);
                        }
                        else
                        {
                            CurrentNotes = "";
                        }
                        // 确保 _lastSavedNotes 也被设置，避免误触发保存
                        _lastSavedNotes = CurrentNotes;
                        
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
                        CurrentNotes = ""; // 清除笔记
                        _lastSavedNotes = ""; // 清除最后保存的笔记
                        ActivePreview = null;
                    }
                }));
            });

            // 订阅笔记更新消息，实时更新笔记显示
            _messageBus.Subscribe<NotesUpdatedMessage>(m =>
            {
                System.Windows.Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                {
                    if (SelectedItem != null && string.Equals(SelectedItem.Path, m.FilePath, StringComparison.OrdinalIgnoreCase))
                    {
                        CurrentNotes = m.Notes;
                        if (SelectedItem != null)
                        {
                            SelectedItem.Notes = m.Notes;
                        }
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
