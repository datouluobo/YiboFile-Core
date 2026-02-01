using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Globalization;
using System.Windows.Data;
using YiboFile.Services.Search;
using YiboFile.Services.Config;

using YiboFile.Services.Core;

namespace YiboFile.Controls
{
    /// <summary>
    /// AddressBarControl.xaml 的交互逻辑
    /// </summary>
    public partial class AddressBarControl : UserControl
    {
        public event EventHandler<string> PathChanged;
        public event EventHandler<string> BreadcrumbClicked;
        public event EventHandler<string> BreadcrumbMiddleClicked;

        private string _currentPath = "";
        private bool _isEditMode = false;
        private string _breadcrumbCustomText = null;

        public AddressBarControl()
        {
            InitializeComponent();
            UpdateSearchModeUI();
        }

        public static readonly DependencyProperty AddressTextProperty =
            DependencyProperty.Register("AddressText", typeof(string), typeof(AddressBarControl),
                new FrameworkPropertyMetadata("", FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnAddressTextChanged));

        public string AddressText
        {
            get => (string)GetValue(AddressTextProperty);
            set => SetValue(AddressTextProperty, value);
        }

        private static void OnAddressTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is AddressBarControl control)
            {
                string newValue = (string)e.NewValue ?? "";
                if (control.AddressTextBox != null)
                {
                    control.AddressTextBox.Text = newValue;
                }
                control._currentPath = newValue;
                if (!control._isEditMode)
                {
                    if (!string.IsNullOrEmpty(control._breadcrumbCustomText))
                        control.UpdateBreadcrumbText(control._breadcrumbCustomText);
                    else
                        control.UpdateBreadcrumb(newValue);
                }
            }
        }

        public static readonly DependencyProperty IsReadOnlyProperty =
            DependencyProperty.Register("IsReadOnly", typeof(bool), typeof(AddressBarControl),
                new PropertyMetadata(false, OnIsReadOnlyChanged));

        public bool IsReadOnly
        {
            get => (bool)GetValue(IsReadOnlyProperty);
            set => SetValue(IsReadOnlyProperty, value);
        }

        private static void OnIsReadOnlyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is AddressBarControl control && control.AddressTextBox != null)
            {
                control.AddressTextBox.IsReadOnly = (bool)e.NewValue;
            }
        }

        /// <summary>
        /// 是否处于编辑模式
        /// </summary>
        public bool IsEditMode => _isEditMode;

        /// <summary>
        /// 地址栏文本框是否有焦点
        /// </summary>
        public bool IsAddressTextBoxFocused => AddressTextBox != null && (AddressTextBox.IsFocused || AddressTextBox.IsKeyboardFocused);

        public void UpdateBreadcrumb(string path)
        {
            if (BreadcrumbText == null)
                return;

            _currentPath = path ?? "";
            BreadcrumbText.Inlines.Clear();

            // 清理并隐藏右侧TextBlock（短路径时不用）
            if (BreadcrumbTail != null)
            {
                BreadcrumbTail.Inlines.Clear();
                BreadcrumbTail.Visibility = Visibility.Collapsed;
            }

            if (string.IsNullOrEmpty(path))
                return;

            // 设置完整路径为 ToolTip
            BreadcrumbText.ToolTip = path;

            // 获取前景色
            var parentWindow = Window.GetWindow(this);
            var defaultBrush = parentWindow?.TryFindResource("ForegroundBrush") as System.Windows.Media.SolidColorBrush
                ?? System.Windows.Media.Brushes.Black;
            var hoverBrush = parentWindow?.TryFindResource("HighlightBrush") as System.Windows.Media.SolidColorBrush
                ?? new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0, 120, 215));

            // 动态识别路径类型并设置标签
            string identifier = "path ";
            string specialContent = null;
            bool isSpecial = false;

            // Reset background rigorously first
            BreadcrumbContainer.ClearValue(Border.BackgroundProperty);
            if (BreadcrumbContainer.Background == null || BreadcrumbContainer.Background == System.Windows.Media.Brushes.Transparent)
            {
                // Ensure it is transparent if style didn't set it (though usually it does or is null)
                BreadcrumbContainer.Background = System.Windows.Media.Brushes.Transparent;
            }

            // ... (rest of logic)



            var protocolInfo = ProtocolManager.Parse(path);

            if (protocolInfo.Type == ProtocolType.Library)
            {
                identifier = "library ";
                specialContent = protocolInfo.TargetPath;
                isSpecial = true;
            }
            else if (protocolInfo.Type == ProtocolType.Archive)
            {
                // Remove yellow background (keep transparent)
                BreadcrumbContainer.Background = System.Windows.Media.Brushes.Transparent;

                // Standard-like Archive Breadcrumbs
                BreadcrumbText.Inlines.Clear();

                // Add "zip " prefix
                var prefixRun = new System.Windows.Documents.Run("zip ")
                {
                    Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 165, 0)),
                    FontWeight = FontWeights.SemiBold
                };
                BreadcrumbText.Inlines.Add(prefixRun);

                string archivePath = protocolInfo.TargetPath; // e.g. C:\Downloads\test.zip
                string innerPath = protocolInfo.ExtraData;    // e.g. Folder/Sub

                // 1. Process the standard file system path to the archive file
                // This breaks down C:\Downloads\test.zip into [C:] [Downloads] [test.zip]
                string archiveRoot = "";
                string[] archiveParts;

                if (archivePath.Length >= 2 && archivePath[1] == ':')
                {
                    archiveRoot = archivePath.Substring(0, 2); // "C:"
                    var remainingPath = archivePath.Substring(2).TrimStart(Path.DirectorySeparatorChar);
                    archiveParts = string.IsNullOrEmpty(remainingPath)
                        ? new[] { archiveRoot }
                        : new[] { archiveRoot }.Concat(remainingPath.Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries)).ToArray();
                }
                else if (archivePath.StartsWith("\\\\"))
                {
                    var uncParts = archivePath.Substring(2).Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries);
                    archiveRoot = "\\\\" + (uncParts.Length > 0 ? uncParts[0] : "");
                    archiveParts = uncParts.Length > 1
                        ? new[] { archiveRoot }.Concat(uncParts.Skip(1)).ToArray()
                        : new[] { archiveRoot };
                }
                else
                {
                    archiveParts = archivePath.Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries);
                }

                // Add the segments for the archive file path
                string currentArchiveSegPath = "";
                for (int i = 0; i < archiveParts.Length; i++)
                {
                    if (i == 0 && archiveParts[i].Length == 2 && archiveParts[i][1] == ':') { currentArchiveSegPath = archiveParts[i] + Path.DirectorySeparatorChar; }
                    else if (i == 0 && archiveParts[i].StartsWith("\\\\")) { currentArchiveSegPath = archiveParts[i]; }
                    else { currentArchiveSegPath = Path.Combine(currentArchiveSegPath, archiveParts[i]); }

                    // Navigation Path Logic:
                    // Only the LAST part (the archive file itself) should navigate to zip://...|
                    // The previous parts should navigate to standard file system folders.

                    bool isLastPart = (i == archiveParts.Length - 1);
                    string navigatePath;

                    if (isLastPart)
                    {
                        // The archive file itself -> Enter the archive root
                        navigatePath = $"{ProtocolManager.ZipProtocol}{archivePath}|";
                    }
                    else
                    {
                        // Parent folders -> Standard navigation
                        navigatePath = currentArchiveSegPath;
                    }

                    AddSegment(BreadcrumbText, archiveParts[i], navigatePath, defaultBrush, hoverBrush, true);
                }

                // 2. Inner Path Segments
                if (!string.IsNullOrEmpty(innerPath))
                {
                    var innerParts = innerPath.Split(new[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries);
                    string currentInner = "";

                    for (int i = 0; i < innerParts.Length; i++)
                    {
                        currentInner = i == 0 ? innerParts[i] : currentInner + "/" + innerParts[i];
                        string navPath = $"{ProtocolManager.ZipProtocol}{archivePath}|{currentInner}";

                        AddSegment(BreadcrumbText, innerParts[i], navPath, defaultBrush, hoverBrush, i < innerParts.Length - 1);
                    }
                }
                else
                {
                    // Remove the trailing separator from the last added segment if there is no inner path
                    if (BreadcrumbText.Inlines.Count > 0 && BreadcrumbText.Inlines.LastInline is System.Windows.Documents.Run lastRun && lastRun.Text == " \\ ")
                    {
                        BreadcrumbText.Inlines.Remove(lastRun);
                    }
                }

                return; // Return early as we handled breadcrumbs manually
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

            // 添加标签 Run (Standard Handling)
            var prefixRunStandard = new System.Windows.Documents.Run(identifier)
            {
                Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 165, 0)),
                FontWeight = FontWeights.SemiBold
            };
            BreadcrumbText.Inlines.Add(prefixRunStandard);

            if (isSpecial)
            {
                // 对于特殊模式，直接显示内容而不拆分路径
                var contentRun = new System.Windows.Documents.Run(specialContent ?? "")
                {
                    Foreground = defaultBrush
                };
                BreadcrumbText.Inlines.Add(contentRun);
                return;
            }

            // 处理路径分段
            string rootPath = "";
            string[] parts;

            if (path.Length >= 2 && path[1] == ':')
            {
                // Windows绝对路径，如 C:\Users\...
                rootPath = path.Substring(0, 2); // "C:"
                var remainingPath = path.Substring(2).TrimStart(Path.DirectorySeparatorChar);
                parts = string.IsNullOrEmpty(remainingPath)
                    ? new[] { rootPath }
                    : new[] { rootPath }.Concat(remainingPath.Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries)).ToArray();
            }
            else if (path.StartsWith("\\\\"))
            {
                // UNC路径，如 \\server\share\...
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

            // 判断是否为长路径
            bool isLongPath = parts.Length > 10;

            if (isLongPath && BreadcrumbTail != null)
            {
                // 长路径：左侧只显示"path"，右侧显示"... 最后3段"（右对齐）

                // 显示右侧TextBlock并填充最后3段
                BreadcrumbTail.Visibility = Visibility.Visible;
                BreadcrumbTail.ToolTip = path;

                var tailParts = parts.Skip(parts.Length - 6).ToArray();
                AddPathSegments(BreadcrumbTail, tailParts, path, defaultBrush, hoverBrush);
            }
            else
            {
                // 短路径：在左侧显示完整路径
                AddPathSegments(BreadcrumbText, parts, path, defaultBrush, hoverBrush);
            }
        }

        private void AddPathSegments(TextBlock targetTextBlock, string[] parts, string fullPath,
            System.Windows.Media.SolidColorBrush defaultBrush, System.Windows.Media.SolidColorBrush hoverBrush)
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
                var run = new System.Windows.Documents.Run(parts[i])
                {
                    Foreground = defaultBrush,
                    Cursor = Cursors.Hand
                };

                var pathToNavigate = currentPath;
                bool isLast = (i == parts.Length - 1);

                // 鼠标悬停效果
                run.MouseEnter += (s, e) =>
                {
                    run.Foreground = hoverBrush;
                };
                run.MouseLeave += (s, e) =>
                {
                    run.Foreground = defaultBrush;
                };

                // 点击事件
                run.MouseDown += (s, e) =>
                {
                    if (e.ChangedButton == MouseButton.Left)
                    {
                        if ((Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
                        {
                            e.Handled = true;
                            BreadcrumbMiddleClicked?.Invoke(this, pathToNavigate);
                        }
                        else
                        {
                            e.Handled = true;

                            // 如果是最后一项，进入编辑模式
                            if (isLast)
                            {
                                SwitchToEditMode();
                            }
                            else
                            {
                                BreadcrumbClicked?.Invoke(this, pathToNavigate);
                            }
                        }
                    }
                    else if (e.ChangedButton == MouseButton.Middle)
                    {
                        e.Handled = true;
                        BreadcrumbMiddleClicked?.Invoke(this, pathToNavigate);
                    }
                };

                targetTextBlock.Inlines.Add(run);

                // 添加分隔符（使用反斜杠）
                if (i < parts.Length - 1)
                {
                    var separator = new System.Windows.Documents.Run(" \\ ")
                    {
                        Foreground = System.Windows.Media.Brushes.Gray
                    };
                    targetTextBlock.Inlines.Add(separator);
                }
            }
        }

        private void AddSegment(TextBlock targetTextBlock, string text, string navigatePath,
            System.Windows.Media.SolidColorBrush defaultBrush, System.Windows.Media.SolidColorBrush hoverBrush, bool addSeparator)
        {
            var run = new System.Windows.Documents.Run(text)
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
                    BreadcrumbClicked?.Invoke(this, navigatePath);
                }
                else if (e.ChangedButton == MouseButton.Middle)
                {
                    e.Handled = true;
                    BreadcrumbMiddleClicked?.Invoke(this, navigatePath);
                }
            };

            targetTextBlock.Inlines.Add(run);

            if (addSeparator)
            {
                targetTextBlock.Inlines.Add(new System.Windows.Documents.Run(" \\ ") { Foreground = System.Windows.Media.Brushes.Gray });
            }
        }

        public void UpdateBreadcrumbText(string text)
        {
            if (BreadcrumbText == null)
                return;

            BreadcrumbText.Inlines.Clear();
            BreadcrumbText.Inlines.Add(new System.Windows.Documents.Run(text)
            {
                Foreground = System.Windows.Media.Brushes.Blue
            });
        }

        public void SetBreadcrumbCustomText(string text)
        {
            _breadcrumbCustomText = text;
            UpdateBreadcrumbText(text ?? "");
        }

        public void SetTagBreadcrumb(string tagName)
        {
            _breadcrumbCustomText = null;
            if (BreadcrumbText == null)
                return;

            BreadcrumbText.Inlines.Clear();

            var container = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(2, 2, 2, 2)
            };

            var badge = new Border
            {
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(8, 2, 8, 2),
                Margin = new Thickness(0, 0, 6, 0)
            };
            try
            {
                var parentWindow = Window.GetWindow(this);
                var bg = parentWindow?.FindResource("HighlightBrush") as System.Windows.Media.SolidColorBrush;
                var bd = parentWindow?.FindResource("HighlightBorderBrush") as System.Windows.Media.SolidColorBrush;
                badge.Background = bg ?? new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 165, 0));
                badge.BorderBrush = bd ?? new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 140, 0));
            }
            catch
            {
                badge.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 165, 0));
                badge.BorderBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 140, 0));
            }

            var badgeText = new TextBlock
            {
                Text = "tag",
                FontWeight = FontWeights.SemiBold,
                FontSize = 12,
                VerticalAlignment = VerticalAlignment.Center
            };
            try
            {
                var parentWindow = Window.GetWindow(this);
                var fg = parentWindow?.FindResource("HighlightForegroundBrush") as System.Windows.Media.SolidColorBrush;
                badgeText.Foreground = fg ?? System.Windows.Media.Brushes.Black;
            }
            catch
            {
                badgeText.Foreground = System.Windows.Media.Brushes.Black;
            }
            badge.Child = badgeText;

            // 让tag按钮可以点击，点击后返回到标签浏览模式
            badge.Cursor = Cursors.Hand;
            badge.MouseLeftButtonDown += (s, e) =>
            {
                e.Handled = true;
                BreadcrumbClicked?.Invoke(this, "tag://");
            };
            badge.MouseEnter += (s, e) =>
            {
                badge.Background = new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromRgb(255, 140, 0));
            };
            badge.MouseLeave += (s, e) =>
            {
                try
                {
                    var parentWindow = Window.GetWindow(this);
                    var bg = parentWindow?.FindResource("HighlightBrush") as System.Windows.Media.SolidColorBrush;
                    badge.Background = bg ?? new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 165, 0));
                }
                catch
                {
                    badge.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 165, 0));
                }
            };

            var nameText = new TextBlock
            {
                Text = tagName ?? "",
                FontSize = 12,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(4, 0, 0, 0)
            };

            container.Children.Add(badge);
            container.Children.Add(nameText);
            // Note: Tag breadcrumb uses inline Runs now
            var prefixRun = new System.Windows.Documents.Run("tag ")
            {
                Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 165, 0)),
                FontWeight = FontWeights.SemiBold
            };
            var tagRun = new System.Windows.Documents.Run(tagName ?? "")
            {
                Foreground = System.Windows.Media.Brushes.Black
            };
            BreadcrumbText.Inlines.Add(prefixRun);
            BreadcrumbText.Inlines.Add(tagRun);
        }

        public void SetSearchBreadcrumb(string keyword)
        {
            _breadcrumbCustomText = null;
            if (BreadcrumbText == null)
                return;

            BreadcrumbText.Inlines.Clear();

            var prefixRun = new System.Windows.Documents.Run("search ")
            {
                Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 165, 0)),
                FontWeight = FontWeights.SemiBold
            };
            var keywordRun = new System.Windows.Documents.Run(keyword ?? "")
            {
                Foreground = System.Windows.Media.Brushes.Black
            };
            BreadcrumbText.Inlines.Add(prefixRun);
            BreadcrumbText.Inlines.Add(keywordRun);
        }

        public void SetLibraryBreadcrumb(string libraryName)
        {
            _breadcrumbCustomText = null;
            if (BreadcrumbText == null)
                return;

            BreadcrumbText.Inlines.Clear();

            var prefixRun = new System.Windows.Documents.Run("library ")
            {
                Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 165, 0)),
                FontWeight = FontWeights.SemiBold
            };
            var libraryRun = new System.Windows.Documents.Run(libraryName ?? "")
            {
                Foreground = System.Windows.Media.Brushes.Black
            };
            BreadcrumbText.Inlines.Add(prefixRun);
            BreadcrumbText.Inlines.Add(libraryRun);
        }

        public void ClearBreadcrumbCustomText()
        {
            _breadcrumbCustomText = null;
            UpdateBreadcrumb(_currentPath);
        }

        private void BreadcrumbContainer_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            // 检查是否点击在按钮上
            var hitElement = e.OriginalSource as FrameworkElement;
            if (hitElement != null)
            {
                // 向上查找，如果找到Button，说明点击在按钮上，不切换到编辑模式
                var button = FindAncestor<Button>(hitElement);
                if (button != null)
                {
                    return; // 让按钮的Click事件处理
                }
            }

            // 点击在空白区域，切换到编辑模式
            SwitchToEditMode();
            e.Handled = true;
        }

        public void SwitchToEditMode()
        {
            if (_isEditMode) return;

            _isEditMode = true;

            // 自动识别模式并优化显示文本
            string displayPath = _currentPath;
            if (SearchModeToggle != null)
            {
                if (!string.IsNullOrEmpty(_currentPath))
                {
                    if (_currentPath.StartsWith("content://", StringComparison.OrdinalIgnoreCase))
                    {
                        // 全文搜索模式：选中开关，隐藏协议头
                        SearchModeToggle.IsChecked = true;
                        displayPath = _currentPath.Substring("content://".Length);
                    }
                    else if (_currentPath.StartsWith("search://", StringComparison.OrdinalIgnoreCase))
                    {
                        // 文件名搜索模式：不选中开关，保留协议头（暂不隐藏以免混淆）
                        SearchModeToggle.IsChecked = false;
                    }
                    else
                    {
                        // 普通路径模式：不选中开关
                        SearchModeToggle.IsChecked = false;
                    }
                }
            }

            AddressTextBox.Text = displayPath;

            BreadcrumbContainer.Visibility = Visibility.Collapsed;

            // 显示编辑容器
            if (EditModeContainer != null) EditModeContainer.Visibility = Visibility.Visible;
            AddressTextBox.Visibility = Visibility.Visible;

            // 延迟设置焦点，确保UI更新完成
            Dispatcher.BeginInvoke(new System.Action(() =>
            {
                AddressTextBox.Focus();
                Keyboard.Focus(AddressTextBox);
                AddressTextBox.SelectAll();
            }), System.Windows.Threading.DispatcherPriority.Input);
        }

        public void SwitchToBreadcrumbMode()
        {
            if (!_isEditMode) return;

            _isEditMode = false;

            // 隐藏编辑容器
            if (EditModeContainer != null) EditModeContainer.Visibility = Visibility.Collapsed;
            AddressTextBox.Visibility = Visibility.Collapsed;

            BreadcrumbContainer.Visibility = Visibility.Visible;
            if (!string.IsNullOrEmpty(_breadcrumbCustomText))
                UpdateBreadcrumbText(_breadcrumbCustomText);
            else
                UpdateBreadcrumb(_currentPath);
        }

        private void AddressTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {

        }

        private void AddressTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                var path = AddressTextBox.Text;

                // 处理搜索模式
                if (SearchModeToggle != null && SearchModeToggle.IsChecked == true)
                {
                    // 全文搜索模式：确保有 content:// 前缀
                    // 注意：这里使用 content:// 作为强制全文搜索的协议头，区别于 content:
                    // 这样可以彻底隔离 Everything 和 Lucene
                    if (!string.IsNullOrWhiteSpace(path) && !path.StartsWith("content://", StringComparison.OrdinalIgnoreCase))
                    {
                        path = "content://" + path;
                    }
                }

                // Update the DP to sync with bound ViewModel
                AddressText = path;

                PathChanged?.Invoke(this, path);

                // 记录历史
                RecordHistory(path);

                SwitchToBreadcrumbMode();
                e.Handled = true;
            }
            else if (e.Key == Key.Escape)
            {
                // 取消编辑，恢复原路径
                AddressTextBox.Text = _currentPath;
                SwitchToBreadcrumbMode();
                e.Handled = true;
            }
            // KeyDown 中不处理快捷键，让 PreviewKeyDown 处理，避免重复
        }

        private void SearchModeToggle_Click(object sender, RoutedEventArgs e)
        {
            UpdateSearchModeUI();
            // UI更新由XAML Trigger处理，这里只需确保焦点回到输入框
            AddressTextBox.Focus();
        }

        private void UpdateSearchModeUI()
        {
            if (SearchModeToggle == null) return;

            if (SearchModeToggle.IsChecked == true)
            {
                SearchModeToggle.Content = "文";
                SearchModeToggle.ToolTip = "当前模式：全文搜索 (再次点击切换回文件名搜索)";
            }
            else
            {
                SearchModeToggle.Content = "名";
                SearchModeToggle.ToolTip = "切换搜索模式：文件名 (默认) / 全文内容";
            }
        }

        // 极速剪贴板操作（无延迟，直接操作）
        private static string FastGetClipboardText()
        {
            try
            {
                return Clipboard.ContainsText() ? Clipboard.GetText() : null;
            }
            catch
            {
                return null;
            }
        }

        private static void FastSetClipboardText(string text)
        {
            try
            {
                Clipboard.SetText(text);
            }
            catch { }
        }

        private void AddressTextBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            // 处理键盘快捷键（仅在地址栏编辑模式下）
            if (!_isEditMode)
                return;

            // 确保 TextBox 有焦点
            if (!AddressTextBox.IsFocused && !AddressTextBox.IsKeyboardFocused)
                return;

            if (Keyboard.Modifiers == ModifierKeys.Control)
            {
                switch (e.Key)
                {
                    case Key.A: // Ctrl+A 全选
                        AddressTextBox.SelectAll();
                        e.Handled = true;
                        break;

                    case Key.C: // Ctrl+C 复制
                        var textToCopy = AddressTextBox.SelectionLength > 0
                            ? AddressTextBox.SelectedText
                            : AddressTextBox.Text;
                        FastSetClipboardText(textToCopy);
                        e.Handled = true;
                        break;

                    case Key.X: // Ctrl+X 剪切
                        if (AddressTextBox.SelectionLength > 0)
                        {
                            var textToCut = AddressTextBox.SelectedText;
                            var selectionStart = AddressTextBox.SelectionStart;
                            var selectionLength = AddressTextBox.SelectionLength;

                            // 先设置剪贴板
                            FastSetClipboardText(textToCut);

                            // 直接操作 Text 属性来删除选中文本
                            var currentText = AddressTextBox.Text;
                            var newText = currentText.Remove(selectionStart, selectionLength);
                            AddressTextBox.Text = newText;
                            AddressTextBox.CaretIndex = selectionStart;
                        }
                        e.Handled = true;
                        break;

                    case Key.V: // Ctrl+V 粘贴
                        var textToPaste = FastGetClipboardText();
                        if (textToPaste != null)
                        {
                            if (AddressTextBox.SelectionLength > 0)
                            {
                                var selectionStart = AddressTextBox.SelectionStart;
                                var selectionLength = AddressTextBox.SelectionLength;
                                var currentText = AddressTextBox.Text;
                                var newText = currentText.Remove(selectionStart, selectionLength).Insert(selectionStart, textToPaste);
                                AddressTextBox.Text = newText;
                                AddressTextBox.CaretIndex = selectionStart + textToPaste.Length;
                            }
                            else
                            {
                                var caretIndex = AddressTextBox.CaretIndex;
                                AddressTextBox.Text = AddressTextBox.Text.Insert(caretIndex, textToPaste);
                                AddressTextBox.CaretIndex = caretIndex + textToPaste.Length;
                            }
                        }
                        e.Handled = true;
                        break;
                }
                if (e.Key == Key.Down)
                {
                    if (!HistoryPopup.IsOpen)
                    {
                        ShowHistoryPopup();
                    }

                    if (HistoryPopup.IsOpen && HistoryListBox.Items.Count > 0)
                    {
                        // 选中第一项并聚焦列表
                        HistoryListBox.SelectedIndex = 0;
                        HistoryListBox.Focus();

                        // 尝试聚焦到具体Item容器(如果有必要)
                        var item = HistoryListBox.ItemContainerGenerator.ContainerFromIndex(0) as ListBoxItem;
                        item?.Focus();
                    }
                    e.Handled = true;
                }
            }
            else if (e.Key == Key.F5)
            {
                // F5 刷新当前路径
                if (!string.IsNullOrEmpty(_currentPath))
                {
                    PathChanged?.Invoke(this, _currentPath);
                }
                e.Handled = true;
            }
        }

        private void AddressTextBox_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            // 右键点击时，如果文本框没有焦点，先获得焦点
            if (!AddressTextBox.IsFocused)
            {
                AddressTextBox.Focus();
                e.Handled = true;
            }
        }

        private void AddressTextBox_ContextMenuOpening(object sender, ContextMenuEventArgs e)
        {
            // 创建自定义右键菜单
            var contextMenu = new ContextMenu();

            // 撤销
            var undoItem = new MenuItem
            {
                Header = "撤销(_U)",
                Command = ApplicationCommands.Undo,
                CommandTarget = AddressTextBox
            };
            contextMenu.Items.Add(undoItem);

            contextMenu.Items.Add(new Separator());

            // 剪切
            var cutItem = new MenuItem
            {
                Header = "剪切(_T)",
                Command = ApplicationCommands.Cut,
                CommandTarget = AddressTextBox
            };
            contextMenu.Items.Add(cutItem);

            // 复制
            var copyItem = new MenuItem
            {
                Header = "复制(_C)",
                Command = ApplicationCommands.Copy,
                CommandTarget = AddressTextBox
            };
            contextMenu.Items.Add(copyItem);

            // 粘贴
            var pasteItem = new MenuItem
            {
                Header = "粘贴(_P)",
                Command = ApplicationCommands.Paste,
                CommandTarget = AddressTextBox
            };
            contextMenu.Items.Add(pasteItem);

            contextMenu.Items.Add(new Separator());

            // 全选
            var selectAllItem = new MenuItem
            {
                Header = "全选(_A)",
                Command = ApplicationCommands.SelectAll,
                CommandTarget = AddressTextBox
            };
            contextMenu.Items.Add(selectAllItem);

            AddressTextBox.ContextMenu = contextMenu;
        }

        // 命令处理
        private void CopyCommand_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            var textToCopy = AddressTextBox.SelectionLength > 0
                ? AddressTextBox.SelectedText
                : AddressTextBox.Text;
            FastSetClipboardText(textToCopy);
        }

        private void CopyCommand_CanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = AddressTextBox != null && !string.IsNullOrEmpty(AddressTextBox.Text);
        }

        private void CutCommand_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            if (AddressTextBox.SelectionLength > 0)
            {
                var textToCut = AddressTextBox.SelectedText;
                var selectionStart = AddressTextBox.SelectionStart;
                var selectionLength = AddressTextBox.SelectionLength;

                FastSetClipboardText(textToCut);

                var currentText = AddressTextBox.Text;
                var newText = currentText.Remove(selectionStart, selectionLength);
                AddressTextBox.Text = newText;
                AddressTextBox.CaretIndex = selectionStart;
            }
        }

        private void CutCommand_CanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = AddressTextBox != null && AddressTextBox.SelectionLength > 0;
        }

        private void PasteCommand_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            var textToPaste = FastGetClipboardText();
            if (textToPaste != null)
            {
                if (AddressTextBox.SelectionLength > 0)
                {
                    var selectionStart = AddressTextBox.SelectionStart;
                    var selectionLength = AddressTextBox.SelectionLength;
                    var currentText = AddressTextBox.Text;
                    var newText = currentText.Remove(selectionStart, selectionLength).Insert(selectionStart, textToPaste);
                    AddressTextBox.Text = newText;
                    AddressTextBox.CaretIndex = selectionStart + textToPaste.Length;
                }
                else
                {
                    var caretIndex = AddressTextBox.CaretIndex;
                    AddressTextBox.Text = AddressTextBox.Text.Insert(caretIndex, textToPaste);
                    AddressTextBox.CaretIndex = caretIndex + textToPaste.Length;
                }
            }
        }

        private void PasteCommand_CanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = Clipboard.ContainsText();
        }

        private void SelectAllCommand_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            AddressTextBox.SelectAll();
        }

        private void AddressTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            // 使用 Dispatcher 延迟执行，以便检查焦点是否移动到了弹出窗口内部
            Dispatcher.BeginInvoke(new Action(() =>
            {
                // 如果焦点在历史记录弹出窗口内，或者点击的是下拉按钮，不关闭
                if (HistoryPopup.IsOpen && (HistoryPopup.IsKeyboardFocusWithin || HistoryListBox.IsKeyboardFocusWithin))
                {
                    return;
                }

                // 只有当点击发生在 UserControl 外部时才关闭 Popup
                // 由于 StaysOpen=True，我们需要手动处理关闭。
                // 这里的 LostFocus 这意味着焦点离开了 TextBox。
                // 如果焦点没有去 Popup，也不在 AddressBarControl 内部，则关闭。
                if (!this.IsKeyboardFocusWithin)
                {
                    HistoryPopup.IsOpen = false;
                }

                // 如果路径没有改变，切换回面包屑模式
                if (AddressTextBox.Text.Trim() == _currentPath)
                {
                    SwitchToBreadcrumbMode();
                }
                else
                {
                    // 路径改变了，但用户没有按Enter，询问是否导航？
                    // 或者直接切换回面包屑模式，保持原路径
                    AddressTextBox.Text = _currentPath;
                    SwitchToBreadcrumbMode();
                }
            }));
        }

        private void AddressTextBox_GotFocus(object sender, RoutedEventArgs e)
        {
            // 获得焦点时选中所有文本
            AddressTextBox.SelectAll();

            // 自动展开历史记录（如果设置启用）
            if (ConfigurationService.Instance.GetSnapshot().AutoExpandHistory)
            {
                // 使用 Dispatcher 延迟打开，防止鼠标点击事件被误判为"点击外部"导致立即关闭
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    if (AddressTextBox.IsFocused)
                    {
                        ShowHistoryPopup();
                    }
                }), System.Windows.Threading.DispatcherPriority.Input);
            }
        }

        private void HistoryDropdownButton_Click(object sender, RoutedEventArgs e)
        {
            if (HistoryPopup.IsOpen)
            {
                HistoryPopup.IsOpen = false;
            }
            else
            {
                // Ensure TextBox has focus so typing works immediately
                AddressTextBox.Focus();
                ShowHistoryPopup();
            }
        }

        private void ShowHistoryPopup()
        {
            var historyItems = SearchHistoryService.Instance.GetRecent();
            if (historyItems.Count > 0)
            {
                HistoryListBox.ItemsSource = historyItems;
                HistoryPopup.IsOpen = true;
            }
        }

        private void HistoryListBox_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            var item = (e.OriginalSource as FrameworkElement)?.DataContext as HistoryItem;
            if (item != null)
            {
                NavigateToHistoryItem(item);
                e.Handled = true;
            }
        }

        private void HistoryListBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                var item = HistoryListBox.SelectedItem as HistoryItem;
                if (item != null)
                {
                    NavigateToHistoryItem(item);
                    e.Handled = true;
                }
            }
            else if (e.Key == Key.Escape)
            {
                HistoryPopup.IsOpen = false;
                AddressTextBox.Focus();
                e.Handled = true;
            }
        }

        private void NavigateToHistoryItem(HistoryItem item)
        {
            HistoryPopup.IsOpen = false;

            string path = item.Content;
            if (item.Type == HistoryType.FullTextSearch && !path.StartsWith("content://", StringComparison.OrdinalIgnoreCase))
            {
                path = "content://" + path;
            }
            else if (item.Type == HistoryType.Search && !path.StartsWith("search://", StringComparison.OrdinalIgnoreCase))
            {
                path = "search://" + path;
            }

            AddressTextBox.Text = path; // 更新文本框显示
            PathChanged?.Invoke(this, path); // 触发导航
            SwitchToBreadcrumbMode();
        }

        private void RecordHistory(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return;

            HistoryType type = HistoryType.LocalPath;
            string content = path;

            if (path.StartsWith("content://", StringComparison.OrdinalIgnoreCase))
            {
                type = HistoryType.FullTextSearch;
                content = path.Substring("content://".Length);
            }
            else if (path.StartsWith("search://", StringComparison.OrdinalIgnoreCase))
            {
                type = HistoryType.Search;
                content = path.Substring("search://".Length);
            }
            // else 默认为 LocalPath

            SearchHistoryService.Instance.Add(content, type);
        }

        private static T FindAncestor<T>(DependencyObject current) where T : DependencyObject
        {
            while (current != null)
            {
                if (current is T ancestor)
                    return ancestor;
                current = System.Windows.Media.VisualTreeHelper.GetParent(current);
            }
            return null;
        }
    }

    public class HistoryTypeToIconConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is HistoryType type)
            {
                return type switch
                {
                    HistoryType.LocalPath => "\uE838", // Folder icon
                    HistoryType.Search => "\uE721",    // Search icon
                    HistoryType.FullTextSearch => "\uE890", // Document Search icon
                    _ => "\uE838"
                };
            }
            return "";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class HistoryTypeToTextConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is HistoryType type)
            {
                return type switch
                {
                    HistoryType.LocalPath => "位置",
                    HistoryType.Search => "搜索",
                    HistoryType.FullTextSearch => "全文",
                    _ => ""
                };
            }
            return "";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}

