using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Runtime.InteropServices;
using YiboFile.Controls;

namespace YiboFile.Previews
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

                // 使用可编辑的 TextBox
                var targetPathText = new TextBox
                {
                    Text = targetPath,
                    FontSize = 13,
                    TextWrapping = TextWrapping.Wrap,
                    IsReadOnly = true,
                    BorderThickness = new Thickness(0),
                    Background = Brushes.Transparent,
                    Margin = new Thickness(0, 0, 0, 15),
                    Padding = new Thickness(0)
                };

                bool isEditMode = false;
                string originalTargetPath = targetPath;
                Button editButton = null;

                // 编辑/保存按钮
                editButton = PreviewHelper.CreateEditButton(
                    () =>
                    {
                        if (isEditMode)
                        {
                            // 保存模式
                            try
                            {
                                string newTargetPath = targetPathText.Text.Trim();

                                if (string.IsNullOrEmpty(newTargetPath))
                                {
                                    MessageBox.Show("目标路径不能为空", "错误", MessageBoxButton.OK, MessageBoxImage.Warning);
                                    return;
                                }

                                // 保存快捷方式目标路径
                                if (SetShortcutTarget(filePath, newTargetPath))
                                {
                                    targetPath = newTargetPath;
                                    originalTargetPath = newTargetPath;

                                    // 切换为只读模式
                                    targetPathText.IsReadOnly = true;
                                    targetPathText.Background = Brushes.Transparent; // LNK预览保持透明背景
                                    isEditMode = false;

                                    // 更新按钮
                                    if (editButton != null)
                                    {
                                        editButton.Content = "✏️ 编辑";
                                        editButton.Background = new SolidColorBrush(Color.FromRgb(33, 150, 243));
                                    }

                                    // 刷新预览内容
                                    RefreshPreviewContent(infoPanel, targetPath);

                                    MessageBox.Show("快捷方式已保存", "成功", MessageBoxButton.OK, MessageBoxImage.Information);
                                }
                                else
                                {
                                    MessageBox.Show("保存快捷方式失败", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                                }
                            }
                            catch (Exception ex)
                            {
                                MessageBox.Show($"保存失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                            }
                        }
                        else
                        {
                            // 编辑模式
                            targetPathText.IsReadOnly = false;
                            targetPathText.Background = PreviewHelper.EditModeBackground; // 浅蓝色背景表示可编辑
                            isEditMode = true;

                            // 更新按钮
                            if (editButton != null)
                            {
                                editButton.Content = "💾 保存";
                                editButton.Background = new SolidColorBrush(Color.FromRgb(76, 175, 80));
                            }
                        }
                    },
                    false
                );

                // 统一工具栏
                var toolbar = new TextPreviewToolbar
                {
                    FileName = System.IO.Path.GetFileName(filePath),
                    FileIcon = "🔗",
                    ShowSearch = false,
                    ShowWordWrap = false,
                    ShowEncoding = false,
                    ShowViewToggle = false,
                    ShowFormat = false
                };
                toolbar.OpenExternalRequested += (s, e) => PreviewHelper.OpenInDefaultApp(filePath);

                // 自定义动作按钮
                var actionData = new StackPanel { Orientation = Orientation.Horizontal };

                // 添加编辑按钮
                editButton.Margin = new Thickness(0, 0, 5, 0);
                actionData.Children.Add(editButton);

                // 检查目标是否存在，如果存在则添加打开文件夹按钮
                if (!string.IsNullOrEmpty(targetPath))
                {
                    bool targetExistsCheck = Directory.Exists(targetPath) || File.Exists(targetPath);
                    bool isDirectoryCheck = Directory.Exists(targetPath);

                    // 确定文件夹路径
                    string folderPath = isDirectoryCheck ? targetPath : Path.GetDirectoryName(targetPath);

                    // 如果目标存在，或者目标所在的目录存在，都显示"打开文件夹"按钮
                    if (targetExistsCheck || (!string.IsNullOrEmpty(folderPath) && Directory.Exists(folderPath)))
                    {
                        if (!string.IsNullOrEmpty(folderPath) && Directory.Exists(folderPath))
                        {
                            var openFolderBtn = PreviewHelper.CreateOpenFolderButton(folderPath);
                            openFolderBtn.Margin = new Thickness(0, 0, 5, 0);
                            actionData.Children.Add(openFolderBtn);
                        }
                    }
                }

                toolbar.CustomActionContent = actionData;
                mainPanel.Children.Add(toolbar);

                // 设置自定义右键菜单，只包含复制（去掉剪切和粘贴）
                var contextMenu = new ContextMenu();
                var copyItem = new MenuItem
                {
                    Header = "复制",
                    InputGestureText = "Ctrl+C"
                };
                copyItem.Click += (s, e) =>
                {
                    if (!string.IsNullOrEmpty(targetPathText.SelectedText))
                    {
                        Clipboard.SetText(targetPathText.SelectedText);
                    }
                    else
                    {
                        Clipboard.SetText(targetPathText.Text);
                    }
                };
                contextMenu.Items.Add(copyItem);
                targetPathText.ContextMenu = contextMenu;

                infoPanel.Children.Add(targetPathText);

                // 添加预览内容
                RefreshPreviewContent(infoPanel, targetPath);

                mainPanel.Children.Add(infoPanel);

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
            object shell = null;
            try
            {
                // 使用WScript.Shell COM对象读取快捷方式目标
                Type shellType = Type.GetTypeFromProgID("WScript.Shell");
                shell = Activator.CreateInstance(shellType);
                object shortcut = shellType.InvokeMember("CreateShortcut",
                    System.Reflection.BindingFlags.InvokeMethod, null, shell, new object[] { lnkPath });
                string target = (string)shortcut.GetType().InvokeMember("TargetPath",
                    System.Reflection.BindingFlags.GetProperty, null, shortcut, null);
                return target;
            }
            catch (COMException)
            {
                // COM 互操作异常，尝试使用 Shell API
            }
            catch
            {
                // 其他异常，尝试使用 Shell API
            }
            finally
            {
                // 释放 COM 对象
                if (shell != null)
                {
                    try
                    {
                        Marshal.ReleaseComObject(shell);
                    }
                    catch { }
                }
            }

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
            catch (COMException)
            {
                return null;
            }
            catch
            {
                return null;
            }
        }

        private bool SetShortcutTarget(string lnkPath, string targetPath)
        {
            object shell = null;
            object shortcut = null;
            try
            {
                // 使用WScript.Shell COM对象设置快捷方式目标
                Type shellType = Type.GetTypeFromProgID("WScript.Shell");
                shell = Activator.CreateInstance(shellType);
                shortcut = shellType.InvokeMember("CreateShortcut",
                    System.Reflection.BindingFlags.InvokeMethod, null, shell, new object[] { lnkPath });

                // 设置目标路径
                shortcut.GetType().InvokeMember("TargetPath",
                    System.Reflection.BindingFlags.SetProperty, null, shortcut, new object[] { targetPath });

                // 保存快捷方式
                shortcut.GetType().InvokeMember("Save",
                    System.Reflection.BindingFlags.InvokeMethod, null, shortcut, null);

                return true;
            }
            catch (COMException)
            {
                // COM 互操作异常，尝试使用 Shell API
            }
            catch
            {
                // 其他异常，尝试使用 Shell API
            }
            finally
            {
                // 释放 COM 对象
                if (shortcut != null)
                {
                    try
                    {
                        Marshal.ReleaseComObject(shortcut);
                    }
                    catch { }
                }
                if (shell != null)
                {
                    try
                    {
                        Marshal.ReleaseComObject(shell);
                    }
                    catch { }
                }
            }

            // 如果COM对象失败，尝试使用Shell API
            try
            {
                IShellLink link = (IShellLink)new ShellLink();
                IPersistFile file = (IPersistFile)link;

                // 加载现有快捷方式
                file.Load(lnkPath, 0);

                // 设置新的目标路径
                link.SetPath(targetPath);

                // 保存快捷方式
                file.Save(lnkPath, true);

                return true;
            }
            catch (COMException)
            {
                return false;
            }
            catch
            {
                return false;
            }
        }

        private void RefreshPreviewContent(StackPanel infoPanel, string targetPath)
        {
            // 移除旧的预览内容（保留前两个元素：标签和文本框）
            while (infoPanel.Children.Count > 2)
            {
                infoPanel.Children.RemoveAt(2);
            }

            // 检查目标是否存在
            bool targetExists = Directory.Exists(targetPath) || File.Exists(targetPath);
            bool isDirectory = Directory.Exists(targetPath);

            if (!targetExists)
            {
                // 检查目标所在的目录是否存在
                string folderPath = Path.GetDirectoryName(targetPath);
                bool folderExists = !string.IsNullOrEmpty(folderPath) && Directory.Exists(folderPath);

                if (folderExists)
                {
                    // 如果目标所在的目录存在，不显示错误，只显示提示信息
                    var infoText = new TextBlock
                    {
                        Text = "⚠️ 目标文件不存在，但所在目录存在",
                        FontSize = 12,
                        Foreground = Brushes.Orange,
                        Margin = new Thickness(0, 0, 0, 15)
                    };
                    infoPanel.Children.Add(infoText);
                }
                else
                {
                    // 如果目标所在的目录也不存在，显示错误
                    var errorText = new TextBlock
                    {
                        Text = "⚠️ 目标路径不存在",
                        FontSize = 12,
                        Foreground = Brushes.OrangeRed,
                        Margin = new Thickness(0, 0, 0, 15)
                    };
                    infoPanel.Children.Add(errorText);
                }
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


