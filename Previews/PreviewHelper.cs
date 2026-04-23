using System;
using System.IO;
using System.Linq;
using System.Windows;

namespace YiboFile.Previews
{
    /// <summary>
    /// 预览辅助类 - 提供通用的文件与外部应用集成方法
    /// </summary>
    public static class PreviewHelper
    {
        /// <summary>
        /// 检测 QuickLook 是否安装
        /// </summary>
        public static bool IsQuickLookInstalled()
        {
            var commonPaths = new[]
            {
                @"C:\Program Files\QuickLook\QuickLook.exe",
                @"C:\Program Files (x86)\QuickLook\QuickLook.exe",
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"Programs\QuickLook\QuickLook.exe")
            };

            return commonPaths.Any(File.Exists);
        }

        /// <summary>
        /// 获取 QuickLook 可执行文件路径
        /// </summary>
        public static string GetQuickLookPath()
        {
            var commonPaths = new[]
            {
                @"C:\Program Files\QuickLook\QuickLook.exe",
                @"C:\Program Files (x86)\QuickLook\QuickLook.exe",
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"Programs\QuickLook\QuickLook.exe")
            };

            return commonPaths.FirstOrDefault(File.Exists);
        }

        /// <summary>
        /// 格式化文件大小
        /// </summary>
        public static string FormatFileSize(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB", "TB" };
            double len = bytes;
            int order = 0;
            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len = len / 1024;
            }
            return $"{len:0.##} {sizes[order]}";
        }

        /// <summary>
        /// 使用系统默认程序打开文件
        /// </summary>
        public static void OpenInDefaultApp(string filePath)
        {
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = filePath,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"无法打开文件: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 打开文件夹（优先在新标签页打开，否则使用资源管理器）
        /// </summary>
        public static void OpenFolderInExplorer(string folderPath)
        {
            try
            {
                // 使用回调在本程序的新标签页中打开文件夹
                var messageBus = YiboFile.App.ServiceProvider?.GetService(typeof(YiboFile.ViewModels.Messaging.IMessageBus)) as YiboFile.ViewModels.Messaging.IMessageBus;
                if (messageBus != null)
                {
                    messageBus.Publish(new YiboFile.ViewModels.Messaging.Messages.CreateTabMessage(Path: folderPath, Activate: true));
                }
                else
                {
                    // 如果系统消息总线未就绪，回退到使用系统默认文件管理器
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = folderPath,
                        UseShellExecute = true
                    });
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"无法打开文件夹: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
