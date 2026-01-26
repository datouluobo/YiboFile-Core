using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace YiboFile.Windows
{
    public partial class ColumnChooserWindow : Window
    {
        private readonly string _initialCsv;
        private readonly string[] _allColumns = new[] { "Name", "Size", "Type", "ModifiedDate", "CreatedTime", "Tags", "Notes" };

        public ColumnChooserWindow(string currentCsv)
        {
            InitializeComponent();
            _initialCsv = currentCsv ?? "";
            Loaded += ColumnChooserWindow_Loaded;
        }

        private void ColumnChooserWindow_Loaded(object sender, RoutedEventArgs e)
        {
            var selected = new HashSet<string>(
                (_initialCsv ?? "").Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries),
                StringComparer.OrdinalIgnoreCase);

            foreach (var col in _allColumns)
            {
                var cb = new CheckBox
                {
                    Content = ToDisplayName(col),
                    Tag = col,
                    IsChecked = selected.Count == 0 ? true : selected.Contains(col),
                    Margin = new Thickness(2, 4, 2, 4)
                };
                ColumnsPanel.Children.Add(cb);
            }
        }

        private string ToDisplayName(string tag)
        {
            switch (tag)
            {
                case "Name": return "名称";
                case "Size": return "大小";
                case "Type": return "类型";
                case "ModifiedDate": return "修改日期";
                case "CreatedTime": return "创建";
                case "Tags": return "标签";
                case "Notes": return "备注";
                default: return tag;
            }
        }

        public string GetSelectedColumnsCsv()
        {
            var list = new List<string>();
            foreach (var child in ColumnsPanel.Children)
            {
                if (child is CheckBox cb && cb.Tag is string tag && cb.IsChecked == true)
                {
                    list.Add(tag);
                }
            }
            // 按预定义顺序输出
            var ordered = _allColumns.Where(c => list.Contains(c, StringComparer.OrdinalIgnoreCase));
            return string.Join(",", ordered);
        }

        private void BtnOK_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = true;
            this.Close();
        }

        private void BtnAll_Click(object sender, RoutedEventArgs e)
        {
            foreach (var child in ColumnsPanel.Children)
                if (child is CheckBox cb) cb.IsChecked = true;
        }

        private void BtnNone_Click(object sender, RoutedEventArgs e)
        {
            foreach (var child in ColumnsPanel.Children)
                if (child is CheckBox cb) cb.IsChecked = false;
        }
    }
}



