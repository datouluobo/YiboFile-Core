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

            // 2. 只有当能解析成数字时，且范围受控，才向底层派发真正的数据变更
            if (double.TryParse(inputString, out double parsedValue))
            {
                double safeValue = Math.Clamp(parsedValue, min, Math.Max(min, max));

                if (!Equals(targetField, safeValue))
                {
                    targetField = safeValue;
                    onSuccessPropertyUpdate?.Invoke(safeValue);
                }
                else if (parsedValue > Math.Max(min, max))
                {
                    onSuccessPropertyUpdate?.Invoke(safeValue);
                }
                else if (parsedValue < min && inputString.Length > 0 && 
                         !inputString.StartsWith("-") && 
                         inputString.Length >= min.ToString().Length)
                {
                    // Force refresh UI if they typed enough digits but it's still too small
                    // This fixes the "blocked" feel when typing a number smaller than min
                    onSuccessPropertyUpdate?.Invoke(safeValue);
                }
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
                int safeValue = Math.Clamp(parsedValue, min, Math.Max(min, max));

                if (!Equals(targetField, safeValue))
                {
                    targetField = safeValue;
                    onSuccessPropertyUpdate?.Invoke(safeValue);
                }
                else if (parsedValue > Math.Max(min, max))
                {
                    onSuccessPropertyUpdate?.Invoke(safeValue);
                }
                else if (parsedValue < min && inputString.Length > 0 && 
                         !inputString.StartsWith("-") && 
                         inputString.Length >= min.ToString().Length)
                {
                    onSuccessPropertyUpdate?.Invoke(safeValue);
                }
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





























