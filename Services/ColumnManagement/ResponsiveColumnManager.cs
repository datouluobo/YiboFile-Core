using System;
using System.Windows.Threading;

namespace YiboFile.Services.ColumnManagement
{
    /// <summary>
    /// 列可见性变化事件参数
    /// </summary>
    public class ColumnVisibilityChangedEventArgs : EventArgs
    {
        public bool IsTypeColumnVisible { get; set; }
        public bool IsSizeColumnVisible { get; set; }
        public bool IsModifiedDateVisible { get; set; }
        public bool IsCreatedTimeVisible { get; set; }
    }

    /// <summary>
    /// 响应式列宽度管理器（优化版）
    /// 根据可用宽度动态调整列显示和名称列宽度
    /// </summary>
    public class ResponsiveColumnManager
    {
        // 列宽度配置常量
        private const double NameColumnMinWidth = 120;
        private const double TypeColumnWidth = 60;
        private const double SizeColumnWidth = 90;
        private const double ModifiedDateColumnWidth = 100;
        private const double CreatedTimeColumnWidth = 60;

        // 当前列可见性状态
        public bool IsTypeColumnVisible { get; private set; } = true;
        public bool IsSizeColumnVisible { get; private set; } = true;
        public bool IsModifiedDateVisible { get; private set; } = true;
        public bool IsCreatedTimeVisible { get; private set; } = true;

        // 当前计算的名称列宽度
        public double NameColumnWidth { get; private set; } = 250;

        // 事件：当列可见性改变时触发
        public event EventHandler<ColumnVisibilityChangedEventArgs> ColumnVisibilityChanged;

        public ResponsiveColumnManager()
        {
            // 不再使用防抖定时器，直接应用布局
        }

        /// <summary>
        /// 计算并应用响应式布局（立即应用）
        /// </summary>
        public void ApplyResponsiveLayout(double availableWidth, double tagsWidth, double notesWidth)
        {
            if (availableWidth <= 0) return;

            // 立即应用，不使用防抖
            ApplyResponsiveLayoutInternal(availableWidth, tagsWidth, notesWidth);
        }

        private void ApplyResponsiveLayoutInternal(double availableWidth, double tagsWidth, double notesWidth)
        {
            // 计算可用宽度（考虑滚动条）
            // 减去较小的值，让列宽度更大以填满空间
            double scrollBarWidth = System.Windows.SystemParameters.VerticalScrollBarWidth;
            double usableWidth = availableWidth - scrollBarWidth + 10; // 增加10px补偿

            // 必需宽度：标签 + 备注（始终显示）
            double requiredWidth = tagsWidth + notesWidth;

            // 记录之前的状态
            bool prevTypeVisible = IsTypeColumnVisible;
            bool prevSizeVisible = IsSizeColumnVisible;
            bool prevModifiedVisible = IsModifiedDateVisible;
            bool prevCreatedVisible = IsCreatedTimeVisible;

            // 计算固定列总宽度并决定显示哪些列
            double fixedColumnsWidth = 0;

            // 尝试显示所有列
            double allFixedWidth = TypeColumnWidth + SizeColumnWidth + ModifiedDateColumnWidth + CreatedTimeColumnWidth;
            double totalWithAllColumns = requiredWidth + allFixedWidth + NameColumnMinWidth;

            if (usableWidth >= totalWithAllColumns)
            {
                // 空间充足，显示所有列
                IsTypeColumnVisible = true;
                IsSizeColumnVisible = true;
                IsModifiedDateVisible = true;
                IsCreatedTimeVisible = true;
                fixedColumnsWidth = allFixedWidth;
            }
            else
            {
                // 空间不足，开始隐藏列（按优先级：创建时间 -> 修改日期 -> 大小 -> 类型）

                // 尝试隐藏创建时间
                double withoutCreatedTime = TypeColumnWidth + SizeColumnWidth + ModifiedDateColumnWidth;
                if (usableWidth >= requiredWidth + withoutCreatedTime + NameColumnMinWidth)
                {
                    IsTypeColumnVisible = true;
                    IsSizeColumnVisible = true;
                    IsModifiedDateVisible = true;
                    IsCreatedTimeVisible = false;
                    fixedColumnsWidth = withoutCreatedTime;
                }
                // 尝试隐藏修改日期
                else if (usableWidth >= requiredWidth + TypeColumnWidth + SizeColumnWidth + NameColumnMinWidth)
                {
                    IsTypeColumnVisible = true;
                    IsSizeColumnVisible = true;
                    IsModifiedDateVisible = false;
                    IsCreatedTimeVisible = false;
                    fixedColumnsWidth = TypeColumnWidth + SizeColumnWidth;
                }
                // 尝试隐藏大小
                else if (usableWidth >= requiredWidth + TypeColumnWidth + NameColumnMinWidth)
                {
                    IsTypeColumnVisible = true;
                    IsSizeColumnVisible = false;
                    IsModifiedDateVisible = false;
                    IsCreatedTimeVisible = false;
                    fixedColumnsWidth = TypeColumnWidth;
                }
                // 隐藏类型列
                else
                {
                    IsTypeColumnVisible = false;
                    IsSizeColumnVisible = false;
                    IsModifiedDateVisible = false;
                    IsCreatedTimeVisible = false;
                    fixedColumnsWidth = 0;
                }
            }

            // 计算名称列宽度
            double calculatedNameWidth = usableWidth - requiredWidth - fixedColumnsWidth;
            NameColumnWidth = Math.Max(NameColumnMinWidth, calculatedNameWidth);

            // 检查是否有变化
            bool hasChanged = prevTypeVisible != IsTypeColumnVisible ||
                            prevSizeVisible != IsSizeColumnVisible ||
                            prevModifiedVisible != IsModifiedDateVisible ||
                            prevCreatedVisible != IsCreatedTimeVisible;

            // 如果有变化，触发事件
            if (hasChanged)
            {
                ColumnVisibilityChanged?.Invoke(this, new ColumnVisibilityChangedEventArgs
                {
                    IsTypeColumnVisible = IsTypeColumnVisible,
                    IsSizeColumnVisible = IsSizeColumnVisible,
                    IsModifiedDateVisible = IsModifiedDateVisible,
                    IsCreatedTimeVisible = IsCreatedTimeVisible
                });
            }
        }
    }
}

