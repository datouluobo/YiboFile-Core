using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using YiboFile.ViewModels.Settings;

namespace YiboFile.Controls.Settings
{
    public partial class FileListSettingsPanel : UserControl, ISettingsPanel
    {
        public event EventHandler SettingsChanged;
        private FileListSettingsViewModel _viewModel;

        public FileListSettingsPanel()
        {
            InitializeComponent();
            var configService = (YiboFile.Services.Config.IConfigurationService)YiboFile.App.ServiceProvider.GetService(typeof(YiboFile.Services.Config.IConfigurationService));
            _viewModel = new FileListSettingsViewModel(configService);
            this.DataContext = _viewModel;

            _viewModel.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(FileListSettingsViewModel.ColTagsWidth) ||
                    e.PropertyName == nameof(FileListSettingsViewModel.ColNotesWidth))
                {
                    RefreshFileListColumns();
                }
                SettingsChanged?.Invoke(this, EventArgs.Empty);
            };
        }

        private void AdjustValue(double current, double delta, double min, double max, Action<double> setter)
        {
            setter(Math.Clamp(current + delta, min, max));
        }

        private void TagsWidthUp_Click(object sender, RoutedEventArgs e) => AdjustValue(_viewModel.ColTagsWidth, 5, 50, 500, v => _viewModel.ColTagsWidth = v);
        private void TagsWidthDown_Click(object sender, RoutedEventArgs e) => AdjustValue(_viewModel.ColTagsWidth, -5, 50, 500, v => _viewModel.ColTagsWidth = v);
        private void NotesWidthUp_Click(object sender, RoutedEventArgs e) => AdjustValue(_viewModel.ColNotesWidth, 5, 100, 800, v => _viewModel.ColNotesWidth = v);
        private void NotesWidthDown_Click(object sender, RoutedEventArgs e) => AdjustValue(_viewModel.ColNotesWidth, -5, 100, 800, v => _viewModel.ColNotesWidth = v);

        private void NumericTextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            e.Handled = !char.IsDigit(e.Text, 0);
        }

        private void RefreshFileListColumns()
        {
            try
            {
                if (System.Windows.Application.Current.MainWindow is System.Windows.Window mainWindow)
                {
                    var fileLists = FindVisualChildren<YiboFile.Controls.FileListControl>(mainWindow);
                    foreach (var list in fileLists)
                    {
                        list.LoadColumnWidths();
                    }
                }
            }
            catch { }
        }

        public void LoadSettings() => _viewModel?.LoadFromConfig();
        public void SaveSettings() { }

        private void NumericTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                CommitNumericInput(sender as TextBox);
                e.Handled = true;
            }
        }

        private void NumericTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            CommitNumericInput(sender as TextBox);
        }

        private void CommitNumericInput(TextBox textBox)
        {
            if (textBox == null) return;

            var binding = textBox.GetBindingExpression(TextBox.TextProperty);
            string propertyName = binding?.ParentBinding?.Path?.Path;
            if (string.IsNullOrEmpty(propertyName) || _viewModel == null) { this.Focus(); return; }

            // 先把最新的文本推送到代理
            binding.UpdateSource();

            // 从 TextBox 文本中解析值，通过底层属性 setter 的 Math.Clamp 完成最终校验
            if (double.TryParse(textBox.Text, out double value))
            {
                switch (propertyName)
                {
                    case "ColTagsWidthInput":
                        _viewModel.ColTagsWidth = value;   // setter 会 Math.Clamp
                        break;
                    case "ColNotesWidthInput":
                        _viewModel.ColNotesWidth = value;   // setter 会 Math.Clamp
                        break;
                }
            }
            // 无论如何都刷新代理，确保显示最终合法值
            _viewModel.InvalidateInputProxy(propertyName);

            this.Focus();
        }

        /// <summary>
        /// 聚焦时自动全选文本，便于直接输入替换
        /// </summary>
        private void NumericTextBox_GotFocus(object sender, RoutedEventArgs e)
        {
            if (sender is TextBox tb)
            {
                tb.Dispatcher.BeginInvoke(new Action(() => tb.SelectAll()),
                    System.Windows.Threading.DispatcherPriority.Input);
            }
        }

        /// <summary>
        /// 滚轮智能调节数值（步进 ±5）
        /// </summary>
        private void NumericTextBox_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (sender is TextBox tb && tb.IsFocused)
            {
                e.Handled = true;
                var binding = tb.GetBindingExpression(TextBox.TextProperty);
                string propName = binding?.ParentBinding?.Path?.Path;
                if (propName == null) return;

                double delta = e.Delta > 0 ? 5 : -5;
                switch (propName)
                {
                    case "ColTagsWidthInput":
                        AdjustValue(_viewModel.ColTagsWidth, delta, 50, 500, v => _viewModel.ColTagsWidth = v);
                        break;
                    case "ColNotesWidthInput":
                        AdjustValue(_viewModel.ColNotesWidth, delta, 100, 800, v => _viewModel.ColNotesWidth = v);
                        break;
                }
            }
        }

        private static System.Collections.Generic.IEnumerable<T> FindVisualChildren<T>(DependencyObject parent) where T : DependencyObject
        {
            if (parent != null)
            {
                for (int i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent); i++)
                {
                    DependencyObject child = System.Windows.Media.VisualTreeHelper.GetChild(parent, i);
                    if (child is T t)
                    {
                        yield return t;
                    }

                    foreach (T childOfChild in FindVisualChildren<T>(child))
                    {
                        yield return childOfChild;
                    }
                }
            }
        }
    }
}
