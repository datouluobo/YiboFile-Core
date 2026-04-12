using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using YiboFile.Interfaces.Plugins;
using YiboFile.Services.Core;
using YiboFile.Services.Tabs;
using YiboFile.ViewModels;
using YiboFile.ViewModels.Messaging;
using YiboFile.ViewModels.Messaging.Messages;
using YiboFile.Models.Navigation;
using YiboFile.Services.Navigation;
using System.Windows.Media.Animation;
using System.Windows.Media;

namespace YiboFile.Controls
{
    /// <summary>
    /// 面板内容宿主控件。
    /// 替代 MainWindow 中直接放置 FileBrowserControl 的方式。
    /// 
    /// 核心职责：
    /// 1. 根据当前活跃标签页的 ContentTypeId 在 FileBrowserControl 和自定义面板之间切换
    /// 2. 管理 ITabContent 的生命周期回调（OnActivated / OnDeactivated）
    /// 3. 维护自定义面板的 View 缓存
    /// 4. 为自定义面板提供地址栏（复用 AddressBarControl）
    /// 
    /// 设计说明：
    /// - FileBrowserControl 始终保持实例化（不销毁不重建），只通过 Visibility 切换
    /// - 自定义面板通过 ContentPresenter 显示，View 实例由 ITabContent 缓存
    /// - DataContext 传递：此控件的 DataContext 会直接传递给内部的 FileBrowserControl
    /// - 地址栏：自定义面板激活时显示独立的 AddressBarControl，支持编辑和导航
    /// </summary>
    public partial class PaneContentHost : UserControl
    {
        private ITabContent _activeCustomContent;
        private TabContentRegistry _registry;
        private IMessageBus _messageBus;

        // ═══ 跨面板预览 ═══
        private bool _isCrossPreviewActive;
        private object _savedPreviewDataContext;

        /// <summary>
        /// 内容类型到显示名称的映射（用于面包屑文本）。
        /// </summary>
        private static readonly Dictionary<string, string> ContentTypeDisplayNames = new()
        {
            { TabContentTypes.Settings, "⚙ 设置" },
            { TabContentTypes.About, "ℹ 关于" },

            { TabContentTypes.Backup, "💾 备份管理" },
            { TabContentTypes.Clipboard, "📋 剪切板历史" },
            { TabContentTypes.Management, "🔧 管理" },
        };

        /// <summary>
        /// content:// 协议前缀
        /// </summary>
        private const string ContentProtocol = "yibofile://";

        public PaneContentHost()
        {
            InitializeComponent();

            // 尝试从 App.ServiceProvider 获取 Registry 和 MessageBus
            try
            {
                _registry = App.ServiceProvider?.GetService(typeof(TabContentRegistry)) as TabContentRegistry;
                _messageBus = App.ServiceProvider?.GetService(typeof(IMessageBus)) as IMessageBus;
            }
            catch
            {
                // 设计时可能失败，忽略
            }

            // 挂载地址栏事件
            ContentAddressBar.PathChanged += ContentAddressBar_PathChanged;

            DataContextChanged += PaneContentHost_DataContextChanged;
        }

        /// <summary>
        /// 设置跨面板预览：另一个面板的预览内容在本面板显示。
        /// </summary>
        /// <param name="previewVM">对方面板的 PanePreviewViewModel，null 表示清除。</param>
        public void SetCrossPreview(object previewVM)
        {
            if (previewVM != null)
            {
                _isCrossPreviewActive = true;

                // 保存原始预览 DataContext
                _savedPreviewDataContext = PreviewAttachment.DataContext;

                // 隐藏文件浏览器
                FileBrowserContainer.Visibility = Visibility.Collapsed;

                // 预览面板占满整个区域
                PreviewAttachment.DataContext = previewVM;
                PreviewAttachment.Visibility = Visibility.Visible;

                FileLogger.Log($"PaneContentHost: 跨面板预览已启用");
            }
            else
            {
                _isCrossPreviewActive = false;

                // 恢复文件浏览器
                FileBrowserContainer.Visibility = Visibility.Visible;
                PreviewAttachment.Visibility = Visibility.Collapsed;

                // 恢复原始 DataContext
                if (_savedPreviewDataContext != null)
                {
                    PreviewAttachment.DataContext = _savedPreviewDataContext;
                    _savedPreviewDataContext = null;
                }

                FileLogger.Log($"PaneContentHost: 跨面板预览已清除");
            }
        }

        private void PaneContentHost_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            // 不再需要调整内部网格，因为内部根本没有多列结构
        }

        #region ActiveContentTypeId DependencyProperty

        /// <summary>
        /// 当前活跃标签页的内容类型 ID。
        /// 由外部绑定驱动（绑定到当前活跃 PathTab 的 ContentTypeId）。
        /// 变化时自动触发内容切换。
        /// </summary>
        public static readonly DependencyProperty ActiveContentTypeIdProperty =
            DependencyProperty.Register(
                nameof(ActiveContentTypeId),
                typeof(string),
                typeof(PaneContentHost),
                new PropertyMetadata(TabContentTypes.Path, OnActiveContentTypeIdChanged));

