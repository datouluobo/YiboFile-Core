using System;
using System.Runtime.InteropServices;

namespace YiboFile.Interop.Native
{
    /// <summary>
    /// 系统设备交互原生 API
    /// 包括设备变更通知和 IO 控制操作
    /// </summary>
    public static class DeviceInterop
    {
        // 窗口消息常量
        public const int WM_DEVICECHANGE = 0x0219;
        
        // DBT (Device Broadcast Type) 常量
        public const int DBT_DEVICEARRIVAL = 0x8000;          // 设备已插入
        public const int DBT_DEVICEREMOVECOMPLETE = 0x8004;   // 设备已移除
        public const int DBT_DEVNODES_CHANGED = 0x0007;       // 设备节点变更
        
        // 设备类型
        public const int DBT_DEVTYP_VOLUME = 0x00000002;      // 逻辑卷 (驱动器)

        /// <summary>
        /// 设备通知头
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        public struct DEV_BROADCAST_HDR
        {
            public int dbch_size;
            public int dbch_devicetype;
            public int dbch_reserved;
        }

        /// <summary>
        /// 卷设备通知数据
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        public struct DEV_BROADCAST_VOLUME
        {
            public int dbcv_size;
            public int dbcv_devicetype;
            public int dbcv_reserved;
            public int dbcv_unitmask; // 掩码,每一位代表一个驱动器盘符 (A=1, B=2, C=4...)
            public ushort dbcv_flags;
        }

        // --- Native API 导入 ---

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        public static extern IntPtr CreateFile(
            string lpFileName,
            uint dwDesiredAccess,
            uint dwShareMode,
            IntPtr lpSecurityAttributes,
            uint dwCreationDisposition,
            uint dwFlagsAndAttributes,
            IntPtr hTemplateFile);

        [DllImport("kernel32.dll", ExactSpelling = true, SetLastError = true, CharSet = CharSet.Auto)]
        public static extern bool DeviceIoControl(
            IntPtr hDevice,
            uint dwIoControlCode,
            IntPtr lpInBuffer,
            uint nInBufferSize,
            IntPtr lpOutBuffer,
            uint nOutBufferSize,
            out uint lpBytesReturned,
            IntPtr lpOverlapped);

        [DllImport("kernel32.dll", ExactSpelling = true, SetLastError = true, CharSet = CharSet.Auto)]
        public static extern bool CloseHandle(IntPtr hObject);

        // 访问模式与共享模式
        public const uint GENERIC_READ = 0x80000000;
        public const uint GENERIC_WRITE = 0x40000000;
        public const uint FILE_SHARE_READ = 0x00000001;
        public const uint FILE_SHARE_WRITE = 0x00000002;
        public const uint OPEN_EXISTING = 3;

        // 控制代码
        public const uint IOCTL_STORAGE_EJECT_MEDIA = 0x2D4808;
        public const uint FSCTL_LOCK_VOLUME = 0x00090018;
        public const uint FSCTL_DISMOUNT_VOLUME = 0x00090020;
    }
}
