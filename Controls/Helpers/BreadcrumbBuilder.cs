using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using YiboFile.Services.Core;

namespace YiboFile.Controls.Helpers
{
    /// <summary>
    /// 面包屑构建器
    /// 负责将路径字符串解析为可交互的面包屑 UI 元素（Inline Run）
    /// 支持标准路径、压缩包路径、库/搜索/标签/全文搜索等协议路径
    /// </summary>
    public class BreadcrumbBuilder
    {
        // UI 元素引用（由 AddressBarControl 注入）
        private readonly TextBlock _breadcrumbText;
        private readonly TextBlock _breadcrumbTail;
        private readonly Border _breadcrumbContainer;

        // 回调：导航事件
        private readonly Action<string> _onBreadcrumbClicked;
        private readonly Action<string> _onBreadcrumbMiddleClicked;
        private readonly Action _onSwitchToEditMode;

        // 资源查找委托
        private readonly Func<Window> _getParentWindow;

        /// <summary>
        /// 自定义面包屑文本（覆盖路径显示）
        /// </summary>
        public string CustomText { get; private set; }

        public BreadcrumbBuilder(
            TextBlock breadcrumbText,
            TextBlock breadcrumbTail,
            Border breadcrumbContainer,
            Action<string> onBreadcrumbClicked,
            Action<string> onBreadcrumbMiddleClicked,
            Action onSwitchToEditMode,
            Func<Window> getParentWindow)
        {
            _breadcrumbText = breadcrumbText ?? throw new ArgumentNullException(nameof(breadcrumbText));
            _breadcrumbTail = breadcrumbTail;
            _breadcrumbContainer = breadcrumbContainer;
            _onBreadcrumbClicked = onBreadcrumbClicked;
            _onBreadcrumbMiddleClicked = onBreadcrumbMiddleClicked;
            _onSwitchToEditMode = onSwitchToEditMode;
            _getParentWindow = getParentWindow;
        }

        #region 主入口

        /// <summary>
        /// 根据路径更新面包屑显示
        /// </summary>
        public void UpdateBreadcrumb(string path)
        {
            if (_breadcrumbText == null)
                return;

            _breadcrumbText.Inlines.Clear();

            // 清理并隐藏右侧TextBlock（短路径时不用）
            if (_breadcrumbTail != null)
            {
                _breadcrumbTail.Inlines.Clear();
                _breadcrumbTail.Visibility = Visibility.Collapsed;
            }

            if (string.IsNullOrEmpty(path))
                return;

            // 设置完整路径为 ToolTip
            _breadcrumbText.ToolTip = path;

            // 获取前景色
            var parentWindow = _getParentWindow?.Invoke();
            var defaultBrush = parentWindow?.TryFindResource("ForegroundBrush") as SolidColorBrush
                ?? Brushes.Black;
            var hoverBrush = parentWindow?.TryFindResource("HighlightBrush") as SolidColorBrush
                ?? new SolidColorBrush(Color.FromRgb(0, 120, 215));

            // 动态识别路径类型并设置标签
            string identifier = "path ";
            string specialContent = null;
            bool isSpecial = false;

            // Reset background
            _breadcrumbContainer?.ClearValue(Border.BackgroundProperty);
            if (_breadcrumbContainer != null &&
                (_breadcrumbContainer.Background == null || _breadcrumbContainer.Background == Brushes.Transparent))
            {
                _breadcrumbContainer.Background = Brushes.Transparent;
            }

            var protocolInfo = ProtocolManager.Parse(path);

            if (protocolInfo.Type == ProtocolType.Library)
            {
                identifier = "lib ";
                specialContent = protocolInfo.TargetPath;
                isSpecial = true;
            }
            else if (protocolInfo.Type == ProtocolType.Archive)
            {
                BuildArchiveBreadcrumb(protocolInfo, defaultBrush, hoverBrush);
                return;
            }
            else if (protocolInfo.Type == ProtocolType.Search)
            {
                identifier = "search ";
                specialContent = protocolInfo.TargetPath;
                isSpecial = true;
            }
            else if (protocolInfo.Type == ProtocolType.ContentSearch)
            {
                identifier = "content ";
                specialContent = protocolInfo.TargetPath;
                isSpecial = true;
            }
            else if (protocolInfo.Type == ProtocolType.Tag)
            {
                identifier = "tag ";
                specialContent = protocolInfo.TargetPath;
                isSpecial = true;
            }

            // 添加标签前缀
            AddPrefixRun(identifier);

            if (isSpecial)
            {
                // 对于特殊模式，直接显示内容而不拆分路径
                var contentRun = new Run(specialContent ?? "")
                {
                    Foreground = defaultBrush
                };
                _breadcrumbText.Inlines.Add(contentRun);
                return;
            }

            // 处理标准文件系统路径
            var (rootPath, parts) = ParsePathSegments(path);

            // 判断是否为长路径
            bool isLongPath = parts.Length > 10;

            if (isLongPath && _breadcrumbTail != null)
            {
                // 长路径：左侧只显示"path"，右侧显示最后6段（右对齐）
                _breadcrumbTail.Visibility = Visibility.Visible;
                _breadcrumbTail.ToolTip = path;

                var tailParts = parts.Skip(parts.Length - 6).ToArray();
                AddPathSegments(_breadcrumbTail, tailParts, path, defaultBrush, hoverBrush);
            }
            else
            {
                // 短路径：在左侧显示完整路径
                AddPathSegments(_breadcrumbText, parts, path, defaultBrush, hoverBrush);
            }
        }

