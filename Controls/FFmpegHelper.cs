using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using FFMpegCore;

namespace OoiMRR.Controls
{
    /// <summary>
    /// FFmpeg 辅助类：自动查找和配置 FFmpeg
    /// </summary>
    public static class FFmpegHelper
    {
        /// <summary>
        /// 自动查找并配置 FFmpeg
        /// 优先使用打包在程序目录中的 FFmpeg（Dependencies\FFmpeg）
        /// </summary>
        /// <returns>是否找到可用的 FFmpeg</returns>
        public static bool InitializeFFmpeg()
        {
            try
            {
                // 1. 优先检查打包在程序目录中的 FFmpeg（Dependencies\FFmpeg）
                // 编译时会自动复制到输出目录，运行时路径为：BaseDirectory\Dependencies\FFmpeg
                string appDir = AppDomain.CurrentDomain.BaseDirectory;
                string bundledFFmpegDir = Path.Combine(appDir, "Dependencies", "FFmpeg");
                string bundledFFmpegPath = Path.Combine(bundledFFmpegDir, "ffmpeg.exe");
                
                if (File.Exists(bundledFFmpegPath))
                {
                    if (TestFFmpeg(bundledFFmpegDir))
                    {
                        GlobalFFOptions.Configure(new FFOptions 
                        { 
                            BinaryFolder = bundledFFmpegDir,
                            TemporaryFilesFolder = Path.GetTempPath()
                        });
                        System.Diagnostics.Debug.WriteLine($"FFmpeg 已配置：打包文件夹 ({bundledFFmpegDir})");
                        return true;
                    }
                }

                // 2. 如果打包的 FFmpeg 不可用，回退到系统 PATH
                if (TestFFmpegInPath())
                {
                    return true;
                }

                // 3. 在常见安装位置搜索（作为最后备选）
                string[] commonSearchPaths = {
                    @"C:\ffmpeg\bin",
                    Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles) + @"\ffmpeg\bin",
                    Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86) + @"\ffmpeg\bin"
                };

                foreach (string path in commonSearchPaths)
                {
                    if (string.IsNullOrEmpty(path))
                        continue;

                    string ffmpegPath = Path.Combine(path, "ffmpeg.exe");
                    if (File.Exists(ffmpegPath))
                    {
                        if (TestFFmpeg(path))
                        {
                            GlobalFFOptions.Configure(new FFOptions 
                            { 
                                BinaryFolder = path,
                                TemporaryFilesFolder = Path.GetTempPath()
                            });
                            return true;
                        }
                    }
                }

                return false;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 测试指定目录中的 FFmpeg 是否可用（需要同时有 ffmpeg.exe 和 ffprobe.exe）
        /// </summary>
        private static bool TestFFmpeg(string ffmpegDirectory)
        {
            try
            {
                string ffmpegPath = Path.Combine(ffmpegDirectory, "ffmpeg.exe");
                string ffprobePath = Path.Combine(ffmpegDirectory, "ffprobe.exe");
                
                // FFMpegCore 需要同时有 ffmpeg.exe 和 ffprobe.exe
                if (!File.Exists(ffmpegPath))
                {
                    return false;
                }
                
                if (!File.Exists(ffprobePath))
                {
                    System.Diagnostics.Debug.WriteLine($"FFmpegHelper: ffprobe.exe 不存在: {ffprobePath} (仅找到 ffmpeg.exe，跳过此目录)");
                    return false;
                }

                // 测试 ffmpeg.exe 是否可执行
                var processStartInfo = new ProcessStartInfo
                {
                    FileName = ffmpegPath,
                    Arguments = "-version",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };

                using (var process = Process.Start(processStartInfo))
                {
                    if (process != null)
                    {
                        process.WaitForExit(3000); // 最多等待3秒
                        bool ffmpegOk = process.ExitCode == 0;
                        
                        if (!ffmpegOk)
                        {
                            System.Diagnostics.Debug.WriteLine($"FFmpegHelper: ffmpeg.exe 测试失败 (退出码: {process.ExitCode})");
                            return false;
                        }
                    }
                    else
                    {
                        return false;
                    }
                }
                
                // 测试 ffprobe.exe 是否可执行
                processStartInfo = new ProcessStartInfo
                {
                    FileName = ffprobePath,
                    Arguments = "-version",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };

                using (var process = Process.Start(processStartInfo))
                {
                    if (process != null)
                    {
                        process.WaitForExit(3000);
                        bool ffprobeOk = process.ExitCode == 0;
                        
                        if (!ffprobeOk)
                        {
                            System.Diagnostics.Debug.WriteLine($"FFmpegHelper: ffprobe.exe 测试失败 (退出码: {process.ExitCode})");
                            return false;
                        }
                    }
                    else
                    {
                        return false;
                    }
                }
                
                return true;
            }
            catch
            {
                // 测试失败
                return false;
            }
        }

