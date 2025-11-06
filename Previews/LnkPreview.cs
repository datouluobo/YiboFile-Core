using System;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Runtime.InteropServices;

namespace OoiMRR.Previews
{
    /// <summary>
    /// 快捷方式文件预览 - 显示链接指向的文件夹和文件
    /// </summary>
    public class LnkPreview : IPreviewProvider
    {
        public UIElement CreatePreview(string filePath)
        {
            try
            {
                string targetPath = GetShortcutTarget(filePath);
                
                if (string.IsNullOrEmpty(targetPath))
                {
                    return PreviewHelper.CreateErrorPreview("无法读取快捷方式目标");
                }

                var mainPanel = new StackPanel
                {
                    Orientation = Orientation.Vertical,
                    Background = Brushes.White
                };

                // 标题区域
                var titlePanel = PreviewHelper.CreateTitlePanel("🔗", $"快捷方式: {Path.GetFileName(filePath)}");
                mainPanel.Children.Add(titlePanel);

                // 目标路径信息
                var infoPanel = new StackPanel
                {
                    Orientation = Orientation.Vertical,
                    Margin = new Thickness(15, 15, 15, 15)
                };

                var targetLabel = new TextBlock
                {
                    Text = "目标路径:",
                    FontSize = 12,
                    Foreground = Brushes.Gray,
                    Margin = new Thickness(0, 0, 0, 5)
                };
                infoPanel.Children.Add(targetLabel);

                var targetPathText = new TextBlock
                {
                    Text = targetPath,
                    FontSize = 13,
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(0, 0, 0, 15)
                };
                infoPanel.Children.Add(targetPathText);

                // 检查目标是否存在
                bool targetExists = Directory.Exists(targetPath) || File.Exists(targetPath);
                bool isDirectory = Directory.Exists(targetPath);

                if (!targetExists)
                {
                    var errorText = new TextBlock
                    {
                        Text = "⚠️ 目标路径不存在",
                        FontSize = 12,
                        Foreground = Brushes.OrangeRed,
                        Margin = new Thickness(0, 0, 0, 15)
                    };
                    infoPanel.Children.Add(errorText);
                }
                else
                {
                    // 如果目标是文件夹，显示文件夹内容
                    if (isDirectory)
                    {
                        var folderPreview = new FolderPreview().CreatePreview(targetPath);
                        infoPanel.Children.Add(folderPreview);
                    }
                    else
                    {
                        // 如果是文件，显示文件信息
                        var fileInfo = new FileInfo(targetPath);
                        var fileInfoText = new TextBlock
                        {
                            Text = $"文件大小: {PreviewHelper.FormatFileSize(fileInfo.Length)}\n" +
                                   $"修改日期: {fileInfo.LastWriteTime:yyyy-MM-dd HH:mm:ss}",
                            FontSize = 12,
                            Foreground = Brushes.Gray,
                            Margin = new Thickness(0, 0, 0, 15)
                        };
                        infoPanel.Children.Add(fileInfoText);
                    }
                }

                mainPanel.Children.Add(infoPanel);

                // 按钮区域
                var buttonPanel = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(15, 0, 15, 15)
                };

                if (targetExists)
                {
                    // 无论目标是文件夹还是文件，都显示"打开文件夹"按钮
                    // 如果目标是文件，获取文件所在的文件夹路径
                    string folderPath = isDirectory ? targetPath : Path.GetDirectoryName(targetPath);
                    
                    if (!string.IsNullOrEmpty(folderPath) && Directory.Exists(folderPath))
                    {
                        // 创建打开文件夹按钮（不使用PreviewHelper.CreateOpenButton，因为我们要自定义行为）
                        var openButton = new Button
                        {
                            Content = "📂 打开文件夹",
                            Padding = new Thickness(16, 8, 16, 8),
                            FontSize = 14,
                            MinWidth = 140,
                            MinHeight = 36,
                            Background = new SolidColorBrush(Color.FromRgb(33, 150, 243)),
                            Foreground = Brushes.White,
                            BorderThickness = new Thickness(0),
                            Cursor = System.Windows.Input.Cursors.Hand
                        };
                        
                        // 使用特殊标记来标识这是"打开文件夹"按钮，并在Tag中存储文件夹路径
                        openButton.Tag = $"OpenFolder:{folderPath}";
                        buttonPanel.Children.Add(openButton);
                    }
                }

                mainPanel.Children.Add(buttonPanel);

                return new ScrollViewer
                {
                    Content = mainPanel,
                    VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                    HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
                };
            }
            catch (Exception ex)
            {
                return PreviewHelper.CreateErrorPreview($"无法读取快捷方式: {ex.Message}");
            }
        }

