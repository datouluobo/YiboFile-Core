using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace YiboFile.ViewModels
{
    /// <summary>
    /// ViewModel 基类，实现 INotifyPropertyChanged
    /// </summary>
    public abstract class BaseViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        protected virtual bool SetProperty<T>(ref T field, T value, [CallerMemberName] string propertyName = null)
        {
            if (Equals(field, value))
                return false;

            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }

        /// <summary>
        /// 双轨数据验证垫圈：防止 WPF TextBox 实时绑定在输入过程发生光标跳转与篡改，同时保证底层配置合法
        /// </summary>
        /// <typeparam name="T">数值类型，通常为 double 或 int</typeparam>
        /// <param name="proxyField">用来承载界面前台乱七八糟乱敲或未完成字符串的虚拟字段</param>
        /// <param name="targetField">纯净合法的底层数值</param>
        /// <param name="inputString">正在输入的字符串参数</param>
        /// <param name="min">允许的最小值</param>
        /// <param name="max">允许的最大值</param>
        /// <param name="onSuccessPropertyUpdate">合法时对底层 Config 服务等发起的持久化更新行为</param>
        /// <param name="propertyName">触发此事件的公开属性名称</param>
        protected bool SetProtectedNumber(
            ref string proxyField,
            ref double targetField,
            string inputString,
            double min,
            double max,
            Action<double> onSuccessPropertyUpdate,
            [CallerMemberName] string propertyName = null)
        {
            // 1. 无脑接受一切输入，以避免拦截中间状态（比如刚删空、或者敲下了负号、或者是“8”暂时没满足下限）
            if (proxyField == inputString) return false;
            proxyField = inputString;
            OnPropertyChanged(propertyName);

            // 2. 解析为数字后，只在值已落入合法范围时才更新底层值（不 Clamp）
            //    超范围的中间输入（如 min=50 时输入 "1"）保留在代理中，等待 Enter/LostFocus 确认时 Clamp
            if (double.TryParse(inputString, out double parsedValue))
            {
                double effectiveMax = Math.Max(min, max);
                if (parsedValue >= min && parsedValue <= effectiveMax)
                {
                    onSuccessPropertyUpdate?.Invoke(parsedValue);
                }
                // 超范围值：不更新底层，保留代理，等确认时处理
            }
            return true;
        }

        protected bool SetProtectedNumber(
            ref string proxyField,
            ref int targetField,
            string inputString,
            int min,
            int max,
            Action<int> onSuccessPropertyUpdate,
            [CallerMemberName] string propertyName = null)
        {
            if (proxyField == inputString) return false;
            proxyField = inputString;
            OnPropertyChanged(propertyName);

            if (int.TryParse(inputString, out int parsedValue))
            {
                int effectiveMax = Math.Max(min, max);
                if (parsedValue >= min && parsedValue <= effectiveMax)
                {
                    onSuccessPropertyUpdate?.Invoke(parsedValue);
                }
                // 超范围值：不更新底层，保留代理，等确认时处理
            }
            return true;
        }

        /// <summary>
        /// 强制清除输入代理并刷新界面，常用处理 Enter 或 LostFocus
        /// </summary>
        public void InvalidateInputProxy(string inputPropertyName, Action forceResetAction = null)
        {
            // 通过执行 forceResetAction (通常是调用底层数值的 Setter)
            // 触发 Setter 中的清除逻辑并发送通知
            forceResetAction?.Invoke();
            OnPropertyChanged(inputPropertyName);
        }
    }
}





























