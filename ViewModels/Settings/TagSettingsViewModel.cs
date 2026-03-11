using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using Microsoft.Extensions.DependencyInjection;
using YiboFile.Services.Features;
using YiboFile.ViewModels.Messaging;
using YiboFile.ViewModels.Messaging.Messages;

namespace YiboFile.ViewModels.Settings
{
    public class TagSettingsViewModel : BaseViewModel
    {
        private readonly YiboFile.Services.Config.IConfigurationService _configService;

        private ObservableCollection<TagGroupManageViewModel> _tagGroups;
        public ObservableCollection<TagGroupManageViewModel> TagGroups
        {
            get => _tagGroups;
            set => SetProperty(ref _tagGroups, value);
        }

        private string _newGroupName;
        public string NewGroupName
        {
            get => _newGroupName;
            set => SetProperty(ref _newGroupName, value);
        }

        private ITagService _tagService;
        private readonly IMessageBus _messageBus;

        public ICommand RefreshTagGroupsCommand { get; }
        public ICommand AddTagGroupCommand { get; }
        public ICommand RenameTagGroupCommand { get; }
        public ICommand DeleteTagGroupCommand { get; }
        public ICommand AddTagCommand { get; }
        public ICommand RenameTagCommand { get; }
        public ICommand DeleteTagCommand { get; }
        public ICommand UpdateTagColorCommand { get; }

        public event EventHandler<TagGroupManageViewModel> RenameTagGroupRequested;
        public event EventHandler<TagItemManageViewModel> RenameTagRequested;
        public event EventHandler<TagItemManageViewModel> UpdateTagColorRequested;

        public TagSettingsViewModel(YiboFile.Services.Config.IConfigurationService configService = null)
        {
            _configService = configService ?? App.ServiceProvider?.GetService<YiboFile.Services.Config.IConfigurationService>();
            _messageBus = App.ServiceProvider?.GetService<IMessageBus>();
            RefreshTagGroupsCommand = new RelayCommand(RefreshTagGroups);
            AddTagGroupCommand = new RelayCommand(AddTagGroup);
            RenameTagGroupCommand = new RelayCommand<TagGroupManageViewModel>(g => RenameTagGroupRequested?.Invoke(this, g));
            DeleteTagGroupCommand = new RelayCommand<TagGroupManageViewModel>(DeleteTagGroup);
            AddTagCommand = new RelayCommand<TagGroupManageViewModel>(AddTag);
            RenameTagCommand = new RelayCommand<TagItemManageViewModel>(t => RenameTagRequested?.Invoke(this, t));
            DeleteTagCommand = new RelayCommand<TagItemManageViewModel>(DeleteTag);
            UpdateTagColorCommand = new RelayCommand<TagItemManageViewModel>(t => UpdateTagColorRequested?.Invoke(this, t));

            InitializeTagManagement();
            LoadFromConfig();
        }

        public void LoadFromConfig()
        {
            if (_configService == null) return;
            var config = _configService.GetSnapshot();
            _tagFontSize = config.TagFontSize > 0 ? config.TagFontSize : 16;
            _tagBoxWidth = config.TagBoxWidth;
        }


        private double _tagFontSize;
        private string _tagFontSizeInput;
        public double TagFontSize
        {
            get => _tagFontSize;
            set
            {
                value = Math.Clamp(value, 10, 48);
                if (SetProperty(ref _tagFontSize, value))
                {
                    _tagFontSizeInput = null;
                    OnPropertyChanged(nameof(TagFontSizeInput));
                    _configService?.Update(c => c.TagFontSize = value);
                }
                else
                {
                    _tagFontSizeInput = null;
                    OnPropertyChanged(nameof(TagFontSizeInput));
                }
            }
        }

        public string TagFontSizeInput
        {
            get => _tagFontSizeInput ?? _tagFontSize.ToString();
            set => SetProtectedNumber(ref _tagFontSizeInput, ref _tagFontSize, value, 10, 48, v => TagFontSize = v);
        }

        private double _tagBoxWidth;
        private string _tagBoxWidthInput;
        public double TagBoxWidth
        {
            get => _tagBoxWidth;
            set
            {
                value = Math.Clamp(value, 0, 500);
                if (SetProperty(ref _tagBoxWidth, value))
                {
                    _tagBoxWidthInput = null;
                    OnPropertyChanged(nameof(TagBoxWidthInput));
                    _configService?.Update(c => c.TagBoxWidth = value);
                }
                else
                {
                    _tagBoxWidthInput = null;
                    OnPropertyChanged(nameof(TagBoxWidthInput));
                }
            }
        }

