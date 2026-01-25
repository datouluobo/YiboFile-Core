using System.Collections.Generic;
using System.Collections.ObjectModel;
using YiboFile.Services.Config;

namespace YiboFile.ViewModels
{
    public partial class SettingsViewModel
    {
        #region Hotkey Settings
        private ObservableCollection<HotkeyItemViewModel> _hotkeys;
        public ObservableCollection<HotkeyItemViewModel> Hotkeys
        {
            get => _hotkeys;
            set => SetProperty(ref _hotkeys, value);
        }

        private void InitializeHotkeySettings(AppConfig config)
        {
            var defaults = new List<HotkeyItemViewModel>
            {
                new HotkeyItemViewModel("新建标签页", "Ctrl+T"),
                new HotkeyItemViewModel("关闭标签页", "Ctrl+W"),
                new HotkeyItemViewModel("下一个标签", "Ctrl+Tab"),
                new HotkeyItemViewModel("上一个标签", "Ctrl+Shift+Tab"),
                new HotkeyItemViewModel("切换双面板焦点", "Tab"),

                new HotkeyItemViewModel("复制", "Ctrl+C"),
                new HotkeyItemViewModel("剪切", "Ctrl+X"),
                new HotkeyItemViewModel("粘贴", "Ctrl+V"),
                new HotkeyItemViewModel("删除 (移到回收站)", "Delete"),
                new HotkeyItemViewModel("永久删除", "Shift+Delete"),
                new HotkeyItemViewModel("重命名", "F2"),
                new HotkeyItemViewModel("全选", "Ctrl+A"),
                new HotkeyItemViewModel("新建文件夹", "Ctrl+N"),
                new HotkeyItemViewModel("新建窗口", "Ctrl+Shift+N"),

                new HotkeyItemViewModel("撤销", "Ctrl+Z"),
                new HotkeyItemViewModel("重做", "Ctrl+Y"),

                new HotkeyItemViewModel("返回上级目录", "Backspace"),
                new HotkeyItemViewModel("地址栏编辑", "Alt+D"),
                new HotkeyItemViewModel("刷新", "F5"),
                new HotkeyItemViewModel("打开文件/文件夹", "Enter"),

                new HotkeyItemViewModel("QuickLook 预览", "Space"),
                new HotkeyItemViewModel("属性", "Alt+Enter"),

                new HotkeyItemViewModel("专注模式", "Ctrl+Shift+F"),
                new HotkeyItemViewModel("工作模式", "Ctrl+Shift+W"),
                new HotkeyItemViewModel("完整模式", "Ctrl+Shift+A"),
            };

            var customs = config.CustomHotkeys ?? new Dictionary<string, string>();

            foreach (var item in defaults)
            {
                if (customs.TryGetValue(item.Description, out var customKey))
                {
                    item.KeyCombination = customKey;
                }

                item.PropertyChanged += (s, e) =>
                {
                    if (e.PropertyName == nameof(HotkeyItemViewModel.KeyCombination))
                    {
                        SaveHotkeySettings();
                    }
                };
            }

            Hotkeys = new ObservableCollection<HotkeyItemViewModel>(defaults);
        }

        private void ResetHotkeys()
        {
            foreach (var item in Hotkeys)
            {
                item.KeyCombination = item.DefaultKey;
            }
            SaveHotkeySettings();
        }

        private void ResetSingleHotkey(HotkeyItemViewModel item)
        {
            if (item != null)
            {
                item.KeyCombination = item.DefaultKey;
                SaveHotkeySettings();
            }
        }

        private void SaveHotkeySettings()
        {
            var customs = new Dictionary<string, string>();
            foreach (var item in Hotkeys)
            {
                if (item.IsModified)
                {
                    customs[item.Description] = item.KeyCombination;
                }
            }
            ConfigurationService.Instance.Update(c => c.CustomHotkeys = customs);
        }
        #endregion
    }
}