        /// <summary>
        /// 测试系统 PATH 中的 FFmpeg（需要同时有 ffmpeg 和 ffprobe）
        /// </summary>
        private static bool TestFFmpegInPath()
        {
            try
            {
                // 测试 ffmpeg
                var processStartInfo = new ProcessStartInfo
                {
                    FileName = "ffmpeg",
                    Arguments = "-version",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };

                bool ffmpegOk = false;
                using (var process = Process.Start(processStartInfo))
                {
                    if (process != null)
                    {
                        process.WaitForExit(3000);
                        ffmpegOk = process.ExitCode == 0;
                    }
                }
                
                if (!ffmpegOk)
                    return false;
                
                // 测试 ffprobe
                processStartInfo = new ProcessStartInfo
                {
                    FileName = "ffprobe",
                    Arguments = "-version",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };

                using (var process = Process.Start(processStartInfo))
                {
                    if (process != null)
                    {
                        process.WaitForExit(3000);
                        return process.ExitCode == 0;
                    }
                }
            }
            catch
            {
                // 不在 PATH 中或不可用
            }
            return false;
        }

        /// <summary>
        /// 在指定目录中递归搜索 ffmpeg.exe
        /// </summary>
        /// <param name="directory">搜索的根目录</param>
        /// <param name="maxDepth">最大搜索深度</param>
        /// <param name="currentDepth">当前深度</param>
        /// <returns>找到的 ffmpeg.exe 路径，如果没找到返回 null</returns>
        private static string SearchFFmpegInDirectory(string directory, int maxDepth, int currentDepth = 0)
        {
            if (currentDepth >= maxDepth)
                return null;

            try
            {
                // 首先在当前目录查找（需要同时有 ffmpeg.exe 和 ffprobe.exe）
                string ffmpegPath = Path.Combine(directory, "ffmpeg.exe");
                string ffprobePath = Path.Combine(directory, "ffprobe.exe");
                
                if (File.Exists(ffmpegPath) && File.Exists(ffprobePath))
                {
                    // 检查文件大小是否合理（至少50KB，避免无效文件）
                    var ffmpegInfo = new FileInfo(ffmpegPath);
                    var ffprobeInfo = new FileInfo(ffprobePath);
                    if (ffmpegInfo.Length > 50 * 1024 && ffprobeInfo.Length > 50 * 1024) // 至少50KB
                    {
                        return ffmpegPath; // 返回 ffmpeg.exe 路径（目录验证会检查两个文件）
                    }
                }

                // 如果在当前目录没找到，且还有深度，则递归搜索子目录
                if (currentDepth < maxDepth - 1)
                {
                    var subdirectories = Directory.GetDirectories(directory);
                    
                    // 排除一些明显不相关的目录，提高搜索效率
                    var excludePatterns = new[] { "WindowsApps", "Windows", "Microsoft", "$Recycle.Bin", "System Volume Information" };
                    
                    foreach (var subDir in subdirectories)
                    {
                        string dirName = Path.GetFileName(subDir);
                        if (excludePatterns.Any(pattern => dirName.Contains(pattern, StringComparison.OrdinalIgnoreCase)))
                            continue;

                        try
                        {
                            var result = SearchFFmpegInDirectory(subDir, maxDepth, currentDepth + 1);
                            if (result != null)
                                return result;
                        }
                        catch
                        {
                            // 某些目录可能无权限访问，跳过
                            continue;
                        }
                    }
                }
            }
            catch
            {
                // 搜索失败，可能是权限问题
            }
            
            return null;
        }
    }
}
