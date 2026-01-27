using System;
using System.Linq;
using System.Collections.Generic; // Added for IEnumerable
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Threading.Tasks;
using YiboFile.Services.Config;
using YiboFile.Services.FullTextSearch;
using YiboFile.ViewModels;
using YiboFile.ViewModels.Settings;

namespace YiboFile.Controls.Settings
{
    public partial class SearchSettingsPanel : UserControl, ISettingsPanel
    {
        // Event reserved for future use
#pragma warning disable CS0067
        public event EventHandler SettingsChanged;
#pragma warning restore CS0067

        private SearchSettingsViewModel _viewModel;

        private CheckBox _enableFtsCheckBox;
        private TextBlock _indexLocationText;
        private TextBlock _indexedCountText;
        private ProgressBar _indexProgressBar;
        private TextBlock _indexProgressText;
        private ListBox _scopeListBox;
        private Button _rebuildIndexButton;
        private StackPanel _extensionsPanel;

        // History config
        private TextBox _historyMaxCountBox;
        private CheckBox _autoExpandHistoryCheck;
        private Button _clearHistoryButton;

        public SearchSettingsPanel()
        {
            InitializeComponent();
            _viewModel = new SearchSettingsViewModel();
            this.DataContext = _viewModel;

            InitializeUI();
            // LoadSettings removed - ViewModel loads itself
        }

        private void InitializeUI()
        {
            var stackPanel = MainStackPanel;

            // 标题：全文搜索设置
            var ftsTitle = new TextBlock
            {
                Text = "全文搜索 (Full Text Search)",
                FontSize = 18,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 0, 0, 16)
            };
            stackPanel.Children.Add(ftsTitle);

            var infoText = new TextBlock
            {
                Text = "启用全文搜索后，您可以使用 'content:关键词' 或切换到全文模式搜索文档内容。",
                FontSize = 12,
                Margin = new Thickness(0, 0, 0, 24),
                TextWrapping = TextWrapping.Wrap
            };
            infoText.SetResourceReference(TextBlock.ForegroundProperty, "TextSecondaryBrush");
            stackPanel.Children.Add(infoText);

            // Everything 索引区域 (保持原样，因 EverythingHelper 是静态工具类，暂不完全移入 VM)
            var everythingGroup = new GroupBox
            {
                Header = "Everything 搜索引擎 (文件名)",
                Margin = new Thickness(0, 0, 0, 24),
                Padding = new Thickness(10),
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0)
            };
            var everythingStack = new StackPanel();

            everythingStack.Children.Add(CreateInfoRow("当前版本:", out var everythingVersionText));
            everythingVersionText.Text = YiboFile.Services.EverythingHelper.GetVersion();