        /// <summary>
        /// 设置面包屑为纯文本
        /// </summary>
        public void UpdateBreadcrumbText(string text)
        {
            if (_breadcrumbText == null)
                return;

            _breadcrumbText.Inlines.Clear();
            _breadcrumbText.Inlines.Add(new Run(text)
            {
                Foreground = Brushes.Blue
            });
        }

        /// <summary>
        /// 设置自定义面包屑文本（覆盖路径显示）
        /// </summary>
        public void SetCustomText(string text)
        {
            CustomText = text;
            UpdateBreadcrumbText(text ?? "");
        }

        /// <summary>
        /// 清除自定义面包屑文本，恢复路径显示
        /// </summary>
        public void ClearCustomText(string currentPath)
        {
            CustomText = null;
            UpdateBreadcrumb(currentPath);
        }

        #endregion

        #region 特殊模式面包屑

        /// <summary>
        /// 设置标签模式面包屑
        /// </summary>
        public void SetTagBreadcrumb(string tagName)
        {
            CustomText = null;
            if (_breadcrumbText == null) return;

            _breadcrumbText.Inlines.Clear();

            // 创建标签前缀和内容
            var prefixRun = new Run("tag ")
            {
                Foreground = new SolidColorBrush(Color.FromRgb(255, 165, 0)),
                FontWeight = FontWeights.SemiBold
            };
            var tagRun = new Run(tagName ?? "")
            {
                Foreground = Brushes.Black
            };
            _breadcrumbText.Inlines.Add(prefixRun);
            _breadcrumbText.Inlines.Add(tagRun);
        }

        /// <summary>
        /// 设置搜索模式面包屑
        /// </summary>
        public void SetSearchBreadcrumb(string keyword)
        {
            CustomText = null;
            if (_breadcrumbText == null) return;

            _breadcrumbText.Inlines.Clear();

            var prefixRun = new Run("search ")
            {
                Foreground = new SolidColorBrush(Color.FromRgb(255, 165, 0)),
                FontWeight = FontWeights.SemiBold
            };
            var keywordRun = new Run(keyword ?? "")
            {
                Foreground = Brushes.Black
            };
            _breadcrumbText.Inlines.Add(prefixRun);
            _breadcrumbText.Inlines.Add(keywordRun);
        }

