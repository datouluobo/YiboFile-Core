using System.Windows;
using System.Windows.Input;

namespace YiboFile.Dialogs
{
    /// <summary>
    /// PathInputDialog.xaml 的交互逻辑
    /// </summary>
    public partial class PathInputDialog : Window
    {
        public string InputText { get; set; }
        public bool SelectFileNameOnly { get; set; }

        public PathInputDialog(string prompt = "请输入路径:")
        {
            InitializeComponent();
            PromptTextBlock.Text = prompt;
            PathTextBox.Focus();
            this.KeyDown += PathInputDialog_KeyDown;
            this.MouseLeftButtonDown += (s, e) => { if (e.LeftButton == MouseButtonState.Pressed) this.DragMove(); };
        }

        private void PathInputDialog_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Enter)
            {
                OK_Click(null, null);
            }
            else if (e.Key == System.Windows.Input.Key.Escape)
            {
                Cancel_Click(null, null);
            }
        }

        public string PromptText
        {
            get => PromptTextBlock.Text;
            set => PromptTextBlock.Text = value;
        }

        protected override void OnContentRendered(System.EventArgs e)
        {
            base.OnContentRendered(e);

            // 设置初始文本
            if (!string.IsNullOrEmpty(InputText))
            {
                PathTextBox.Text = InputText;

                if (SelectFileNameOnly)
                {
                    // 只选中文件名部分（不包含扩展名）
                    var fileName = System.IO.Path.GetFileNameWithoutExtension(InputText);
                    if (!string.IsNullOrEmpty(fileName))
                    {
                        var startIndex = InputText.LastIndexOf(fileName);
                        if (startIndex >= 0)
                        {
                            PathTextBox.Select(startIndex, fileName.Length);
                        }
                        else
                        {
                            PathTextBox.SelectAll();
                        }
                    }
                    else
                    {
                        PathTextBox.SelectAll();
                    }
                }
                else
                {
                    PathTextBox.SelectAll();
                }
            }

            PathTextBox.Focus();
        }

        private void OK_Click(object sender, RoutedEventArgs e)
        {
            InputText = PathTextBox.Text;
            DialogResult = true;
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}

