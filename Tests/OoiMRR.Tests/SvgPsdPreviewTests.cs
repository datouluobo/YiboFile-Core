using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using ImageMagick;
using OoiMRR.Previews;
using OoiMRR.Controls.Converters;
using Xunit;

namespace OoiMRR.Tests
{
    public class SvgPsdPreviewTests
    {
        [Fact]
        public void SvgPreview_RendersImage()
        {
            var tempSvg = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid():N}.svg");
            File.WriteAllText(tempSvg, "<svg xmlns='http://www.w3.org/2000/svg' width='400' height='300'><rect x='10' y='10' width='380' height='280' fill='red'/><text x='20' y='50' font-size='24'>SVG</text></svg>");
            Exception ex = null;
            var t = new System.Threading.Thread(() =>
            {
                try
                {
                    var preview = new ImagePreview();
                    var ui = preview.CreatePreview(tempSvg);
                    Assert.NotNull(ui);
                    var image = FindImage(ui);
                    Assert.NotNull(image);
                    var src = image.Source as BitmapSource;
                    Assert.NotNull(src);
                    Assert.True(src.PixelWidth > 0);
                }
                catch (Exception e) { ex = e; }
            });
            t.SetApartmentState(System.Threading.ApartmentState.STA);
            t.Start();
            t.Join();
            TryDelete(tempSvg);
            if (ex != null) throw ex;
        }

        [Fact]
        public void PsdPreview_RendersImage()
        {
            var tempPsd = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid():N}.psd");
            using (var mi = new MagickImage("xc:white", 800, 600))
            {
                mi.Format = MagickFormat.Psd;
                mi.Write(tempPsd);
            }
            Exception ex = null;
            var t = new System.Threading.Thread(() =>
            {
                try
                {
                    var preview = new ImagePreview();
                    var ui = preview.CreatePreview(tempPsd);
                    Assert.NotNull(ui);
                    var image = FindImage(ui);
                    Assert.NotNull(image);
                    var src = image.Source as BitmapSource;
                    Assert.NotNull(src);
                    Assert.True(src.PixelWidth > 0);
                }
                catch (Exception e) { ex = e; }
            });
            t.SetApartmentState(System.Threading.ApartmentState.STA);
            t.Start();
            t.Join();
            TryDelete(tempPsd);
            if (ex != null) throw ex;
        }

        [Fact]
        public void SvgAndPsdThumbnail_GeneratesAndCaches()
        {
            var tempSvg = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid():N}.svg");
            File.WriteAllText(tempSvg, "<svg xmlns='http://www.w3.org/2000/svg' width='600' height='400'><circle cx='200' cy='200' r='150' fill='blue'/></svg>");
            var tempPsd = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid():N}.psd");
            using (var mi = new MagickImage("xc:white", 1024, 768))
            {
                mi.Format = MagickFormat.Psd;
                mi.Write(tempPsd);
            }
            Exception ex = null;
            var t = new System.Threading.Thread(() =>
            {
                try
                {
                    var conv = new ThumbnailConverter();
                    var m = typeof(ThumbnailConverter).GetMethod("LoadThumbnailSync", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                    Assert.NotNull(m);
                    var svg1 = (BitmapSource)m.Invoke(conv, new object[] { tempSvg, 256 });
                    var svg2 = (BitmapSource)m.Invoke(conv, new object[] { tempSvg, 256 });
                    Assert.NotNull(svg1);
                    Assert.True(svg1.PixelWidth <= 256);
                    Assert.True(object.ReferenceEquals(svg1, svg2));

                    var psd1 = (BitmapSource)m.Invoke(conv, new object[] { tempPsd, 128 });
                    var psd2 = (BitmapSource)m.Invoke(conv, new object[] { tempPsd, 128 });
                    Assert.NotNull(psd1);
                    Assert.True(psd1.PixelWidth <= 128);
                    Assert.True(object.ReferenceEquals(psd1, psd2));
                }
                catch (Exception e) { ex = e; }
            });
            t.SetApartmentState(System.Threading.ApartmentState.STA);
            t.Start();
            t.Join();
            TryDelete(tempSvg);
            TryDelete(tempPsd);
            if (ex != null) throw ex;
        }

        private static Image FindImage(UIElement ui)
        {
            if (ui is Grid g)
            {
                var sv = g.Children.OfType<ScrollViewer>().FirstOrDefault();
                if (sv?.Content is Image img) return img;
            }
            return null;
        }

        private static void TryDelete(string path)
        {
            try { if (File.Exists(path)) File.Delete(path); } catch { }
        }
    }
}
