using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Windows.Media.Imaging;

namespace YiboFile.Services
{
    public static class CadImageCache
    {
        private static string Root
        {
            get
            {
                var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "YiboFile", "Cache", "CAD");
                Directory.CreateDirectory(path);
                return path;
            }
        }

        private static string Key(string filePath, int size)
        {
            var info = new System.IO.FileInfo(filePath);
            var s = $"{filePath}|{size}|{(info.Exists ? info.LastWriteTimeUtc.Ticks : 0)}";
            using var sha = SHA256.Create();
            var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(s));
            return BitConverter.ToString(bytes).Replace("-", "").ToLowerInvariant();
        }

        public static BitmapSource Get(string filePath, int size)
        {
            var k = Key(filePath, size);
            var p = Path.Combine(Root, k + ".png");
            if (!File.Exists(p)) return null;
            try
            {
                var bi = new BitmapImage();
                bi.BeginInit();
                bi.CacheOption = BitmapCacheOption.OnLoad;
                bi.UriSource = new Uri(p);
                bi.EndInit();
                bi.Freeze();
                return bi;
            }
            catch { return null; }
        }

        public static void Put(BitmapSource bmp, string filePath, int size)
        {
            try
            {
                var k = Key(filePath, size);
                var p = Path.Combine(Root, k + ".png");
                using var fs = new FileStream(p, FileMode.Create, FileAccess.Write);
                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(bmp));
                encoder.Save(fs);
            }
            catch { }
        }
    }
}


