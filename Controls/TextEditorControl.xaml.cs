using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.Highlighting;

namespace YiboFile.Controls
{
    public partial class TextEditorControl : UserControl
    {
        private string _currentFilePath;
        private bool _isModified;

        public TextEditorControl()
        {
            InitializeComponent();
            InitializeEditor();
            InitializeSyntaxComboBox();
        }

        private void InitializeEditor()
        {
            // 设置编辑器选项
            TextEditor.Options.ConvertTabsToSpaces = true;
            TextEditor.Options.IndentationSize = 4;
            TextEditor.Options.EnableRectangularSelection = true;
            TextEditor.Options.EnableTextDragDrop = true;

            // 订阅文本变化事件
            TextEditor.TextChanged += TextEditor_TextChanged;

            // 订阅按钮事件
            SaveButton.Click += SaveButton_Click;
            UndoButton.Click += (s, e) => TextEditor.Undo();
            RedoButton.Click += (s, e) => TextEditor.Redo();

            // 添加快捷键
            InitializeKeyBindings();
        }

        private void InitializeSyntaxComboBox()
        {
            // 填充语法高亮选项
            SyntaxComboBox.ItemsSource = new string[]
            {
                "Plain Text", "C#", "C++", "Python", "JavaScript",
                "XML", "HTML", "JSON", "Markdown","SQL", "PHP"
            };
            SyntaxComboBox.SelectedIndex = 0;
            SyntaxComboBox.SelectionChanged += SyntaxComboBox_SelectionChanged;
        }

        private void InitializeKeyBindings()
        {
            // Ctrl+S 保存
            var saveCommand = new RoutedCommand();
            saveCommand.InputGestures.Add(new KeyGesture(Key.S, ModifierKeys.Control));
            TextEditor.InputBindings.Add(new InputBinding(saveCommand, new KeyGesture(Key.S, ModifierKeys.Control)));
            CommandBindings.Add(new CommandBinding(saveCommand, (s, e) => SaveButton_Click(null, null)));
        }

        public void LoadFile(string filePath)
        {
            try
            {
                _currentFilePath = filePath;
                TextEditor.Load(filePath);
                _isModified = false;

                // 自动检测语法高亮
                AutoDetectSyntax(filePath);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"加载文件失败: {ex.Message}", "错误",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void AutoDetectSyntax(string filePath)
        {
            string extension = Path.GetExtension(filePath).ToLower();
            string syntaxName = extension switch
            {
                ".cs" => "C#",
                ".cpp" or ".h" or ".hpp" or ".c" => "C++",
                ".py" => "Python",
                ".js" or ".ts" => "JavaScript",
                ".xml" => "XML",
                ".html" or ".htm" => "HTML",
                ".json" => "JSON",
                ".md" or ".markdown" => "Markdown",
                ".sql" => "SQL",
                ".php" => "PHP",
                _ => "Plain Text"
            };

            SyntaxComboBox.SelectedItem = syntaxName;
        }

        private void SyntaxComboBox_SelectionChanged(object sender,
            SelectionChangedEventArgs e)
        {
            if (SyntaxComboBox.SelectedItem == null) return;

            string syntaxName = SyntaxComboBox.SelectedItem.ToString();
            ApplySyntaxHighlighting(syntaxName);
        }

        private void ApplySyntaxHighlighting(string syntaxName)
        {
            // 映射到 AvalonEdit 的语法定义名称
            string highlightingName = syntaxName switch
            {
                "Plain Text" => null,
                "C#" => "C#",
                "C++" => "C++",
                "Python" => "Python",
                "JavaScript" => "JavaScript",
                "XML" => "XML",
                "HTML" => "HTML",
                "JSON" => "JavaScript", // JSON 使用 JavaScript 高亮
                "Markdown" => "MarkDown",
                "SQL" => "SQL",
                "PHP" => "PHP",
                _ => null
            };

            if (highlightingName != null)
            {
                TextEditor.SyntaxHighlighting =
                    HighlightingManager.Instance.GetDefinition(highlightingName);
            }
            else
            {
                TextEditor.SyntaxHighlighting = null;
            }
        }

        private void TextEditor_TextChanged(object sender, EventArgs e)
        {
            _isModified = true;
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_currentFilePath))
            {
                // MessageBox.Show("没有可保存的文件", "提示",
                //    MessageBoxButton.OK, MessageBoxImage.Information);
                Services.Core.NotificationService.ShowInfo("没有可保存的文件");
                return;
            }

            try
            {
                TextEditor.Save(_currentFilePath);
                _isModified = false;
                // MessageBox.Show("保存成功", "提示",
                //    MessageBoxButton.OK, MessageBoxImage.Information);
                Services.Core.NotificationService.ShowSuccess("保存成功");
            }
            catch (Exception ex)
            {
                // MessageBox.Show($"保存失败: {ex.Message}", "错误",
                //    MessageBoxButton.OK, MessageBoxImage.Error);
                Services.Core.NotificationService.ShowError($"保存失败: {ex.Message}");
            }
        }

        public bool HasUnsavedChanges => _isModified;

        public string CurrentFilePath => _currentFilePath;
    }
}

