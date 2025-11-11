using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;

namespace OoiMRR
{
    /// <summary>
    /// TagSelectionDialog.xaml 的交互逻辑
    /// </summary>
    public partial class TagSelectionDialog : Window
    {
        public List<int> SelectedTagIds { get; private set; } = new List<int>();
        private readonly HashSet<int> _preselectedTagIds;

        public TagSelectionDialog(IEnumerable<int> preselectedTagIds = null)
        {
            InitializeComponent();
            _preselectedTagIds = preselectedTagIds != null
                ? new HashSet<int>(preselectedTagIds)
                : new HashSet<int>();
            LoadTags();
            this.KeyDown += TagSelectionDialog_KeyDown;
        }

        private void TagSelectionDialog_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
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

        private void LoadTags()
        {
            var tags = DatabaseManager.GetAllTags();
            TagsListBox.ItemsSource = tags;

            if (_preselectedTagIds.Count > 0)
            {
                this.Dispatcher.BeginInvoke(new Action(() =>
                {
                    foreach (Tag tag in tags)
                    {
                        if (_preselectedTagIds.Contains(tag.Id))
                        {
                            TagsListBox.SelectedItems.Add(tag);
                        }
                    }
                }), System.Windows.Threading.DispatcherPriority.Loaded);
            }
        }

        private void OK_Click(object sender, RoutedEventArgs e)
        {
            SelectedTagIds.Clear();
            foreach (Tag selectedTag in TagsListBox.SelectedItems)
            {
                SelectedTagIds.Add(selectedTag.Id);
            }
            
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
