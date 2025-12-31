using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace OoiMRR.Controls
{
    /// <summary>
    /// TextPreviewToolbar.xaml 的交互逻辑
    /// </summary>
    public partial class TextPreviewToolbar : UserControl
    {
        public TextPreviewToolbar()
        {
            InitializeComponent();

            // 初始化编码列表
            InitializeEncodings();
        }

        private void InitializeEncodings()
        {
            EncodingCombo.Items.Clear();
            var encodings = new[]
            {
                "UTF-8",
                "UTF-8 (BOM)",
                "GBK",
                "GB2312",
                "GB18030",
                "UTF-16 LE",
                "UTF-16 BE",
                "ASCII",
                "系统默认"
            };
            foreach (var enc in encodings)
            {
                EncodingCombo.Items.Add(enc);
            }
        }

        #region Dependency Properties

        public static readonly DependencyProperty FileNameProperty =
            DependencyProperty.Register("FileName", typeof(string), typeof(TextPreviewToolbar), new PropertyMetadata(string.Empty));

        public string FileName
        {
            get { return (string)GetValue(FileNameProperty); }
            set { SetValue(FileNameProperty, value); }
        }

        public static readonly DependencyProperty FileIconProperty =
            DependencyProperty.Register("FileIcon", typeof(string), typeof(TextPreviewToolbar), new PropertyMetadata("📄"));

        public string FileIcon
        {
            get { return (string)GetValue(FileIconProperty); }
            set { SetValue(FileIconProperty, value); }
        }

        public static readonly DependencyProperty CustomActionContentProperty =
            DependencyProperty.Register("CustomActionContent", typeof(object), typeof(TextPreviewToolbar), new PropertyMetadata(null));

        public object CustomActionContent
        {
            get { return GetValue(CustomActionContentProperty); }
            set { SetValue(CustomActionContentProperty, value); }
        }

        // Feature visibility flags

        public static readonly DependencyProperty ShowSearchProperty =
            DependencyProperty.Register("ShowSearch", typeof(bool), typeof(TextPreviewToolbar), new PropertyMetadata(true));

        public bool ShowSearch
        {
            get { return (bool)GetValue(ShowSearchProperty); }
            set { SetValue(ShowSearchProperty, value); }
        }

        public static readonly DependencyProperty ShowWordWrapProperty =
            DependencyProperty.Register("ShowWordWrap", typeof(bool), typeof(TextPreviewToolbar), new PropertyMetadata(true));

        public bool ShowWordWrap
        {
            get { return (bool)GetValue(ShowWordWrapProperty); }
            set { SetValue(ShowWordWrapProperty, value); }
        }

        public static readonly DependencyProperty ShowEncodingProperty =
            DependencyProperty.Register("ShowEncoding", typeof(bool), typeof(TextPreviewToolbar), new PropertyMetadata(true));

        public bool ShowEncoding
        {
            get { return (bool)GetValue(ShowEncodingProperty); }
            set { SetValue(ShowEncodingProperty, value); }
        }

        public static readonly DependencyProperty ShowViewToggleProperty =
            DependencyProperty.Register("ShowViewToggle", typeof(bool), typeof(TextPreviewToolbar), new PropertyMetadata(false));

        public bool ShowViewToggle
        {
            get { return (bool)GetValue(ShowViewToggleProperty); }
            set { SetValue(ShowViewToggleProperty, value); }
        }

        public static readonly DependencyProperty ShowFormatProperty =
            DependencyProperty.Register("ShowFormat", typeof(bool), typeof(TextPreviewToolbar), new PropertyMetadata(false));

        public bool ShowFormat
        {
            get { return (bool)GetValue(ShowFormatProperty); }
            set { SetValue(ShowFormatProperty, value); }
        }

        // State properties

        public static readonly DependencyProperty IsWordWrapEnabledProperty =
            DependencyProperty.Register("IsWordWrapEnabled", typeof(bool), typeof(TextPreviewToolbar), new PropertyMetadata(true));

        public bool IsWordWrapEnabled
        {
            get { return (bool)GetValue(IsWordWrapEnabledProperty); }
            set { SetValue(IsWordWrapEnabledProperty, value); }
        }

        // Match Count Text
        public void SetMatchCount(int current, int total)
        {
            if (total == 0)
                MatchCountText.Text = "0/0";
            else
                MatchCountText.Text = $"{current}/{total}";
        }

        // Encoding Selection
        public void SetSelectedEncoding(string encodingName)
        {
            EncodingCombo.SelectedItem = encodingName;
        }

        // View toggle state
        public void SetViewToggleText(string text)
        {
            ViewToggleButton.Content = text;
        }

        // Edit button state
        public void SetEditMode(bool isEdit)
        {
            if (isEdit)
            {
                EditButton.Content = "💾 保存";
                // 可以在这里设置颜色，但建议通过绑定或样式
            }
            else
            {
                EditButton.Content = "✏️ 编辑";
            }
        }

        #endregion

        #region Events

        public event EventHandler<string> SearchRequested;
        public event EventHandler SearchNextRequested;
        public event EventHandler SearchPrevRequested;
        public event EventHandler<bool> WordWrapChanged;
        public event EventHandler<string> EncodingChanged;
        public event EventHandler ViewToggleRequested;
        public event EventHandler FormatRequested;
        public event EventHandler CopyRequested;
        public event EventHandler EditRequested; // Toggle Edit/Save
        public event EventHandler OpenExternalRequested;

        #endregion

        #region Event Handlers

        private void SearchBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
                {
                    SearchPrevRequested?.Invoke(this, EventArgs.Empty);
                }
                else
                {
                    // 如果有文本更改未触发搜索，可以在这里触发
                    // 但通常 Changed 事件已经处理了
                    SearchNextRequested?.Invoke(this, EventArgs.Empty);
                }
                e.Handled = true;
            }
        }

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            SearchRequested?.Invoke(this, SearchBox.Text);
        }

        private void PrevMatchButton_Click(object sender, RoutedEventArgs e)
        {
            SearchPrevRequested?.Invoke(this, EventArgs.Empty);
        }

        private void NextMatchButton_Click(object sender, RoutedEventArgs e)
        {
            SearchNextRequested?.Invoke(this, EventArgs.Empty);
        }

        private void ViewToggleButton_Click(object sender, RoutedEventArgs e)
        {
            ViewToggleRequested?.Invoke(this, EventArgs.Empty);
        }

        private void FormatButton_Click(object sender, RoutedEventArgs e)
        {
            FormatRequested?.Invoke(this, EventArgs.Empty);
        }

        private void WrapButton_CheckedChanged(object sender, RoutedEventArgs e)
        {
            WordWrapChanged?.Invoke(this, WrapButton.IsChecked == true);
        }

        private void EncodingCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (EncodingCombo.SelectedItem != null)
            {
                EncodingChanged?.Invoke(this, EncodingCombo.SelectedItem.ToString());
            }
        }

        private void CopyButton_Click(object sender, RoutedEventArgs e)
        {
            CopyRequested?.Invoke(this, EventArgs.Empty);
        }

        private void EditButton_Click(object sender, RoutedEventArgs e)
        {
            EditRequested?.Invoke(this, EventArgs.Empty);
        }

        private void OpenExternalButton_Click(object sender, RoutedEventArgs e)
        {
            OpenExternalRequested?.Invoke(this, EventArgs.Empty);
        }

        #endregion
    }
}
