using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Markdig;

namespace YiboFile.Controls
{
    public partial class MarkdownEditorControl : UserControl
    {
        private DispatcherTimer _previewUpdateTimer;
        private MarkdownPipeline _markdownPipeline;
        private string _currentFilePath;
        private bool _isModified;

        public MarkdownEditorControl()
        {
            InitializeComponent();
            InitializeMarkdown();
            InitializePreviewTimer();
            InitializeUI();
        }

        private void InitializeMarkdown()
        {
            // 配置 Markdig 管道（支持扩展功能）
            _markdownPipeline = new MarkdownPipelineBuilder()
                .UseAdvancedExtensions()  // 支持表格、任务列表等
                .UseSoftlineBreakAsHardlineBreak()
                .Build();

            // 设置 Markdown 语法高亮
            MarkdownEditor.SyntaxHighlighting =
                ICSharpCode.AvalonEdit.Highlighting.HighlightingManager.Instance.GetDefinition("MarkDown");
        }

        private void InitializePreviewTimer()
        {
            // 设置延迟更新预览（避免频繁刷新）
            _previewUpdateTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(500)
            };
            _previewUpdateTimer.Tick += (s, e) =>
            {
                UpdatePreview();
                _previewUpdateTimer.Stop();
            };

            MarkdownEditor.TextChanged += (s, e) =>
            {
                _isModified = true;
                _previewUpdateTimer.Stop();
                _previewUpdateTimer.Start();
            };
        }

        private void InitializeUI()
        {
            // 设置编辑器选项
            MarkdownEditor.Options.ConvertTabsToSpaces = true;
            MarkdownEditor.Options.IndentationSize = 2;
            MarkdownEditor.Options.EnableTextDragDrop = true;

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

        public void LoadMarkdown(string filePath)
        {
            try
            {
                _currentFilePath = filePath;
                MarkdownEditor.Load(filePath);
                _isModified = false;
                UpdatePreview();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"加载文件失败: {ex.Message}", "错误",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void UpdatePreview()
        {
            try
            {
                string markdown = MarkdownEditor.Text;
                string html = Markdown.ToHtml(markdown, _markdownPipeline);

                // 添加样式
                string styledHtml = $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'>
    <style>
        body {{
            font-family: 'Segoe UI', 'Microsoft YaHei', Arial, sans-serif;
            padding: 20px;
            line-height: 1.6;
            color: #333;
        }}
        h1, h2, h3, h4, h5, h6 {{
            margin-top: 24px;
            margin-bottom: 16px;
            font-weight: 600;
            line-height: 1.25;
        }}
        h1 {{
            font-size: 2em;
            border-bottom: 1px solid #eaecef;
            padding-bottom: 0.3em;
        }}
        h2 {{
            font-size: 1.5em;
            border-bottom: 1px solid #eaecef;
            padding-bottom: 0.3em;
        }}
        code {{
            background-color: #f6f8fa;
            padding: 2px 6px;
            border-radius: 3px;
            font-family: 'Consolas', 'Monaco', monospace;
            font-size: 85%;
        }}
        pre {{
            background-color: #f6f8fa;
            padding: 16px;
            border-radius: 6px;
            overflow-x: auto;
        }}
        pre code {{
            background-color: transparent;
            padding: 0;
        }}
        blockquote {{
            margin: 0;
            padding: 0 1em;
            color: #6a737d;
            border-left: 0.25em solid #dfe2e5;
        }}
        table {{
            border-collapse: collapse;
            width: 100%;
            margin: 16px 0;
        }}
        th, td {{
            border: 1px solid #dfe2e5;
            padding: 8px 13px;
            text-align: left;
        }}
        th {{
            background-color: #f6f8fa;
            font-weight: 600;
        }}
        tr:nth-child(even) {{
            background-color: #f6f8fa;
        }}
        a {{
            color: #0366d6;
            text-decoration: none;
        }}
        a:hover {{
            text-decoration: underline;
        }}
        img {{
            max-width: 100%;
            height: auto;
        }}
        ul, ol {{
            padding-left: 2em;
        }}
        li {{
            margin: 0.25em 0;
        }}
        hr {{
            height: 0.25em;
            padding: 0;
            margin: 24px 0;
            background-color: #e1e4e8;
            border: 0;
        }}
        input[type='checkbox'] {{
            margin-right: 0.5em;
        }}
    </style>
</head>
<body>
    {html}
</body>
</html>";

                PreviewBrowser.NavigateToString(styledHtml);
            }
            catch (Exception)
            {
            }
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_currentFilePath))
            {
                MessageBox.Show("没有可保存的文件", "提示",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            try
            {
                MarkdownEditor.Save(_currentFilePath);
                _isModified = false;
                MessageBox.Show("保存成功", "提示",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"保存失败: {ex.Message}", "错误",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public bool HasUnsavedChanges => _isModified;

        public string CurrentFilePath => _currentFilePath;
    }
}

