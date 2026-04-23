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
        }

        private double _colTagsWidth;
        private string _colTagsWidthInput;
        public double ColTagsWidth
        {
            get => _colTagsWidth;
            set
            {
                value = Math.Clamp(value, 50, 500);
                if (Math.Abs(_colTagsWidth - value) < 0.001)
                {
                    _colTagsWidthInput = null;
                    OnPropertyChanged(nameof(ColTagsWidthInput));
                    return;
                }

                // 1. 先更新底层数据模型，确保后续 UI 刷新能读到新值
                _colTagsWidth = value;
                SyncTagsWidthToState(value);

                // 2. 更新配置门面并发射全局变更消息（同时维护副面板属性一致性）
                _configService.Update(c => {
                    c.ColTagsWidth = value;
                    c.ColTagsWidth_Secondary = value;
                });

                // 3. 最后发出通知，触发面板 UI 逻辑执行 RefreshFileListColumns
                OnPropertyChanged();
                _colTagsWidthInput = null;
                OnPropertyChanged(nameof(ColTagsWidthInput));
            }
        }

        public string ColTagsWidthInput
        {
            get => _colTagsWidthInput ?? _colTagsWidth.ToString();
            set => SetProtectedNumber(ref _colTagsWidthInput, ref _colTagsWidth, value, 50, 500, v => ColTagsWidth = v);
        }

        /// <summary>
        /// 将标签列宽度同步到 AppState 的所有面板
        /// </summary>
        private void SyncTagsWidthToState(double width)
        {
            try
            {
                var state = ConfigurationService.Instance.State;
                if (state?.Panes != null)
                {
                    foreach (var pane in state.Panes)
                    {
                        pane.Columns.ColTagsWidth = width;
                    }
                }
            }
            catch { }
        }

        public bool IsRenameLostFocusCommit
        {
            get => _configService.GetSnapshot().RenameLostFocusBehavior == "Commit";
            set
            {
                if (value)
                {
                    _configService.Update(c => c.RenameLostFocusBehavior = "Commit");
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(IsRenameLostFocusCancel));
                }
            }
        }

        public bool IsRenameLostFocusCancel
        {
            get => _configService.GetSnapshot().RenameLostFocusBehavior == "Cancel";
            set
            {
                if (value)
                {
                    _configService.Update(c => c.RenameLostFocusBehavior = "Cancel");
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(IsRenameLostFocusCommit));
                }
            }
        }
    }
}
