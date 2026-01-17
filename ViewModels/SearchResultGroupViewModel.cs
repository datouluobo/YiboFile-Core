using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using YiboFile.Models;
using YiboFile.Services.Search;

namespace YiboFile.ViewModels
{
    /// <summary>
    /// 搜索结果分组视图模型
    /// </summary>
    public class SearchResultGroupViewModel : INotifyPropertyChanged
    {
        private bool _isExpanded = true;

        public SearchResultType GroupType { get; set; }
        public string GroupName { get; set; }
        public ObservableCollection<FileSystemItem> Items { get; set; }
        public int ItemCount => Items?.Count ?? 0;

        public bool IsExpanded
        {
            get => _isExpanded;
            set
            {
                _isExpanded = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(ExpandIcon));
            }
        }

        public string ExpandIcon => IsExpanded ? "−" : "+";

        /// <summary>
        /// 不同类型使用不同的背景色
        /// </summary>
        public string GroupBackground
        {
            get
            {
                return GroupType switch
                {
                    SearchResultType.Notes => "#FFF3E0",      // 橙色（备注）
                    SearchResultType.Folder => "#E8F5E9",     // 绿色（文件夹）
                    SearchResultType.File => "#E3F2FD",       // 蓝色（文件）
                    SearchResultType.Tag => "#F3E5F5",        // 紫色（标签）
                    SearchResultType.Date => "#E1F5FE",      // 浅蓝色（日期）
                    _ => "#F5F5F5"                            // 灰色（其他）
                };
            }
        }

        public string GroupBorderBrush
        {
            get
            {
                return GroupType switch
                {
                    SearchResultType.Notes => "#FFB74D",
                    SearchResultType.Folder => "#81C784",
                    SearchResultType.File => "#64B5F6",
                    SearchResultType.Tag => "#BA68C8",
                    SearchResultType.Date => "#4FC3F7",
                    _ => "#E0E0E0"
                };
            }
        }

        public string ItemBackground => "#FFFFFF";
        public string ItemBorderBrush => "#E0E0E0";

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}

