        /// <summary>
        /// 设置库模式面包屑
        /// </summary>
        public void SetLibraryBreadcrumb(string libraryName)
        {
            CustomText = null;
            if (_breadcrumbText == null) return;

            _breadcrumbText.Inlines.Clear();

            var prefixRun = new Run("lib ")
            {
                Foreground = new SolidColorBrush(Color.FromRgb(255, 165, 0)),
                FontWeight = FontWeights.SemiBold
            };
            var libraryRun = new Run(libraryName ?? "")
            {
                Foreground = Brushes.Black
            };
            _breadcrumbText.Inlines.Add(prefixRun);
            _breadcrumbText.Inlines.Add(libraryRun);
        }

        #endregion

        #region 内部方法

        /// <summary>
        /// 构建压缩包路径的面包屑
        /// </summary>
        private void BuildArchiveBreadcrumb(ProtocolInfo protocolInfo, SolidColorBrush defaultBrush, SolidColorBrush hoverBrush)
        {
            _breadcrumbContainer.Background = Brushes.Transparent;
            _breadcrumbText.Inlines.Clear();

            // 添加 "zip " 前缀
            AddPrefixRun("zip ");

            string archivePath = protocolInfo.TargetPath;
            string innerPath = protocolInfo.ExtraData;

            // 1. 解析压缩包的文件系统路径
            var (_, archiveParts) = ParsePathSegments(archivePath);

            // 添加压缩包路径的各段
            string currentArchiveSegPath = "";
            for (int i = 0; i < archiveParts.Length; i++)
            {
                if (i == 0 && archiveParts[i].Length == 2 && archiveParts[i][1] == ':')
                    currentArchiveSegPath = archiveParts[i] + Path.DirectorySeparatorChar;
                else if (i == 0 && archiveParts[i].StartsWith("\\\\"))
                    currentArchiveSegPath = archiveParts[i];
                else
                    currentArchiveSegPath = Path.Combine(currentArchiveSegPath, archiveParts[i]);

                bool isLastPart = (i == archiveParts.Length - 1);
                string navigatePath = isLastPart
                    ? $"{ProtocolManager.ZipProtocol}{archivePath}|"
                    : currentArchiveSegPath;

                AddSegment(_breadcrumbText, archiveParts[i], navigatePath, defaultBrush, hoverBrush, true);
            }

            // 2. 内部路径段
            if (!string.IsNullOrEmpty(innerPath))
            {
                var innerParts = innerPath.Split(new[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries);
                string currentInner = "";

                for (int i = 0; i < innerParts.Length; i++)
                {
                    currentInner = i == 0 ? innerParts[i] : currentInner + "/" + innerParts[i];
                    string navPath = $"{ProtocolManager.ZipProtocol}{archivePath}|{currentInner}";
                    AddSegment(_breadcrumbText, innerParts[i], navPath, defaultBrush, hoverBrush, i < innerParts.Length - 1);
                }
            }
            else
            {
                // 移除最后一个分隔符
                if (_breadcrumbText.Inlines.Count > 0 &&
                    _breadcrumbText.Inlines.LastInline is Run lastRun &&
                    lastRun.Text == " \\ ")
                {
                    _breadcrumbText.Inlines.Remove(lastRun);
                }
            }
        }

        /// <summary>
        /// 解析路径为根路径和段数组
        /// </summary>
        private (string rootPath, string[] parts) ParsePathSegments(string path)
        {
            string rootPath = "";
            string[] parts;

            if (path.Length >= 2 && path[1] == ':')
            {
                // Windows 绝对路径
                rootPath = path.Substring(0, 2);
                var remainingPath = path.Substring(2).TrimStart(Path.DirectorySeparatorChar);
                parts = string.IsNullOrEmpty(remainingPath)
                    ? new[] { rootPath }
                    : new[] { rootPath }.Concat(remainingPath.Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries)).ToArray();
            }
            else if (path.StartsWith("\\\\"))
            {
                // UNC 路径
                var uncParts = path.Substring(2).Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries);
                rootPath = "\\\\" + (uncParts.Length > 0 ? uncParts[0] : "");
                parts = uncParts.Length > 1
                    ? new[] { rootPath }.Concat(uncParts.Skip(1)).ToArray()
                    : new[] { rootPath };
            }
            else
            {
                // 相对路径
                parts = path.Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries);
            }