            var rebuildEverythingButton = new Button
            {
                Content = "强制重建 Everything 索引",
                Padding = new Thickness(16, 8, 16, 8),
                HorizontalAlignment = HorizontalAlignment.Left,
                Margin = new Thickness(0, 10, 0, 10),
                Cursor = System.Windows.Input.Cursors.Hand,
                Style = (Style)Application.Current.Resources["ModernButtonStyle"]
            };
            rebuildEverythingButton.Click += (s, e) =>
            {
                try
                {
                    if (MessageBox.Show("确定要强制重建 Everything 索引吗？这可能会触发 UAC 提示。", "重建索引", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
                    {
                        YiboFile.Services.EverythingHelper.ForceRebuildIndex();
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"操作失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            };
            everythingStack.Children.Add(rebuildEverythingButton);

            var everythingHint = new TextBlock
            {
                Text = "Everything 通常会自动维护索引。仅在搜索结果不准确或遗漏文件时使用此功能。",
                FontSize = 12,
                TextWrapping = TextWrapping.Wrap
            };
            everythingHint.SetResourceReference(TextBlock.ForegroundProperty, "TextSecondaryBrush");
            everythingStack.Children.Add(everythingHint);

            everythingGroup.Content = everythingStack;
            stackPanel.Children.Add(everythingGroup);

            // --- 全文搜索设置 ---
            _enableFtsCheckBox = new CheckBox
            {
                Content = "启用全文索引与搜索",
                FontSize = 14,
                MinHeight = 32,
                Margin = new Thickness(0, 0, 0, 24)
            };
            _enableFtsCheckBox.SetBinding(CheckBox.IsCheckedProperty, new System.Windows.Data.Binding(nameof(SearchSettingsViewModel.IsEnableFullTextSearch)));
            stackPanel.Children.Add(_enableFtsCheckBox);

            // 索引状态区域
            var statusGroup = new GroupBox
            {
                Header = "全文索引状态",
                Margin = new Thickness(0, 0, 0, 24),
                Padding = new Thickness(10),
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0)
            };
            var statusStack = new StackPanel();

            // 索引位置
            var indexLocationGrid = new Grid { Margin = new Thickness(0, 0, 0, 8) };
            indexLocationGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            indexLocationGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            indexLocationGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var locLabel = new TextBlock
            {
                Text = "索引数据库位置:",
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(0, 0, 10, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(locLabel, 0);
            indexLocationGrid.Children.Add(locLabel);

            _indexLocationText = new TextBlock
            {
                TextWrapping = TextWrapping.Wrap,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 10, 0)
            };
            _indexLocationText.SetBinding(TextBlock.TextProperty, new System.Windows.Data.Binding(nameof(SearchSettingsViewModel.IndexLocation)));
            Grid.SetColumn(_indexLocationText, 1);
            indexLocationGrid.Children.Add(_indexLocationText);

            var changeLocButton = new Button
            {
                Content = "更改",
                Padding = new Thickness(8, 2, 8, 2),
                Style = (Style)Application.Current.Resources["ModernButtonStyle"]
            };
            changeLocButton.Click += ChangeIndexLocationButton_Click;
            Grid.SetColumn(changeLocButton, 2);
            indexLocationGrid.Children.Add(changeLocButton);

            statusStack.Children.Add(indexLocationGrid);

            // 已索引数量
            statusStack.Children.Add(CreateInfoRow("已索引文档数:", out _indexedCountText));
            _indexedCountText.SetBinding(TextBlock.TextProperty, new System.Windows.Data.Binding(nameof(SearchSettingsViewModel.IndexedFileCount)));

            // 索引进度
            var progressBox = new StackPanel { Margin = new Thickness(0, 5, 0, 0) };
            _indexProgressBar = new ProgressBar { Height = 6, Margin = new Thickness(0, 5, 0, 5), Maximum = 100 };
            _indexProgressBar.SetBinding(ProgressBar.ValueProperty, new System.Windows.Data.Binding(nameof(SearchSettingsViewModel.IndexingProgress)));
            // 可选: 绑定 IsIndeterminate 

            _indexProgressText = new TextBlock { FontSize = 12, Opacity = 0.7 };
            _indexProgressText.SetBinding(TextBlock.TextProperty, new System.Windows.Data.Binding(nameof(SearchSettingsViewModel.IndexingStatusText)));

            progressBox.Children.Add(_indexProgressBar);
            progressBox.Children.Add(_indexProgressText);
            statusStack.Children.Add(progressBox);

            statusGroup.Content = statusStack;
            stackPanel.Children.Add(statusGroup);

            // 搜索历史区域
            var historyGroup = new GroupBox
            {
                Header = "搜索历史与地址栏",
                Margin = new Thickness(0, 0, 0, 24),
                Padding = new Thickness(10),
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0)
            };
            var historyStack = new StackPanel();

            _autoExpandHistoryCheck = new CheckBox
            {
                Content = "地址栏获得焦点时自动展开历史记录",
                FontSize = 14,
                Margin = new Thickness(0, 0, 0, 10)
            };
            _autoExpandHistoryCheck.SetBinding(CheckBox.IsCheckedProperty, new System.Windows.Data.Binding(nameof(SearchSettingsViewModel.AutoExpandHistory)));
            historyStack.Children.Add(_autoExpandHistoryCheck);

            // 记录数量限制
            var countGrid = new Grid { Margin = new Thickness(0, 0, 0, 10) };
            countGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            countGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(100) });

            var countLabel = new TextBlock
            {
                Text = "保留历史记录数量:",
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 10, 0)
            };
            Grid.SetColumn(countLabel, 0);
            countGrid.Children.Add(countLabel);

            _historyMaxCountBox = new TextBox
            {
                VerticalContentAlignment = VerticalAlignment.Center,
                Padding = new Thickness(4)
            };
            _historyMaxCountBox.SetBinding(TextBox.TextProperty, new System.Windows.Data.Binding(nameof(SearchSettingsViewModel.HistoryMaxCount)));
            Grid.SetColumn(_historyMaxCountBox, 1);
            countGrid.Children.Add(_historyMaxCountBox);

            historyStack.Children.Add(countGrid);

            // 清除历史按钮
            _clearHistoryButton = new Button
            {
                Content = "清除所有历史记录",
                Padding = new Thickness(12, 6, 12, 6),
                HorizontalAlignment = HorizontalAlignment.Left,
                Style = (Style)Application.Current.Resources["ModernButtonStyle"]
            };
            _clearHistoryButton.Click += (s, e) =>
            {
                if (MessageBox.Show("确定要删除所有本地搜索和路径历史记录吗？", "清除历史", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                {
                    _viewModel.ClearHistoryCommand.Execute(null);
                    MessageBox.Show("历史记录已清除。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            };
            historyStack.Children.Add(_clearHistoryButton);

            historyGroup.Content = historyStack;
            stackPanel.Children.Add(historyGroup);

            // 操作区域
            var actionGroup = new GroupBox
            {
                Header = "维护操作",
                Margin = new Thickness(0, 0, 0, 24),
                Padding = new Thickness(10),
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0)
            };
            var actionStack = new StackPanel();

            var scopeLabel = new TextBlock { Text = "索引范围 (为空时默认扫描所有库)", FontWeight = FontWeights.Bold, Margin = new Thickness(0, 0, 0, 5) };
            actionStack.Children.Add(scopeLabel);

            _scopeListBox = new ListBox
            {
                Height = 100,
                Margin = new Thickness(0, 0, 0, 5),
                SelectionMode = SelectionMode.Extended
            };
            _scopeListBox.SetBinding(ListBox.ItemsSourceProperty, new System.Windows.Data.Binding(nameof(SearchSettingsViewModel.IndexScopes)));
            actionStack.Children.Add(_scopeListBox);

            var scopeButtons = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 15) };
            var addScopeButton = new Button
            {
                Content = "添加目录",
                Padding = new Thickness(10, 4, 10, 4),
                Margin = new Thickness(0, 0, 10, 0),
                Style = (Style)Application.Current.Resources["ModernButtonStyle"]
            };
            addScopeButton.Click += AddScopeButton_Click;

            var removeScopeButton = new Button
            {
                Content = "移除选中",
                Padding = new Thickness(10, 4, 10, 4),
                Style = (Style)Application.Current.Resources["ModernButtonStyle"]
            };
            removeScopeButton.Click += RemoveScopeButton_Click;

            scopeButtons.Children.Add(addScopeButton);
            scopeButtons.Children.Add(removeScopeButton);
            actionStack.Children.Add(scopeButtons);

            _rebuildIndexButton = new Button
            {
                Content = "重建索引",
                Padding = new Thickness(16, 8, 16, 8),
                HorizontalAlignment = HorizontalAlignment.Left,
                Margin = new Thickness(0, 0, 0, 10),
                Style = (Style)Application.Current.Resources["ModernButtonStyle"]
            };
            // 绑定 IsEnabled 以防止重复点击 (ViewModel 中设置 IsIndexing)
            // 简单起见，暂不绑定 IsEnabled，点击事件里判断

            _rebuildIndexButton.Click += RebuildIndexButton_Click;
            actionStack.Children.Add(_rebuildIndexButton);

            var rebuildHint = new TextBlock
            {
                Text = "重建索引会清空当前所有索引数据并重新扫描。对于大型文档库可能需要较长时间。后台任务将以低优先级运行。",
                FontSize = 12,
                TextWrapping = TextWrapping.Wrap
            };
            rebuildHint.SetResourceReference(TextBlock.ForegroundProperty, "TextSecondaryBrush");
            actionStack.Children.Add(rebuildHint);

            actionGroup.Content = actionStack;
            stackPanel.Children.Add(actionGroup);

            // 支持的文件格式
            var formatsGroup = new Expander
            {
                Header = "支持的文件格式",
                IsExpanded = false,
                Margin = new Thickness(0, 0, 0, 24)
            };

            _extensionsPanel = new StackPanel { Margin = new Thickness(10) };
            LoadSupportedExtensions();
            formatsGroup.Content = _extensionsPanel;

            stackPanel.Children.Add(formatsGroup);
        }

        private void AddScopeButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new System.Windows.Forms.FolderBrowserDialog();
            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                string path = dialog.SelectedPath;
                if (!_viewModel.IndexScopes.Contains(path))
                {
                    _viewModel.IndexScopes.Add(path);
                    _viewModel.UpdateIndexScopes(_viewModel.IndexScopes);

                    // Trigger indexing logic
                    YiboFile.Services.FullTextSearch.FullTextSearchService.Instance.StartBackgroundIndexing();
                }
            }
        }

