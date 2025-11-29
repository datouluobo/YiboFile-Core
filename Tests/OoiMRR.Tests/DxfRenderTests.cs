using System;
using System.IO;
using IxMilia.Dxf;
using IxMilia.Dxf.Entities;
using OoiMRR.Rendering;
using OoiMRR.Services;
using Xunit;

namespace OoiMRR.Tests
{
    public class DxfRenderTests
    {
        [Fact]
        public void Render_SimpleDxf_ShouldProduceBitmap()
        {
            var tmp = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".dxf");
            var dxf = new DxfFile();
            dxf.Entities.Add(new DxfLine(new DxfPoint(0, 0, 0), new DxfPoint(100, 100, 0)));
            dxf.Entities.Add(new DxfCircle(new DxfPoint(50, 50, 0), 20));
            using (var fs = new FileStream(tmp, FileMode.Create, FileAccess.Write))
            {
                dxf.Save(fs);
            }
            var bmp = DxfRenderEngine.Render(tmp, "Model", 256, 256);
            Assert.NotNull(bmp);
            var cacheHit = CadImageCache.Get(tmp, 256);
            Assert.Null(cacheHit);
            CadImageCache.Put(bmp, tmp, 256);
            var cacheAgain = CadImageCache.Get(tmp, 256);
            Assert.NotNull(cacheAgain);
            File.Delete(tmp);
        }
    }
}

