using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;

namespace YiboFile.Controls
{
    public partial class AboutPanelControl : UserControl
    {
        public event EventHandler CloseRequested;

        public AboutPanelControl()
        {
            InitializeComponent();
            LoadShortcuts();
            LoadLicenses();
        }

        private void Tab_Click(object sender, RoutedEventArgs e)
        {
            if (ContentInfo == null || ContentShortcuts == null || ContentLicense == null) return;

            ContentInfo.Visibility = Visibility.Collapsed;
            ContentShortcuts.Visibility = Visibility.Collapsed;
            ContentLicense.Visibility = Visibility.Collapsed;

            if (TabInfo.IsChecked == true)
                ContentInfo.Visibility = Visibility.Visible;
            else if (TabShortcuts.IsChecked == true)
                ContentShortcuts.Visibility = Visibility.Visible;
            else if (TabLicense.IsChecked == true)
                ContentLicense.Visibility = Visibility.Visible;
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            CloseRequested?.Invoke(this, EventArgs.Empty);
        }

        private void LoadShortcuts()
        {
            var shortcuts = new List<ShortcutItem>
            {
                // 基础鼠标操作
                new ShortcutItem("打开文件/进入文件夹", "鼠标左键双击"),
                new ShortcutItem("返回上级目录", "鼠标左键双击空白处"),
                new ShortcutItem("上下文菜单", "鼠标右键点击"),
                new ShortcutItem("多选文件", "Ctrl / Shift + 点击"),
                
                // 视图与导航
                new ShortcutItem("缩略图缩放", "Ctrl + 滚轮滚动"),
                new ShortcutItem("新标签页打开文件夹", "鼠标中键点击"),
                new ShortcutItem("新标签页打开库", "鼠标中键点击库列表项"),
                
                // 常用操作
                new ShortcutItem("属性", "Alt + Enter"),
                new ShortcutItem("查找/筛选", "Ctrl + F"),
                new ShortcutItem("重命名", "F2"),
                new ShortcutItem("删除文件", "Delete"),
                new ShortcutItem("刷新", "F5"),
                
                // 界面模式
                new ShortcutItem("全屏模式", "F11 / F10"),
                new ShortcutItem("专注模式", "Ctrl + Shift + F"),
                new ShortcutItem("工作模式", "Ctrl + Shift + W"),
                new ShortcutItem("完整模式", "Ctrl + Shift + A")
            };

            // Template is now defined in XAML
            ShortcutsList.ItemsSource = shortcuts;
        }

        private void LoadLicenses()
        {
            var licenses = new List<LicenseItem>
            {
                // 核心组件
                new LicenseItem { Name = "FFMpegCore", Description = "FFmpeg的.NET封装库", License = "MIT License" },
                new LicenseItem { Name = "Magick.NET", Description = "强大的图像处理库 (ImageMagick封装)", License = "Apache-2.0" },
                new LicenseItem { Name = "AvalonEdit", Description = "基于WPF的代码编辑器组件", License = "MIT License" },
                new LicenseItem { Name = "Markdig", Description = "快速且强大的Markdown处理器", License = "BSD-2-Clause" },
                new LicenseItem { Name = "SkiaSharp", Description = "跨平台2D图形API", License = "MIT License" },
                new LicenseItem { Name = "WpfAnimatedGif", Description = "WPF GIF动画支持", License = "Apache-2.0" },
                new LicenseItem { Name = "Microsoft.Web.WebView2", Description = "Edge WebView2 控件", License = "Microsoft License" },
                new LicenseItem { Name = "IxMilia.Dxf", Description = "DXF文件读写库", License = "MIT License" },
                
                // 机器学习
                new LicenseItem { Name = "Microsoft.ML", Description = "跨平台机器学习框架", License = "MIT License" },
                new LicenseItem { Name = "SciSharp.TensorFlow.Redist", Description = "TensorFlow重新分发包", License = "Apache-2.0" },

                // 字体与图标资源
                new LicenseItem { Name = "Remix Icon", Description = "开源中性风格图标系统", License = "Apache-2.0" },
                new LicenseItem { Name = "Fluent System Icons", Description = "微软流利设计系统图标", License = "MIT License" },
                new LicenseItem { Name = "Material Icons", Description = "Google Material Design图标", License = "Apache-2.0" },

                // 其他组件
                new LicenseItem { Name = "Ookii.Dialogs.Wpf", Description = "现代化的文件选择对话框", License = "BSD-3-Clause" },
                new LicenseItem { Name = "Newtonsoft.Json", Description = "高性能JSON处理库", License = "MIT License" },
                new LicenseItem { Name = "PDF.js", Description = "基于Web标准的PDF渲染引擎", License = "Apache-2.0" }
            };
            LicensesList.ItemsSource = licenses;
        }
    }

    public class ShortcutItem
    {
        public string Action { get; set; }
        public string Keys { get; set; }
        public ShortcutItem(string action, string keys) { Action = action; Keys = keys; }
    }

    public class LicenseItem
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public string License { get; set; }
    }
}

