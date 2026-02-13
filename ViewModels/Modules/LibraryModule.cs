using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using YiboFile.Models;
using YiboFile.Services;
using YiboFile.ViewModels.Messaging;
using YiboFile.ViewModels.Messaging.Messages;
using YiboFile.Services.Navigation;

namespace YiboFile.ViewModels.Modules
{
    /// <summary>
    /// 库管理模块
    /// 处理库的加载、选择和管理操作
    /// </summary>
    public class LibraryModule : ModuleBase
    {
        private readonly LibraryService _libraryService;
        private Library _selectedLibrary;
        private ObservableCollection<Library> _libraries = new();

        public override string Name => "Library";

        /// <summary>
        /// 所有库列表
        /// </summary>
        public ObservableCollection<Library> Libraries
        {
            get => _libraries;
            private set => SetProperty(ref _libraries, value);
        }

        private bool _isSilentUpdate = false;
        private bool _isInitialLoad = true;

        /// <summary>
        /// 当前选中的库
        /// </summary>
        public Library SelectedLibrary
        {
            get => _selectedLibrary;
            set
            {
                // [硬核防御] 如果处于模式切换、静默更新、初始加载，或者最重要的：【不是由命令触发的人为操作】
                // 我们拒绝接受任何非空的赋值。这能彻底封杀 WPF ListBox 的自动选择行为。
                if (value != null && (_isSilentUpdate || _isModeChanging || _isInitialLoad || !_isUserInitiated))
                {
                    System.Diagnostics.Debug.WriteLine($"[NAV-DEBUG] LibraryModule: Rejected automated/silent selection of '{value.Name}'. UserInitiated={_isUserInitiated}, ModeChanging={_isModeChanging}, InitialLoad={_isInitialLoad}");
                    // 拒绝赋值，直接返回。不触发 SetProperty，也不触发 NotifyPropertyChanged
                    return;
                }

                if (SetProperty(ref _selectedLibrary, value))
                {
                    System.Diagnostics.Debug.WriteLine($"[NAV-DEBUG] LibraryModule: SelectedLibrary changed to '{(value?.Name ?? "null")}'. UserInitiated={_isUserInitiated}");
                    OnLibrarySelected(value);
                }
            }
        }

        private void SetSelectedLibrarySilently(Library library)
        {
            if (library == null || SelectedLibrary == library) return;

            _isSilentUpdate = true;
            try
            {
                SelectedLibrary = library;
            }
            finally
            {
                _isSilentUpdate = false;
            }
        }

        public ICommand LoadLibrariesCommand { get; private set; }
        public ICommand AddLibraryCommand { get; private set; }
        public ICommand DeleteLibraryCommand { get; private set; }
        public ICommand OpenLibraryCommand { get; private set; }

        public LibraryModule(IMessageBus messageBus, LibraryService libraryService)
            : base(messageBus)
        {
            _libraryService = libraryService ?? throw new ArgumentNullException(nameof(libraryService));

            InitializeCommands();

            if (_libraryService != null)
            {
                _libraryService.LibrariesLoaded += OnLibrariesLoadedFromService;
                _libraryService.LibraryFilesLoaded += OnLibraryFilesLoadedFromService;
                _libraryService.LibraryHighlightRequested += OnLibraryHighlightRequestedFromService;
            }

            // 在构造函数末尾，异步标记初始化完成
            System.Windows.Application.Current.Dispatcher.BeginInvoke(new Action(() =>
            {
                _isInitialLoad = false;
            }), System.Windows.Threading.DispatcherPriority.ApplicationIdle);
        }

        private void InitializeCommands()
        {
            LoadLibrariesCommand = new RelayCommand(() => LoadLibraries());

            AddLibraryCommand = new RelayCommand<string>(name =>
            {
                if (!string.IsNullOrEmpty(name))
                    _libraryService.AddLibrary(name);
            });

            DeleteLibraryCommand = new RelayCommand<Library>(lib =>
            {
                if (lib != null)
                    _libraryService.DeleteLibrary(lib.Id, lib.Name);
            });

            OpenLibraryCommand = new RelayCommand<Library>(lib =>
            {
                if (lib != null)
                {
                    _isUserInitiated = true;
                    SelectedLibrary = lib;
                    // 移除 Publish(new NavigateToLibraryMessage(lib));
                    // 因为 SelectedLibrary 的 Setter 已经会触发带有 PaneId 路由的 LibrarySelectedMessage
                }
            });
        }

        private Library _lastLoadedLibrary;
        private bool _isModeChanging = false;
        private PaneId _activePaneId = PaneId.Main;

        protected override void OnInitialize()
        {
            // 初始加载
            LoadLibraries();

            // 订阅焦点变更，确保侧边栏操作能定向到最后一次激活的面板
            Subscribe<Messaging.Messages.FocusedPaneChangedMessage>(m =>
            {
                _activePaneId = m.IsSecondPaneFocused ? PaneId.Second : PaneId.Main;
            });

            // 监听导航模式变更
            Subscribe<NavigationModeChangedMessage>(m =>
            {
                if (m.Mode == "Library")
                {
                    // 开启静默屏障，防止模式切换期间产生的任何变更触发导航
                    _isModeChanging = true;
                    _isSilentUpdate = true;
                    try
                    {
                        // [移除自动高亮] 不再尝试恢复历史选中项，而是强制设为 null
                        // 这样进入列表时它是干净的，只有用户点击才会触发导航
                        SelectedLibrary = null;
                        _lastLoadedLibrary = null;
                    }
                    finally
                    {
                        System.Windows.Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                        {
                            _isSilentUpdate = false;
                            _isModeChanging = false;
                        }), System.Windows.Threading.DispatcherPriority.ApplicationIdle);
                    }
                }
            });

