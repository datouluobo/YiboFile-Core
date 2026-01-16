using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows;

namespace YiboFile.Services
{
    public static class OdaDownloader
    {
        private static readonly string OdaDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Dependencies", "ODAFileConverter");
        private static readonly string OdaExePath = Path.Combine(OdaDirectory, "ODAFileConverter.exe");
        
        // ODA File Converter 下载链接（这是一个示例，实际需要从 ODA 官网获取最新版本）
        // 注意：ODA 要求用户注册后才能下载，所以这里提供的是引导用户到官网的方案
        private static readonly string OdaDownloadPageUrl = "https://www.opendesign.com/guestfiles/oda_file_converter";
        
        /// <summary>
        /// 检查 ODA File Converter 是否已安装
        /// </summary>
        public static bool IsInstalled()
        {
            return File.Exists(OdaExePath);
        }
        
        /// <summary>
        /// 获取 ODA 安装路径
        /// </summary>
        public static string GetInstallPath()
        {
            return OdaExePath;
        }
        
        /// <summary>
        /// 打开 ODA 官方下载页面
        /// </summary>
        public static void OpenDownloadPage()
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = OdaDownloadPageUrl,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"无法打开下载页面: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        /// <summary>
        /// 从本地文件安装 ODA File Converter
        /// </summary>
        public static async Task<bool> InstallFromLocalFile(string zipFilePath, IProgress<string> progress)
        {
            try
            {
                progress?.Report("正在验证文件...");
                
                if (!File.Exists(zipFilePath))
                {
                    progress?.Report("错误：文件不存在");
                    return false;
                }
                
                // 创建目标目录
                Directory.CreateDirectory(OdaDirectory);
                
                progress?.Report("正在解压文件...");
                
                // 解压 ZIP 文件
                await Task.Run(() =>
                {
                    ZipFile.ExtractToDirectory(zipFilePath, OdaDirectory, true);
                });
                
                progress?.Report("正在验证安装...");
                
                // 验证是否成功安装
                if (File.Exists(OdaExePath))
                {
                    progress?.Report("安装成功！");
                    return true;
                }
                else
                {
                    // 尝试查找 ODAFileConverter.exe
                    var foundExe = FindOdaExecutable(OdaDirectory);
                    if (!string.IsNullOrEmpty(foundExe))
                    {
                        progress?.Report($"找到可执行文件: {foundExe}");
                        return true;
                    }
                    
                    progress?.Report("错误：未找到 ODAFileConverter.exe");
                    return false;
                }
            }
            catch (Exception ex)
            {
                progress?.Report($"安装失败: {ex.Message}");
                return false;
            }
        }
        
        /// <summary>
        /// 在目录中递归查找 ODAFileConverter.exe
        /// </summary>
        private static string FindOdaExecutable(string directory)
        {
            try
            {
                var files = Directory.GetFiles(directory, "ODAFileConverter.exe", SearchOption.AllDirectories);
                return files.Length > 0 ? files[0] : null;
            }
            catch
            {
                return null;
            }
        }
        
        /// <summary>
        /// 卸载 ODA File Converter
        /// </summary>
        public static bool Uninstall()
        {
            try
            {
                if (Directory.Exists(OdaDirectory))
                {
                    Directory.Delete(OdaDirectory, true);
                    return true;
                }
                return false;
            }
            catch
            {
                return false;
            }
        }
    }
}

