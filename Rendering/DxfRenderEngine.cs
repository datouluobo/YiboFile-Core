using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Media.Imaging;
using IxMilia.Dxf;
using IxMilia.Dxf.Entities;
using SkiaSharp;

namespace YiboFile.Rendering
{
    public static class DxfRenderEngine
    {
        public static List<string> GetLayouts(string filePath)
        {
            var list = new List<string>();
            if (!File.Exists(filePath)) return list;
            
            try
            {
                using var fs = File.OpenRead(filePath);
                var dxf = DxfFile.Load(fs);
                var hasAny = dxf.Entities.Any();
                if (hasAny) list.Add("Model");
                if (list.Count == 0) list.Add("Model");
            }
            catch (Exception)
            {// 处理异常，防止程序卡死
                                list.Add("Model");}
            
            return list;
        }

        public static BitmapSource Render(string filePath, string layout, int width, int height)
        {
            try
            {
                using var fs = File.OpenRead(filePath);
                var dxf = DxfFile.Load(fs);
                var entities = dxf.Entities.ToList();

            double minX = double.PositiveInfinity, minY = double.PositiveInfinity;
            double maxX = double.NegativeInfinity, maxY = double.NegativeInfinity;

            void Acc(double x, double y)
            {
                if (x < minX) minX = x; if (y < minY) minY = y;
                if (x > maxX) maxX = x; if (y > maxY) maxY = y;
            }

            void AccEnt(DxfEntity ent)
            {
                if (ent is DxfLine l) { Acc(l.P1.X, l.P1.Y); Acc(l.P2.X, l.P2.Y); }
                else if (ent is DxfLwPolyline lw) { foreach (var v in lw.Vertices) Acc(v.X, v.Y); }
                else if (ent is DxfPolyline pl) { foreach (var v in pl.Vertices) Acc(v.Location.X, v.Location.Y); }
                else if (ent is DxfCircle c) { Acc(c.Center.X - c.Radius, c.Center.Y - c.Radius); Acc(c.Center.X + c.Radius, c.Center.Y + c.Radius); }
                else if (ent is DxfArc a) { Acc(a.Center.X - a.Radius, a.Center.Y - a.Radius); Acc(a.Center.X + a.Radius, a.Center.Y + a.Radius); }
                else if (ent is DxfText t) { Acc(t.Location.X, t.Location.Y); }
                else if (ent is DxfInsert ins)
                {
                    var blk = dxf.Blocks.FirstOrDefault(b => string.Equals(b.Name, ins.Name, StringComparison.OrdinalIgnoreCase));
                    if (blk != null)
                    {
                        double sx = ins.XScaleFactor;
                        double sy = ins.YScaleFactor;
                        double rot = ins.Rotation;
                        double tx = ins.Location.X;
                        double ty = ins.Location.Y;
                        double rad = rot * Math.PI / 180.0;
                        double cos = Math.Cos(rad);
                        double sin = Math.Sin(rad);
                        Action<double,double> AccT = (x,y) =>
                        {
                            double xx = sx * x;
                            double yy = sy * y;
                            double xr = xx * cos - yy * sin + tx;
                            double yr = xx * sin + yy * cos + ty;
                            Acc(xr, yr);
                        };
                        foreach (var be in blk.Entities)
                        {
                            if (be is DxfLine bl) { AccT(bl.P1.X, bl.P1.Y); AccT(bl.P2.X, bl.P2.Y); }
                            else if (be is DxfLwPolyline blw) { foreach (var v in blw.Vertices) AccT(v.X, v.Y); }
                            else if (be is DxfPolyline bpl) { foreach (var v in bpl.Vertices) AccT(v.Location.X, v.Location.Y); }
                            else if (be is DxfCircle bc) { AccT(bc.Center.X - bc.Radius, bc.Center.Y - bc.Radius); AccT(bc.Center.X + bc.Radius, bc.Center.Y + bc.Radius); }
                            else if (be is DxfArc ba) { AccT(ba.Center.X - ba.Radius, ba.Center.Y - ba.Radius); AccT(ba.Center.X + ba.Radius, ba.Center.Y + ba.Radius); }
                            else if (be is DxfText bt) { AccT(bt.Location.X, bt.Location.Y); }
                        }
                    }
                }
            }
            foreach (var ent in entities) AccEnt(ent);

            if (!double.IsFinite(minX) || !double.IsFinite(minY) || !double.IsFinite(maxX) || !double.IsFinite(maxY))
            {
                minX = -10; minY = -10; maxX = 10; maxY = 10;
            }

            double dx = maxX - minX; if (dx <= 0) dx = 1;
            double dy = maxY - minY; if (dy <= 0) dy = 1;
            double margin = 20.0;
            double scaleX = (width - 2 * margin) / dx;
            double scaleY = (height - 2 * margin) / dy;
            double scale = Math.Min(scaleX, scaleY);
            double offsetX = margin - minX * scale;
            double offsetY = margin - minY * scale;

            using var bmp = new SKBitmap(width, height, true);
            using var canvas = new SKCanvas(bmp);
            canvas.Clear(SKColors.White);
            SKPoint Map(double x, double y) => new SKPoint((float)(x * scale + offsetX), (float)(height - (y * scale + offsetY)));

            void DrawEnt(DxfEntity ent)
            {
                var paint = new SKPaint { Color = SKColors.Black, IsAntialias = true, StrokeWidth = 2f, Style = SKPaintStyle.Stroke };
                if (ent is DxfLine l)
                {
                    canvas.DrawLine(Map(l.P1.X, l.P1.Y), Map(l.P2.X, l.P2.Y), paint);
                }
                else if (ent is DxfLwPolyline lw)
                {
                    for (int i = 0; i < lw.Vertices.Count - 1; i++)
                    {
                        var va = lw.Vertices[i];
                        var vb = lw.Vertices[i + 1];
                        canvas.DrawLine(Map(va.X, va.Y), Map(vb.X, vb.Y), paint);
                    }
                    if (lw.IsClosed && lw.Vertices.Count > 2)
                    {
                        var va = lw.Vertices[^1];
                        var vb = lw.Vertices[0];
                        canvas.DrawLine(Map(va.X, va.Y), Map(vb.X, vb.Y), paint);
                    }
                }
                else if (ent is DxfPolyline pl)
                {
                    for (int i = 0; i < pl.Vertices.Count - 1; i++)
                    {
                        var va = pl.Vertices[i].Location;
                        var vb = pl.Vertices[i + 1].Location;
                        canvas.DrawLine(Map(va.X, va.Y), Map(vb.X, vb.Y), paint);
                    }
                }
                else if (ent is DxfCircle c)
                {
                    canvas.DrawCircle(Map(c.Center.X, c.Center.Y), (float)(c.Radius * scale), paint);
                }
                else if (ent is DxfArc a)
                {
                    var rect = SKRect.Create((float)((a.Center.X - a.Radius) * scale + offsetX), (float)(height - ((a.Center.Y + a.Radius) * scale + offsetY)), (float)(2 * a.Radius * scale), (float)(2 * a.Radius * scale));
                    canvas.DrawArc(rect, (float)(-a.EndAngle), (float)(-(a.StartAngle - a.EndAngle)), false, paint);
                }
                else if (ent is DxfText t)
                {
                    var font = new SKFont { Size = 14f };
                    var tp = new SKPaint { Color = SKColors.Gray, IsAntialias = true };
                    var p = Map(t.Location.X, t.Location.Y);
                    canvas.DrawText(t.Value ?? "TEXT", p.X, p.Y, SKTextAlign.Left, font, tp);
                }
                else if (ent is DxfInsert ins)
                {
                    var blk = dxf.Blocks.FirstOrDefault(b => string.Equals(b.Name, ins.Name, StringComparison.OrdinalIgnoreCase));
                    if (blk != null)
                    {
                        double sx = ins.XScaleFactor;
                        double sy = ins.YScaleFactor;
                        double rot = ins.Rotation;
                        double tx = ins.Location.X;
                        double ty = ins.Location.Y;
                        double rad = rot * Math.PI / 180.0;
                        double cos = Math.Cos(rad);
                        double sin = Math.Sin(rad);
                        Func<double,double,(double x,double y)> T = (x,y) =>
                        {
                            double xx = sx * x;
                            double yy = sy * y;
                            double xr = xx * cos - yy * sin + tx;
                            double yr = xx * sin + yy * cos + ty;
                            return (xr, yr);
                        };
                        foreach (var be in blk.Entities)
                        {
                            if (be is DxfLine bl)
                            {
                                var p1 = T(bl.P1.X, bl.P1.Y);
                                var p2 = T(bl.P2.X, bl.P2.Y);
                                canvas.DrawLine(Map(p1.x, p1.y), Map(p2.x, p2.y), paint);
                            }
                            else if (be is DxfLwPolyline blw)
                            {
                                for (int i = 0; i < blw.Vertices.Count - 1; i++)
                                {
                                    var va = T(blw.Vertices[i].X, blw.Vertices[i].Y);
                                    var vb = T(blw.Vertices[i + 1].X, blw.Vertices[i + 1].Y);
                                    canvas.DrawLine(Map(va.x, va.y), Map(vb.x, vb.y), paint);
                                }
                                if (blw.IsClosed && blw.Vertices.Count > 2)
                                {
                                    var va = T(blw.Vertices[^1].X, blw.Vertices[^1].Y);
                                    var vb = T(blw.Vertices[0].X, blw.Vertices[0].Y);
                                    canvas.DrawLine(Map(va.x, va.y), Map(vb.x, vb.y), paint);
                                }
                            }
                            else if (be is DxfPolyline bpl)
                            {
                                for (int i = 0; i < bpl.Vertices.Count - 1; i++)
                                {
                                    var va = T(bpl.Vertices[i].Location.X, bpl.Vertices[i].Location.Y);
                                    var vb = T(bpl.Vertices[i + 1].Location.X, bpl.Vertices[i + 1].Location.Y);
                                    canvas.DrawLine(Map(va.x, va.y), Map(vb.x, vb.y), paint);
                                }
                            }
                            else if (be is DxfCircle bc)
                            {
                                var cc = T(bc.Center.X, bc.Center.Y);
                                canvas.DrawCircle(Map(cc.x, cc.y), (float)(bc.Radius * scale * Math.Max(sx, sy)), paint);
                            }
                            else if (be is DxfArc ba)
                            {
                                var cc = T(ba.Center.X, ba.Center.Y);
                                var rect = SKRect.Create((float)((cc.x - ba.Radius) * scale + offsetX), (float)(height - ((cc.y + ba.Radius) * scale + offsetY)), (float)(2 * ba.Radius * scale), (float)(2 * ba.Radius * scale));
                                canvas.DrawArc(rect, (float)(-ba.EndAngle), (float)(-(ba.StartAngle - ba.EndAngle)), false, paint);
                            }
                            else if (be is DxfText bt)
                            {
                                var font = new SKFont { Size = 14f };
                                var tp = new SKPaint { Color = SKColors.Gray, IsAntialias = true };
                                var pp = T(bt.Location.X, bt.Location.Y);
                                var mp = Map(pp.x, pp.y);
                                canvas.DrawText(bt.Value ?? "TEXT", mp.X, mp.Y, SKTextAlign.Left, font, tp);
                            }
                        }
                    }
                }
            }
            foreach (var e in entities) DrawEnt(e);

            using var image = SKImage.FromBitmap(bmp);
            using var data = image.Encode(SKEncodedImageFormat.Png, 90);
            using var ms = new MemoryStream(data.ToArray());
            var bi = new BitmapImage();
            bi.BeginInit();
            bi.CacheOption = BitmapCacheOption.OnLoad;
            bi.StreamSource = ms;
            bi.EndInit();
            bi.Freeze();
            return bi;
            }
            catch (Exception)
            {// 处理异常，防止程序卡死
                                return null;}
        }
    }
}


