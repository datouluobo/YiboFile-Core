using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace YiboFile.Services.UIHelper
{
    /// <summary>
    /// UI 辅助服务实现
    /// 提供通用的 UI 操作辅助方法
    /// </summary>
    public class UIHelperService : IUIHelperService
    {
        private readonly Controls.FileBrowserControl _fileBrowser;
        private readonly Dispatcher _dispatcher;

        /// <summary>
        /// 初始化 UIHelperService
        /// </summary>
        /// <param name="fileBrowser">文件浏览器控件</param>
        /// <param name="dispatcher">UI 线程调度器</param>
        public UIHelperService(Controls.FileBrowserControl fileBrowser, Dispatcher dispatcher)
        {
            _fileBrowser = fileBrowser ?? throw new ArgumentNullException(nameof(fileBrowser));
            _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        }

        /// <summary>
        /// 确保 ListBox 的选中项可见并正确显示（强制刷新视觉状态）
        /// </summary>
        public void EnsureSelectedItemVisible(ListBox listBox, object selectedItem)
        {
            if (listBox == null || selectedItem == null) return;

            try
            {
                // 先更新布局，确保容器已生成
                listBox.UpdateLayout();

                // 设置选中项
                listBox.SelectedItem = selectedItem;

                // 等待容器生成
                _dispatcher.BeginInvoke(new Action(() =>
                {
                    try
                    {
                        // 获取容器并强制刷新视觉状态
                        var container = listBox.ItemContainerGenerator.ContainerFromItem(selectedItem) as ListBoxItem;
                        if (container != null)
                        {
                            // 检查是否是匹配高亮（优先级最高，不应清除）
                            var tag = container.Tag as string;
                            bool isMatch = (tag == "Match");

                            // 只清除拖拽高亮（半透明），保留匹配高亮（黄色）
                            if (!isMatch)
                            {
                                var localBg = container.Background as SolidColorBrush;
                                // 只清除拖拽高亮（半透明背景）
                                if (localBg != null && localBg.Color.A < 255)
                                {
                                    container.ClearValue(ListBoxItem.BackgroundProperty);
                                    container.ClearValue(ListBoxItem.ForegroundProperty);
                                    container.ClearValue(ListBoxItem.BorderBrushProperty);
                                }
                            }

                            // 强制刷新视觉状态
                            container.InvalidateVisual();
                            container.UpdateLayout();

                            // 滚动到选中项
                            container.BringIntoView();
                        }
                        else
                        {
                            // 如果容器还未生成，稍后重试
                            _dispatcher.BeginInvoke(new Action(() =>
                            {
                                var retryContainer = listBox.ItemContainerGenerator.ContainerFromItem(selectedItem) as ListBoxItem;
                                if (retryContainer != null)
                                {
                                    // 检查是否是匹配高亮
                                    var tag = retryContainer.Tag as string;
                                    bool isMatch = (tag == "Match");

                                    // 如果不是匹配，清除拖拽高亮
                                    if (!isMatch)
                                    {
                                        var localBg = retryContainer.Background as SolidColorBrush;
                                        if (localBg != null && localBg.Color.A < 255)
                                        {
                                            retryContainer.ClearValue(ListBoxItem.BackgroundProperty);
                                            retryContainer.ClearValue(ListBoxItem.ForegroundProperty);
                                            retryContainer.ClearValue(ListBoxItem.BorderBrushProperty);
                                        }
                                    }
                                    retryContainer.InvalidateVisual();
                                    retryContainer.UpdateLayout();
                                    retryContainer.BringIntoView();
                                }
                            }), DispatcherPriority.Loaded);
                        }
                    }
                    catch (Exception)
                    {
                    }
                }), DispatcherPriority.Loaded);
            }
            catch (Exception)
            {
            }
        }

        /// <summary>
        /// 确保 ListView 的选中项可见并正确显示（强制刷新视觉状态）
        /// </summary>
        public void EnsureSelectedItemVisible(ListView listView, object selectedItem)
        {
            if (listView == null || selectedItem == null) return;

            try
            {
                // 先更新布局，确保容器已生成
                listView.UpdateLayout();

                // 设置选中项
                listView.SelectedItem = selectedItem;

                // 等待容器生成
                _dispatcher.BeginInvoke(new Action(() =>
                {
                    try
                    {
                        // 获取容器并强制刷新视觉状态
                        var container = listView.ItemContainerGenerator.ContainerFromItem(selectedItem) as ListViewItem;
                        if (container != null)
                        {
                            // 强制刷新视觉状态
                            container.InvalidateVisual();
                            container.UpdateLayout();

                            // 滚动到选中项
                            container.BringIntoView();
                        }
                        else
                        {
                            // 如果容器还未生成，稍后重试
                            _dispatcher.BeginInvoke(new Action(() =>
                            {
                                var retryContainer = listView.ItemContainerGenerator.ContainerFromItem(selectedItem) as ListViewItem;
                                if (retryContainer != null)
                                {
                                    retryContainer.InvalidateVisual();
                                    retryContainer.UpdateLayout();
                                    retryContainer.BringIntoView();
                                }
                            }), DispatcherPriority.Loaded);
                        }
                    }
                    catch (Exception)
                    {
                    }
                }), DispatcherPriority.Loaded);
            }
            catch (Exception)
            {
            }
        }

        /// <summary>
        /// 更新地址栏文本
        /// </summary>
        public void UpdateAddressBar(string text)
        {
            if (_fileBrowser != null)
            {
                _fileBrowser.AddressText = text;
            }
        }

        /// <summary>
        /// 更新面包屑导航文本
        /// </summary>
        public void UpdateBreadcrumb(string text)
        {
            if (_fileBrowser != null)
            {
                _fileBrowser.UpdateBreadcrumbText(text);
            }
        }
    }
}