            Subscribe<ToggleLibraryPathRequestMessage>(OnToggleLibraryPathRequest);
            Subscribe<CreateLibraryRequestMessage>(OnCreateLibraryRequest);

            // 关键：当库文件加载成功时，记录并同步高亮
            Subscribe<LibraryFilesLoadedMessage>(msg =>
            {
                _lastLoadedLibrary = msg.Library;
                // 注意：如果当前已经在库侧边栏模式，实时同步高亮
                SetSelectedLibrarySilently(msg.Library);
            });
        }

        private void OnToggleLibraryPathRequest(ToggleLibraryPathRequestMessage msg)
        {
            if (msg.Library == null || msg.Paths == null || msg.Paths.Count == 0) return;

            bool anyIn = msg.Paths.Any(p => msg.Library.Paths != null && msg.Library.Paths.Contains(p));
            bool shouldAdd = msg.ForceAdd || !anyIn;

            foreach (var path in msg.Paths)
            {
                if (shouldAdd) _libraryService.AddLibraryPath(msg.Library.Id, path);
                else _libraryService.RemoveLibraryPath(msg.Library.Id, path);
            }

            // 加载库以更新其内部路径列表
            LoadLibraries();
        }

        private void OnCreateLibraryRequest(CreateLibraryRequestMessage msg)
        {
            if (string.IsNullOrWhiteSpace(msg.Name)) return;

            int newLibId = _libraryService.AddLibrary(msg.Name);
            if (newLibId != 0 && msg.InitialPaths != null)
            {
                int targetId = Math.Abs(newLibId);
                foreach (var path in msg.InitialPaths)
                {
                    _libraryService.AddLibraryPath(targetId, path);
                }
            }
            LoadLibraries();
        }

        public void LoadLibraries()
        {
            var libs = _libraryService.LoadLibraries();
            UpdateLibrariesCollection(libs);
        }

        private void OnLibrariesLoadedFromService(object sender, System.Collections.Generic.List<Library> libs)
        {
            // Ensure UI thread for ObservableCollection update
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                UpdateLibrariesCollection(libs);
            });
        }

        private void UpdateLibrariesCollection(System.Collections.Generic.List<Library> libs)
        {
            _isSilentUpdate = true;
            try
            {
                var newCollection = new ObservableCollection<Library>();
                if (libs != null)
                {
                    foreach (var lib in libs)
                    {
                        newCollection.Add(lib);
                    }
                }
                // Replacing the instance triggers PropertyChanged("Libraries") via SetProperty
                Libraries = newCollection;
                Publish(new LibraryListChangedMessage());

                // [已移除自动高亮同步] 彻底移除加载列表后的自动选中逻辑
                // 确保只有用户主动点击时才触发导航
                SelectedLibrary = null;
            }
            finally
            {
                // 异步解除屏障
                System.Windows.Application.Current.Dispatcher.BeginInvoke(new Action(() => _isSilentUpdate = false), System.Windows.Threading.DispatcherPriority.Loaded);
            }
        }

        private bool _isUserInitiated = false;

        private void OnLibrarySelected(Library library)
        {
            // 只有在非静默更新且非正在切换模式且【显式的人为操作】时才触发导航
            // 这可以防止 WPF ListBox 在 ItemsSource 变化时自动选择第一项导致的误导航
            if (library == null || _isSilentUpdate || _isModeChanging || _isInitialLoad) return;

            if (!_isUserInitiated)
            {
                // [防御性拦截] 如果不是用户显式点击导致的选中，我们只允许它的视觉高亮（如果屏障已解），
                // 但拒绝产生任何实际的导航消息
                return;
            }

            try
            {
                // 发布库选择变更消息，定向到最后活跃的面板
                Publish(new LibrarySelectedMessage(library, _activePaneId));
            }
            finally
            {
                _isUserInitiated = false;
            }
        }

        private void OnLibraryFilesLoadedFromService(object sender, LibraryFilesLoadedEventArgs e)
        {
            Publish(new LibraryFilesLoadedMessage(e.Library, e.Files, e.IsEmpty, e.TargetPane));
        }

        private void OnLibraryHighlightRequestedFromService(object sender, Library library)
        {
            if (library != null)
            {
                SetSelectedLibrarySilently(library);
            }
        }

        protected override void OnShutdown()
        {
            if (_libraryService != null)
            {
                _libraryService.LibrariesLoaded -= OnLibrariesLoadedFromService;
                _libraryService.LibraryFilesLoaded -= OnLibraryFilesLoadedFromService;
                _libraryService.LibraryHighlightRequested -= OnLibraryHighlightRequestedFromService;
            }
            base.OnShutdown();
        }
    }
}
