using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using YiboFile.Interop.Native;
using YiboFile.ViewModels.Messaging;
using YiboFile.ViewModels.Messaging.Messages;
using Microsoft.Extensions.DependencyInjection;

namespace YiboFile.Services.Hardware
{
    /// <summary>
    /// 硬件监控服务接口
    /// </summary>
    public interface IHardwareMonitorService
    {
        /// <summary>
        /// 初始化服务并关联窗口句柄
        /// </summary>
        void Initialize(IntPtr windowHandle);

        /// <summary>
        /// 消息钩子回调
        /// </summary>
        IntPtr HookProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled);

        /// <summary>
        /// 弹出驱动器
        /// </summary>
        /// <param name="drivePath">驱动器路径,如 "E:\"</param>
        bool EjectDrive(string drivePath);
    }

    /// <summary>
    /// 硬件监控服务实现
    /// 利用 Win32 消息钩子捕获热插拔事件，并提供 Native 弹出功能
    /// </summary>
    public class HardwareMonitorService : IHardwareMonitorService
    {
        private readonly IMessageBus _messageBus;
        private readonly System.Windows.Threading.Dispatcher _dispatcher;
        private IntPtr _windowHandle;

        public HardwareMonitorService(IMessageBus messageBus = null)
        {
            _messageBus = messageBus ?? App.ServiceProvider?.GetService<IMessageBus>();
            _dispatcher = System.Windows.Application.Current?.Dispatcher ?? System.Windows.Threading.Dispatcher.CurrentDispatcher;

            // 订阅弹出请求消息
            _messageBus?.Subscribe<RequestEjectDriveMessage>(msg =>
            {
                bool success = EjectDrive(msg.DrivePath);
                if (!success)
                {
                    _dispatcher.Invoke(() =>
                    {
                        var dialogService = App.ServiceProvider?.GetService<YiboFile.Services.UI.IDialogService>();
                        dialogService?.ShowWarning($"无法弹出驱动器 {msg.DrivePath}。请确保没有程序正在使用该驱动器。");
                    });
                }
            });
        }

        public void Initialize(IntPtr windowHandle)
        {
            _windowHandle = windowHandle;
        }

        /// <summary>
        /// 处理 Windows 消息通知
        /// </summary>
        public IntPtr HookProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == DeviceInterop.WM_DEVICECHANGE)
            {
                int eventType = wParam.ToInt32();
                
                // 处理设备插入或移除
                if (eventType == DeviceInterop.DBT_DEVICEARRIVAL || eventType == DeviceInterop.DBT_DEVICEREMOVECOMPLETE)
                {
                    // 检查 lParam 是否包含有效的设备信息
                    if (lParam != IntPtr.Zero)
                    {
                        var header = (DeviceInterop.DEV_BROADCAST_HDR)Marshal.PtrToStructure(lParam, typeof(DeviceInterop.DEV_BROADCAST_HDR));
                        
                        // 仅处理卷设备 (驱动器)
                        if (header.dbch_devicetype == DeviceInterop.DBT_DEVTYP_VOLUME)
                        {
                            var volume = (DeviceInterop.DEV_BROADCAST_VOLUME)Marshal.PtrToStructure(lParam, typeof(DeviceInterop.DEV_BROADCAST_VOLUME));
                            string driveLetter = MaskToDriveLetter(volume.dbcv_unitmask);
                            
                            char changeType = eventType == DeviceInterop.DBT_DEVICEARRIVAL ? 'A' : 'R';
                            
                            // 发布消息到总线
                            _messageBus?.Publish(new DeviceChangedMessage(changeType, driveLetter));
                        }
                    }
                    else
                    {
                        // 即使 lParam 为空，也触发通用变更通知以确保刷新
                        _messageBus?.Publish(new DeviceChangedMessage('C'));
                    }
                }
            }
            return IntPtr.Zero;
        }

        /// <summary>
        /// 执行底层弹出操作
        /// </summary>
        public bool EjectDrive(string drivePath)
        {
            if (string.IsNullOrEmpty(drivePath)) return false;
            
            // 规范化盘符 (如 "E" 或 "E:\" -> "\\.\E:")
            string driveLetter = drivePath.Substring(0, 1).ToUpper() + ":";
            string devicePath = $@"\\.\{driveLetter}";

            // 1. 获取设备句柄
            IntPtr hDevice = DeviceInterop.CreateFile(
                devicePath,
                DeviceInterop.GENERIC_READ | DeviceInterop.GENERIC_WRITE,
                DeviceInterop.FILE_SHARE_READ | DeviceInterop.FILE_SHARE_WRITE,
                IntPtr.Zero,
                DeviceInterop.OPEN_EXISTING,
                0,
                IntPtr.Zero);

            if (hDevice == new IntPtr(-1))
            {
                // 如果获取失败，尝试仅以读取权限打开
                hDevice = DeviceInterop.CreateFile(
                    devicePath,
                    DeviceInterop.GENERIC_READ,
                    DeviceInterop.FILE_SHARE_READ | DeviceInterop.FILE_SHARE_WRITE,
                    IntPtr.Zero,
                    DeviceInterop.OPEN_EXISTING,
                    0,
                    IntPtr.Zero);
            }

            if (hDevice == new IntPtr(-1)) return false;

            try
            {
                uint bytesReturned;
                
                // 2. 尝试锁定卷 (避免弹出时有其他进程写数据)
                DeviceInterop.DeviceIoControl(hDevice, DeviceInterop.FSCTL_LOCK_VOLUME, IntPtr.Zero, 0, IntPtr.Zero, 0, out bytesReturned, IntPtr.Zero);
                
                // 3. 卸载卷
                DeviceInterop.DeviceIoControl(hDevice, DeviceInterop.FSCTL_DISMOUNT_VOLUME, IntPtr.Zero, 0, IntPtr.Zero, 0, out bytesReturned, IntPtr.Zero);
                
                // 4. 弹出物理媒体
                bool success = DeviceInterop.DeviceIoControl(hDevice, DeviceInterop.IOCTL_STORAGE_EJECT_MEDIA, IntPtr.Zero, 0, IntPtr.Zero, 0, out bytesReturned, IntPtr.Zero);
                
                // 5. 即使弹出失败，也向外发送变更通知，因为逻辑卷可能已失效
                _messageBus?.Publish(new DeviceChangedMessage('R', driveLetter));

                return success;
            }
            catch
            {
                return false;
            }
            finally
            {
                DeviceInterop.CloseHandle(hDevice);
            }
        }

        /// <summary>
        /// 将位掩码转换为盘符字符串
        /// </summary>
        private string MaskToDriveLetter(int mask)
        {
            for (int i = 0; i < 26; i++)
            {
                if (((mask >> i) & 1) == 1)
                {
                    return ((char)('A' + i)).ToString() + ":";
                }
            }
            return null;
        }
    }
}
