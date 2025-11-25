using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Text;
using System.Windows.Data;
using System.Windows.Media.Imaging;
using System.Windows.Interop;
using System.Windows.Threading;
using System.Windows;
using System.Windows.Media;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using FFMpegCore;
using ImageMagick;

namespace OoiMRR.Controls.Converters
{
    public class ThumbnailConverter : IValueConverter
    {
        private static readonly Dictionary<string, BitmapSource> _videoThumbnailCache = new();
        private static readonly object _cacheLock = new object();
        private const int MaxCacheSize = 1000;
        
        private static readonly HashSet<string> _priorityLoadPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private static readonly object _priorityLock = new object();
        
        private static readonly Dictionary<int, BitmapSource> _placeholderCache = new Dictionary<int, BitmapSource>();
        [DllImport("shell32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr SHGetFileInfo(string pszPath, uint dwFileAttributes, ref SHFILEINFO psfi, uint cbSizeFileInfo, uint uFlags);

        [DllImport("user32.dll")]
        private static extern bool DestroyIcon(IntPtr hIcon);

        [DllImport("shell32.dll", CharSet = CharSet.Unicode, PreserveSig = false)]
        [return: MarshalAs(UnmanagedType.Interface)]
        private static extern IShellItem SHCreateItemFromParsingName(
            [MarshalAs(UnmanagedType.LPWStr)] string pszPath,
            IntPtr pbc,
            [MarshalAs(UnmanagedType.LPStruct)] Guid riid);

        [DllImport("shell32.dll", CharSet = CharSet.Auto)]
        private static extern int ExtractIconEx(string lpszFile, int nIconIndex, out IntPtr phiconLarge, out IntPtr phiconSmall, int nIcons);

        [DllImport("shell32.dll")]
        private static extern int SHGetImageList(int iImageList, ref Guid riid, out IImageList ppv);

        private const uint SHGFI_ICON = 0x100;
        private const uint SHGFI_LARGEICON = 0x0;
        private const uint SHGFI_SMALLICON = 0x1;
        private const uint SHGFI_USEFILEATTRIBUTES = 0x10;
        private const uint SHGFI_SYSICONINDEX = 0x4000;
        private const int SIIGBF_THUMBNAILONLY = 0x1;
        private const int SIIGBF_BIGGERSIZEOK = 0x2;
        private const int SIIGBF_RESIZETOFIT = 0x4;
        private const int ILD_TRANSPARENT = 0x1;
        private const int SHIL_EXTRALARGE = 0x2;
        private const int SHIL_JUMBO = 0x4;

        [StructLayout(LayoutKind.Sequential)]
        private struct SHFILEINFO
        {
            public IntPtr hIcon;
            public int iIcon;
            public uint dwAttributes;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
            public string szDisplayName;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]
            public string szTypeName;
        }

        [ComImport]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        [Guid("43826d1e-e718-42ee-bc55-a1e261c37bfe")]
        private interface IShellItem
        {
            void BindToHandler(IntPtr pbc, [MarshalAs(UnmanagedType.LPStruct)] Guid bhid, [MarshalAs(UnmanagedType.LPStruct)] Guid riid, out IntPtr ppv);
            void GetParent(out IShellItem ppsi);
            void GetDisplayName(uint sigdnName, out IntPtr ppszName);
            void GetAttributes(uint sfgaoMask, out uint psfgaoAttribs);
            void Compare(IShellItem psi, uint hint, out int piOrder);
        }

        [ComImport]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        [Guid("bcc18b79-ba16-442f-80c4-8a59c30c463b")]
        private interface IShellItemImageFactory
        {
            void GetImage([MarshalAs(UnmanagedType.Struct)] SIZE size, int flags, out IntPtr phbm);
        }

        [ComImport]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        [Guid("46EB5926-582E-4017-9FDF-E8998DAA0950")]
        private interface IImageList
        {
            [PreserveSig]
            int GetIcon(int i, int flags, out IntPtr picon);
        }

        public static void SetPriorityLoadPaths(IEnumerable<string> paths)
        {
            lock (_priorityLock)
            {
                _priorityLoadPaths.Clear();
                if (paths != null)
                {
                    foreach (var path in paths)
                    {
                        if (!string.IsNullOrEmpty(path))
                            _priorityLoadPaths.Add(path);
                    }
                }
            }
        }
        
        public static void ClearPriorityLoadPaths()
        {
            lock (_priorityLock)
            {
                _priorityLoadPaths.Clear();
            }
        }
        
        private BitmapSource CreatePlaceholder(int size)
        {
            lock (_cacheLock)
            {
                if (_placeholderCache.TryGetValue(size, out var cached))
                    return cached;
                
                var renderTarget = new RenderTargetBitmap(size, size, 96, 96, PixelFormats.Pbgra32);
                var drawingVisual = new DrawingVisual();
                using (var drawingContext = drawingVisual.RenderOpen())
                {
                    drawingContext.DrawRectangle(
                        new SolidColorBrush(System.Windows.Media.Color.FromRgb(240, 240, 240)),
                        null,
                        new Rect(0, 0, size, size));
                    drawingContext.DrawRectangle(
                        null,
                        new System.Windows.Media.Pen(new SolidColorBrush(System.Windows.Media.Color.FromRgb(200, 200, 200)), 1),
                        new Rect(0, 0, size, size));
                }
                renderTarget.Render(drawingVisual);
                renderTarget.Freeze();
                
                _placeholderCache[size] = renderTarget;
                return renderTarget;
            }
        }
        
        private void LoadThumbnailAsync(string path, DependencyObject target, int targetSize)
        {
            Task.Run(() =>
        {
            try
            {
                    BitmapSource thumbnail = LoadThumbnailSync(path, targetSize);
                    if (thumbnail != null)
                    {
                        Application.Current?.Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() =>
                        {
                            try
                            {
                                if (target is System.Windows.Controls.Image image)
                                {
                                    image.Source = thumbnail;
                                }
                                else if (target is FrameworkElement fe)
                                {
                                    var img = FindVisualChild<System.Windows.Controls.Image>(fe);
                                    if (img != null)
                                        img.Source = thumbnail;
                                }
                            }
                            catch { }
                        }));
                    }
                }
                catch { }
            });
        }
        
