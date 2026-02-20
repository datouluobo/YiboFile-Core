using System;
using System.Collections.ObjectModel;
using System.Windows.Input;
using System.Threading;
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
        private readonly ConfigurationService _configService;

        private readonly FileListService _fileListService;
        private Timer _debounceTimer;
        private string _pendingPreviewPath;

        private bool _isVisible = true;
        private bool _isLayoutVisible = true; // 用户可见性设置
        private bool _isMainLayoutVisible = true; // 布局全局设置（特殊面板）
        private bool _isDualListActive = false; // 是否处于双列表模式
        public bool IsVisible
        {
            get => _isVisible;
            set
            {
                if (SetProperty(ref _isVisible, value))
                {
                    NotifyVisibilityChanged();
                    _configService.Set(x => x.IsRightPanelVisible, value);
                }

            }
        }

        public bool IsLayoutVisible
        {
            get => _isLayoutVisible;
            set
            {
                if (SetProperty(ref _isLayoutVisible, value))
                {
                    NotifyVisibilityChanged();
                }
            }
        }

        public bool IsMainLayoutVisible
        {
            get => _isMainLayoutVisible;
            set
            {
                if (SetProperty(ref _isMainLayoutVisible, value))
                {
                    NotifyVisibilityChanged();
                }
            }
        }

        public bool IsDualListActive
        {
            get => _isDualListActive;
            set
            {
                if (SetProperty(ref _isDualListActive, value))
                {
                    NotifyVisibilityChanged();
                }
            }
        }

        public bool EffectiveVisibility => IsVisible && IsLayoutVisible && IsMainLayoutVisible && !IsDualListActive;

        private void NotifyVisibilityChanged()
        {
            OnPropertyChanged(nameof(EffectiveVisibility));

        }

        private double _notesHeight;
        public double NotesHeight
        {
            get => _notesHeight;
            set
            {
                if (SetProperty(ref _notesHeight, value))
                {
                    _configService.Set(x => x.RightPanelNotesHeight, value);
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
                    // UpdatePreview logic is now handled in message subscription or explicitly called
                }
            }
        }

        public RightPanelViewModel(IMessageBus messageBus, ConfigurationService configService, FileListService fileListService)

        {
            _messageBus = messageBus ?? throw new ArgumentNullException(nameof(messageBus));
            _configService = configService ?? throw new ArgumentNullException(nameof(configService));
            _fileListService = fileListService ?? throw new ArgumentNullException(nameof(fileListService));

            var cfg = _configService.Config;
            _isVisible = cfg.IsRightPanelVisible;
            _notesHeight = cfg.RightPanelNotesHeight;

            _messageBus.Subscribe<FileSelectionChangedMessage>(m =>
            {


                // 确保 UI 状态更新在调度器线程执行
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
                // 如果当前没有选中的文件，则显示背景容器（如当前文件夹）的信息
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
                // Ensure UI update happens on UI thread
                System.Windows.Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                {
                    ActivePreview = m.Preview;
                }));
            });

            _messageBus.Subscribe<DualListModeChangedMessage>(m =>
            {
                IsDualListActive = m.IsEnabled;
            });

            _messageBus.Subscribe<MainLayoutVisibilityChangedMessage>(m =>
            {
                IsMainLayoutVisible = m.IsVisible;
            });

            _debounceTimer = new Timer(OnDebounceTick, null, Timeout.Infinite, Timeout.Infinite);
        }

        private void UpdatePreview(string path)
        {


            // Immediate clear if path is null
            if (string.IsNullOrEmpty(path))
            {
                _pendingPreviewPath = null;
                _debounceTimer.Change(Timeout.Infinite, Timeout.Infinite);
                ActivePreview = null;
                return;
            }



            // Debounce request
            _pendingPreviewPath = path;
            _debounceTimer.Change(250, Timeout.Infinite);
        }

        private void OnDebounceTick(object state)
        {
            var path = _pendingPreviewPath;
            if (!string.IsNullOrEmpty(path))
            {

                _messageBus.Publish(new PreviewRequestMessage(path));
            }
        }
    }
}
