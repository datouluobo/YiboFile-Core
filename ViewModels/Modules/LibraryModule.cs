using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using YiboFile.Models;
using YiboFile.Services;
using YiboFile.ViewModels.Messaging;
using YiboFile.ViewModels.Messaging.Messages;

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

        /// <summary>
        /// 当前选中的库
        /// </summary>
        public Library SelectedLibrary
        {
            get => _selectedLibrary;
            set
            {
                if (SetProperty(ref _selectedLibrary, value))
                {
                    OnLibrarySelected(value);
                }
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
                    SelectedLibrary = lib;
                    Publish(new NavigateToLibraryMessage(lib));
                }
            });
        }

        protected override void OnInitialize()
        {
            // 初始加载
            LoadLibraries();

            // 订阅导航模式变更，以便在进入库模式时确保有库被选中
            Subscribe<NavigationModeChangedMessage>(m =>
            {
                if (m.Mode == "Library" && SelectedLibrary == null)
                {
                    SelectedLibrary = Libraries.FirstOrDefault();
                }
            });

            Subscribe<ToggleLibraryPathRequestMessage>(OnToggleLibraryPathRequest);
            Subscribe<CreateLibraryRequestMessage>(OnCreateLibraryRequest);
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
        }

        private void OnLibrarySelected(Library library)
        {
            if (library == null) return;

            // 发布库选择变更消息，供 MainWindow 或其他模块响应
            // 此处可以触发 NavigationCoordinator 的逻辑
            Publish(new LibrarySelectedMessage(library));
        }

        private void OnLibraryFilesLoadedFromService(object sender, LibraryFilesLoadedEventArgs e)
        {
            Publish(new LibraryFilesLoadedMessage(e.Library, e.Files, e.IsEmpty, e.TargetPane));
        }

        private void OnLibraryHighlightRequestedFromService(object sender, Library library)
        {
            // 这里可以复用 LibrarySelectedMessage 或者定义特定的 HighlightLibraryMessage
            // 为了最小化变更，我们先发布 LibrarySelectedMessage，如果不合适再区分
            Publish(new LibrarySelectedMessage(library));
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