        private static T FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
        {
            if (parent == null) return null;
            
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is T result)
                    return result;
                
                var childOfChild = FindVisualChild<T>(child);
                if (childOfChild != null)
                    return childOfChild;
            }
                    return null;
        }
        
        internal BitmapSource LoadThumbnailSync(string path, int targetSize)
        {
            try
            {
                var ext = Path.GetExtension(path)?.ToLowerInvariant();
                
                bool isDirectory = Directory.Exists(path);
                bool isFile = File.Exists(path);

                if (!isDirectory && !isFile)
                    return null;

                if (isFile)
                {
                    switch (ext)
                    {
                        case ".jpg":
                        case ".jpeg":
                        case ".png":
                        case ".bmp":
                        case ".gif":
                        case ".webp":
                        case ".tiff":
                        case ".tif":
                        case ".ico":
                            try
                            {
                                if (!File.Exists(path))
                                    return null;
                                
                                var bmp = new BitmapImage();
                                bmp.BeginInit();
                                bmp.StreamSource = new FileStream(path, FileMode.Open, FileAccess.Read);
                                bmp.CacheOption = BitmapCacheOption.OnLoad;
                                bmp.DecodePixelWidth = targetSize;
                                bmp.EndInit();
                                bmp.Freeze();
                                return bmp;
                            }
                            catch
                            {
                                return null;
                            }
                        
                        case ".svg":
                        case ".psd":
                            var magickThumbnail = GenerateThumbnailWithMagick(path, targetSize);
                            if (magickThumbnail != null)
                                return magickThumbnail;
                            
                            try
                            {
                                var thumbnail = GetShellThumbnail(path, targetSize);
                                if (thumbnail != null)
                                    return thumbnail;
                            }
                            catch { }
                            break;
                        
                        // 视频格式：使用 FFmpeg 提取第一帧
                        case ".mp4":
                        case ".avi":
                        case ".mkv":
                        case ".mov":
                        case ".wmv":
                        case ".flv":
                        case ".webm":
                        case ".m4v":
                        case ".3gp":
                        case ".asf":
                        case ".rm":
                        case ".rmvb":
                        case ".mpg":
                        case ".mpeg":
                        case ".m2v":
                        case ".vob":
                        case ".ogv":
                        case ".ts":
                        case ".mts":
                        case ".m2ts":
                            try
                            {
                                return ExtractVideoThumbnail(path, targetSize);
                            }
                            catch
                            {
                                // FFmpeg 提取失败，继续尝试其他方法
                            }
                            break;
                    }
                }

                if (isFile && !isDirectory)
                {
                    try
                    {
                        var thumbnail = GetShellThumbnail(path, targetSize);
                        if (thumbnail != null)
                            return thumbnail;
                    }
                    catch { }
                }
                
                try
                {
                    var icon = GetHighQualitySystemIcon(path, isDirectory, targetSize);
                    return icon ?? CreatePlaceholder(targetSize);
                }
                catch
                {
                    return CreatePlaceholder(targetSize);
                }
            }
            catch
            {
                return null;
            }
        }

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
                            {
            try
            {
                var path = value as string;
                if (string.IsNullOrEmpty(path))
                    return null;

                bool isDirectory = Directory.Exists(path);
                bool isFile = File.Exists(path);

                if (!isDirectory && !isFile)
                    return null;

                int targetSize = 256;
                if (parameter != null)
                {
                    if (parameter is int size)
                        targetSize = size;
                    else if (int.TryParse(parameter.ToString(), out int parsedSize))
                        targetSize = parsedSize;
                }

                bool isPriorityLoad = false;
                lock (_priorityLock)
                {
                    isPriorityLoad = _priorityLoadPaths.Contains(path);
                }
                
                if (isPriorityLoad)
                {
                    var thumbnail = LoadThumbnailSync(path, targetSize);
                    return thumbnail ?? CreatePlaceholder(targetSize);
                }
                
                return CreatePlaceholder(targetSize);
            }
            catch
            {
                return null;
            }
        }

        private string[] GetPathsToTry(string path)
        {
            if (path.StartsWith(@"\\?\"))
                return new[] { path, path.Substring(4) };
            
            if (path.Length >= 260)
            {
                if (path.StartsWith(@"\\"))
                    return new[] { path, @"\\?\UNC" + path.Substring(1) };
                else
                    return new[] { path, @"\\?\" + path };
            }
            
            return new[] { path };
        }

        private BitmapSource GetShellThumbnail(string path, int size)
        {
            IntPtr hBitmap = IntPtr.Zero;
            try
            {
                if (string.IsNullOrEmpty(path))
                    return null;

                Guid shellItemGuid = new Guid("43826d1e-e718-42ee-bc55-a1e261c37bfe");
                IShellItem shellItem = null;
                
                foreach (var tryPath in GetPathsToTry(path))
                {
                    try
                        {
                        shellItem = SHCreateItemFromParsingName(tryPath, IntPtr.Zero, shellItemGuid);
                        if (shellItem != null)
                            break;
                        }
                        catch { }
                }
                
                if (shellItem != null)
                {
                    IShellItemImageFactory imageFactory = shellItem as IShellItemImageFactory;
                    if (imageFactory != null)
                    {
                        int[] flagCombinations = new[]
                        {
                            SIIGBF_THUMBNAILONLY | SIIGBF_BIGGERSIZEOK | SIIGBF_RESIZETOFIT,
                            SIIGBF_THUMBNAILONLY | SIIGBF_RESIZETOFIT,
                            SIIGBF_THUMBNAILONLY | SIIGBF_BIGGERSIZEOK,
                            SIIGBF_THUMBNAILONLY,
                        };
                        
                        foreach (var flags in flagCombinations)
                        {
                            try
                            {
                                imageFactory.GetImage(new SIZE { cx = size, cy = size }, flags, out hBitmap);
                                
                                if (hBitmap != IntPtr.Zero)
                                {
                                    try
                                    {
                                        var bitmapSource = Imaging.CreateBitmapSourceFromHBitmap(
                                            hBitmap,
                                            IntPtr.Zero,
                                            System.Windows.Int32Rect.Empty,
                                            BitmapSizeOptions.FromWidthAndHeight(size, size));
                                        
                                        RenderOptions.SetBitmapScalingMode(bitmapSource, BitmapScalingMode.HighQuality);
                                        RenderOptions.SetCachingHint(bitmapSource, CachingHint.Cache);
                                        
                                        if (Math.Abs(bitmapSource.PixelWidth - size) > 1 || Math.Abs(bitmapSource.PixelHeight - size) > 1)
                                        {
                                            var scaleTransform = new ScaleTransform(
                                                (double)size / bitmapSource.PixelWidth,
                                                (double)size / bitmapSource.PixelHeight,
                                                0, 0);
                                            
                                            var scaledBitmap = new TransformedBitmap(bitmapSource, scaleTransform);
                                            RenderOptions.SetBitmapScalingMode(scaledBitmap, BitmapScalingMode.HighQuality);
                                            RenderOptions.SetCachingHint(scaledBitmap, CachingHint.Cache);
                                            scaledBitmap.Freeze();
                                            
                                            var tempBitmap1 = hBitmap;
                                            hBitmap = IntPtr.Zero;
                                            DeleteObject(tempBitmap1);
                                            
                                            return scaledBitmap;
                                        }
                                        
                                        bitmapSource.Freeze();
                                        
                                        var tempBitmap2 = hBitmap;
                                        hBitmap = IntPtr.Zero;
                                        DeleteObject(tempBitmap2);
                                        
                                        return bitmapSource;
                                    }
                                    catch
                                    {
                                        if (hBitmap != IntPtr.Zero)
                                        {
                                            DeleteObject(hBitmap);
                                            hBitmap = IntPtr.Zero;
                                        }
                                    }
                                }
                            }
                            catch
                            {
                                if (hBitmap != IntPtr.Zero)
                                {
                                    DeleteObject(hBitmap);
                                    hBitmap = IntPtr.Zero;
                                }
                            }
                        }
                    }
                }
            }
            catch { }
            finally
            {
                if (hBitmap != IntPtr.Zero)
                    DeleteObject(hBitmap);
            }
            return null;
        }

        private BitmapSource ExtractVideoThumbnail(string videoPath, int targetSize)
        {
            // 生成缓存键（包含文件路径、大小和最后修改时间）
            var fileInfo = new FileInfo(videoPath);
            if (!fileInfo.Exists)
                return null;
                
            string cacheKey = $"{videoPath}_{targetSize}_{fileInfo.LastWriteTime.Ticks}";
            
            lock (_cacheLock)
            {
                if (_videoThumbnailCache.TryGetValue(cacheKey, out var cached))
                    return cached;
            }
            
            try
            {
                // 创建临时输出文件
                string tempImagePath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString() + ".jpg");
                
                try
                {
                    if (!Path.IsPathRooted(videoPath))
                        videoPath = Path.GetFullPath(videoPath);
                    
                    if (!File.Exists(videoPath))
                        return null;
                    
                    string ffmpegPath = GetFFmpegPath();
                    if (string.IsNullOrEmpty(ffmpegPath))
                        return null;
                    
                    string escapedVideoPath = $"\"{videoPath}\"";
                    string escapedOutputPath = $"\"{tempImagePath}\"";
                    string arguments = $"-ss 1 -i {escapedVideoPath} -vframes 1 -vf scale={targetSize}:-1 -q:v 2 -y {escapedOutputPath}";
                    
                    if (!RunFFmpegCommand(ffmpegPath, arguments, out _, out _))
                        return null;
                    
                    int maxWaitMs = 10000;
                    int waitIntervalMs = 100;
                    int waitedMs = 0;
                    bool fileExists = false;
                    
                    while (waitedMs < maxWaitMs)
                    {
                        if (File.Exists(tempImagePath))
                        {
                            try
                            {
                                var checkInfo = new FileInfo(tempImagePath);
                                if (checkInfo.Length > 0)
                                {
                                    fileExists = true;
                                    break;
                                }
                            }
                            catch { }
                        }
                        
                        Thread.Sleep(waitIntervalMs);
                        waitedMs += waitIntervalMs;
                    }
                    
                    if (!fileExists)
                        return null;
                    
                    var tempFileInfo = new FileInfo(tempImagePath);
                    if (tempFileInfo.Length == 0)
                        return null;
                    
                    byte[] imageBytes;
                    using (var fileStream = new FileStream(tempImagePath, FileMode.Open, FileAccess.Read))
                    {
                        imageBytes = new byte[fileStream.Length];
                        fileStream.Read(imageBytes, 0, imageBytes.Length);
                    }
                    
                    var bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.StreamSource = new MemoryStream(imageBytes);
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.DecodePixelWidth = targetSize;
                    bitmap.EndInit();
                    bitmap.Freeze();
                    
                    lock (_cacheLock)
                    {
                        if (_videoThumbnailCache.Count >= MaxCacheSize)
                        {
                            var firstKey = _videoThumbnailCache.Keys.First();
                            _videoThumbnailCache.Remove(firstKey);
                        }
                        _videoThumbnailCache[cacheKey] = bitmap;
                    }
                    
                    try
                    {
                        if (File.Exists(tempImagePath))
                            File.Delete(tempImagePath);
                    }
                    catch { }
                    
                    return bitmap;
                }
                finally
                {
                    try
                    {
                        if (File.Exists(tempImagePath))
                        {
                            Thread.Sleep(100);
                            File.Delete(tempImagePath);
                        }
                    }
                    catch { }
                }
            }
            catch
            {
                return null;
            }
        }
        
        private static bool IsDirectoryWritable(string directory)
        {
            try
            {
                if (!Directory.Exists(directory))
                    return false;
                    
                string testFile = Path.Combine(directory, Guid.NewGuid().ToString() + ".tmp");
                File.WriteAllText(testFile, "test");
                File.Delete(testFile);
                return true;
            }
            catch
            {
                return false;
            }
        }
        
        private static string GetFFmpegPath()
        {
            try
            {
                var options = GlobalFFOptions.Current;
                if (options != null && !string.IsNullOrEmpty(options.BinaryFolder))
                {
                    string ffmpegPath = Path.Combine(options.BinaryFolder, "ffmpeg.exe");
                    if (File.Exists(ffmpegPath))
                    {
                        return ffmpegPath;
                    }
                }
                
                return "ffmpeg";
            }
            catch
            {
                return "ffmpeg";
            }
        }
        
        private static bool RunFFmpegCommand(string ffmpegPath, string arguments, out string stdout, out string stderr)
        {
            stdout = string.Empty;
            stderr = string.Empty;
            
            try
            {
                var processStartInfo = new ProcessStartInfo
                {
                    FileName = ffmpegPath,
                    Arguments = arguments,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    StandardOutputEncoding = Encoding.UTF8,
                    StandardErrorEncoding = Encoding.UTF8
                };
                
                using (var process = Process.Start(processStartInfo))
                {
                    if (process == null)
                        return false;
                    
                    var stdoutBuilder = new StringBuilder();
                    var stderrBuilder = new StringBuilder();
                    
                    process.OutputDataReceived += (sender, e) =>
                    {
                        if (!string.IsNullOrEmpty(e.Data))
                            stdoutBuilder.AppendLine(e.Data);
                    };
                    
                    process.ErrorDataReceived += (sender, e) =>
                    {
                        if (!string.IsNullOrEmpty(e.Data))
                            stderrBuilder.AppendLine(e.Data);
                    };
                    
                    process.BeginOutputReadLine();
                    process.BeginErrorReadLine();
                    
                    bool finished = process.WaitForExit(30000);
                    
                    if (!finished)
                    {
                        process.Kill();
                        process.WaitForExit(5000);
                        return false;
                    }
                    
                    stdout = stdoutBuilder.ToString().Trim();
                    stderr = stderrBuilder.ToString().Trim();
                    
                    return process.ExitCode == 0;
                }
            }
            catch
            {
                return false;
            }
        }

        [DllImport("gdi32.dll")]
        private static extern bool DeleteObject(IntPtr hObject);

        [StructLayout(LayoutKind.Sequential)]
        private struct SIZE
        {
            public int cx;
            public int cy;
        }

        private BitmapSource GetHighQualitySystemIcon(string path, bool isDirectory, int targetSize = 256)
        {
            try
            {
                Guid shellItemGuid = new Guid("43826d1e-e718-42ee-bc55-a1e261c37bfe");
                IShellItem shellItem = null;
                
                foreach (var tryPath in GetPathsToTry(path))
                            {
                                try
                                {
                        shellItem = SHCreateItemFromParsingName(tryPath, IntPtr.Zero, shellItemGuid);
                        if (shellItem != null)
                            break;
                    }
                    catch { }
                }

                if (shellItem != null)
                {
                    try
                    {
                        IShellItemImageFactory imageFactory = shellItem as IShellItemImageFactory;
                        if (imageFactory != null)
                        {
                            IntPtr hBitmap = IntPtr.Zero;
                            try
                            {
                                int[] flagCombinations = new[]
                                {
                                    SIIGBF_BIGGERSIZEOK | SIIGBF_RESIZETOFIT,  // 标准组合：允许更大尺寸并缩放
                                    SIIGBF_RESIZETOFIT,                         // 仅缩放到目标尺寸
                                    SIIGBF_BIGGERSIZEOK,                       // 仅允许更大尺寸
                                    0                                           // 无标志，使用默认行为
                                };
                                
                                foreach (var flagValue in flagCombinations)
                            {
                                try
                                {
                                        imageFactory.GetImage(new SIZE { cx = targetSize, cy = targetSize }, flagValue, out hBitmap);
                                        
                                        if (hBitmap != IntPtr.Zero)
                                        {
                                            try
                                    {
                                                var bitmapSource = Imaging.CreateBitmapSourceFromHBitmap(
                                                    hBitmap,
                                                    IntPtr.Zero,
                                            System.Windows.Int32Rect.Empty,
                                            BitmapSizeOptions.FromWidthAndHeight(targetSize, targetSize));
                                                
                                                RenderOptions.SetBitmapScalingMode(bitmapSource, BitmapScalingMode.HighQuality);
                                                RenderOptions.SetCachingHint(bitmapSource, CachingHint.Cache);
                                                
                                                if (Math.Abs(bitmapSource.PixelWidth - targetSize) > 1 || Math.Abs(bitmapSource.PixelHeight - targetSize) > 1)
                                                {
                                                    var scaleTransform = new ScaleTransform(
                                                        (double)targetSize / bitmapSource.PixelWidth,
                                                        (double)targetSize / bitmapSource.PixelHeight,
                                                        0, 0);
                                                    
                                                    var scaledBitmap = new TransformedBitmap(bitmapSource, scaleTransform);
                                                    RenderOptions.SetBitmapScalingMode(scaledBitmap, BitmapScalingMode.HighQuality);
                                                    RenderOptions.SetCachingHint(scaledBitmap, CachingHint.Cache);
                                                    scaledBitmap.Freeze();
                                                    
                                                    var temp = hBitmap;
                                                    hBitmap = IntPtr.Zero;
                                                    DeleteObject(temp);
                                                    
                                                    return scaledBitmap;
                                                }
                                                
                                        bitmapSource.Freeze();
                                                
                                                var tempBitmap = hBitmap;
                                                hBitmap = IntPtr.Zero;
                                                DeleteObject(tempBitmap);
                                                
                                        return bitmapSource;
                                    }
                                            catch
                                            {
                                                if (hBitmap != IntPtr.Zero)
                                                {
                                                    DeleteObject(hBitmap);
                                                    hBitmap = IntPtr.Zero;
                                                }
                                            }
                                        }
                                    }
                                    catch
                                    {
                                        if (hBitmap != IntPtr.Zero)
                            {
                                            DeleteObject(hBitmap);
                                            hBitmap = IntPtr.Zero;
                                        }
                                    }
                                }
                            }
                            finally
                            {
                                if (hBitmap != IntPtr.Zero)
                                    DeleteObject(hBitmap);
                                }
                            }
                        }
                        catch { }
                    }
                
                SHFILEINFO shfi = new SHFILEINFO();
                uint flags = SHGFI_SYSICONINDEX | SHGFI_ICON;
                
                if (isDirectory)
                {
                    flags |= SHGFI_USEFILEATTRIBUTES;
                    SHGetFileInfo(path, 0x10, ref shfi, (uint)Marshal.SizeOf(shfi), flags);
                }
                else
                {
                    SHGetFileInfo(path, 0, ref shfi, (uint)Marshal.SizeOf(shfi), flags);
                }

                if (shfi.iIcon != 0)
                {
                    try
                    {
                        Guid iidImageList = new Guid("46EB5926-582E-4017-9FDF-E8998DAA0950");
                        IImageList imageList;
                        
                        int imageListType = SHIL_JUMBO;
                        
                        if (SHGetImageList(imageListType, ref iidImageList, out imageList) == 0)
                        {
                            IntPtr hIcon = IntPtr.Zero;
                            if (imageList.GetIcon(shfi.iIcon, ILD_TRANSPARENT, out hIcon) == 0 && hIcon != IntPtr.Zero)
                            {
                                try
                                {
                                    using (Icon icon = Icon.FromHandle(hIcon))
                        {
                            var bitmapSource = Imaging.CreateBitmapSourceFromHIcon(
                                icon.Handle,
                                System.Windows.Int32Rect.Empty,
                                BitmapSizeOptions.FromWidthAndHeight(targetSize, targetSize));
                                        
                                        RenderOptions.SetBitmapScalingMode(bitmapSource, BitmapScalingMode.HighQuality);
                                        RenderOptions.SetCachingHint(bitmapSource, CachingHint.Cache);
                                        
                                        if (Math.Abs(bitmapSource.PixelWidth - targetSize) > 1 || Math.Abs(bitmapSource.PixelHeight - targetSize) > 1)
                                        {
                                            var scaleTransform = new ScaleTransform(
                                                (double)targetSize / bitmapSource.PixelWidth,
                                                (double)targetSize / bitmapSource.PixelHeight,
                                                0, 0);
                                            
                                            var scaledBitmap = new TransformedBitmap(bitmapSource, scaleTransform);
                                            RenderOptions.SetBitmapScalingMode(scaledBitmap, BitmapScalingMode.HighQuality);
                                            RenderOptions.SetCachingHint(scaledBitmap, CachingHint.Cache);
                                            scaledBitmap.Freeze();
                                            return scaledBitmap;
                                        }
                                        
                            bitmapSource.Freeze();
                            return bitmapSource;
                        }
                    }
                    finally
                    {
                                    DestroyIcon(hIcon);
                                }
                    }
                        }
                    }
                    catch { }
                }
            }
            catch { }
            return null;
        }

        private BitmapSource GenerateThumbnailWithMagick(string filePath, int targetSize)
        {
            try
            {
                if (!File.Exists(filePath))
                    return null;
                
                using var image = new MagickImage(filePath);
                image.Thumbnail(new MagickGeometry((uint)targetSize, (uint)targetSize)
                {
                    IgnoreAspectRatio = false
                });
                
                return ConvertMagickImageToBitmapSource(image);
            }
            catch
            {
                return null;
            }
        }

        private BitmapSource ConvertMagickImageToBitmapSource(MagickImage image)
        {
            try
            {
                var bytes = image.ToByteArray(MagickFormat.Png);
                
                if (bytes == null || bytes.Length == 0)
                    return null;
                
                string tempFile = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString() + ".png");
                try
                {
                    File.WriteAllBytes(tempFile, bytes);
                    
                    var bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.UriSource = new Uri(tempFile, UriKind.Absolute);
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.EndInit();
                    bitmap.Freeze();
                    
                    try
                    {
                        File.Delete(tempFile);
                    }
                    catch { }
                    
                    return bitmap;
                }
                catch
                {
                    try
                    {
                        var stream = new MemoryStream(bytes);
                        var bitmap = new BitmapImage();
                        bitmap.BeginInit();
                        bitmap.StreamSource = stream;
                        bitmap.CacheOption = BitmapCacheOption.OnLoad;
                        bitmap.EndInit();
                        bitmap.Freeze();
                        return bitmap;
                    }
                    catch
                    {
                        return null;
                    }
                }
            }
            catch
            {
                return null;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}



