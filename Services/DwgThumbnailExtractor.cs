using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Text;
using System.Windows.Media.Imaging;

namespace YiboFile.Services
{
    public static class DwgThumbnailExtractor
    {
        public static BitmapSource ExtractThumbnail(string filePath)
        {
            try
            {
                using (var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (var reader = new BinaryReader(fs))
                {
                    if (fs.Length < 32) return null;

                    // Check signature
                    var signatureBytes = reader.ReadBytes(6);
                    string signature = Encoding.ASCII.GetString(signatureBytes);
                    if (!signature.StartsWith("AC")) return null; // Not a DWG file

                    // 1. Try standard header location (R13 - R2010+)
                    // Thumbnail address is usually at 0x0D (img_loc)
                    fs.Seek(0x0D, SeekOrigin.Begin);
                    int imageAddress = reader.ReadInt32();

                    BitmapSource result = null;

                    if (imageAddress > 0 && imageAddress < fs.Length)
                    {
                        fs.Seek(imageAddress, SeekOrigin.Begin);
                        result = TryReadBitmap(reader, fs.Length);
                    }

                    if (result != null) return result;

                    // 2. Fallback: Scan the first 64KB for the sentinel
                    // Sentinel: 1F 25 6C 3D
                    fs.Seek(0, SeekOrigin.Begin);
                    long foundPos = ScanForSentinel(fs, 64 * 1024);
                    if (foundPos >= 0)
                    {
                        fs.Seek(foundPos, SeekOrigin.Begin);
                        return TryReadBitmap(reader, fs.Length);
                    }

                    return null;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"DWG Thumbnail extraction failed: {ex.Message}");
                return null;
            }
        }

        private static BitmapSource TryReadBitmap(BinaryReader reader, long streamLength)
        {
            try
            {
                // Read Sentinel
                byte[] sentinel = reader.ReadBytes(16); // 1F 25 6C 3D ...
                if (sentinel.Length < 4) return null;

                // Check raw BMP (BM)
                if (sentinel[0] == 0x42 && sentinel[1] == 0x4D) // 'B' 'M'
                {
                    reader.BaseStream.Seek(-16, SeekOrigin.Current);
                    return ReadBmpFromStream(reader.BaseStream);
                }

                // Check Sentinel 1F 25 6C 3D
                if (sentinel[0] != 0x1F || sentinel[1] != 0x25 || sentinel[2] != 0x6C || sentinel[3] != 0x3D)
                {
                    return null;
                }

                int imageSize = reader.ReadInt32();
                if (imageSize <= 0 || imageSize > 10 * 1024 * 1024) return null;
                if (reader.BaseStream.Position + imageSize > streamLength) return null;

                byte[] bmpData = reader.ReadBytes(imageSize);
                using (var ms = new MemoryStream(bmpData))
                {
                    return ReadBmpFromStream(ms);
                }
            }
            catch
            {
                return null;
            }
        }

        private static long ScanForSentinel(Stream stream, int maxBytes)
        {
            try
            {
                byte[] buffer = new byte[4096];
                long startPos = stream.Position;
                long bytesToRead = Math.Min(stream.Length - startPos, maxBytes);
                long bytesReadTotal = 0;

                while (bytesReadTotal < bytesToRead)
                {
                    int read = stream.Read(buffer, 0, buffer.Length);
                    if (read == 0) break;

                    for (int i = 0; i < read - 3; i++)
                    {
                        if (buffer[i] == 0x1F && buffer[i + 1] == 0x25 && buffer[i + 2] == 0x6C && buffer[i + 3] == 0x3D)
                        {
                            return startPos + bytesReadTotal + i;
                        }
                    }
                    bytesReadTotal += read;
                    // Overlap check (last 3 bytes)
                    if (read == buffer.Length)
                    {
                        stream.Seek(-3, SeekOrigin.Current);
                        bytesReadTotal -= 3;
                    }
                }
            }
            catch { }
            return -1;
        }

        private static BitmapSource ReadBmpFromStream(Stream stream)
        {
            try
            {
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
}
