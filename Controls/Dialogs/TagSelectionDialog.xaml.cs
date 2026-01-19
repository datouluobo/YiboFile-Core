using System;
using System.Windows;

namespace YiboFile.Controls.Dialogs
{
    public partial class TagSelectionDialog : Window
    {
        public int SelectedTagId { get; private set; }
        public string SelectedTagName { get; private set; }

        public TagSelectionDialog()
        {
            InitializeComponent();
            TagPanel.TagClicked += OnTagClicked;

            // 隐藏不需要的按钮或者调整布局（如果有必要）
            // TagBrowsePanel 目前设计比较通用，直接用即可
        }

        private void OnTagClicked(int tagId, string tagName)
        {
            SelectedTagId = tagId;
            SelectedTagName = tagName;
            DialogResult = true;
            Close();
        }
    }
}
