using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using System.Windows.Threading;

namespace YiboFile.Helpers
{
    public static class ClipboardHelper
    {
        /// <summary>
        /// 同步设置剪贴板文本（仅供内部调用，不建议直接在 UI 线程使用）
        /// </summary>
        public static bool SetText(string text, int retryCount = 5, int delayMs = 10)
        {
            if (string.IsNullOrEmpty(text)) return false;

            for (int i = 0; i < retryCount; i++)
            {
                try
                {
                    // copy=false: 不做 OLE FlushClipboard，速度极快且不会卡顿
                    // 数据在应用退出前始终可用，退出后失效（对"复制路径"场景完全可接受）
                    Clipboard.SetDataObject(text, false);
                    return true;
                }
                catch (COMException ex)
                {
                    // 0x800401D0 (CLIPBRD_E_CANT_OPEN) — 剪贴板被其他进程占用
                    if (ex.ErrorCode != -2147221040) throw;
                    Thread.Sleep(delayMs);
                }
                catch (Exception)
                {
                    Thread.Sleep(delayMs);
                }
            }

            return false;
        }

        /// <summary>
        /// 异步设置剪贴板文本（通过 UI 线程 Dispatcher 异步执行，零卡顿）
        /// 
        /// 核心改进：
        /// 1. 使用 Dispatcher.BeginInvoke 在 UI 线程异步执行，确保 STA 消息泵正常工作
        /// 2. 使用 copy=false 避免耗时的 OLE FlushClipboard 操作
        /// 3. 优先级设为 Background，不抢占 UI 渲染和输入处理
        /// </summary>
        public static void SetTextAsync(string text, Action<bool> completion = null)
        {
            if (string.IsNullOrEmpty(text))
            {
                completion?.Invoke(false);
                return;
            }

            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher == null)
            {
                completion?.Invoke(false);
                return;
            }

            // 在 UI 线程以 Background 优先级异步执行
            // Background 优先级确保不会阻塞当前的 UI 交互（如菜单关闭动画）
            dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() =>
            {
                bool success = false;
                try
                {
                    // 快速尝试，最多 3 次，每次间隔 5ms（总计最多 ~15ms）
                    for (int i = 0; i < 3; i++)
                    {
                        try
                        {
                            Clipboard.SetDataObject(text, false);
                            success = true;
                            break;
                        }
                        catch (COMException ex)
                        {
                            if (ex.ErrorCode != -2147221040) throw;
                            // 短暂等待后重试（在 Background 优先级下不会影响 UI 响应性）
                            Thread.Sleep(5);
                        }
                    }
                }
                catch
                {
                    success = false;
                }

                completion?.Invoke(success);
            }));
        }
    }
}