        private string GetShortcutTarget(string lnkPath)
        {
            try
            {
                // 使用WScript.Shell COM对象读取快捷方式目标
                Type shellType = Type.GetTypeFromProgID("WScript.Shell");
                object shell = Activator.CreateInstance(shellType);
                object shortcut = shellType.InvokeMember("CreateShortcut", 
                    System.Reflection.BindingFlags.InvokeMethod, null, shell, new object[] { lnkPath });
                string target = (string)shortcut.GetType().InvokeMember("TargetPath",
                    System.Reflection.BindingFlags.GetProperty, null, shortcut, null);
                return target;
            }
            catch
            {
                // 如果COM对象失败，尝试使用Shell API
                try
                {
                    IShellLink link = (IShellLink)new ShellLink();
                    IPersistFile file = (IPersistFile)link;
                    file.Load(lnkPath, 0);
                    StringBuilder sb = new StringBuilder(260);
                    link.GetPath(sb, sb.Capacity, IntPtr.Zero, 0);
                    return sb.ToString();
                }
                catch
                {
                    return null;
                }
            }
        }

        [ComImport]
        [Guid("00021401-0000-0000-C000-000000000046")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IShellLink
        {
            void GetPath([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszFile, int cchMaxPath, IntPtr pfd, int fFlags);
            void GetIDList(out IntPtr ppidl);
            void SetIDList(IntPtr pidl);
            void GetDescription([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszName, int cchMaxName);
            void SetDescription([MarshalAs(UnmanagedType.LPWStr)] string pszName);
            void GetWorkingDirectory([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszDir, int cchMaxPath);
            void SetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)] string pszDir);
            void GetArguments([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszArgs, int cchMaxArgs);
            void SetArguments([MarshalAs(UnmanagedType.LPWStr)] string pszArgs);
            void GetHotkey(out short pwHotkey);
            void SetHotkey(short wHotkey);
            void GetShowCmd(out int piShowCmd);
            void SetShowCmd(int iShowCmd);
            void GetIconLocation([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszIconPath, int cchIconPath, out int piIcon);
            void SetIconLocation([MarshalAs(UnmanagedType.LPWStr)] string pszIconPath, int iIcon);
            void SetRelativePath([MarshalAs(UnmanagedType.LPWStr)] string pszPathRel, int dwReserved);
            void Resolve(IntPtr hwnd, int fFlags);
            void SetPath([MarshalAs(UnmanagedType.LPWStr)] string pszFile);
        }

        [ComImport]
        [ClassInterface(ClassInterfaceType.None)]
        [Guid("00021401-0000-0000-C000-000000000046")]
        private class ShellLink
        {
        }

        [ComImport]
        [Guid("0000010c-0000-0000-c000-000000000046")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IPersistFile
        {
            void GetClassID(out Guid pClassID);
            [PreserveSig]
            int IsDirty();
            [PreserveSig]
            int Load([In, MarshalAs(UnmanagedType.LPWStr)] string pszFileName, uint dwMode);
            [PreserveSig]
            int Save([In, MarshalAs(UnmanagedType.LPWStr)] string pszFileName, [In, MarshalAs(UnmanagedType.Bool)] bool fRemember);
            [PreserveSig]
            int SaveCompleted([In, MarshalAs(UnmanagedType.LPWStr)] string pszFileName);
            [PreserveSig]
            int GetCurFile([In, MarshalAs(UnmanagedType.LPWStr)] string ppszFileName);
        }
    }
}

