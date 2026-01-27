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

        public LibraryModule(IMessageBus messageBus, LibraryService libraryService)
            : base(messageBus)
        {
            _libraryService = libraryService ?? throw new ArgumentNullException(nameof(libraryService));

            InitializeCommands();

            // 监听库服务事件
            _libraryService.LibrariesLoaded += OnLibrariesLoadedFromService;
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
        }

        public void LoadLibraries()
        {
            var libs = _libraryService.LoadLibraries();
            UpdateLibrariesCollection(libs);
        }

        private void OnLibrariesLoadedFromService(object sender, System.Collections.Generic.List<Library> libs)
        {
            UpdateLibrariesCollection(libs);
        }

        private void UpdateLibrariesCollection(System.Collections.Generic.List<Library> libs)
        {
            _libraries.Clear();
            if (libs != null)
            {
                foreach (var lib in libs)
                    _libraries.Add(lib);
            }
            OnPropertyChanged(nameof(Libraries));
        }

        private void OnLibrarySelected(Library library)
        {
            if (library == null) return;

            // 发布库选择变更消息，供 MainWindow 或其他模块响应
            // 此处可以触发 NavigationCoordinator 的逻辑
            Publish(new LibrarySelectedMessage(library));
        }

        protected override void OnShutdown()
        {
            _libraryService.LibrariesLoaded -= OnLibrariesLoadedFromService;
            base.OnShutdown();
        }
    }
}
