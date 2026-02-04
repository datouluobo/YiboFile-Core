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

using ImageMagick;

namespace YiboFile.Controls.Converters
{
    public class ThumbnailConverter : IValueConverter
    {
        // 视频缩略图缓存
        private static readonly Dictionary<string, BitmapSource> _videoThumbnailCache = new();
        private static readonly object _cacheLock = new object();
        private const int MaxCacheSize = 50; // 从100降到50，减少内存占用
        private static readonly Dictionary<string, BitmapSource> _imageThumbnailCache = new();
        private const int MaxImageCacheSize = 100; // 从200降到100，减少内存占用


        // 立即加载的文件路径集合（第一页文件）
        private static readonly HashSet<string> _priorityLoadPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private static readonly object _priorityLock = new object();

        // 标记 Magick.NET 是否可用，防止重复尝试损坏的组件
        private static bool _isMagickDisabled = false;

        // 占位符图片缓存（按大小）
        private static readonly Dictionary<int, BitmapSource> _placeholderCache = new Dictionary<int, BitmapSource>();
        [DllImport("shell32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr SHGetFileInfo(string pszPath, uint dwFileAttributes, ref SHFILEINFO psfi, uint cbSizeFileInfo, uint uFlags);

        [DllImport("user32.dll")]
        private static extern bool DestroyIcon(IntPtr hIcon);

        [DllImport("shell32.dll", CharSet = CharSet.Unicode, PreserveSig = true)]
        private static extern int SHCreateItemFromParsingName(
            [MarshalAs(UnmanagedType.LPWStr)] string pszPath,
            IntPtr pbc,
            [MarshalAs(UnmanagedType.LPStruct)] Guid riid,
            out IShellItem ppv);

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
            [PreserveSig]
            int BindToHandler(IntPtr pbc, [MarshalAs(UnmanagedType.LPStruct)] Guid bhid, [MarshalAs(UnmanagedType.LPStruct)] Guid riid, out IntPtr ppv);
            [PreserveSig]
            int GetParent(out IShellItem ppsi);
            [PreserveSig]
            int GetDisplayName(uint sigdnName, out IntPtr ppszName);
            [PreserveSig]
            int GetAttributes(uint sfgaoMask, out uint psfgaoAttribs);
            [PreserveSig]
            int Compare(IShellItem psi, uint hint, out int piOrder);
        }

        [ComImport]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        [Guid("bcc18b79-ba16-442f-80c4-8a59c30c463b")]
        private interface IShellItemImageFactory
        {
            [PreserveSig]
            int GetImage([MarshalAs(UnmanagedType.Struct)] SIZE size, int flags, out IntPtr phbm);
        }

        [ComImport]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        [Guid("46EB5926-582E-4017-9FDF-E8998DAA0950")]
        private interface IImageList
        {
            [PreserveSig]
            int GetIcon(int i, int flags, out IntPtr picon);
        }

        /// <summary>
        /// 设置需要立即加载的文件路径（第一页文件）
        /// </summary>
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

        /// <summary>
        /// 清除优先加载列表
        /// </summary>
        public static void ClearPriorityLoadPaths()
        {
            lock (_priorityLock)
            {
                _priorityLoadPaths.Clear();
            }
        }

        /// <summary>
        /// 创建占位符图片
        /// </summary>
        private BitmapSource CreatePlaceholder(int size)
        {
            lock (_cacheLock)
            {
                if (_placeholderCache.TryGetValue(size, out var cached))
                    return cached;

                // 创建一个简单的灰色占位符
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

        /// <summary>
        /// 异步加载缩略图并更新UI
        /// </summary>
        private void LoadThumbnailAsync(string path, DependencyObject target, int targetSize)
        {
            Task.Run(() =>
        {
            try
            {
                BitmapSource thumbnail = LoadThumbnailSync(path, targetSize);
                if (thumbnail != null)
                {
                    // 在UI线程更新
                    Application.Current?.Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() =>
                    {
                        try
                        {
                            // 查找Image控件并更新Source
                            if (target is System.Windows.Controls.Image image)
                            {
                                image.Source = thumbnail;
                            }
                            else if (target is FrameworkElement fe)
                            {
                                // 尝试查找子元素中的Image
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

        /// <summary>
        /// 查找视觉树中的子元素
        /// </summary>
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

        /// <summary>
        /// 同步加载缩略图（内部方法，供外部调用）
        /// </summary>
        internal BitmapSource LoadThumbnailSync(string path, int targetSize)
        {
            try
            {
                if (string.IsNullOrEmpty(path)) return null;

                // 快速基本检查
                string fileName = Path.GetFileName(path);
                if (string.IsNullOrEmpty(fileName)) return null;

                bool isDirectory = Directory.Exists(path);
                bool isFile = File.Exists(path);

                if (!isDirectory && !isFile)
                    return null;

                // 如果是图片文件，直接加载图片
                if (isFile)
                {
                    var ext = Path.GetExtension(path)?.ToLowerInvariant();

                    // 图片格式：直接加载
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
                                // 使用文件流加载，避免URI路径解析问题，并设置共享模式
                                if (!File.Exists(path))
                                {
                                    return null;
                                }

                                var bmp = new BitmapImage();
                                using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                                {
                                    bmp.BeginInit();
                                    bmp.StreamSource = fs;
                                    bmp.CacheOption = BitmapCacheOption.OnLoad;
                                    bmp.DecodePixelWidth = targetSize;
                                    bmp.EndInit();
                                }
                                bmp.Freeze();
                                return bmp;
                            }
                            catch
                            {
                                return null;
                            }
                        case ".svg":
                        case ".psd":
                            try
                            {
                                var fi = new FileInfo(path);
                                if (!fi.Exists)
                                {
                                    return null;
                                }
                                var key = $"{path}_{targetSize}_{fi.LastWriteTimeUtc.Ticks}";
                                lock (_cacheLock)
                                {
                                    if (_imageThumbnailCache.TryGetValue(key, out var cached))
                                    {
                                        return cached;
                                    }
                                }
                                if (_isMagickDisabled) return null;

                                BitmapSource result = null;
                                try
                                {
                                    result = DecodeWithMagickSafe(path, targetSize);
                                }
                                catch (Exception ex)
                                {
                                    System.Diagnostics.Debug.WriteLine($"Magick error for {path}: {ex.Message}");
                                    // 如果是严重的初始化问题，禁用 Magick
                                    if (ex is TypeInitializationException || ex is DllNotFoundException)
                                    {
                                        _isMagickDisabled = true;
                                    }
                                    return null;
                                }

                                if (result != null)
                                {
                                    lock (_cacheLock)
                                    {
                                        if (_imageThumbnailCache.Count >= MaxImageCacheSize)
                                        {
                                            var firstKey = _imageThumbnailCache.Keys.First();
                                            _imageThumbnailCache.Remove(firstKey);
                                        }
                                        _imageThumbnailCache[key] = result;
                                    }
                                }
                                return result;
                            }
                            catch (Exception)
                            {
                                return null;
                            }

                        // 视频格式：交由 Shell 处理 (GetShellThumbnail)
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
                            break; // Fall through to generic shell handler

                    }
                }

                if (isFile && !isDirectory)
                {
                    try
                    {
                        var ext = Path.GetExtension(path)?.ToLowerInvariant();
                        if (ext == ".dxf")
                        {
                            var cached = YiboFile.Services.CadImageCache.Get(path, targetSize);
                            if (cached != null) return cached;
                            // Keep using DxfRenderEngine for thumbnails as it produces a bitmap directly
                            var bmp = YiboFile.Rendering.DxfRenderEngine.Render(path, "Model", targetSize, targetSize);
                            if (bmp != null)
                            {
                                YiboFile.Services.CadImageCache.Put(bmp, path, targetSize);
                                return bmp;
                            }
                        }
                        if (ext == ".dwg")
                        {
                            var cached = YiboFile.Services.CadImageCache.Get(path, targetSize);
                            if (cached != null) return cached;
                            var ph = CreatePlaceholder(targetSize);
                            if (ph != null)
                            {
                                YiboFile.Services.CadImageCache.Put(ph, path, targetSize);
                                return ph;
                            }
                        }
                        var thumbnail = GetShellThumbnail(path, targetSize);
                        if (thumbnail != null) return thumbnail;
                    }
                    catch { }
                }

                // 对于文件夹和无法获取缩略图的文件类型，使用系统图标
                // 如果系统缩略图缓存获取失败，也会回退到这里
                try
                {
                    var icon = GetHighQualitySystemIcon(path, isDirectory, targetSize);
                    // 如果获取图标失败，返回占位符而不是null
                    return icon ?? CreatePlaceholder(targetSize);
                }
                catch
                {
                    // 异常时也返回占位符
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

                // 检查是否为文件夹
                bool isDirectory = Directory.Exists(path);
                bool isFile = File.Exists(path);

                if (!isDirectory && !isFile)
                    return null;

                // 根据实际显示大小生成缩略图（提高性能，避免卡顿）
                // 从parameter获取目标尺寸，如果没有则使用默认值
                int targetSize = 256; // 默认256像素，平衡质量和性能
                if (parameter != null)
                {
                    if (parameter is int size)
                        targetSize = size;
                    else if (int.TryParse(parameter.ToString(), out int parsedSize))
                        targetSize = parsedSize;
                }

                // 检查是否在优先加载列表中
                bool isPriorityLoad = false;
                lock (_priorityLock)
                {
                    isPriorityLoad = _priorityLoadPaths.Contains(path);
                }

                // 如果是优先加载，立即同步加载
                if (isPriorityLoad)
                {
                    var thumbnail = LoadThumbnailSync(path, targetSize);
                    // 如果加载失败，返回占位符而不是null
                    return thumbnail ?? CreatePlaceholder(targetSize);
                }

                // 否则先返回占位符，然后异步加载
                // 注意：这里我们需要获取目标控件来更新，但IValueConverter无法直接获取
                // 所以我们先返回占位符，然后在FileBrowserControl中处理异步加载
                return CreatePlaceholder(targetSize);
            }
            catch
            {
                return null;
            }
        }

        private string NormalizePath(string path)
        {
            try
            {
                if (string.IsNullOrEmpty(path))
                    return null;

                // 确保路径是绝对路径
                if (!Path.IsPathRooted(path))
                    return null;

                // 转换为长路径格式（\\?\）以提高兼容性
                if (path.Length >= 260 && !path.StartsWith(@"\\?\"))
                {
                    if (path.StartsWith(@"\\"))
                        return @"\\?\UNC\" + path.Substring(2);
                    else
                        return @"\\?\" + path;
                }

                return path;
            }
            catch
            {
                return path; // 如果转换失败，返回原路径
            }
        }

        /// <summary>
        /// 获取系统缩略图缓存（用于Office文档等支持缩略图的文件）
        /// 优先使用 SIIGBF_THUMBNAILONLY 标志获取系统缓存的缩略图预览
        /// </summary>
        private BitmapSource GetShellThumbnail(string path, int size)
        {
            IntPtr hBitmap = IntPtr.Zero;
            try
            {
                // 确保路径格式正确
                if (string.IsNullOrEmpty(path))
                    return null;

                Guid shellItemGuid = new Guid("43826d1e-e718-42ee-bc55-a1e261c37bfe"); // IShellItem
                IShellItem shellItem = null;

                // 尝试多种路径格式（处理长路径和UNC路径）
                string[] pathsToTry = { path };
                if (path.StartsWith(@"\\?\"))
                {
                    pathsToTry = new[] { path, path.Substring(4) };
                }
                else if (path.Length >= 260)
                {
                    // 长路径，尝试添加长路径前缀
                    if (path.StartsWith(@"\\"))
                        pathsToTry = new[] { path, @"\\?\UNC" + path.Substring(1) };
                    else
                        pathsToTry = new[] { path, @"\\?\" + path };
                }

                foreach (var tryPath in pathsToTry)
                {
                    try
                    {
                        int hr = SHCreateItemFromParsingName(tryPath, IntPtr.Zero, shellItemGuid, out shellItem);
                        if (hr >= 0 && shellItem != null)
                            break;
                    }
                    catch
                    {
                        // 异常时尝试下一个路径格式
                    }
                }

                if (shellItem != null)
                {
                    IShellItemImageFactory imageFactory = shellItem as IShellItemImageFactory;
                    if (imageFactory != null)
                    {
                        // 尝试多种标志组合，优先获取系统缩略图缓存
                        // 注意：SIIGBF_THUMBNAILONLY 标志要求必须存在系统缩略图缓存，否则会失败
                        int[] flagCombinations = new[]
                        {
                            SIIGBF_THUMBNAILONLY | SIIGBF_BIGGERSIZEOK | SIIGBF_RESIZETOFIT,  // 优先：仅缩略图，允许更大尺寸并缩放
                            SIIGBF_THUMBNAILONLY | SIIGBF_RESIZETOFIT,                         // 仅缩略图，缩放到目标尺寸
                            SIIGBF_THUMBNAILONLY | SIIGBF_BIGGERSIZEOK,                       // 仅缩略图，允许更大尺寸
                            SIIGBF_THUMBNAILONLY,                                             // 仅缩略图，无其他标志
                            // 注意：不使用 SIIGBF_BIGGERSIZEOK | SIIGBF_RESIZETOFIT，因为这会返回图标而不是缩略图
                            // 如果缩略图缓存不存在，应该回退到 GetHighQualitySystemIcon
                        };

                        foreach (var flags in flagCombinations)
                        {
                            try
                            {
                                // 对于系统缩略图缓存，直接请求目标尺寸
                                // 系统缩略图缓存通常是256x256或512x512，直接请求目标尺寸即可
                                // 如果请求太大，可能会失败
                                int hr = imageFactory.GetImage(new SIZE { cx = size, cy = size }, flags, out hBitmap);

                                if (hr == 0 && hBitmap != IntPtr.Zero)
                                {
                                    try
                                    {
                                        // 先获取原始尺寸图像，不在创建时强制缩放
                                        var bitmapSource = Imaging.CreateBitmapSourceFromHBitmap(
                                            hBitmap,
                                            IntPtr.Zero,
                                            System.Windows.Int32Rect.Empty,
                                            BitmapSizeOptions.FromEmptyOptions()); // 使用原始尺寸

                                        // 设置高质量渲染选项
                                        RenderOptions.SetBitmapScalingMode(bitmapSource, BitmapScalingMode.HighQuality);
                                        RenderOptions.SetCachingHint(bitmapSource, CachingHint.Cache);

                                        // 始终进行高质量缩放到目标尺寸
                                        if (bitmapSource.PixelWidth != size || bitmapSource.PixelHeight != size)
                                        {
                                            var scaleTransform = new ScaleTransform(
                                                (double)size / bitmapSource.PixelWidth,
                                                (double)size / bitmapSource.PixelHeight,
                                                0, 0);

                                            var scaledBitmap = new TransformedBitmap(bitmapSource, scaleTransform);
                                            RenderOptions.SetBitmapScalingMode(scaledBitmap, BitmapScalingMode.HighQuality);
                                            RenderOptions.SetCachingHint(scaledBitmap, CachingHint.Cache);
                                            scaledBitmap.Freeze();

                                            // 释放原始位图
                                            var tempBitmap1 = hBitmap;
                                            hBitmap = IntPtr.Zero;
                                            DeleteObject(tempBitmap1);

                                            return scaledBitmap;
                                        }

                                        // 如果尺寸正好，直接使用
                                        bitmapSource.Freeze();

                                        // 释放原始位图
                                        var tempBitmap2 = hBitmap;
                                        hBitmap = IntPtr.Zero;
                                        DeleteObject(tempBitmap2);

                                        return bitmapSource;
                                    }
                                    catch (COMException)
                                    {
                                        if (hBitmap != IntPtr.Zero)
                                        {
                                            DeleteObject(hBitmap);
                                            hBitmap = IntPtr.Zero;
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
                // 只有在失败时才释放位图句柄
                if (hBitmap != IntPtr.Zero)
                    DeleteObject(hBitmap);
            }
            return null;
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
                // 方案1：使用 IShellItemImageFactory 获取最高质量的图标（Windows Vista+）
                // 这个方法可以获取系统提供的最佳质量图标，包括高DPI支持
                Guid shellItemGuid = new Guid("43826d1e-e718-42ee-bc55-a1e261c37bfe"); // IShellItem
                Guid imageFactoryGuid = new Guid("bcc18b79-ba16-442f-80c4-8a59c30c463b"); // IShellItemImageFactory
                IShellItem shellItem = null;

                // 尝试多种路径格式
                string[] pathsToTry = { path };
                if (path.StartsWith(@"\\?\"))
                {
                    pathsToTry = new[] { path, path.Substring(4) };
                }
                else if (path.Length >= 260)
                {
                    // 长路径，尝试添加长路径前缀
                    if (path.StartsWith(@"\\"))
                        pathsToTry = new[] { path, @"\\?\UNC" + path.Substring(1) };
                    else
                        pathsToTry = new[] { path, @"\\?\" + path };
                }

                foreach (var tryPath in pathsToTry)
                {
                    try
                    {
                        int hr = SHCreateItemFromParsingName(tryPath, IntPtr.Zero, shellItemGuid, out shellItem);
                        if (hr >= 0 && shellItem != null)
                            break;
                    }
                    catch
                    {
                        // 异常时尝试下一个路径格式
                    }
                }

                if (shellItem != null)
                {
                    try
                    {
                        // 直接转换获取 IShellItemImageFactory（C# 的 as 操作符会自动调用 QueryInterface）
                        IShellItemImageFactory imageFactory = shellItem as IShellItemImageFactory;
                        if (imageFactory != null)
                        {
                            IntPtr hBitmap = IntPtr.Zero;
                            try
                            {
                                // 尝试多种标志组合，找到最合适的
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
                                                // 先获取原始尺寸图像，不在创建时强制缩放
                                                var bitmapSource = Imaging.CreateBitmapSourceFromHBitmap(
                                                    hBitmap,
                                                    IntPtr.Zero,
                                                    System.Windows.Int32Rect.Empty,
                                                    BitmapSizeOptions.FromEmptyOptions()); // 使用原始尺寸

                                                // 设置高质量渲染选项
                                                RenderOptions.SetBitmapScalingMode(bitmapSource, BitmapScalingMode.HighQuality);
                                                RenderOptions.SetCachingHint(bitmapSource, CachingHint.Cache);

                                                // 始终进行高质量缩放到目标尺寸
                                                if (bitmapSource.PixelWidth != targetSize || bitmapSource.PixelHeight != targetSize)
                                                {
                                                    var scaleTransform = new ScaleTransform(
                                                        (double)targetSize / bitmapSource.PixelWidth,
                                                        (double)targetSize / bitmapSource.PixelHeight,
                                                        0, 0);

                                                    var scaledBitmap = new TransformedBitmap(bitmapSource, scaleTransform);
                                                    RenderOptions.SetBitmapScalingMode(scaledBitmap, BitmapScalingMode.HighQuality);
                                                    RenderOptions.SetCachingHint(scaledBitmap, CachingHint.Cache);
                                                    scaledBitmap.Freeze();

                                                    // 释放原始位图
                                                    var temp = hBitmap;
                                                    hBitmap = IntPtr.Zero;
                                                    DeleteObject(temp);

                                                    return scaledBitmap;
                                                }

                                                // 如果尺寸正好，直接使用
                                                bitmapSource.Freeze();

                                                // 释放原始位图
                                                var tempBitmap = hBitmap;
                                                hBitmap = IntPtr.Zero;
                                                DeleteObject(tempBitmap);

                                                return bitmapSource;
                                            }
                                            catch (COMException)
                                            {
                                                if (hBitmap != IntPtr.Zero)
                                                {
                                                    DeleteObject(hBitmap);
                                                    hBitmap = IntPtr.Zero;
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
                                    catch (COMException)
                                    {
                                        if (hBitmap != IntPtr.Zero)
                                        {
                                            DeleteObject(hBitmap);
                                            hBitmap = IntPtr.Zero;
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
            }
            catch (COMException)
            {
                // COM 互操作异常，继续使用回退方案
            }
            catch { }

            // 方案2：如果方案1失败，使用SHGetImageList作为回退（确保始终有图标显示）
            try
            {
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

                        // 使用JUMBO尺寸（256x256）
                        int imageListType = SHIL_JUMBO;

                        int result = SHGetImageList(imageListType, ref iidImageList, out imageList);
                        if (result == 0 && imageList != null)
                        {
                            IntPtr hIcon = IntPtr.Zero;
                            if (imageList.GetIcon(shfi.iIcon, ILD_TRANSPARENT, out hIcon) == 0 && hIcon != IntPtr.Zero)
                            {
                                try
                                {
                                    using (Icon icon = Icon.FromHandle(hIcon))
                                    {
                                        // 先获取完整尺寸的图标（不在创建时缩放，避免质量损失）
                                        var bitmapSource = Imaging.CreateBitmapSourceFromHIcon(
                                            icon.Handle,
                                            System.Windows.Int32Rect.Empty,
                                            BitmapSizeOptions.FromEmptyOptions()); // 使用原始尺寸

                                        // 设置高质量渲染选项
                                        RenderOptions.SetBitmapScalingMode(bitmapSource, BitmapScalingMode.HighQuality);
                                        RenderOptions.SetCachingHint(bitmapSource, CachingHint.Cache);

                                        // 始终进行高质量缩放到目标尺寸
                                        if (bitmapSource.PixelWidth != targetSize || bitmapSource.PixelHeight != targetSize)
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

                                        // 如果尺寸正好，直接使用
                                        bitmapSource.Freeze();
                                        return bitmapSource;
                                    }
                                }
                                finally
                                {
                                    if (hIcon != IntPtr.Zero) DestroyIcon(hIcon);
                                }
                            }
                        }
                        return null; // 遍历完所有比例仍未找到合适的图标
                    }
                    catch (COMException)
                    {
                        // COM 互操作异常，继续使用回退方案
                    }
                    catch { }
                }
            }
            catch (COMException)
            {
                // COM 互操作异常，返回 null
            }
            catch { }
            return null;
        }

        /// <summary>
        /// 安全地使用 Magick.NET 解码图片。
        /// 将其放在独立方法中可以防止 JIT 编译器在类加载时就因为缺少原生 DLL 而崩溃。
        /// </summary>
        private BitmapSource DecodeWithMagickSafe(string path, int targetSize)
        {
            using (var mi = new MagickImage(path))
            {
                var w = mi.Width;
                var h = mi.Height;
                int tw = targetSize;
                int th = targetSize;
                if (w >= h)
                {
                    mi.Resize(new MagickGeometry((uint)tw, (uint)0) { IgnoreAspectRatio = false });
                }
                else
                {
                    mi.Resize(new MagickGeometry((uint)0, (uint)th) { IgnoreAspectRatio = false });
                }
                var bytes = mi.ToByteArray(MagickFormat.Png);
                var bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.StreamSource = new MemoryStream(bytes);
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.EndInit();
                bmp.Freeze();
                return bmp;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}

