using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using IxMilia.Dxf;
using IxMilia.Dxf.Entities;

namespace YiboFile.Rendering
{
    public static class DxfSvgConverter
    {
        public static string ConvertToSvg(string dxfFilePath, string layoutName = "Model")
        {
            if (!File.Exists(dxfFilePath)) return GenerateErrorSvg("DXF file not found");

            try
            {
                using var fs = File.OpenRead(dxfFilePath);
                var dxf = DxfFile.Load(fs);
                var entities = dxf.Entities.ToList();

                // Calculate bounding box
                double minX = double.PositiveInfinity, minY = double.PositiveInfinity;
                double maxX = double.NegativeInfinity, maxY = double.NegativeInfinity;

                void UpdateBounds(double x, double y)
                {
                    if (x < minX) minX = x;
                    if (y < minY) minY = y;
                    if (x > maxX) maxX = x;
                    if (y > maxY) maxY = y;
                }

                void ProcessEntityBounds(DxfEntity ent, double offsetX = 0, double offsetY = 0, double scaleX = 1, double scaleY = 1, double rotation = 0)
                {
                    // Basic transformation logic for blocks
                    var rad = rotation * Math.PI / 180.0;
                    var cos = Math.Cos(rad);
                    var sin = Math.Sin(rad);

                    (double x, double y) Transform(double x, double y)
                    {
                        var tx = x * scaleX;
                        var ty = y * scaleY;
                        var rx = tx * cos - ty * sin + offsetX;
                        var ry = tx * sin + ty * cos + offsetY;
                        return (rx, ry);
                    }

                    if (ent is DxfLine l)
                    {
                        var p1 = Transform(l.P1.X, l.P1.Y);
                        var p2 = Transform(l.P2.X, l.P2.Y);
                        UpdateBounds(p1.x, p1.y);
                        UpdateBounds(p2.x, p2.y);
                    }
                    else if (ent is DxfLwPolyline lw)
                    {
                        foreach (var v in lw.Vertices)
                        {
                            var p = Transform(v.X, v.Y);
                            UpdateBounds(p.x, p.y);
                        }
                    }
                    else if (ent is DxfPolyline pl)
                    {
                        foreach (var v in pl.Vertices)
                        {
                            var p = Transform(v.Location.X, v.Location.Y);
                            UpdateBounds(p.x, p.y);
                        }
                    }
                    else if (ent is DxfCircle c)
                    {
                        var center = Transform(c.Center.X, c.Center.Y);
                        var r = c.Radius * Math.Max(Math.Abs(scaleX), Math.Abs(scaleY));
                        UpdateBounds(center.x - r, center.y - r);
                        UpdateBounds(center.x + r, center.y + r);
                    }
                    else if (ent is DxfArc a)
                    {
                        // Simplified: treat arc as circle for bounds
                        var center = Transform(a.Center.X, a.Center.Y);
                        var r = a.Radius * Math.Max(Math.Abs(scaleX), Math.Abs(scaleY));
                        UpdateBounds(center.x - r, center.y - r);
                        UpdateBounds(center.x + r, center.y + r);
                    }
                    else if (ent is DxfText t)
                    {
                        var p = Transform(t.Location.X, t.Location.Y);
                        UpdateBounds(p.x, p.y);
                        // Estimate text size (very rough)
                        UpdateBounds(p.x + t.Value.Length * t.TextHeight * 0.6, p.y + t.TextHeight);
                    }
                    else if (ent is DxfInsert ins)
                    {
                        var block = dxf.Blocks.FirstOrDefault(b => string.Equals(b.Name, ins.Name, StringComparison.OrdinalIgnoreCase));
                        if (block != null)
                        {
                            foreach (var subEnt in block.Entities)
                            {
                                // Recursive transform
                                // Note: This simple recursion doesn't handle nested blocks perfectly with full matrix multiplication, 
                                // but sufficient for bounding box estimation in most 2D CAD files.
                                var subScaleX = ins.XScaleFactor;
                                var subScaleY = ins.YScaleFactor;
                                var subRot = ins.Rotation;
                                
                                // Calculate new offset based on current transform
                                var origin = Transform(ins.Location.X, ins.Location.Y);
                                
                                ProcessEntityBounds(subEnt, origin.x, origin.y, subScaleX, subScaleY, subRot);
                            }
                        }
                    }
                }

                foreach (var ent in entities)
                {
                    ProcessEntityBounds(ent);
                }

                // Default bounds if empty
                if (double.IsInfinity(minX)) { minX = 0; minY = 0; maxX = 100; maxY = 100; }

                // Add padding (5%)
                var width = maxX - minX;
                var height = maxY - minY;
                if (width <= 0) width = 1;
                if (height <= 0) height = 1;
                
                var paddingX = width * 0.05;
                var paddingY = height * 0.05;
                minX -= paddingX;
                minY -= paddingY;
                width += 2 * paddingX;
                height += 2 * paddingY;

                // Generate SVG
                var sb = new StringBuilder();
                sb.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"no\"?>");
                // Note: SVG y-axis is down, CAD y-axis is up. We use transform="scale(1, -1)" on a group to flip it, 
                // but we need to adjust viewBox accordingly. 
                // Actually, easier to map coordinates or just flip the viewbox?
                // Let's flip with a group transform.
                
                // ViewBox: minX, -maxY (because of flip), width, height
                // Wait, if we flip Y, the Y coordinates become negative.
                // Original: Y=10. Flipped: Y=-10.
                // MaxY=100 -> -100. MinY=0 -> 0.
                // So the vertical range is [-100, 0].
                // The viewBox top-left should be minX, -maxY.
                
                sb.AppendLine($"<svg xmlns=\"http://www.w3.org/2000/svg\" version=\"1.1\" viewBox=\"{minX} {-maxY} {width} {height}\">");
                sb.AppendLine($"<g transform=\"scale(1, -1)\">"); // Flip Y axis to match CAD coordinates
                
                // Helper for colors
                string GetColor(DxfColor color)
                {
                    if (color.IsIndex)
                    {
                        // Simple mapping for standard colors, others default to black
                        // In SVG with white background, white (index 7) should be black
                        switch (color.Index)
                        {
                            case 1: return "red";
                            case 2: return "yellow";
                            case 3: return "lime";
                            case 4: return "cyan";
                            case 5: return "blue";
                            case 6: return "magenta";
                            case 7: return "black"; 
                            default: return "black"; // TODO: Full ACI palette
                        }
                    }
                    return "black";
                }

                void WriteEntity(DxfEntity ent, double offsetX = 0, double offsetY = 0, double scaleX = 1, double scaleY = 1, double rotation = 0)
                {
                    var stroke = GetColor(ent.Color);
                    // If color is ByLayer (default), we should look up layer. For now default to black.
                    if (ent.Color.IsByLayer) stroke = "black"; 
                    
                    var strokeWidth = $"{width * 0.001}"; // Relative stroke width

                    // Transform logic (same as bounds)
                    var rad = rotation * Math.PI / 180.0;
                    var cos = Math.Cos(rad);
                    var sin = Math.Sin(rad);

                    (double x, double y) Transform(double x, double y)
                    {
                        var tx = x * scaleX;
                        var ty = y * scaleY;
                        var rx = tx * cos - ty * sin + offsetX;
                        var ry = tx * sin + ty * cos + offsetY;
                        return (rx, ry);
                    }

                    if (ent is DxfLine l)
                    {
                        var p1 = Transform(l.P1.X, l.P1.Y);
                        var p2 = Transform(l.P2.X, l.P2.Y);
                        sb.AppendLine($"<line x1=\"{p1.x}\" y1=\"{p1.y}\" x2=\"{p2.x}\" y2=\"{p2.y}\" stroke=\"{stroke}\" stroke-width=\"{strokeWidth}\" vector-effect=\"non-scaling-stroke\" />");
                    }
                    else if (ent is DxfLwPolyline lw)
                    {
                        sb.Append($"<polyline points=\"");
                        foreach (var v in lw.Vertices)
                        {
                            var p = Transform(v.X, v.Y);
                            sb.Append($"{p.x},{p.y} ");
                        }
                        // Close loop if needed
                        if (lw.IsClosed && lw.Vertices.Count > 0)
                        {
                            var p = Transform(lw.Vertices[0].X, lw.Vertices[0].Y);
                            sb.Append($"{p.x},{p.y}");
                        }
                        sb.AppendLine($"\" fill=\"none\" stroke=\"{stroke}\" stroke-width=\"{strokeWidth}\" vector-effect=\"non-scaling-stroke\" />");
                    }
                    else if (ent is DxfPolyline pl)
                    {
                        sb.Append($"<polyline points=\"");
                        foreach (var v in pl.Vertices)
                        {
                            var p = Transform(v.Location.X, v.Location.Y);
                            sb.Append($"{p.x},{p.y} ");
                        }
                        if (pl.IsClosed && pl.Vertices.Count > 0)
                        {
                            var p = Transform(pl.Vertices[0].Location.X, pl.Vertices[0].Location.Y);
                            sb.Append($"{p.x},{p.y}");
                        }
                        sb.AppendLine($"\" fill=\"none\" stroke=\"{stroke}\" stroke-width=\"{strokeWidth}\" vector-effect=\"non-scaling-stroke\" />");
                    }
                    else if (ent is DxfCircle c)
                    {
                        var center = Transform(c.Center.X, c.Center.Y);
                        // Ellipse if scales are different, but for now assume uniform scale for circles
                        var r = c.Radius * Math.Max(Math.Abs(scaleX), Math.Abs(scaleY));
                        sb.AppendLine($"<circle cx=\"{center.x}\" cy=\"{center.y}\" r=\"{r}\" fill=\"none\" stroke=\"{stroke}\" stroke-width=\"{strokeWidth}\" vector-effect=\"non-scaling-stroke\" />");
                    }
                    else if (ent is DxfArc a)
                    {
                        // SVG arc path: A rx ry x-axis-rotation large-arc-flag sweep-flag x y
                        // This is complex to transform manually. 
                        // Simplified: approximate with polyline or implement full arc math.
                        // For this MVP, let's try to output a proper path.
                        
                        var center = Transform(a.Center.X, a.Center.Y);
                        var r = a.Radius * Math.Max(Math.Abs(scaleX), Math.Abs(scaleY));
                        
                        var startAngle = a.StartAngle * Math.PI / 180.0;
                        var endAngle = a.EndAngle * Math.PI / 180.0;
                        
                        // Handle rotation in transform
                        startAngle += rad;
                        endAngle += rad;
                        
                        var startX = center.x + r * Math.Cos(startAngle);
                        var startY = center.y + r * Math.Sin(startAngle);
                        var endX = center.x + r * Math.Cos(endAngle);
                        var endY = center.y + r * Math.Sin(endAngle);
                        
                        // Large arc flag
                        var diff = endAngle - startAngle;
                        while (diff < 0) diff += 2 * Math.PI;
                        while (diff > 2 * Math.PI) diff -= 2 * Math.PI;
                        var largeArc = diff > Math.PI ? 1 : 0;
                        
                        sb.AppendLine($"<path d=\"M {startX} {startY} A {r} {r} 0 {largeArc} 1 {endX} {endY}\" fill=\"none\" stroke=\"{stroke}\" stroke-width=\"{strokeWidth}\" vector-effect=\"non-scaling-stroke\" />");
                    }
                    else if (ent is DxfText t)
                    {
                        var p = Transform(t.Location.X, t.Location.Y);
                        // SVG text is scale-invariant by default, but we want it to scale with the drawing?
                        // Actually, we want it to be readable.
                        // Transform Y needs to be un-flipped for text content? No, the group flips everything.
                        // But text itself renders upside down if we just place it in a flipped group?
                        // Yes, <g transform="scale(1,-1)"> makes text upside down.
                        // We need to flip the text back: transform="scale(1,-1)" on the text element itself?
                        
                        var fontSize = t.TextHeight * Math.Max(Math.Abs(scaleX), Math.Abs(scaleY));
                        
                        sb.AppendLine($"<text x=\"{p.x}\" y=\"{p.y}\" font-family=\"sans-serif\" font-size=\"{fontSize}\" fill=\"{stroke}\" transform=\"scale(1,-1)\" style=\"transform-box: fill-box; transform-origin: center;\">{t.Value}</text>");
                        // Note: Text handling in SVG from CAD is tricky due to anchor points and flipping. 
                        // This is a basic approximation.
                    }
                    else if (ent is DxfInsert ins)
                    {
                        var block = dxf.Blocks.FirstOrDefault(b => string.Equals(b.Name, ins.Name, StringComparison.OrdinalIgnoreCase));
                        if (block != null)
                        {
                            var subScaleX = ins.XScaleFactor;
                            var subScaleY = ins.YScaleFactor;
                            var subRot = ins.Rotation;
                            var origin = Transform(ins.Location.X, ins.Location.Y);
                            
                            foreach (var subEnt in block.Entities)
                            {
                                WriteEntity(subEnt, origin.x, origin.y, subScaleX, subScaleY, subRot);
                            }
                        }
                    }
                }

                foreach (var ent in entities)
                {
                    WriteEntity(ent);
                }

                sb.AppendLine("</g>");
                sb.AppendLine("</svg>");

                return sb.ToString();
            }
            catch (Exception ex)
            {
                return GenerateErrorSvg($"Conversion failed: {ex.Message}");
            }
        }

        private static string GenerateErrorSvg(string message)
        {
            return $@"<?xml version=""1.0"" encoding=""UTF-8""?>
<svg xmlns=""http://www.w3.org/2000/svg"" viewBox=""0 0 400 100"">
  <text x=""20"" y=""50"" font-family=""Segoe UI"" font-size=""16"" fill=""red"">{message}</text>
</svg>";
        }
    }
}

