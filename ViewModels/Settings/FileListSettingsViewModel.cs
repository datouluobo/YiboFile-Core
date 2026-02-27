using System;
using YiboFile.Services.Config;

namespace YiboFile.ViewModels.Settings
{
    public class FileListSettingsViewModel : BaseViewModel
    {
        private readonly IConfigurationService _configService;

        public FileListSettingsViewModel(IConfigurationService configService)
        {
            _configService = configService;
            LoadFromConfig();
        }

        public void LoadFromConfig()
        {
            var config = _configService.GetSnapshot();
            _colTagsWidth = config.ColTagsWidth > 0 ? config.ColTagsWidth : 150;
            _colNotesWidth = config.ColNotesWidth > 0 ? config.ColNotesWidth : 200;
        }

        private double _colTagsWidth;
        private string _colTagsWidthInput;
        public double ColTagsWidth
        {
            get => _colTagsWidth;
            set
            {
                value = Math.Clamp(value, 50, 500);
                bool changed = SetProperty(ref _colTagsWidth, value);
                {
                    _colTagsWidthInput = null;
                    OnPropertyChanged(nameof(ColTagsWidthInput));
                    if (changed) _configService.Update(c => c.ColTagsWidth = value);
                }
            }
        }

        public string ColTagsWidthInput
        {
            get => _colTagsWidthInput ?? _colTagsWidth.ToString();
            set => SetProtectedNumber(ref _colTagsWidthInput, ref _colTagsWidth, value, 50, 500, v => ColTagsWidth = v);
        }

        private double _colNotesWidth;
        private string _colNotesWidthInput;
        public double ColNotesWidth
        {
            get => _colNotesWidth;
            set
            {
                value = Math.Clamp(value, 100, 800);
                bool changed = SetProperty(ref _colNotesWidth, value);
                {
                    _colNotesWidthInput = null;
                    OnPropertyChanged(nameof(ColNotesWidthInput));
                    if (changed) _configService.Update(c => c.ColNotesWidth = value);
                }
            }
        }

        public string ColNotesWidthInput
        {
            get => _colNotesWidthInput ?? _colNotesWidth.ToString();
            set => SetProtectedNumber(ref _colNotesWidthInput, ref _colNotesWidth, value, 100, 800, v => ColNotesWidth = v);
        }
    }
}