            return (rootPath, parts);
        }

        /// <summary>
        /// 为路径段列表创建可交互的 Run 元素
        /// </summary>
        private void AddPathSegments(TextBlock targetTextBlock, string[] parts, string fullPath,
            SolidColorBrush defaultBrush, SolidColorBrush hoverBrush)
        {
            var currentPath = "";

            for (int i = 0; i < parts.Length; i++)
            {
                // 构建完整路径
                if (i == 0 && parts[i].Length == 2 && parts[i][1] == ':')
                {
                    currentPath = parts[i] + Path.DirectorySeparatorChar;
                }
                else if (i == 0 && parts[i].StartsWith("\\\\"))
                {
                    currentPath = parts[i];
                }
                else
                {
                    currentPath = Path.Combine(currentPath, parts[i]);
                }

                // 创建可点击的 Run
                var run = new Run(parts[i])
                {
                    Foreground = defaultBrush,
                    Cursor = Cursors.Hand
                };

                var pathToNavigate = currentPath;
                bool isLast = (i == parts.Length - 1);

                // 鼠标悬停效果
                run.MouseEnter += (s, e) => run.Foreground = hoverBrush;
                run.MouseLeave += (s, e) => run.Foreground = defaultBrush;

                // 点击事件
                run.MouseDown += (s, e) =>
                {
                    if (e.ChangedButton == MouseButton.Left)
                    {
                        if ((Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
                        {
                            e.Handled = true;
                            _onBreadcrumbMiddleClicked?.Invoke(pathToNavigate);
                        }
                        else
                        {
                            e.Handled = true;
                            if (isLast)
                            {
                                _onSwitchToEditMode?.Invoke();
                            }
                            else
                            {
                                _onBreadcrumbClicked?.Invoke(pathToNavigate);
                            }
                        }
                    }
                    else if (e.ChangedButton == MouseButton.Middle)
                    {
                        e.Handled = true;
                        _onBreadcrumbMiddleClicked?.Invoke(pathToNavigate);
                    }
                };

                targetTextBlock.Inlines.Add(run);

                // 添加分隔符
                if (i < parts.Length - 1)
                {
                    var separator = new Run(" \\ ")
                    {
                        Foreground = Brushes.Gray
                    };
                    targetTextBlock.Inlines.Add(separator);
                }
            }
        }

        /// <summary>
        /// 创建单个面包屑段（用于压缩包路径等）
        /// </summary>
        private void AddSegment(TextBlock targetTextBlock, string text, string navigatePath,
            SolidColorBrush defaultBrush, SolidColorBrush hoverBrush, bool addSeparator)
        {
            var run = new Run(text)
            {
                Foreground = defaultBrush,
                Cursor = Cursors.Hand
            };

            run.MouseEnter += (s, e) => run.Foreground = hoverBrush;
            run.MouseLeave += (s, e) => run.Foreground = defaultBrush;
            run.MouseDown += (s, e) =>
            {
                if (e.ChangedButton == MouseButton.Left)
                {
                    e.Handled = true;
                    _onBreadcrumbClicked?.Invoke(navigatePath);
                }
                else if (e.ChangedButton == MouseButton.Middle)
                {
                    e.Handled = true;
                    _onBreadcrumbMiddleClicked?.Invoke(navigatePath);
                }
            };

            targetTextBlock.Inlines.Add(run);

            if (addSeparator)
            {
                targetTextBlock.Inlines.Add(new Run(" \\ ") { Foreground = Brushes.Gray });
            }
        }

        /// <summary>
        /// 添加协议类型前缀 Run（如 "path ", "lib ", "zip " 等）
        /// </summary>
        private void AddPrefixRun(string identifier)
        {
            var prefixRun = new Run(identifier)
            {
                Foreground = new SolidColorBrush(Color.FromRgb(255, 165, 0)),
                FontWeight = FontWeights.SemiBold
            };
            _breadcrumbText.Inlines.Add(prefixRun);
        }

        #endregion
    }
}
