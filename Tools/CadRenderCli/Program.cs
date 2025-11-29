using System;
using System.Collections.Generic;
using System.IO;
using IxMilia.Dxf;
using IxMilia.Dxf.Entities;
using SkiaSharp;

static class Renderer
{
    public static void RenderDxf(string input, string output, int size, string layout)
    {
        using var fs = File.OpenRead(input);
        var dxf = DxfFile.Load(fs);
        var ents = new List<DxfEntity>(dxf.Entities);

        double minX = double.PositiveInfinity, minY = double.PositiveInfinity;
        double maxX = double.NegativeInfinity, maxY = double.NegativeInfinity;
        void Acc(double x, double y)
        { if (x < minX) minX = x; if (y < minY) minY = y; if (x > maxX) maxX = x; if (y > maxY) maxY = y; }
        foreach (var ent in ents)
        {
            if (ent is DxfLine l) { Acc(l.P1.X, l.P1.Y); Acc(l.P2.X, l.P2.Y); }
            else if (ent is DxfLwPolyline lw) { foreach (var v in lw.Vertices) Acc(v.X, v.Y); }
            else if (ent is DxfPolyline pl) { foreach (var v in pl.Vertices) Acc(v.Location.X, v.Location.Y); }
            else if (ent is DxfCircle c) { Acc(c.Center.X - c.Radius, c.Center.Y - c.Radius); Acc(c.Center.X + c.Radius, c.Center.Y + c.Radius); }
            else if (ent is DxfArc a) { Acc(a.Center.X - a.Radius, a.Center.Y - a.Radius); Acc(a.Center.X + a.Radius, a.Center.Y + a.Radius); }
            else if (ent is DxfText t) { Acc(t.Location.X, t.Location.Y); }
        }
        if (!double.IsFinite(minX)) { minX = -10; minY = -10; maxX = 10; maxY = 10; }
        double dx = maxX - minX; if (dx <= 0) dx = 1; double dy = maxY - minY; if (dy <= 0) dy = 1;
        double margin = 20.0; double scaleX = (size - 2 * margin) / dx; double scaleY = (size - 2 * margin) / dy; double scale = Math.Min(scaleX, scaleY);
        double ox = margin - minX * scale; double oy = margin - minY * scale;

        using var bmp = new SKBitmap(size, size, true);
        using var canvas = new SKCanvas(bmp);
        canvas.Clear(SKColors.White);
        SKPoint Map(double x, double y) => new SKPoint((float)(x * scale + ox), (float)(size - (y * scale + oy)));
        foreach (var ent in ents)
        {
            var paint = new SKPaint { Color = SKColors.Black, IsAntialias = true, StrokeWidth = 1f, Style = SKPaintStyle.Stroke };
            if (ent is DxfLine l)
            {
                canvas.DrawLine(Map(l.P1.X, l.P1.Y), Map(l.P2.X, l.P2.Y), paint);
            }
            else if (ent is DxfLwPolyline lw)
            {
                for (int i = 0; i < lw.Vertices.Count - 1; i++)
                    canvas.DrawLine(Map(lw.Vertices[i].X, lw.Vertices[i].Y), Map(lw.Vertices[i + 1].X, lw.Vertices[i + 1].Y), paint);
                if (lw.IsClosed && lw.Vertices.Count > 2)
                    canvas.DrawLine(Map(lw.Vertices[^1].X, lw.Vertices[^1].Y), Map(lw.Vertices[0].X, lw.Vertices[0].Y), paint);
            }
            else if (ent is DxfPolyline pl)
            {
                for (int i = 0; i < pl.Vertices.Count - 1; i++)
                    canvas.DrawLine(Map(pl.Vertices[i].Location.X, pl.Vertices[i].Location.Y), Map(pl.Vertices[i + 1].Location.X, pl.Vertices[i + 1].Location.Y), paint);
            }
            else if (ent is DxfCircle c)
            {
                canvas.DrawCircle(Map(c.Center.X, c.Center.Y), (float)(c.Radius * scale), paint);
            }
            else if (ent is DxfArc a)
            {
                var rect = SKRect.Create((float)((a.Center.X - a.Radius) * scale + ox), (float)(size - ((a.Center.Y + a.Radius) * scale + oy)), (float)(2 * a.Radius * scale), (float)(2 * a.Radius * scale));
                canvas.DrawArc(rect, (float)(-a.EndAngle), (float)(-(a.StartAngle - a.EndAngle)), false, paint);
            }
            else if (ent is DxfText t)
            {
                var tp = new SKPaint { Color = SKColors.Gray, IsAntialias = true, TextSize = 12f };
                var p = Map(t.Location.X, t.Location.Y);
                canvas.DrawText(t.Value ?? "TEXT", p.X, p.Y, tp);
            }
        }
        using var image = SKImage.FromBitmap(bmp);
        using var data = image.Encode(SKEncodedImageFormat.Png, 90);
        using var fsOut = new FileStream(output, FileMode.Create, FileAccess.Write);
        data.SaveTo(fsOut);
    }
}

static class Program
{
    static int Main(string[] args)
    {
        string input = null; string output = null; int size = 256; string layout = "Model";
        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--input": input = args[++i]; break;
                case "--output": output = args[++i]; break;
                case "--size": size = int.Parse(args[++i]); break;
                case "--layout": layout = args[++i]; break;
            }
        }
        if (string.IsNullOrEmpty(input) || string.IsNullOrEmpty(output))
        {
            Console.Error.WriteLine("Usage: CadRenderCli --input <file.dxf> --output <out.png> [--size 256] [--layout Model|Paper]");
            return 2;
        }
        try
        {
            var ext = Path.GetExtension(input).ToLowerInvariant();
            if (ext == ".dxf")
            {
                Renderer.RenderDxf(input, output, size, layout);
                Console.WriteLine("OK");
                return 0;
            }
            else
            {
                Console.Error.WriteLine("DWG not supported in CLI yet");
                return 3;
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex.Message);
            return 1;
        }
    }
}

