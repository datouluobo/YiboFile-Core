using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using ICSharpCode.AvalonEdit.Highlighting;

namespace YiboFile.Controls
{
    public partial class HtmlEditorControl : UserControl
    {
        private DispatcherTimer _previewUpdateTimer;
        private string _currentFilePath;
        private bool _isModified;

        public HtmlEditorControl()
        {
            InitializeComponent();
            InitializePreviewTimer();
            InitializeUI();
        }

        private void InitializePreviewTimer()
        {
            // 设置延迟更新预览
            _previewUpdateTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(500)
            };
            _previewUpdateTimer.Tick += (s, e) =>
            {
                UpdatePreview();
                _previewUpdateTimer.Stop();
            };

            CodeEditor.TextChanged += (s, e) =>
            {
                _isModified = true;
                _previewUpdateTimer.Stop();
                _previewUpdateTimer.Start();
            };
        }

        private void InitializeUI()
        {
            // 设置编辑器选项
            CodeEditor.Options.ConvertTabsToSpaces = true;
            CodeEditor.Options.IndentationSize = 2;
            CodeEditor.Options.EnableTextDragDrop = true;

            // 按钮事件
            SaveButton.Click += SaveButton_Click;
            RefreshButton.Click += (s, e) => UpdatePreview();

            // 视图模式切换事件
            EditOnlyMode.Checked += ViewMode_Changed;
            PreviewOnlyMode.Checked += ViewMode_Changed;
            SplitViewMode.Checked += ViewMode_Changed;

            // 初始应用当前选中模式的布局
            ViewMode_Changed(null, null);
        }

        private void ViewMode_Changed(object sender, RoutedEventArgs e)
        {
            if (EditOnlyMode.IsChecked == true)
            {
                // 只显示编辑器
                EditorColumn.Width = new GridLength(1, GridUnitType.Star);
                SplitterColumn.Width = new GridLength(0);
                PreviewColumn.Width = new GridLength(0);
                ViewSplitter.Visibility = Visibility.Collapsed;
                if (SaveButton != null) SaveButton.Visibility = Visibility.Visible;
            }
            else if (PreviewOnlyMode.IsChecked == true)
            {
                // 只显示预览
                EditorColumn.Width = new GridLength(0);
                SplitterColumn.Width = new GridLength(0);
                PreviewColumn.Width = new GridLength(1, GridUnitType.Star);
                ViewSplitter.Visibility = Visibility.Collapsed;
                if (SaveButton != null) SaveButton.Visibility = Visibility.Collapsed;
            }
            else if (SplitViewMode.IsChecked == true)
            {
                // 分屏显示
                EditorColumn.Width = new GridLength(1, GridUnitType.Star);
                SplitterColumn.Width = new GridLength(5);
                PreviewColumn.Width = new GridLength(1, GridUnitType.Star);
                ViewSplitter.Visibility = Visibility.Visible;
                if (SaveButton != null) SaveButton.Visibility = Visibility.Visible;
            }
        }

        public void LoadFile(string filePath)
        {
            try
            {
                _currentFilePath = filePath;
                CodeEditor.Load(filePath);
                _isModified = false;

                // 自动应用高亮
                ApplySyntaxHighlighting(Path.GetExtension(filePath)?.ToLower());

                UpdatePreview();
            }
            catch (Exception ex)
            {
                // MessageBox.Show($"加载文件失败: {ex.Message}", "错误",
                //    MessageBoxButton.OK, MessageBoxImage.Error);
                Services.Core.NotificationService.ShowError($"加载文件失败: {ex.Message}");
            }
        }

        private void ApplySyntaxHighlighting(string extension)
        {
            string syntaxName = extension switch
            {
                ".html" or ".htm" => "HTML",
                ".xml" or ".xaml" or ".config" or ".svg" => "XML",
                _ => null
            };

            if (syntaxName != null)
            {
                CodeEditor.SyntaxHighlighting = HighlightingManager.Instance.GetDefinition(syntaxName);
            }
        }

        private void UpdatePreview()
        {
            try
            {
                // 如果是 HTML/XML/SVG，尝试直接渲染内容
                string content = CodeEditor.Text;

                // 简单的防乱码处理
                if (!content.Contains("http-equiv=\"Content-Type\"") && !content.Contains("charset="))
                {
                    // 如果不是完整的HTML文档，可能需要包装一下
                    // 但对于纯XML/SVG，不应随意添加HTML头
                    // 这里我们假设WebBrowser足够智能，或者是用户提供的完整内容
                }

                PreviewBrowser.NavigateToString(content);
            }
            catch (Exception)
            {
            }
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_currentFilePath)) return;

            try
            {
                CodeEditor.Save(_currentFilePath);
                _isModified = false;
                // MessageBox.Show("保存成功", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                Services.Core.NotificationService.ShowSuccess("保存成功");
            }
            catch (Exception ex)
            {
                // MessageBox.Show($"保存失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                Services.Core.NotificationService.ShowError($"保存失败: {ex.Message}");
            }
        }

        public bool HasUnsavedChanges => _isModified;
        public string CurrentFilePath => _currentFilePath;
    }
}

