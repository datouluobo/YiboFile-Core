using System;
using YiboFile.Services.Config;

namespace YiboFile.ViewModels.Settings
{
    public class FileListSettingsViewModel : BaseViewModel
    {
        public FileListSettingsViewModel()
        {
            LoadFromConfig();
        }

        public void LoadFromConfig()
        {
            var config = ConfigurationService.Instance.GetSnapshot();
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
                    ConfigurationService.Instance.Update(c => c.ColTagsWidth = value);
            }
        }

        private double _colNotesWidth;
        public double ColNotesWidth
        {
            get => _colNotesWidth;
            set
            {
                if (SetProperty(ref _colNotesWidth, value))
                    ConfigurationService.Instance.Update(c => c.ColNotesWidth = value);
            }
        }
    }
}
