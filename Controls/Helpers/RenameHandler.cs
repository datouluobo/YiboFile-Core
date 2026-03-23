using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
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
                    RestoreFocusToList(textBox);
                    e.Handled = true;
                }
                else if (e.Key == Key.Escape)
                {
                    CancelRename(item);
                    RestoreFocusToList(textBox);
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

                // 使用较高级别的优先级确保在布局就绪后尽快捕获焦点
                dispatcher.BeginInvoke(new Action(() =>
                {
                    // 再次检查状态
                    if (!item.IsRenaming || !textBox.IsVisible)
                        return;

                    // 1. 强制设置键盘焦点
                    textBox.Focus();
                    Keyboard.Focus(textBox);

                    // 2. 确保它在焦点范围中是选中的
                    FocusManager.SetFocusedElement(FocusManager.GetFocusScope(textBox), textBox);

                    // 确保文本已正确同步（处理绑定延迟）
                    string name = !string.IsNullOrEmpty(item.RenameText)
                        ? item.RenameText
                        : (string.IsNullOrEmpty(textBox.Text) ? System.IO.Path.GetFileName(item.Path) : textBox.Text);

                    if (string.IsNullOrEmpty(textBox.Text) && !string.IsNullOrEmpty(name))
                    {
                        textBox.Text = name;
                    }

                    // 选中逻辑
                    if (!string.IsNullOrEmpty(textBox.Text))
                    {
                        var text = textBox.Text;
                        int lastDotIndex = text.LastIndexOf('.');
                        if (lastDotIndex > 0 && !item.IsDirectory)
                        {
                            textBox.Select(0, lastDotIndex);
                        }
                        else
                        {
                            textBox.SelectAll();
                        }
                    }
                }), System.Windows.Threading.DispatcherPriority.Input);
            }
        }

        /// <summary>
        /// 提交重命名
        /// </summary>
        public static void CommitRename(FileSystemItem item, object controlDataContext)
        {
            if (item == null || !item.IsRenaming) return;

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
                // Fallback: 如果 DataContext 设置不正确，使用 ServiceProvider
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

        /// <summary>
        /// 恢复列表视图的焦点
        /// </summary>
        private static void RestoreFocusToList(TextBox textBox)
        {
            if (textBox == null) return;

            // 寻找父级 ListView
            DependencyObject current = textBox;
            
            // 如果在 Popup 中，先寻找 Popup 及其 PlacementTarget
            while (current != null && !(current is ListView))
            {
                var parent = VisualTreeHelper.GetParent(current);
                if (parent == null)
                {
                    // 尝试寻找 Popup 的逻辑父级（跳出独立视觉树）
                    if (current is FrameworkElement fe && fe.Parent is Popup popup)
                    {
                        current = popup.PlacementTarget;
                        continue;
                    }
                    else if (current is Popup p)
                    {
                        current = p.PlacementTarget;
                        continue;
                    }
                }
                current = parent;
            }

            if (current is ListView listView)
            {
                listView.Focus();
                // 确保容器也有焦点，这样键盘上下键才能工作
                if (listView.SelectedItem != null)
                {
                    var container = listView.ItemContainerGenerator.ContainerFromItem(listView.SelectedItem) as FrameworkElement;
                    container?.Focus();
                }
            }
        }
    }
}
