using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace OoiMRR.Controls
{
    /// <summary>
    /// AddressBarControl.xaml 的交互逻辑
    /// </summary>
    public partial class AddressBarControl : UserControl
    {
        public event EventHandler<string> PathChanged;
        public event EventHandler<string> BreadcrumbClicked;

        private string _currentPath = "";
        private bool _isEditMode = false;
        private string _breadcrumbCustomText = null;

        public AddressBarControl()
        {
            InitializeComponent();
        }

        public string AddressText
        {
            get => AddressTextBox.Text;
            set
            {
                AddressTextBox.Text = value;
                _currentPath = value;
                if (!_isEditMode)
                {
                    if (!string.IsNullOrEmpty(_breadcrumbCustomText))
                        UpdateBreadcrumbText(_breadcrumbCustomText);
                    else
                        UpdateBreadcrumb(value);
                }
            }
        }

        public bool IsReadOnly
        {
            get => AddressTextBox.IsReadOnly;
            set => AddressTextBox.IsReadOnly = value;
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
            if (BreadcrumbPanel == null)
                return;

            _currentPath = path ?? "";
            BreadcrumbPanel.Children.Clear();

            if (string.IsNullOrEmpty(path))
                return;

            var parentWindowForPrefix = Window.GetWindow(this);
            var bgPrefix = parentWindowForPrefix?.TryFindResource("HighlightBrush") as System.Windows.Media.SolidColorBrush;
            var bdPrefix = parentWindowForPrefix?.TryFindResource("HighlightBorderBrush") as System.Windows.Media.SolidColorBrush;
            var fgPrefix = parentWindowForPrefix?.TryFindResource("HighlightForegroundBrush") as System.Windows.Media.SolidColorBrush;

            var prefixBadge = new Border
            {
                Background = bgPrefix ?? new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 165, 0)),
                BorderBrush = bdPrefix ?? new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 140, 0)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(8, 2, 8, 2),
                Margin = new Thickness(0, 0, 6, 0)
            };

            var prefixText = new TextBlock
            {
                Text = "path",
                Foreground = fgPrefix ?? System.Windows.Media.Brushes.Black,
                FontWeight = FontWeights.SemiBold,
                FontSize = 12,
                VerticalAlignment = VerticalAlignment.Center
            };
            prefixBadge.Child = prefixText;
            BreadcrumbPanel.Children.Add(prefixBadge);

            // 处理Windows路径（C:\ 或 UNC路径）
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

            var currentPath = "";

            for (int i = 0; i < parts.Length; i++)
            {
                if (i == 0 && parts[i].Length == 2 && parts[i][1] == ':')
                {
                    // Windows驱动器，如 C:
                    currentPath = parts[i] + Path.DirectorySeparatorChar;
                }
                else if (i == 0 && parts[i].StartsWith("\\\\"))
                {
                    // UNC根
                    currentPath = parts[i];
                }
                else
                {
                    currentPath = Path.Combine(currentPath, parts[i]);
                }

                var button = new Button
                {
                    Content = parts[i],
                    Margin = new Thickness(2, 2, 2, 2),
                    Padding = new Thickness(4, 2, 4, 2),
                    Background = System.Windows.Media.Brushes.Transparent,
                    BorderThickness = new Thickness(0),
                    Cursor = Cursors.Hand
                };

                // 鼠标悬停效果
                button.MouseEnter += (s, e) => 
                {
                    button.Background = new System.Windows.Media.SolidColorBrush(
                        System.Windows.Media.Color.FromArgb(30, 0, 0, 0));
                };
                button.MouseLeave += (s, e) => 
                {
                    button.Background = System.Windows.Media.Brushes.Transparent;
                };

                // 尝试从父窗口获取样式
                try
                {
                    var parentWindow = Window.GetWindow(this);
                    if (parentWindow != null)
                    {
                        var style = parentWindow.TryFindResource("BreadcrumbButtonStyle") as Style;
                        if (style != null)
                        {
                            button.Style = style;
                        }
                    }
                }
                catch { }

                var pathToNavigate = currentPath;
                button.Click += (s, e) => 
                {
                    e.Handled = true; // 阻止事件冒泡到容器
                    BreadcrumbClicked?.Invoke(this, pathToNavigate);
                };

                BreadcrumbPanel.Children.Add(button);

                if (i < parts.Length - 1)
                {
                    var separator = new TextBlock
                    {
                        Text = " › ",
                        Margin = new Thickness(4, 0, 4, 0),
                        Foreground = System.Windows.Media.Brushes.Gray,
                        VerticalAlignment = VerticalAlignment.Center
                    };
                    BreadcrumbPanel.Children.Add(separator);
                }
            }
        }

        public void UpdateBreadcrumbText(string text)
        {
            if (BreadcrumbPanel == null)
                return;

            BreadcrumbPanel.Children.Clear();
            BreadcrumbPanel.Children.Add(new TextBlock
            {
                Text = text,
                Margin = new Thickness(2, 2, 2, 2),
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
            if (BreadcrumbPanel == null)
                return;

            BreadcrumbPanel.Children.Clear();

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
            BreadcrumbPanel.Children.Add(container);
        }

        public void SetSearchBreadcrumb(string keyword)
        {
            _breadcrumbCustomText = null;
            if (BreadcrumbPanel == null)
                return;

            BreadcrumbPanel.Children.Clear();

            var container = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(2, 2, 2, 2)
            };

            var parentWindow = Window.GetWindow(this);
            var bg = parentWindow?.TryFindResource("HighlightBrush") as System.Windows.Media.SolidColorBrush;
            var bd = parentWindow?.TryFindResource("HighlightBorderBrush") as System.Windows.Media.SolidColorBrush;
            var fg = parentWindow?.TryFindResource("HighlightForegroundBrush") as System.Windows.Media.SolidColorBrush;

            var badge = new Border
            {
                Background = bg ?? new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 165, 0)),
                BorderBrush = bd ?? new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 140, 0)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(8, 2, 8, 2),
                Margin = new Thickness(0, 0, 6, 0)
            };

            var badgeText = new TextBlock
            {
                Text = "search",
                Foreground = fg ?? System.Windows.Media.Brushes.Black,
                FontWeight = FontWeights.SemiBold,
                FontSize = 12,
                VerticalAlignment = VerticalAlignment.Center
            };
            badge.Child = badgeText;

            var nameText = new TextBlock
            {
                Text = keyword ?? "",
                FontSize = 12,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(4, 0, 0, 0)
            };

            container.Children.Add(badge);
            container.Children.Add(nameText);
            BreadcrumbPanel.Children.Add(container);
        }

        public void SetLibraryBreadcrumb(string libraryName)
        {
            _breadcrumbCustomText = null;
            if (BreadcrumbPanel == null)
                return;

            BreadcrumbPanel.Children.Clear();

            var container = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(2, 2, 2, 2)
            };

            var parentWindow = Window.GetWindow(this);
            var bg = parentWindow?.TryFindResource("HighlightBrush") as System.Windows.Media.SolidColorBrush;
            var bd = parentWindow?.TryFindResource("HighlightBorderBrush") as System.Windows.Media.SolidColorBrush;
            var fg = parentWindow?.TryFindResource("HighlightForegroundBrush") as System.Windows.Media.SolidColorBrush;

            var badge = new Border
            {
                Background = bg ?? new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 165, 0)),
                BorderBrush = bd ?? new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 140, 0)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(8, 2, 8, 2),
                Margin = new Thickness(0, 0, 6, 0)
            };

            var badgeText = new TextBlock
            {
                Text = "library",
                Foreground = fg ?? System.Windows.Media.Brushes.Black,
                FontWeight = FontWeights.SemiBold,
                FontSize = 12,
                VerticalAlignment = VerticalAlignment.Center
            };
            badge.Child = badgeText;

            var nameText = new TextBlock
            {
                Text = libraryName ?? "",
                FontSize = 12,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(4, 0, 0, 0)
            };

            container.Children.Add(badge);
            container.Children.Add(nameText);
            BreadcrumbPanel.Children.Add(container);
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

        private void SwitchToEditMode()
        {
            if (_isEditMode) return;

            _isEditMode = true;
            AddressTextBox.Text = _currentPath;
            AddressTextBox.Visibility = Visibility.Visible;
            BreadcrumbContainer.Visibility = Visibility.Collapsed;
            
            // 延迟设置焦点，确保UI更新完成
            Dispatcher.BeginInvoke(new System.Action(() =>
            {
                AddressTextBox.Focus();
                Keyboard.Focus(AddressTextBox);
                AddressTextBox.SelectAll();
            }), System.Windows.Threading.DispatcherPriority.Input);
        }

        private void SwitchToBreadcrumbMode()
        {
            if (!_isEditMode) return;

            _isEditMode = false;
            AddressTextBox.Visibility = Visibility.Collapsed;
            BreadcrumbContainer.Visibility = Visibility.Visible;
            if (!string.IsNullOrEmpty(_breadcrumbCustomText))
                UpdateBreadcrumbText(_breadcrumbCustomText);
            else
                UpdateBreadcrumb(_currentPath);
        }

        private void AddressTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            // 可以在这里添加实时验证逻辑
        }

        private void AddressTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                var path = AddressTextBox.Text.Trim();
                if (!string.IsNullOrEmpty(path))
                {
                    _currentPath = path;
                    PathChanged?.Invoke(this, path);
                    SwitchToBreadcrumbMode();
                }
                else
                {
                    SwitchToBreadcrumbMode();
                }
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
        }

        private void AddressTextBox_GotFocus(object sender, RoutedEventArgs e)
        {
            // 获得焦点时选中所有文本
            AddressTextBox.SelectAll();
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
}
