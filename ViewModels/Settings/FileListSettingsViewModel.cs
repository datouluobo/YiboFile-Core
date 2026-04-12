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

        private double _colNotesWidth;
        private string _colNotesWidthInput;
        public double ColNotesWidth
        {
            get => _colNotesWidth;
            set
            {
                value = Math.Clamp(value, 100, 800);
                if (Math.Abs(_colNotesWidth - value) < 0.001)
                {
                    _colNotesWidthInput = null;
                    OnPropertyChanged(nameof(ColNotesWidthInput));
                    return;
                }

                // 1. 同步底层模型
                _colNotesWidth = value;
                SyncNotesWidthToState(value);

                // 2. 同步配置门面
                _configService.Update(c => {
                    c.ColNotesWidth = value;
                    c.ColNotesWidth_Secondary = value;
                });

                // 3. 通知 UI
                OnPropertyChanged();
                _colNotesWidthInput = null;
                OnPropertyChanged(nameof(ColNotesWidthInput));
            }
        }

        public string ColNotesWidthInput
        {
            get => _colNotesWidthInput ?? _colNotesWidth.ToString();
            set => SetProtectedNumber(ref _colNotesWidthInput, ref _colNotesWidth, value, 100, 800, v => ColNotesWidth = v);
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

        /// <summary>
        /// 将备注列宽度同步到 AppState 的所有面板
        /// </summary>
        private void SyncNotesWidthToState(double width)
        {
            try
            {
                var state = ConfigurationService.Instance.State;
                if (state?.Panes != null)
                {
                    foreach (var pane in state.Panes)
                    {
                        pane.Columns.ColNotesWidth = width;
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