        private void ChangeIndexLocationButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                Title = "选择索引数据库位置",
                Filter = "SQLite 数据库 (*.db)|*.db",
                FileName = "fts_index.db",
                DefaultExt = ".db"
            };

            if (dialog.ShowDialog() == true)
            {
                _viewModel.UpdateIndexLocation(dialog.FileName);
                MessageBox.Show("索引位置已更新。请重启应用以生效。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void RemoveScopeButton_Click(object sender, RoutedEventArgs e)
        {
            var selectedItems = _scopeListBox.SelectedItems.Cast<string>().ToList();
            if (selectedItems.Count > 0)
            {
                foreach (var item in selectedItems)
                {
                    _viewModel.IndexScopes.Remove(item);
                }
                _viewModel.UpdateIndexScopes(_viewModel.IndexScopes);
            }
        }

        private void RebuildIndexButton_Click(object sender, RoutedEventArgs e)
        {
            if (_viewModel.IsIndexing) return;

            if (MessageBox.Show("确信要清空并重建索引吗？此操作不可撤销。", "重建索引", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
                return;

            _viewModel.RebuildIndexCommand.Execute(null);

            MessageBox.Show("索引已清空。后台任务将自动开始重新扫描您的文件（请确保应用保持运行）。", "完成", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private Grid CreateInfoRow(string label, out TextBlock valueBlock)
        {
            var grid = new Grid { Margin = new Thickness(0, 0, 0, 8) };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var labelBlock = new TextBlock
            {
                Text = label,
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(0, 0, 10, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(labelBlock, 0);
            grid.Children.Add(labelBlock);

            valueBlock = new TextBlock
            {
                Text = "...",
                TextWrapping = TextWrapping.Wrap,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(valueBlock, 1);
            grid.Children.Add(valueBlock);

            return grid;
        }

        private void LoadSupportedExtensions()
        {
            var formats = new[]
            {
                new { Ext = ".txt", Desc = "纯文本文档" },
                new { Ext = ".md", Desc = "Markdown 文档" },
                new { Ext = ".pdf", Desc = "PDF 文档 (PdfPig)" },
                new { Ext = ".docx", Desc = "Word 文档 (OpenXML)" },
                new { Ext = ".doc", Desc = "Word 97-2003 (NPOI)" },
                new { Ext = ".xlsx", Desc = "Excel 工作簿 (OpenXML)" },
                new { Ext = ".xls", Desc = "Excel 97-2003 (NPOI)" },
                new { Ext = ".cpp/.c/.h/.cs/.java/.py/.js/.ts/.html/.css/.xml/.json/.sql", Desc = "代码源文件" }
            };

            foreach (var fmt in formats)
            {
                var row = new Grid { Margin = new Thickness(0, 4, 0, 4) };
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(120) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

                var extBlock = new TextBlock { Text = fmt.Ext, FontWeight = FontWeights.Bold };
                Grid.SetColumn(extBlock, 0);
                row.Children.Add(extBlock);

                var descBlock = new TextBlock { Text = fmt.Desc };
                Grid.SetColumn(descBlock, 1);
                row.Children.Add(descBlock);

                _extensionsPanel.Children.Add(row);
            }
        }

        public void LoadSettings()
        {
            _viewModel?.LoadFromConfig();
        }

        public void SaveSettings()
        {
            // Auto-saved by bindings
        }
    }
}


