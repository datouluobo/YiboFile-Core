using System;
using System.Collections.ObjectModel;
using System.Windows.Input;
using System.Windows.Media;
using YiboFile.Models.Shell;

namespace YiboFile.ViewModels
{
    /// <summary>
    /// Shell 菜单项的视图模型
    /// </summary>
    public class ShellMenuItemViewModel : BaseViewModel
    {
        private string _text;
        private ImageSource _icon;
        private bool _isSeparator;
        private bool _isPinned;

        public string Text
        {
            get => _text;
            set => SetProperty(ref _text, value);
        }

        public ImageSource Icon
        {
            get => _icon;
            set => SetProperty(ref _icon, value);
        }

        public bool IsSeparator
        {
            get => _isSeparator;
            set => SetProperty(ref _isSeparator, value);
        }

        public bool IsPinned
        {
            get => _isPinned;
            set => SetProperty(ref _isPinned, value);
        }

        public ObservableCollection<ShellMenuItemViewModel> Children { get; } = new();

        /// <summary>
        /// 菜单项对应的原始数据
        /// </summary>
        internal ShellMenuItem ShellData { get; set; }

        /// <summary>
        /// 执行命令
        /// </summary>
        public ICommand ExecuteCommand { get; set; }

        /// <summary>
        /// 固定/取消固定命令
        /// </summary>
        public ICommand PinCommand { get; set; }

        /// <summary>
        /// 隐藏命令
        /// </summary>
        public ICommand HideCommand { get; set; }

        public ShellMenuItemViewModel(ShellMenuItem data)
        {
            ShellData = data;
            Text = data.Text;
            Icon = data.Icon;
            IsSeparator = data.IsSeparator;
            IsPinned = data.IsPinned;

            if (data.Children != null)
            {
                foreach (var child in data.Children)
                {
                    Children.Add(new ShellMenuItemViewModel(child));
                }
            }
        }
    }
}
