using System;
using System.IO;
using System.Runtime.InteropServices;

namespace YiboFile.Services.FileSystem
{
    /// <summary>
    /// 符号链接和重定向文件夹辅助类
    /// 用于检测和解析Windows Junction Points、Symbolic Links等
    /// </summary>
    public static class SymbolicLinkHelper
    {
        /// <summary>
        /// 检测指定路径是否为符号链接或重定向点(Reparse Point)
        /// </summary>
        /// <param name="path">要检测的路径</param>
        /// <returns>如果是符号链接返回true，否则返回false</returns>
        public static bool IsSymbolicLink(string path)
        {
            if (string.IsNullOrEmpty(path))
                return false;

            try
            {
                var attrs = File.GetAttributes(path);
                // ReparsePoint标志表示这是一个重定向点（包括符号链接、Junction等）
                return (attrs & FileAttributes.ReparsePoint) != 0;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 获取符号链接或Junction Point的目标路径
        /// </summary>
        /// <param name="path">符号链接路径</param>
        /// <returns>目标路径，如果无法解析则返回原路径</returns>
        public static string GetSymbolicLinkTarget(string path)
        {
            if (string.IsNullOrEmpty(path))
                return path;

            // 如果不是符号链接，直接返回原路径
            if (!IsSymbolicLink(path))
                return path;

            try
            {
                // 使用.NET 6+的新API (DirectoryInfo.LinkTarget)
                var dirInfo = new DirectoryInfo(path);
                var linkTarget = dirInfo.LinkTarget;

                if (!string.IsNullOrEmpty(linkTarget))
                {
                    // 如果是相对路径，转换为绝对路径
                    if (!Path.IsPathRooted(linkTarget))
                    {
                        var baseDir = Path.GetDirectoryName(path);
                        linkTarget = Path.GetFullPath(Path.Combine(baseDir, linkTarget));
                    }

                    return linkTarget;
                }
            }
            catch
            {
                // .NET 6+的API可能在某些情况下失败，继续尝试Win32方法
            }

            // 降级方案：尝试使用Win32 API
            try
            {
                return GetTargetPathViaWin32(path);
            }
            catch
            {
                // 如果都失败了，返回原路径
                return path;
            }
        }

        /// <summary>
        /// 尝试解析符号链接（如果失败则返回null）
        /// </summary>
        /// <param name="path">符号链接路径</param>
        /// <param name="target">输出参数：目标路径</param>
        /// <returns>成功返回true，失败返回false</returns>
        public static bool TryGetSymbolicLinkTarget(string path, out string target)
        {
            target = null;

            if (string.IsNullOrEmpty(path) || !IsSymbolicLink(path))
                return false;

            try
            {
                target = GetSymbolicLinkTarget(path);
                return !string.IsNullOrEmpty(target) && target != path;
            }
            catch
            {
                return false;
            }
        }

        #region Win32 API降级方案

        private const int FSCTL_GET_REPARSE_POINT = 0x000900A8;
        private const uint IO_REPARSE_TAG_MOUNT_POINT = 0xA0000003;
        private const uint IO_REPARSE_TAG_SYMLINK = 0xA000000C;

        [StructLayout(LayoutKind.Sequential)]
        private struct REPARSE_DATA_BUFFER
        {
            public uint ReparseTag;
            public ushort ReparseDataLength;
            public ushort Reserved;
            public ushort SubstituteNameOffset;
            public ushort SubstituteNameLength;
            public ushort PrintNameOffset;
            public ushort PrintNameLength;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 0x3FF0)]
            public byte[] PathBuffer;
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr CreateFile(
            string lpFileName,
            uint dwDesiredAccess,
            uint dwShareMode,
            IntPtr lpSecurityAttributes,
            uint dwCreationDisposition,
            uint dwFlagsAndAttributes,
            IntPtr hTemplateFile);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool DeviceIoControl(
            IntPtr hDevice,
            uint dwIoControlCode,
            IntPtr lpInBuffer,
            int nInBufferSize,
            IntPtr lpOutBuffer,
            int nOutBufferSize,
            out int lpBytesReturned,
            IntPtr lpOverlapped);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool CloseHandle(IntPtr hObject);

        private static string GetTargetPathViaWin32(string path)
        {
            const uint GENERIC_READ = 0x80000000;
            const uint FILE_SHARE_READ = 0x00000001;
            const uint FILE_SHARE_WRITE = 0x00000002;
            const uint OPEN_EXISTING = 3;
            const uint FILE_FLAG_BACKUP_SEMANTICS = 0x02000000;
            const uint FILE_FLAG_OPEN_REPARSE_POINT = 0x00200000;

            IntPtr handle = CreateFile(
                path,
                GENERIC_READ,
                FILE_SHARE_READ | FILE_SHARE_WRITE,
                IntPtr.Zero,
                OPEN_EXISTING,
                FILE_FLAG_BACKUP_SEMANTICS | FILE_FLAG_OPEN_REPARSE_POINT,
                IntPtr.Zero);

            if (handle == new IntPtr(-1))
                return path;

            try
            {
                int bufferSize = Marshal.SizeOf(typeof(REPARSE_DATA_BUFFER));
                IntPtr buffer = Marshal.AllocHGlobal(bufferSize);

                try
                {
                    int bytesReturned;
                    bool success = DeviceIoControl(
                        handle,
                        FSCTL_GET_REPARSE_POINT,
                        IntPtr.Zero,
                        0,
                        buffer,
                        bufferSize,
                        out bytesReturned,
                        IntPtr.Zero);

                    if (!success)
                        return path;

                    REPARSE_DATA_BUFFER reparseData = Marshal.PtrToStructure<REPARSE_DATA_BUFFER>(buffer);

                    // 只处理Junction Points和Symbolic Links
                    if (reparseData.ReparseTag != IO_REPARSE_TAG_MOUNT_POINT &&
                        reparseData.ReparseTag != IO_REPARSE_TAG_SYMLINK)
                        return path;

                    // 提取目标路径
                    int offset = reparseData.SubstituteNameOffset / 2;
                    int length = reparseData.SubstituteNameLength / 2;

                    string targetPath = System.Text.Encoding.Unicode.GetString(
                        reparseData.PathBuffer,
                        offset * 2,
                        length * 2);

                    // 移除 "\??\" 前缀（Win32路径格式）
                    if (targetPath.StartsWith(@"\??\"))
                        targetPath = targetPath.Substring(4);

                    return targetPath;
                }
                finally
                {
                    Marshal.FreeHGlobal(buffer);
                }
            }
            finally
            {
                CloseHandle(handle);
            }
        }

        #endregion
    }
}

