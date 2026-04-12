using System;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using YiboFile.Services.UI;

namespace YiboFile.Windows
{
    public partial class DateFilterWindow : Window
    {
        public DateTime? SelectedStartDate { get; private set; }
        public DateTime? SelectedEndDate { get; private set; }
        public bool SearchCreatedDate { get; private set; }
        public bool SearchModifiedDate { get; private set; }
        public bool SearchAllDrives { get; private set; }

        public DateFilterWindow()
        {
            InitializeComponent();

            // 初始化日期
            SingleDatePicker.SelectedDate = DateTime.Today;
            StartDatePicker.SelectedDate = DateTime.Today;
            EndDatePicker.SelectedDate = DateTime.Today;

            // 初始化单选按钮状态
            UpdateDatePickerState();
        }

        private void DateTypeRadio_Checked(object sender, RoutedEventArgs e)
        {
            UpdateDatePickerState();
        }

        private void UpdateDatePickerState()
        {
            bool isSingleDate = SingleDateRadio.IsChecked == true;
            SingleDatePicker.IsEnabled = isSingleDate;
            StartDatePicker.IsEnabled = !isSingleDate;
            EndDatePicker.IsEnabled = !isSingleDate;
        }

        private void SearchButton_Click(object sender, RoutedEventArgs e)
        {
            // 验证日期选择
            var dialogService = App.ServiceProvider?.GetService<IDialogService>();

            if (SingleDateRadio.IsChecked == true)
            {
                if (!SingleDatePicker.SelectedDate.HasValue)
                {
                    dialogService?.ShowInfo("请选择日期");
                    return;
                }
                SelectedStartDate = SingleDatePicker.SelectedDate.Value.Date;
                SelectedEndDate = SingleDatePicker.SelectedDate.Value.Date;
            }
            else
            {
                if (!StartDatePicker.SelectedDate.HasValue || !EndDatePicker.SelectedDate.HasValue)
                {
                    dialogService?.ShowInfo("请选择完整的日期范围");
                    return;
                }
 
                SelectedStartDate = StartDatePicker.SelectedDate.Value.Date;
                SelectedEndDate = EndDatePicker.SelectedDate.Value.Date;
 
                if (SelectedStartDate > SelectedEndDate)
                {
                    dialogService?.ShowWarning("开始日期不能晚于结束日期");
                    return;
                }
            }
 
            // 验证至少选择一个搜索类型
            if (SearchCreatedDateCheckBox.IsChecked != true && SearchModifiedDateCheckBox.IsChecked != true)
            {
                dialogService?.ShowInfo("请至少选择一种搜索类型（创建日期或修改日期）");
                return;
            }

            SearchCreatedDate = SearchCreatedDateCheckBox.IsChecked.Value;
            SearchModifiedDate = SearchModifiedDateCheckBox.IsChecked.Value;
            SearchAllDrives = AllDrivesRadio.IsChecked == true;

            DialogResult = true;
            Close();
        }
    }
}










