using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace YiboFile.Interop
{
    /// <summary>
    /// 文件操作进度回调
    /// </summary>
    public delegate void FileProgressCallback(long currentBytes, long totalBytes, string fileName);

    /// <summary>
    /// 原生 Windows 文件操作 — Win32 API，支持进度回调
    /// </summary>
    internal static class NativeFileOperations
    {
        #region Win32 P/Invoke

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool MoveFileEx(
            string lpExistingFileName,
            string lpNewFileName,
            int dwFlags);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool CopyFileEx(
            string lpExistingFileName,
            string lpNewFileName,
            IntPtr lpProgressRoutine,
            IntPtr lpData,
            ref int pbCancel,
            int dwCopyFlags);

        // CopyProgressRoutine 返回值
        private const uint PROGRESS_CONTINUE = 0;
        private const uint PROGRESS_CANCEL = 1;
        private const uint PROGRESS_STOP = 2;
        private const uint PROGRESS_QUIET = 3;

        // dwCallbackReason
        private const uint CALLBACK_CHUNK_FINISHED = 0x00000000;
        private const uint CALLBACK_STREAM_SWITCH = 0x00000001;

        private const int MOVEFILE_REPLACE_EXISTING = 0x01;
        private const int MOVEFILE_COPY_ALLOWED    = 0x02;
        private const int MOVEFILE_WRITE_THROUGH   = 0x08;

        private static int _cancel;

        #endregion

        #region Win32 FindFirstFile / FindNextFile (批量文件枚举)

        /// <summary>
        /// 一次性返回目录中所有条目的元数据（文件+文件夹），
        /// 每个条目只需 1 次系统调用，避免逐个 new FileInfo/DirectoryInfo。
        /// </summary>
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct WIN32_FIND_DATA
        {
            public FileAttributes dwFileAttributes;
            public System.Runtime.InteropServices.ComTypes.FILETIME ftCreationTime;
            public System.Runtime.InteropServices.ComTypes.FILETIME ftLastAccessTime;
            public System.Runtime.InteropServices.ComTypes.FILETIME ftLastWriteTime;
            public uint nFileSizeHigh;
            public uint nFileSizeLow;
            public uint dwReserved0;
            public uint dwReserved1;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
            public string cFileName;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 14)]
            public string cAlternateFileName;
        }

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern IntPtr FindFirstFile(string lpFileName, out WIN32_FIND_DATA lpFindFileData);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool FindNextFile(IntPtr hFindFile, out WIN32_FIND_DATA lpFindFileData);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool FindClose(IntPtr hFindFile);

        private static readonly IntPtr INVALID_HANDLE_VALUE = new IntPtr(-1);

        /// <summary>
        /// 从 WIN32_FIND_DATA 提取文件大小（64 位）
        /// </summary>
        private static long GetFileSizeFromFindData(WIN32_FIND_DATA data)
        {
            return ((long)data.nFileSizeHigh << 32) | data.nFileSizeLow;
        }

        /// <summary>
        /// 从 WIN32_FIND_DATA 提取 DateTime
        /// </summary>
        private static DateTime FileTimeToDateTime(System.Runtime.InteropServices.ComTypes.FILETIME ft)
        {
            long hFT = ((long)ft.dwHighDateTime << 32) | (uint)ft.dwLowDateTime;
            return DateTime.FromFileTimeUtc(hFT);
        }

        /// <summary>
        /// 目录条目元数据（单次枚举返回）
        /// </summary>
        public readonly struct DirectoryEntry
        {
            public readonly string Name;
            public readonly string FullPath;
            public readonly bool IsDirectory;
            public readonly bool IsReparsePoint;
            public readonly long Size;
            public readonly DateTime LastWriteTimeUtc;
            public readonly DateTime CreationTimeUtc;

            public DirectoryEntry(string directory, WIN32_FIND_DATA data)
            {
                Name = data.cFileName;
                FullPath = System.IO.Path.Combine(directory, data.cFileName);
                IsDirectory = (data.dwFileAttributes & FileAttributes.Directory) != 0;
                IsReparsePoint = (data.dwFileAttributes & FileAttributes.ReparsePoint) != 0;
                Size = GetFileSizeFromFindData(data);
                LastWriteTimeUtc = FileTimeToDateTime(data.ftLastWriteTime);
                CreationTimeUtc = FileTimeToDateTime(data.ftCreationTime);
            }
        }

        /// <summary>
        /// 枚举目录中所有条目（文件+文件夹），每个条目 1 次系统调用。
        /// 替代 Directory.GetFiles/DirectoryInfo.EnumerateDirectories + 逐个 FileInfo/DirectoryInfo。
        /// 跳过 . 和 ..
        /// </summary>
        /// <param name="directory">目录路径</param>
        /// <param name="skipReparsePoints">是否跳过重解析点（符号链接/junction）</param>
        /// <param name="skipBlacklist">是否跳过系统隐藏目录（System Volume Information, $RECYCLE.BIN）</param>
        /// <returns>所有条目的元数据列表</returns>
        public static List<DirectoryEntry> EnumerateDirectoryEntries(
            string directory,
            bool skipReparsePoints = true,
            bool skipBlacklist = true)
        {
            var results = new List<DirectoryEntry>();
            string searchPattern = directory.TrimEnd('\\') + "\\*";

            IntPtr hFind = FindFirstFile(searchPattern, out WIN32_FIND_DATA data);
            if (hFind == INVALID_HANDLE_VALUE)
                return results;

            try
            {
                do
                {
                    // 跳过当前目录和父目录
                    if (data.cFileName == "." || data.cFileName == "..")
                        continue;

                    // 跳过重解析点
                    if (skipReparsePoints && (data.dwFileAttributes & FileAttributes.ReparsePoint) != 0)
                        continue;

                    // 跳过系统黑名单目录
                    if (skipBlacklist && (data.dwFileAttributes & FileAttributes.Directory) != 0)
                    {
                        if (data.cFileName.Equals("System Volume Information", StringComparison.OrdinalIgnoreCase) ||
                            data.cFileName.Equals("$RECYCLE.BIN", StringComparison.OrdinalIgnoreCase) ||
                            data.cFileName.Equals("$Recycle.Bin", StringComparison.OrdinalIgnoreCase))
                            continue;
                    }

                    results.Add(new DirectoryEntry(directory, data));

                } while (FindNextFile(hFind, out data));
            }
            finally
            {
                FindClose(hFind);
            }

            return results;
        }

        #endregion

        /// <summary>
        /// 计算文件/目录总大小（字节）
        /// </summary>
        public static long CalculateTotalSize(string path)
        {
            if (File.Exists(path))
                return new FileInfo(path).Length;

            if (Directory.Exists(path))
                return CalculateDirectorySize(path);

            return 0;
        }

        private static long CalculateDirectorySize(string dir)
        {
            long size = 0;
            try
            {
                foreach (var file in Directory.GetFiles(dir))
                {
                    try { size += new FileInfo(file).Length; } catch { }
                }
                foreach (var subDir in Directory.GetDirectories(dir))
                {
                    size += CalculateDirectorySize(subDir);
                }
            }
            catch { }
            return size;
        }

        /// <summary>
        /// 剪切文件（同卷用 MoveFileEx，跨卷用 CopyFileEx+Delete 以获得进度）
        /// </summary>
        public static void MoveFile(string source, string dest, FileProgressCallback progress = null)
        {
            if (!File.Exists(source))
                throw new FileNotFoundException("源文件不存在", source);

            // 同卷：原子重命名，瞬间完成，无需进度
            if (IsSameVolume(source, dest))
            {
                if (!MoveFileEx(source, dest,
                    MOVEFILE_REPLACE_EXISTING | MOVEFILE_WRITE_THROUGH))
                {
                    int err = Marshal.GetLastWin32Error();
                    throw new IOException($"剪切文件失败 (Win32 错误 {err})");
                }
                progress?.Invoke(100, 100, Path.GetFileName(source));
                return;
            }

            // 跨卷：CopyFileEx 获得进度 → 删除源文件
            CopyFileWithProgress(source, dest, progress);
            File.Delete(source);
        }

        /// <summary>
        /// 剪切目录（同卷用 MoveFileEx，跨卷逐文件复制）
        /// </summary>
        public static async Task MoveDirectoryAsync(string source, string dest,
            FileProgressCallback progress = null, string basePath = null)
        {
            if (!Directory.Exists(source))
                throw new DirectoryNotFoundException($"源目录不存在: {source}");

            // 同卷：原子重命名，瞬间完成
            if (IsSameVolume(source, dest))
            {
                if (!MoveFileEx(source, dest,
                    MOVEFILE_REPLACE_EXISTING | MOVEFILE_WRITE_THROUGH))
                {
                    int err = Marshal.GetLastWin32Error();
                    throw new IOException($"剪切目录失败 (Win32 错误 {err})");
                }
                string name = Path.GetFileName(source);
                if (progress != null && string.IsNullOrEmpty(basePath))
                    progress?.Invoke(100, 100, name);
                return;
            }

            // 跨卷：逐文件复制 + 进度
            await CopyDirectoryWithProgressAsync(source, dest, progress, basePath);

            // 安全删除源目录
            await SafeDeleteDirectoryAsync(source);
        }

        /// <summary>
        /// 复制文件（支持进度回调）
        /// </summary>
        public static void CopyFile(string source, string dest, FileProgressCallback progress = null)
        {
            if (!File.Exists(source))
                throw new FileNotFoundException("源文件不存在", source);
            CopyFileWithProgress(source, dest, progress);
        }

        /// <summary>
        /// 复制目录（递归，支持进度回调）
        /// </summary>
        public static void CopyDirectory(string source, string dest,
            FileProgressCallback progress = null, string basePath = null)
        {
            if (!Directory.Exists(source))
                throw new DirectoryNotFoundException($"源目录不存在: {source}");
            CopyDirectoryWithProgressAsync(source, dest, progress, basePath).GetAwaiter().GetResult();
        }

        public static async Task CopyDirectoryAsync(string source, string dest,
            FileProgressCallback progress = null, string basePath = null)
        {
            if (!Directory.Exists(source))
                throw new DirectoryNotFoundException($"源目录不存在: {source}");
            await CopyDirectoryWithProgressAsync(source, dest, progress, basePath);
        }

        #region 内部实现

        private static void CopyFileWithProgress(string source, string dest, FileProgressCallback progress)
        {
            long totalBytes = new FileInfo(source).Length;
            long copiedBytes = 0;
            string fileName = Path.GetFileName(source);

            CopyProgressRoutineDelegate callback = (TotalFileSize, TotalBytesTransferred,
                StreamSize, StreamBytesTransferred, dwStreamNumber,
                dwCallbackReason, hSourceFile, hDestinationFile, lpData) =>
            {
                if (dwCallbackReason == CALLBACK_CHUNK_FINISHED)
                {
                    copiedBytes = TotalBytesTransferred;
                    progress?.Invoke(copiedBytes, TotalFileSize, fileName);
                }
                return PROGRESS_CONTINUE;
            };

            IntPtr callbackPtr = Marshal.GetFunctionPointerForDelegate(callback);

            if (!CopyFileEx(source, dest, callbackPtr, IntPtr.Zero, ref _cancel, 0))
            {
                int err = Marshal.GetLastWin32Error();
                if (err != 80) // ERROR_FILE_EXISTS — 覆盖模式正常
                    throw new IOException($"复制文件失败: {fileName} (Win32 错误 {err})");
            }

            progress?.Invoke(totalBytes, totalBytes, fileName);
        }

        private static async Task CopyDirectoryWithProgressAsync(string source, string dest,
            FileProgressCallback progress, string basePath)
        {
            string relative = basePath ?? "";
            string label = string.IsNullOrEmpty(relative)
                ? Path.GetFileName(source)
                : relative;

            Directory.CreateDirectory(dest);

            foreach (var file in Directory.GetFiles(source))
            {
                var fileName = Path.GetFileName(file);
                var destFile = Path.Combine(dest, fileName);
                string fileLabel = string.IsNullOrEmpty(relative) ? fileName : $"{relative}\\{fileName}";
                CopyFileWithProgress(file, destFile, (current, total, name) =>
                {
                    progress?.Invoke(current, total, fileLabel);
                });
            }

            foreach (var dir in Directory.GetDirectories(source))
            {
                var dirName = Path.GetFileName(dir);
                var subRel = string.IsNullOrEmpty(relative) ? dirName : $"{relative}\\{dirName}";
                await CopyDirectoryWithProgressAsync(dir, Path.Combine(dest, dirName), progress, subRel);
            }
        }

        private static bool IsSameVolume(string path1, string path2)
        {
            try
            {
                var d1 = Path.GetPathRoot(Path.GetFullPath(path1));
                var d2 = Path.GetPathRoot(Path.GetFullPath(path2));
                return string.Equals(d1, d2, StringComparison.OrdinalIgnoreCase);
            }
            catch { return false; }
        }

        private static async Task SafeDeleteDirectoryAsync(string path)
        {
            if (!Directory.Exists(path)) return;
            RemoveReadOnlyRecursive(path);

            for (int i = 0; i < 10; i++)
            {
                try { Directory.Delete(path, true); return; }
                catch (IOException)
                {
                    if (i == 9) break;
                    await Task.Delay(200 * (i + 1));
                }
            }
            try { Directory.Delete(path, true); } catch { }
        }

        private static void RemoveReadOnlyRecursive(string dir)
        {
            foreach (var file in Directory.GetFiles(dir))
                try { File.SetAttributes(file, FileAttributes.Normal); } catch { }
            foreach (var subDir in Directory.GetDirectories(dir))
                RemoveReadOnlyRecursive(subDir);
        }

        #endregion

        #region 非托管回调
        // 必须作为字段持有引用，防止 GC 回收
        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        private delegate uint CopyProgressRoutineDelegate(
            long TotalFileSize,
            long TotalBytesTransferred,
            long StreamSize,
            long StreamBytesTransferred,
            uint dwStreamNumber,
            uint dwCallbackReason,
            IntPtr hSourceFile,
            IntPtr hDestinationFile,
            IntPtr lpData);
        #endregion
    }
}
