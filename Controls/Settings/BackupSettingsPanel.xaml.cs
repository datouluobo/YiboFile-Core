using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using YiboFile.Services.Config;
using System.Diagnostics;

namespace YiboFile.Controls.Settings
{
    public partial class BackupSettingsPanel : UserControl, ISettingsPanel
    {
        private AppConfig _config;
        private bool _isDirty = false;

        public event EventHandler SettingsChanged;

        public BackupSettingsPanel()
        {
            InitializeComponent();
            LoadSettings();
        }

        public void LoadSettings()
        {
            _config = ConfigurationService.Instance.GetSnapshot();

            BackupPathTextBox.Text = _config.BackupDirectory;

            // Set Combobox
            foreach (ComboBoxItem item in RetentionComboBox.Items)
            {
                if (int.TryParse(item.Tag.ToString(), out int days))
                {
                    if (days == _config.BackupRetentionDays)
                    {
                        RetentionComboBox.SelectedItem = item;
                        break;
                    }
                }
            }
            // If not found (custom value?), select closest or add? For now default to 30 if not found
            if (RetentionComboBox.SelectedItem == null)
                RetentionComboBox.SelectedIndex = 2; // 30 days default index

            _isDirty = false;
        }

        public void SaveSettings()
        {
            if (!_isDirty) return;

            ConfigurationService.Instance.Update(cfg =>
            {
                cfg.BackupDirectory = BackupPathTextBox.Text;

                if (RetentionComboBox.SelectedItem is ComboBoxItem item &&
                    int.TryParse(item.Tag.ToString(), out int days))
                {
                    cfg.BackupRetentionDays = days;
                }
            });

            _isDirty = false;
        }

        private void BrowseButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new System.Windows.Forms.FolderBrowserDialog();
            dialog.Description = "选择备份存储目录";
            dialog.UseDescriptionForTitle = true;
            dialog.ShowNewFolderButton = true;

            if (!string.IsNullOrEmpty(BackupPathTextBox.Text))
                dialog.SelectedPath = BackupPathTextBox.Text;

            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                BackupPathTextBox.Text = dialog.SelectedPath;
                _isDirty = true;
                SettingsChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        private void OpenFolderButton_Click(object sender, RoutedEventArgs e)
        {
            string path = BackupPathTextBox.Text;
            if (string.IsNullOrEmpty(path))
            {
                path = System.IO.Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "YiboFile", "Backup");
            }

            if (!System.IO.Directory.Exists(path))
            {
                try { System.IO.Directory.CreateDirectory(path); } catch { }
            }

            try
            {
                Process.Start("explorer.exe", path);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"无法打开文件夹: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void RetentionComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            _isDirty = true;
            SettingsChanged?.Invoke(this, EventArgs.Empty);
        }

        private void CleanButton_Click(object sender, RoutedEventArgs e)
        {
            // TODO: Call BackupService.CleanOldBackupsAsync
            MessageBox.Show("清理功能将在下一阶段实现。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void ManageButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new YiboFile.Dialogs.BackupBrowserDialog();
            dialog.Owner = Window.GetWindow(this);
            dialog.ShowDialog();
        }
    }
}