        public string ActiveContentTypeId
        {
            get => (string)GetValue(ActiveContentTypeIdProperty);
            set => SetValue(ActiveContentTypeIdProperty, value);
        }

        #endregion

        #region ActiveTab DependencyProperty

        /// <summary>
        /// 当前活跃的标签页对象。
        /// 用于获取 CustomContent 缓存和 ContentTypeId。
        /// </summary>
        public static readonly DependencyProperty ActiveTabProperty =
            DependencyProperty.Register(
                nameof(ActiveTab),
                typeof(Services.Tabs.PathTab),
                typeof(PaneContentHost),
                new PropertyMetadata(null, OnActiveTabChanged));

        public Services.Tabs.PathTab ActiveTab
        {
            get => (Services.Tabs.PathTab)GetValue(ActiveTabProperty);
            set => SetValue(ActiveTabProperty, value);
        }

        #endregion

        /// <summary>
        /// 获取内部的 FileBrowserControl 实例（供外部事件挂载等使用）。
        /// </summary>
        public FileBrowserControl InternalFileBrowser => FileBrowserView;

        /// <summary>
        /// 获取自定义面板的地址栏（供外部访问）。
        /// </summary>
        public AddressBarControl InternalContentAddressBar => ContentAddressBar;

        private static void OnActiveContentTypeIdChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is PaneContentHost host)
            {
                host.SwitchContent((string)e.OldValue, (string)e.NewValue);
            }
        }

        private static void OnActiveTabChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is PaneContentHost host && e.NewValue is Services.Tabs.PathTab newTab)
            {
                // 当 ActiveTab 变化时，同步更新 ContentTypeId
                host.ActiveContentTypeId = newTab.ContentTypeId ?? TabContentTypes.Path;
            }
        }

        /// <summary>
        /// 根据新的 ContentTypeId 执行内容切换。
        /// </summary>
        private void SwitchContent(string oldTypeId, string newTypeId)
        {
            // 1. 通知旧的自定义面板停用
            if (_activeCustomContent != null)
            {
                try { _activeCustomContent.OnDeactivated(); }
                catch (Exception ex) { FileLogger.LogException("PaneContentHost: OnDeactivated failed", ex); }
            }

            // 2. 判断新类型
            if (string.IsNullOrEmpty(newTypeId) || TabContentTypes.IsFileBrowserType(newTypeId))
            {
                // 文件浏览类：显示 FileBrowserControl，隐藏自定义面板容器
                FileBrowserView.Visibility = Visibility.Visible;
                CustomContentContainer.Visibility = Visibility.Collapsed;
                CustomContentPresenter.Content = null;
                _activeCustomContent = null;

                // 执行入场动画
                PlayEnterAnimation(FileBrowserView);
            }
            else
            {
                // 自定义面板类：获取或创建 ITabContent
                var content = ResolveTabContent(newTypeId);
                if (content != null)
                {
                    var view = content.CreateView();
                    if (view != null)
                    {
                        // 隐藏 FileBrowserControl，显示自定义面板容器
                        FileBrowserView.Visibility = Visibility.Collapsed;
                        CustomContentPresenter.Content = view;
                        CustomContentContainer.Visibility = Visibility.Visible;
                        _activeCustomContent = content;

                        // 执行入场动画
                        if (view is UIElement uiView)
                        {
                            PlayEnterAnimation(uiView);
                        }

                        // 更新地址栏
                        UpdateContentAddressBar(newTypeId);

                        // 通知新面板激活
                        try { content.OnActivated(); }
                        catch (Exception ex) { FileLogger.LogException("PaneContentHost: OnActivated failed", ex); }
                    }
                    else
                    {
                        // CreateView 返回 null（不应发生），回退到文件浏览
                        FileLogger.Log($"PaneContentHost: CreateView returned null for '{newTypeId}', falling back to FileBrowser");
                        FallbackToFileBrowser();
                    }
                }
                else
                {
                    // 未注册的类型，回退到文件浏览
                    FileLogger.Log($"PaneContentHost: Unknown content type '{newTypeId}', falling back to FileBrowser");
                    FallbackToFileBrowser();
                }
            }
        }

        /// <summary>
        /// 回退到文件浏览器视图。
        /// </summary>
        private void FallbackToFileBrowser()
        {
            FileBrowserView.Visibility = Visibility.Visible;
            CustomContentContainer.Visibility = Visibility.Collapsed;
            CustomContentPresenter.Content = null;
            _activeCustomContent = null;
            PlayEnterAnimation(FileBrowserView);
        }

        /// <summary>
        /// 播放入场动画
        /// </summary>
        private void PlayEnterAnimation(UIElement view)
        {
            var da = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(200))
            {
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            };
            view.BeginAnimation(OpacityProperty, da);

            if (!(view.RenderTransform is TranslateTransform))
            {
                view.RenderTransform = new TranslateTransform();
            }
            
            var ta = new DoubleAnimation(15, 0, TimeSpan.FromMilliseconds(250))
            {
                EasingFunction = new QuarticEase { EasingMode = EasingMode.EaseOut }
            };
            view.RenderTransform.BeginAnimation(TranslateTransform.YProperty, ta);
        }

        /// <summary>
        /// 更新自定义面板的地址栏。
        /// </summary>
        private void UpdateContentAddressBar(string contentTypeId)
        {
            // 先设置面包屑显示文本（必须在 AddressText 之前设置，
            // 因为 AddressText 变化会触发 UpdateBreadcrumb，
            // 需要提前有 CustomText 才能正确显示友好名称）
            if (ContentTypeDisplayNames.TryGetValue(contentTypeId, out var displayName))
            {
                ContentAddressBar.SetBreadcrumbCustomText(displayName);
            }
            else
            {
                ContentAddressBar.SetBreadcrumbCustomText(contentTypeId);
            }

            // 设置地址文本为 yibofile://contentTypeId
            string address = ContentProtocol + contentTypeId;
            ContentAddressBar.AddressText = address;
        }

        /// <summary>
        /// 处理自定义面板地址栏的路径变更（用户编辑后按 Enter）。
        /// </summary>
        private void ContentAddressBar_PathChanged(object sender, string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return;

            // 获取当前 PaneId
            PaneId? targetPane = (DataContext as PaneViewModel)?.MyPaneId;

            // 检查是否为 yibofile:// 协议
            if (path.StartsWith(ContentProtocol, StringComparison.OrdinalIgnoreCase))
            {
                var contentTypeId = path.Substring(ContentProtocol.Length).Trim();
                if (!string.IsNullOrEmpty(contentTypeId))
                {
                    // 打开对应的内容标签页
                    _messageBus?.Publish(new OpenContentTabMessage(contentTypeId, targetPane));
                    return;
                }
            }

            // 检查是否为搜索协议 (content:// 或 search://)
            if (path.StartsWith("content://", StringComparison.OrdinalIgnoreCase) ||
                path.StartsWith("search://", StringComparison.OrdinalIgnoreCase))
            {
                // 搜索请求：交给导航系统处理
                _messageBus?.Publish(new RequestNavigationModeMessage("Path"));
                // 延迟一帧发送导航消息，确保模式已切换
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    var fileBrowser = InternalFileBrowser;
                    fileBrowser?.AddressBarControl?.SwitchToEditMode();
                    if (fileBrowser?.AddressBarControl != null)
                    {
                        var addressTextBox = fileBrowser.AddressBarControl.FindName("AddressTextBox") as System.Windows.Controls.TextBox;
                        if (addressTextBox != null)
                        {
                            addressTextBox.Text = path;
                        }
                        // 触发 PathChanged
                        fileBrowser.AddressBarControl.AddressText = path;
                    }
                }), System.Windows.Threading.DispatcherPriority.Background);
                return;
            }

            // 普通文件路径：导航到该路径
            // 这将创建/切换到一个 Path 标签页并导航
            if (System.IO.Directory.Exists(path) || System.IO.File.Exists(path))
            {
                // 如果是文件，取其目录
                if (System.IO.File.Exists(path))
                {
                    path = System.IO.Path.GetDirectoryName(path);
                }

                // 发送导航请求（PathChanged 会触发 NavigationCoordinator 处理）
                _messageBus?.Publish(new NavigateToPathMessage(path, true, targetPane));
            }
        }

        /// <summary>
        /// 从 ActiveTab.CustomContent 或 Registry 获取 ITabContent。
        /// </summary>
        private ITabContent ResolveTabContent(string contentTypeId)
        {
            // 优先使用标签页上缓存的 ITabContent 实例
            var tab = ActiveTab;
            if (tab?.CustomContent != null && tab.CustomContent.Id == contentTypeId)
            {
                return tab.CustomContent;
            }

            // 从 Registry 解析
            if (_registry != null)
            {
                var content = _registry.Resolve(contentTypeId);
                if (content != null && tab != null)
                {
                    // 缓存到标签页上
                    tab.CustomContent = content;
                }
                return content;
            }

            return null;
        }

        /// <summary>
        /// 点击面板内任何区域（即使是自定义面板内部）时，向总线申请焦点切换。
        /// 这解决了特殊页面在三态预览模式下点击时蓝框焦点没动的问题。
        /// </summary>
        protected override void OnPreviewMouseDown(System.Windows.Input.MouseButtonEventArgs e)
        {
            base.OnPreviewMouseDown(e);
            
            // ═══ 关键修复 ═══
            // 如果当前处于“跨面板预览”激活状态（即本区域正在替另一个面板显示预览），
            // 则点击此区域不应触发面板焦点切换。
            // 否则在“预览”布局模式下，点击右侧预览区会导致焦点移到右侧，从而触发布局翻转（右侧变列表，左侧变预览）。
            if (_isCrossPreviewActive) return;

            if (DataContext is PaneViewModel vm && !vm.IsActive)
            {
                // 仅当 DataContext 可用且当前为非活跃时请求切换
                _messageBus?.Publish(new SetFocusedPaneMessage(vm.IsSecondary));
            }
        }
    }
}
