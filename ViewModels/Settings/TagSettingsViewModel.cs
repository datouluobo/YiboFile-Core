using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using Microsoft.Extensions.DependencyInjection;
using YiboFile.Services.Features;

namespace YiboFile.ViewModels.Settings
{
    public class TagSettingsViewModel : BaseViewModel
    {
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

        public TagSettingsViewModel()
        {
            RefreshTagGroupsCommand = new RelayCommand(RefreshTagGroups);
            AddTagGroupCommand = new RelayCommand(AddTagGroup);
            RenameTagGroupCommand = new RelayCommand<TagGroupManageViewModel>(g => RenameTagGroupRequested?.Invoke(this, g));
            DeleteTagGroupCommand = new RelayCommand<TagGroupManageViewModel>(DeleteTagGroup);
            AddTagCommand = new RelayCommand<TagGroupManageViewModel>(AddTag);
            RenameTagCommand = new RelayCommand<TagItemManageViewModel>(t => RenameTagRequested?.Invoke(this, t));
            DeleteTagCommand = new RelayCommand<TagItemManageViewModel>(DeleteTag);
            UpdateTagColorCommand = new RelayCommand<TagItemManageViewModel>(t => UpdateTagColorRequested?.Invoke(this, t));

            InitializeTagManagement();
        }

        ~TagSettingsViewModel()
        {
            if (_tagService != null)
            {
                _tagService.TagUpdated -= OnTagServiceTagUpdated;
            }
        }

        private void InitializeTagManagement()
        {
            _tagService = App.ServiceProvider?.GetService<ITagService>();
            if (_tagService != null)
            {
                _tagService.TagUpdated -= OnTagServiceTagUpdated;
                _tagService.TagUpdated += OnTagServiceTagUpdated;
            }
            RefreshTagGroups();
        }

        private void OnTagServiceTagUpdated(int tagId, string newColor)
        {
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                if (TagGroups != null)
                {
                    foreach (var group in TagGroups)
                    {
                        var tag = group.Tags.FirstOrDefault(t => t.Id == tagId);
                        if (tag != null)
                        {
                            tag.Color = newColor;
                        }
                    }
                }
            });
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
