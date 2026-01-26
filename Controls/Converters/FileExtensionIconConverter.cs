using System;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Windows.Data;
using System.Windows.Media.Imaging;
using System.Windows.Interop;
using System.Windows.Media;
using System.Drawing;

namespace YiboFile.Controls.Converters
{
    /// <summary>
    /// 文件扩展名图标转换器
    /// 用于获取文件格式的小图标（用于缩略图左下角显示）
    /// 使用 IShellItemImageFactory 获取高质量图标
    /// </summary>
    public class FileExtensionIconConverter : IValueConverter
    {
        [DllImport("shell32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr SHGetFileInfo(string pszPath, uint dwFileAttributes, ref SHFILEINFO psfi, uint cbSizeFileInfo, uint uFlags);

        [DllImport("user32.dll")]
        private static extern bool DestroyIcon(IntPtr hIcon);

        [DllImport("shell32.dll")]
        private static extern int SHGetImageList(int iImageList, ref Guid riid, out IImageList ppv);

        [DllImport("shell32.dll", CharSet = CharSet.Unicode, PreserveSig = false)]
        [return: MarshalAs(UnmanagedType.Interface)]
        private static extern IShellItem SHCreateItemFromParsingName(
            [MarshalAs(UnmanagedType.LPWStr)] string pszPath,
            IntPtr pbc,
            [MarshalAs(UnmanagedType.LPStruct)] Guid riid);

        [DllImport("gdi32.dll")]
        private static extern bool DeleteObject(IntPtr hObject);

        private const uint SHGFI_ICON = 0x100;
        private const uint SHGFI_SMALLICON = 0x1;
        private const uint SHGFI_SYSICONINDEX = 0x4000;
        private const uint SHGFI_USEFILEATTRIBUTES = 0x10;
        private const int ILD_TRANSPARENT = 0x1;
        private const int SHIL_SMALL = 0x1;
        private const int SHIL_LARGE = 0x0;
        private const int SHIL_EXTRALARGE = 0x2;
        private const int SIIGBF_BIGGERSIZEOK = 0x2;
        private const int SIIGBF_RESIZETOFIT = 0x4;

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
        [Guid("46EB5926-582E-4017-9FDF-E8998DAA0950")]
        private interface IImageList
        {
            [PreserveSig]
            int GetIcon(int i, int flags, out IntPtr picon);
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

        [StructLayout(LayoutKind.Sequential)]
        private struct SIZE
        {
            public int cx;
            public int cy;
        }

        // Office文档扩展名列表
        private static readonly string[] OfficeExtensions = {
            ".doc", ".docx", ".docm", ".dot", ".dotx", ".dotm",
            ".xls", ".xlsx", ".xlsm", ".xlt", ".xltx", ".xltm",
            ".ppt", ".pptx", ".pptm", ".pot", ".potx", ".potm",
            ".odt", ".ods", ".odp"
        };

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            try
            {
                var path = value as string;
                if (string.IsNullOrEmpty(path))
                    return null;

                // 从parameter获取缩略图大小或图标大小
                // 如果parameter是double/int且较大（>100），则认为是缩略图大小，需要计算图标大小
                // 否则认为是直接的图标大小
                int iconSize = 16; // 默认16x16
                
                if (parameter != null)
                {
                    // 尝试作为缩略图大小处理（可能是double或int）
                    double thumbnailSize = 0;
                    if (parameter is double dSize)
                        thumbnailSize = dSize;
                    else if (parameter is int iSize)
                        thumbnailSize = iSize;
                    else if (double.TryParse(parameter.ToString(), out double parsedSize))
                        thumbnailSize = parsedSize;

                    if (thumbnailSize > 100)
                    {
                        // 这是缩略图大小，需要根据文件类型计算图标大小
                        var ext = Path.GetExtension(path)?.ToLowerInvariant();
                        bool isOfficeDocument = !string.IsNullOrEmpty(ext) && Array.IndexOf(OfficeExtensions, ext) >= 0;
                        
                        // Office文档使用10%，其他使用15%
                        double ratio = isOfficeDocument ? 0.10 : 0.15;
                        iconSize = (int)(thumbnailSize * ratio);
                        
                        // 应用范围限制（测试用：2-30px）
                        iconSize = Math.Max(2, Math.Min(30, iconSize));
                    }
                    else if (thumbnailSize > 0)
                    {
                        // 这是直接的图标大小
                        iconSize = (int)thumbnailSize;
                    }
                }

                bool isDirectory = Directory.Exists(path);
                bool isFile = File.Exists(path);

                if (!isDirectory && !isFile)
                    return null;

                return GetFileExtensionIcon(path, isDirectory, iconSize);
            }
            catch
            {
                return null;
            }
        }

        private BitmapSource GetFileExtensionIcon(string path, bool isDirectory, int size)
        {
            try
            {
                // 方案1：使用 IShellItemImageFactory 获取最高质量的图标（Windows Vista+）
                Guid shellItemGuid = new Guid("43826d1e-e718-42ee-bc55-a1e261c37bfe"); // IShellItem
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
                        shellItem = SHCreateItemFromParsingName(tryPath, IntPtr.Zero, shellItemGuid);
                        if (shellItem != null)
                            break;
                    }
                    catch (COMException)
                    {
                        // COM 互操作异常，尝试下一个路径格式
                    }
                    catch
                    {
                        // 其他异常，尝试下一个路径格式
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
                                
                                // 对于小尺寸图标，请求更大的尺寸然后高质量缩放（类似Office文档的处理方式）
                                // 这样可以获得更清晰的图标，即使最终显示尺寸很小
                                int requestSize = Math.Max(size * 2, 32); // 请求至少32px或2倍目标尺寸，确保有足够的细节
                                
                                foreach (var flagValue in flagCombinations)
                                {
                                    try
                                    {
                                        // 请求更大的尺寸，然后高质量缩放到目标尺寸
                                        imageFactory.GetImage(new SIZE { cx = requestSize, cy = requestSize }, flagValue, out hBitmap);
                                        
                                        if (hBitmap != IntPtr.Zero)
                                        {
                                            try
                                            {
                                                var bitmapSource = Imaging.CreateBitmapSourceFromHBitmap(
                                                    hBitmap,
                                                    IntPtr.Zero,
                                                    System.Windows.Int32Rect.Empty,
                                                    BitmapSizeOptions.FromWidthAndHeight(requestSize, requestSize));
                                                
                                                // 设置高质量渲染选项
                                                RenderOptions.SetBitmapScalingMode(bitmapSource, BitmapScalingMode.HighQuality);
                                                RenderOptions.SetCachingHint(bitmapSource, CachingHint.Cache);
                                                
                                                // 强制缩放到目标尺寸，确保所有格式（包括Office文档）都使用相同大小
                                                // 即使获取的尺寸与目标尺寸相同，也进行缩放以确保一致性
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
                                                    
                                                    // 释放原始位图
                                                    var tempBitmap = hBitmap;
                                                    hBitmap = IntPtr.Zero;
                                                    DeleteObject(tempBitmap);
                                                    
                                                    return scaledBitmap;
                                                }
                                                
                                                // 如果尺寸正好（误差在1px内），直接使用
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
                    catch (COMException)
                    {
                        // COM 互操作异常，继续使用回退方案
                    }
                    catch { }
                }
                
                // 方案2：如果方案1失败，使用SHGetImageList作为回退（确保始终有图标显示）
                SHFILEINFO shfi = new SHFILEINFO();
                uint flags = SHGFI_SYSICONINDEX | SHGFI_ICON | SHGFI_SMALLICON;

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
                    // 优先尝试获取EXTRA_LARGE尺寸图标（48x48），然后高质量缩放
                    try
                    {
                        Guid iidImageList = new Guid("46EB5926-582E-4017-9FDF-E8998DAA0950");
                        IImageList imageList;
                        
                        // 使用EXTRA_LARGE尺寸（48x48），然后高质量缩放到目标尺寸
                        int imageListType = SHIL_EXTRALARGE;
                        
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
                                        var bitmapSource = Imaging.CreateBitmapSourceFromHIcon(
                                            icon.Handle,
                                            System.Windows.Int32Rect.Empty,
                                            BitmapSizeOptions.FromWidthAndHeight(size, size));
                                        
                                        // 设置高质量渲染选项
                                        RenderOptions.SetBitmapScalingMode(bitmapSource, BitmapScalingMode.HighQuality);
                                        RenderOptions.SetCachingHint(bitmapSource, CachingHint.Cache);
                                        
                                        // 如果图标尺寸小于目标尺寸，使用高质量缩放
                                        if (bitmapSource.PixelWidth < size || bitmapSource.PixelHeight < size)
                                        {
                                            var scaleTransform = new ScaleTransform(
                                                (double)size / bitmapSource.PixelWidth,
                                                (double)size / bitmapSource.PixelHeight,
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
                    catch (COMException)
                    {
                        // COM 互操作异常，继续使用回退方案
                    }
                    catch { }
                    
                    // 如果EXTRA_LARGE失败，尝试使用SMALL或LARGE
                    try
                    {
                        Guid iidImageList = new Guid("46EB5926-582E-4017-9FDF-E8998DAA0950");
                        IImageList imageList;
                        int imageListType = size <= 16 ? SHIL_SMALL : SHIL_LARGE;
                        
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
                                        var bitmapSource = Imaging.CreateBitmapSourceFromHIcon(
                                            icon.Handle,
                                            System.Windows.Int32Rect.Empty,
                                            BitmapSizeOptions.FromWidthAndHeight(size, size));
                                        
                                        // 设置高质量渲染选项
                                        RenderOptions.SetBitmapScalingMode(bitmapSource, BitmapScalingMode.HighQuality);
                                        RenderOptions.SetCachingHint(bitmapSource, CachingHint.Cache);
                                        
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
                    catch (COMException)
                    {
                        // COM 互操作异常，继续使用回退方案
                    }
                    catch { }
                }

                // 回退方案：使用SHGetFileInfo获取的图标
                if (shfi.hIcon != IntPtr.Zero)
                {
                    try
                    {
                        using (Icon icon = Icon.FromHandle(shfi.hIcon))
                        {
                            var bitmapSource = Imaging.CreateBitmapSourceFromHIcon(
                                icon.Handle,
                                System.Windows.Int32Rect.Empty,
                                BitmapSizeOptions.FromWidthAndHeight(size, size));
                            
                            // 设置高质量渲染选项
                            RenderOptions.SetBitmapScalingMode(bitmapSource, BitmapScalingMode.HighQuality);
                            RenderOptions.SetCachingHint(bitmapSource, CachingHint.Cache);
                            
                            bitmapSource.Freeze();
                            return bitmapSource;
                        }
                    }
                    finally
                    {
                        DestroyIcon(shfi.hIcon);
                    }
                }
            }
            catch (COMException)
            {
                // COM 互操作异常，返回 null
            }
            catch { }
            return null;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}


