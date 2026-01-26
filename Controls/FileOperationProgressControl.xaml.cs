using System;
using System.Threading;
using System.Windows;
using System.Windows.Controls;

namespace YiboFile.Controls
{
    /// <summary>
    /// 文件操作进度控件
    /// 显示复制/移动/删除等操作的进度
    /// </summary>
    public partial class FileOperationProgressControl : UserControl
    {
        private CancellationTokenSource _cancellationTokenSource;

        public event EventHandler CancelRequested;

        public FileOperationProgressControl()
        {
            InitializeComponent();
        }

        /// <summary>
        /// 设置取消令牌源
        /// </summary>
        public CancellationTokenSource CancellationTokenSource
        {
            get => _cancellationTokenSource;
            set => _cancellationTokenSource = value;
        }

        /// <summary>
        /// 设置操作标题
        /// </summary>
        public void SetTitle(string title)
        {
            TitleText.Text = title;
        }

        /// <summary>
        /// 更新当前处理的文件名
        /// </summary>
        public void SetCurrentFile(string fileName)
        {
            CurrentFileText.Text = fileName;
        }

        /// <summary>
        /// 更新进度
        /// </summary>
        /// <param name="current">当前完成数量</param>
        /// <param name="total">总数量</param>
        public void SetProgress(int current, int total)
        {
            if (total > 0)
            {
                ProgressBar.Value = (double)current / total * 100;
                ProgressText.Text = $"{current} / {total} 项";
            }
            else
            {
                ProgressBar.Value = 0;
                ProgressText.Text = "准备中...";
            }
        }

        /// <summary>
        /// 设置为不确定进度模式
        /// </summary>
        public void SetIndeterminate(bool isIndeterminate)
        {
            ProgressBar.IsIndeterminate = isIndeterminate;
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            _cancellationTokenSource?.Cancel();
            CancelRequested?.Invoke(this, EventArgs.Empty);
            CancelButton.IsEnabled = false;
            CancelButton.Content = "正在取消...";
        }
    }
}

