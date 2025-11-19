using System;
using System.IO;
using System.Linq;
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
                    UpdateBreadcrumb(value);
                }
            }
        }

        public bool IsReadOnly
        {
            get => AddressTextBox.IsReadOnly;
            set => AddressTextBox.IsReadOnly = value;
        }

        public void UpdateBreadcrumb(string path)
        {
            if (BreadcrumbPanel == null)
                return;

            _currentPath = path ?? "";
            BreadcrumbPanel.Children.Clear();

            if (string.IsNullOrEmpty(path))
                return;

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

            _currentPath = text ?? "";
            BreadcrumbPanel.Children.Clear();
            BreadcrumbPanel.Children.Add(new TextBlock
            {
                Text = text,
                Margin = new Thickness(2, 2, 2, 2),
                Foreground = System.Windows.Media.Brushes.Blue
            });
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
            AddressTextBox.Focus();
            AddressTextBox.SelectAll();
        }

        private void SwitchToBreadcrumbMode()
        {
            if (!_isEditMode) return;

            _isEditMode = false;
            AddressTextBox.Visibility = Visibility.Collapsed;
            BreadcrumbContainer.Visibility = Visibility.Visible;
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

