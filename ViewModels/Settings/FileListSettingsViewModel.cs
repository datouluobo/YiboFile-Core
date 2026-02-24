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
        public double ColTagsWidth
        {
            get => _colTagsWidth;
            set
            {
                if (SetProperty(ref _colTagsWidth, value))
                    _configService.Update(c => c.ColTagsWidth = value);
            }
        }

        private double _colNotesWidth;
        public double ColNotesWidth
        {
            get => _colNotesWidth;
            set
            {
                if (SetProperty(ref _colNotesWidth, value))
                    _configService.Update(c => c.ColNotesWidth = value);
            }
        }
    }
}
