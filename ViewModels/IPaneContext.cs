using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using YiboFile.Models;
using YiboFile.Controls;

namespace YiboFile.ViewModels
{
    /// <summary>
    /// 面板上下文接口，定义面板的基本状态和操作
    /// </summary>
    public interface IPaneContext : INotifyPropertyChanged
    {
        /// <summary>
        /// 当前路径（文件系统路径或虚拟路径）
        /// </summary>
        string CurrentPath { get; set; }

        /// <summary>
        /// 当前库（库模式时使用）
        /// </summary>
        Library CurrentLibrary { get; set; }

        /// <summary>
        /// 当前标签（标签模式时使用）
        /// </summary>
        TagViewModel CurrentTag { get; set; }

        /// <summary>
        /// 当前导航模式 ("Path", "Library", "Tag")
        /// </summary>
        string NavigationMode { get; set; }

        /// <summary>
        /// 文件列表
        /// </summary>
        ObservableCollection<FileSystemItem> Files { get; }

        /// <summary>
        /// 是否正在加载
        /// </summary>
        bool IsLoading { get; set; }

        /// <summary>
        /// 是否为次要面板（双栏模式的右侧面板）
        /// </summary>
        bool IsSecondary { get; }

        /// <summary>
        /// 导航到指定路径
        /// </summary>
        void NavigateTo(string path);

        /// <summary>
        /// 导航到指定库
        /// </summary>
        void NavigateTo(Library library);

        /// <summary>
        /// 导航到指定标签
        /// </summary>
        void NavigateTo(TagViewModel tag);

        /// <summary>
        /// 刷新当前视图
        /// </summary>
        void Refresh();
    }
}

