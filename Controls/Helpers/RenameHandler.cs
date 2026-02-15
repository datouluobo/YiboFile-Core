using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using YiboFile.Models;
using YiboFile.ViewModels;
using YiboFile.ViewModels.Messaging;
using Microsoft.Extensions.DependencyInjection;

namespace YiboFile.Controls.Helpers
{
    /// <summary>
    /// 重命名逻辑处理器
    /// 处理文件列表项的内联重命名（TextBox 事件、提交/取消逻辑）
    /// </summary>
    public static class RenameHandler
    {
        /// <summary>
        /// 处理重命名文本框的键盘按下事件
        /// </summary>
        public static void HandleKeyDown(object sender, KeyEventArgs e, object controlDataContext)
        {
            if (sender is TextBox textBox && textBox.DataContext is FileSystemItem item)
            {
                if (e.Key == Key.Enter)
                {
                    // Force sync the TextBox text to RenameText in case binding hasn't updated
                    item.RenameText = textBox.Text;
                    CommitRename(item, controlDataContext);
                    e.Handled = true;
                }
                else if (e.Key == Key.Escape)
                {
                    CancelRename(item);
                    e.Handled = true;
                }
            }
        }

        /// <summary>
        /// 处理重命名文本框失去焦点事件
        /// </summary>
        public static void HandleLostFocus(object sender, RoutedEventArgs e, object controlDataContext)
        {
            if (sender is TextBox textBox && textBox.DataContext is FileSystemItem item)
            {
                // Commit on lost focus
                if (item.IsRenaming)
                {
                    // Delay slightly to allow Cancel to process if Esc was pressed
                    // But actually, KeyDown happens before LostFocus.
                    CommitRename(item, controlDataContext);
                }
            }
        }

        /// <summary>
        /// 处理重命名文本框可见性变更事件（初始化选中状态等）
        /// </summary>
        public static void HandleIsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e, System.Windows.Threading.Dispatcher dispatcher)
        {
            if (sender is TextBox textBox && textBox.DataContext is FileSystemItem item)
            {
                // 只在变为可见时处理
                bool isVisible = (bool)e.NewValue;
                if (!isVisible || !item.IsRenaming)
                    return;

                // 使用 Render 优先级以确保在布局完成后执行
                dispatcher.BeginInvoke(new Action(() =>
                {
                    // 再次检查 - 可能在调度期间状态已改变
                    if (!item.IsRenaming || !textBox.IsVisible)
                        return;

                    // 使用 Keyboard.Focus() 确保键盘焦点设置正确
                    Keyboard.Focus(textBox);
                    textBox.Focus();

                    // 从 Path 获取完整文件名（因为 RenameText 可能还没同步）
                    string name = !string.IsNullOrEmpty(item.RenameText)
                        ? item.RenameText
                        : System.IO.Path.GetFileName(item.Path);

                    if (!string.IsNullOrEmpty(name))
                    {
                        // 确保 TextBox 有正确的文本
                        if (string.IsNullOrEmpty(textBox.Text))
                        {
                            textBox.Text = name;
                        }

                        int lastDotIndex = name.LastIndexOf('.');
                        if (lastDotIndex > 0 && !item.IsDirectory)
                        {
                            // 选中文件名部分（不包含扩展名）
                            textBox.Select(0, lastDotIndex);
                        }
                        else
                        {
                            textBox.SelectAll();
                        }
                    }
                }), System.Windows.Threading.DispatcherPriority.Render);
            }
        }

        /// <summary>
        /// 提交重命名
        /// </summary>
        public static void CommitRename(FileSystemItem item, object controlDataContext)
        {
            if (!item.IsRenaming) return;

            // Check if name actually changed
            if (string.IsNullOrWhiteSpace(item.RenameText) || item.RenameText == item.Name)
            {
                item.IsRenaming = false;
                return;
            }

            // 触发重命名提交请求 (MVVM)
            // 优先尝试从 DataContext 获取 MessageBus (PaneViewModel)
            if (controlDataContext is PaneViewModel paneVm)
            {
                paneVm.MessageBus.Publish(new YiboFile.ViewModels.Messaging.Messages.RenameItemRequestMessage(item, item.RenameText));
            }
            else
            {
                // Fallback: 如果 DataContext 设置不正确，使用 ServiceProvider (不推荐，但在重构过渡期保留作为最后手段)
                var messageBus = App.ServiceProvider.GetService<IMessageBus>();
                messageBus?.Publish(new YiboFile.ViewModels.Messaging.Messages.RenameItemRequestMessage(item, item.RenameText));
            }

            // 重置状态
            item.IsRenaming = false;
        }

        /// <summary>
        /// 取消重命名
        /// </summary>
        public static void CancelRename(FileSystemItem item)
        {
            if (item == null) return;
            item.IsRenaming = false;
            // Revert text
            item.RenameText = item.Name;
        }
    }
}