        public string TagBoxWidthInput
        {
            get => _tagBoxWidthInput ?? _tagBoxWidth.ToString();
            set => SetProtectedNumber(ref _tagBoxWidthInput, ref _tagBoxWidth, value, 0, 500, v => TagBoxWidth = v);
        }

        ~TagSettingsViewModel()
        {
            _messageBus?.Unsubscribe<TagListChangedMessage>(OnTagListChanged);
        }

        private void InitializeTagManagement()
        {
            _tagService = App.ServiceProvider?.GetService<ITagService>();
            _messageBus?.Subscribe<TagListChangedMessage>(OnTagListChanged);
            RefreshTagGroups();
        }

        private void OnTagListChanged(TagListChangedMessage msg)
        {
            RefreshTagGroups();
        }



        public void RefreshTagGroups()
        {
            if (_tagService == null)
            {
                _tagService = App.ServiceProvider?.GetService<ITagService>();
            }
            if (_tagService == null) return;

            var list = new ObservableCollection<TagGroupManageViewModel>();
            var groups = _tagService.GetTagGroups();

            foreach (var group in groups)
            {
                var groupVm = new TagGroupManageViewModel
                {
                    Id = group.Id,
                    Name = group.Name,
                    Color = group.Color,
                    Tags = new ObservableCollection<TagItemManageViewModel>()
                };

                var tags = _tagService.GetTagsByGroup(group.Id);
                foreach (var tag in tags)
                {
                    groupVm.Tags.Add(new TagItemManageViewModel
                    {
                        Id = tag.Id,
                        Name = tag.Name,
                        Color = tag.Color ?? "#2E8B57",
                        GroupId = tag.GroupId
                    });
                }

                list.Add(groupVm);
            }

            TagGroups = list;
        }

        private void AddTagGroup()
        {
            if (string.IsNullOrWhiteSpace(NewGroupName)) return;
            try
            {
                _tagService?.AddTagGroup(NewGroupName);
                NewGroupName = string.Empty;
                RefreshTagGroups();
            }
            catch (Exception ex) { throw new Exception(ex.Message); }
        }

        public void RenameTagGroup(TagGroupManageViewModel group, string newName)
        {
            if (group == null || string.IsNullOrWhiteSpace(newName)) return;
            try
            {
                _tagService?.RenameTagGroup(group.Id, newName);
                RefreshTagGroups();
            }
            catch (Exception ex) { throw new Exception(ex.Message); }
        }

        private void DeleteTagGroup(TagGroupManageViewModel group)
        {
            if (group == null) return;
            try
            {
                _tagService?.DeleteTagGroup(group.Id);
                RefreshTagGroups();
            }
            catch (Exception ex) { throw new Exception(ex.Message); }
        }

        private void AddTag(TagGroupManageViewModel group)
        {
            if (group == null || string.IsNullOrWhiteSpace(group.NewTagText)) return;
            try
            {
                _tagService?.AddTag(group.Id, group.NewTagText);
                group.NewTagText = string.Empty;
                RefreshTagGroups();
            }
            catch (Exception ex) { throw new Exception(ex.Message); }
        }

        public void RenameTag(TagItemManageViewModel tag, string newName)
        {
            if (tag == null || string.IsNullOrWhiteSpace(newName)) return;
            try
            {
                _tagService?.RenameTag(tag.Id, newName);
                RefreshTagGroups();
            }
            catch (Exception ex) { throw new Exception(ex.Message); }
        }

        private void DeleteTag(TagItemManageViewModel tag)
        {
            if (tag == null) return;
            try
            {
                _tagService?.DeleteTag(tag.Id);
                RefreshTagGroups();
            }
            catch (Exception ex) { throw new Exception(ex.Message); }
        }

        public void UpdateTagColor(TagItemManageViewModel tag, string newColor)
        {
            if (tag == null) return;
            try
            {
                _tagService?.UpdateTagColor(tag.Id, newColor);
                RefreshTagGroups();
            }
            catch (Exception ex) { throw new Exception(ex.Message); }
        }
    }
}
