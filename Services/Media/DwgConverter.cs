using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace YiboFile.Services
{
    public static class DwgConverter
    {
        private static readonly string CacheDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "YiboFile", "Cache", "DWGtoDXF");
        
        static DwgConverter()
        {
            Directory.CreateDirectory(CacheDirectory);
        }
        
        public static string GetConvertedDxfPath(string dwgFilePath)
        {
            if (!File.Exists(dwgFilePath))
                return null;
            
            var fileInfo = new System.IO.FileInfo(dwgFilePath);
            var cacheKey = $"{dwgFilePath}_{fileInfo.LastWriteTimeUtc.Ticks}";
            var cacheFileName = $"{Path.GetFileNameWithoutExtension(dwgFilePath)}_{cacheKey.GetHashCode():x8}.dxf";
            var cachePath = Path.Combine(CacheDirectory, cacheFileName);
            
            return cachePath;
        }
        
        public static bool IsConversionNeeded(string dwgFilePath)
        {
            var dxfPath = GetConvertedDxfPath(dwgFilePath);
            return !File.Exists(dxfPath);
        }
        
        public static async Task<string> ConvertToDxfAsync(string dwgFilePath)
        {
            if (!File.Exists(dwgFilePath))
                throw new FileNotFoundException("DWG file not found", dwgFilePath);
            
            var dxfPath = GetConvertedDxfPath(dwgFilePath);
            
            // 检查缓存是否存在
            if (File.Exists(dxfPath))
                return dxfPath;
            
            // 尝试使用不同的转换方法
            if (await TryConvertWithOdaFileConverter(dwgFilePath, dxfPath))
                return dxfPath;
            
            if (await TryConvertWithQcad(dwgFilePath, dxfPath))
                return dxfPath;
            
            // 如果所有转换方法都失败，抛出异常
            throw new InvalidOperationException("DWG转换失败。请确保已正确安装 ODA File Converter。");
        }
        
        private static async Task<bool> TryConvertWithOdaFileConverter(string dwgFilePath, string dxfPath)
        {
            // 查找ODA File Converter
            var odaPaths = new[]
            {
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Dependencies", "ODAFileConverter", "ODAFileConverter.exe"),
                @"C:\Program Files\ODA\ODAFileConverter\ODAFileConverter.exe",
                @"C:\Program Files (x86)\ODA\ODAFileConverter\ODAFileConverter.exe"
            };
            
            var odaExe = Array.Find(odaPaths, File.Exists);
            if (string.IsNullOrEmpty(odaExe))
                return false;
            
            try
            {
                // 创建临时输入和输出目录
                var tempInputDir = Path.Combine(Path.GetTempPath(), "ODA_Input_" + Guid.NewGuid().ToString("N"));
                var tempOutputDir = Path.Combine(Path.GetTempPath(), "ODA_Output_" + Guid.NewGuid().ToString("N"));
                
                Directory.CreateDirectory(tempInputDir);
                Directory.CreateDirectory(tempOutputDir);
                
                try
                {
                    // 复制 DWG 文件到临时输入目录
                    var tempDwgPath = Path.Combine(tempInputDir, Path.GetFileName(dwgFilePath));
                    File.Copy(dwgFilePath, tempDwgPath, true);
                    
                    // ODA File Converter 命令行格式
                    var arguments = $"\"{tempInputDir}\" \"{tempOutputDir}\" ACAD2018 DXF 0 1";
                    
                    var processStartInfo = new ProcessStartInfo
                    {
                        FileName = odaExe,
                        Arguments = arguments,
                        CreateNoWindow = true,              // 不创建窗口
                        UseShellExecute = false,            // 不使用系统外壳
                        WindowStyle = ProcessWindowStyle.Hidden,  // 隐藏窗口（静默运行）
                        RedirectStandardOutput = true,
                        RedirectStandardError = true
                    };
                    
                    using var process = Process.Start(processStartInfo);
                    if (process == null)
                        return false;
                    
                    // 读取输出（用于调试）
                    var output = await process.StandardOutput.ReadToEndAsync();
                    var error = await process.StandardError.ReadToEndAsync();
                    
                    await process.WaitForExitAsync();
                    
                    // 查找转换后的 DXF 文件
                    var outputDxfName = Path.GetFileNameWithoutExtension(dwgFilePath) + ".dxf";
                    var outputDxfPath = Path.Combine(tempOutputDir, outputDxfName);
                    
                    if (File.Exists(outputDxfPath))
                    {
                        // 确保目标目录存在
                        var dxfDir = Path.GetDirectoryName(dxfPath);
                        if (!string.IsNullOrEmpty(dxfDir))
                        {
                            Directory.CreateDirectory(dxfDir);
                        }
                        
                        // 复制到目标位置
                        File.Copy(outputDxfPath, dxfPath, true);
                        return true;
                    }
                    
                    // 转换失败，记录错误信息
                    if (!string.IsNullOrEmpty(error))
                    {
                                            }
                    
                    return false;
                }
                finally
                {
                    // 清理临时目录
                    try
                    {
                        if (Directory.Exists(tempInputDir))
                            Directory.Delete(tempInputDir, true);
                        if (Directory.Exists(tempOutputDir))
                            Directory.Delete(tempOutputDir, true);
                    }
                    catch { }
                }
            }
            catch (Exception)
            {return false;}
        }
        
        private static async Task<bool> TryConvertWithQcad(string dwgFilePath, string dxfPath)
        {
            // 查找QCAD命令行工具
            var qcadPaths = new[]
            {
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Dependencies", "QCAD", "qcad-cli.exe"),
                @"C:\Program Files\QCAD\qcad-cli.exe",
                @"C:\Program Files (x86)\QCAD\qcad-cli.exe"
            };
            
            var qcadExe = Array.Find(qcadPaths, File.Exists);
            if (string.IsNullOrEmpty(qcadExe))
                return false;
            
            try
            {
                var arguments = $"-platform offscreen -no-gui -autostart-script \"scripts/Pro/Modify/Export/ExportDXF.js\" \"{dwgFilePath}\" \"{dxfPath}\"";
                
                var processStartInfo = new ProcessStartInfo
                {
                    FileName = qcadExe,
                    Arguments = arguments,
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    WindowStyle = ProcessWindowStyle.Hidden,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };
                
                using var process = Process.Start(processStartInfo);
                await process.WaitForExitAsync();
                
                return File.Exists(dxfPath);
            }
            catch
            {
                return false;
            }
        }
        
        public static void CleanupCache()
        {
            try
            {
                var files = Directory.GetFiles(CacheDirectory);
                var now = DateTime.UtcNow;
                
                foreach (var file in files)
                {
                    var fileInfo = new System.IO.FileInfo(file);
                    if (now - fileInfo.CreationTimeUtc > TimeSpan.FromDays(7))
                    {
                        File.Delete(file);
                    }
                }
            }
            catch { }
        }
    }
}
