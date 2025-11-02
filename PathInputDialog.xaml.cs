using System.Windows;

namespace OoiMRR
{
    /// <summary>
    /// PathInputDialog.xaml 的交互逻辑
    /// </summary>
    public partial class PathInputDialog : Window
    {
        public string InputText { get; set; }

        public PathInputDialog(string prompt = "请输入路径:")
        {
            InitializeComponent();
            PromptTextBlock.Text = prompt;
            PathTextBox.Focus();
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
                PathTextBox.SelectAll();
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
