using System;
using YiboFile.Models;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

using YiboFile.Services.Core;

namespace YiboFile.Services.Archive
{
    public class ArchiveService
    {
        private readonly string _sevenZipPath;

        public ArchiveService()
        {
            // Set path to 7z.exe (assuming it's in Dependencies/7-Zip relative to the executable)
            _sevenZipPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Dependencies", "7-Zip", "7z.exe");
        }

        public bool IsArchive(string path)
        {
            if (string.IsNullOrEmpty(path)) return false;

            // Check extension first for quick filtering
            string ext = Path.GetExtension(path).ToLowerInvariant();
            if (ext == ".zip" || ext == ".7z" || ext == ".rar" || ext == ".tar" || ext == ".gz")
            {
                return true;
            }
            return false;
        }

        /// <summary>
        /// Gets the content of an archive at a specific inner path.
        /// </summary>
        /// <param name="archivePath">Path to the archive file.</param>
        /// <param name="innerPath">Virtual path inside the archive (e.g., "Folder/Subfolder"). Use "" for root.</param>
        /// <returns>List of FileSystemItems representing the content.</returns>
        public async Task<List<FileSystemItem>> GetArchiveContentAsync(string archivePath, string innerPath)
        {
            var items = new List<FileSystemItem>();

            if (!File.Exists(_sevenZipPath))
            {
                // Fallback or error logging? 
                // For now, return empty list if CLI tool is missing.
                return items;
            }

            // Normalizing inner path
            innerPath = innerPath?.Replace("\\", "/").Trim('/') ?? "";

            // 7z l "archive.zip"
            var startInfo = new ProcessStartInfo
            {
                FileName = _sevenZipPath,
                Arguments = $"l \"{archivePath}\" -slt -sccUTF-8", // -slt for easy parsing, -sccUTF-8 for utf8 output
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                StandardOutputEncoding = System.Text.Encoding.UTF8 // Ensure we read UTF8 if possible
            };

            // Using Try-Catch for process execution
            try
            {
                using (var process = new Process { StartInfo = startInfo })
                {
                    process.Start();
                    string output = await process.StandardOutput.ReadToEndAsync();
                    await process.WaitForExitAsync();

                    // Parse Output
                    items = Parse7zOutput(output, archivePath, innerPath);
                }
            }
            catch (Exception ex)
            {
                // Log error
                Console.WriteLine($"Error reading archive: {ex.Message}");
            }

            return items;
        }

        private List<FileSystemItem> Parse7zOutput(string output, string archivePath, string targetInnerPath)
        {
            var results = new List<FileSystemItem>();

            // Normalize target path
            string normalizedTarget = string.IsNullOrEmpty(targetInnerPath) ? "" : targetInnerPath.TrimEnd('/') + "/";
            bool isRoot = string.IsNullOrEmpty(normalizedTarget);

            // 7z -slt output format is blocks of properties separated by empty lines.
            // Path = Folder/File.txt
            // Size = 1234
            // Attributes = A
            // Modified = 2023-01-01 12:00:00

            var blocks = output.Split(new[] { "\r\n\r\n", "\n\n" }, StringSplitOptions.RemoveEmptyEntries);

            foreach (var block in blocks)
            {
                var lines = block.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                string path = null;
                string sizeStr = null;
                string modifiedStr = null;
                bool isDir = false;
                string attributes = null;

                foreach (var line in lines)
                {
                    if (line.StartsWith("Path = ")) path = line.Substring(7).Trim();
                    else if (line.StartsWith("Size = ")) sizeStr = line.Substring(7).Trim();
                    else if (line.StartsWith("Modified = ")) modifiedStr = line.Substring(11).Trim();
                    else if (line.StartsWith("Attributes = ")) attributes = line.Substring(13).Trim();
                    else if (line.StartsWith("Folder = +")) isDir = true;
                }

                if (path == null) continue;

                // Normalize path from 7z
                path = path.Replace("\\", "/");

                // Filter items that are direct children of targetInnerPath
                // e.g. Target: "MyFolder/"
                // Item: "MyFolder/MyFile.txt" -> YES
                // Item: "MyFolder/SubFolder/File.txt" -> NO (That's a grandchild)
                // Item: "OtherFolder/File.txt" -> NO

                if (isRoot)
                {
                    // If we are looking for root, we want items that have NO slashes, 
                    // OR items that have slashes but are only 1 level deep? 
                    // Actually, 7z lists ALL files. We need to filter.

                    if (path.StartsWith(normalizedTarget))
                    {
                        string relativePath = path;
                        // For root, relativePath is just path.

                        // Check if it's a direct child
                        // If it contains '/', it might be a subfolder or file in subfolder.
                        int slashIndex = relativePath.IndexOf('/');

                        // Case 1: "Folder" (Directory) -> slashIndex = -1. Keep.
                        // Case 2: "File.txt" -> slashIndex = -1. Keep.
                        // Case 3: "Folder/File.txt" -> slashIndex = 6. 
                        // If 7z reports directories explicitly, "Folder" will appear as a separate entry.
                        // If it doesn't, we might need to synthesize directory entries.
                        // But 7z usually reports folder entries if they exist in the archive.

                        if (slashIndex == -1)
                        {
                            // It's a direct child
                            results.Add(CreateItem(path, sizeStr, modifiedStr, isDir, attributes, archivePath));
                        }
                        else
                        {
                            // It's a deeper item. 
                            // However, some archives don't have explicit directory entries.
                            // e.g. "Folder/File.txt" exists, but "Folder" entry doesn't.
                            // In that case, we should check if we already added "Folder".
                            string topLevel = relativePath.Substring(0, slashIndex);
                            if (!results.Exists(x => x.Name == topLevel))
                            {
                                results.Add(CreateSynthesizedDirectory(topLevel, archivePath));
                            }
                        }
                    }
                }
                else
                {
                    // Target: "Inner/"
                    // Item: "Inner/File.txt" -> relative: "File.txt" -> Keep
                    // Item: "Inner/Sub/File.txt" -> relative: "Sub/File.txt" -> Synthesize "Sub"

                    if (path.StartsWith(normalizedTarget))
                    {
                        // Must be strictly longer to be a child (avoid listing the folder itself if 7z returns it)
                        if (path.Length > normalizedTarget.Length)
                        {
                            string relativePath = path.Substring(normalizedTarget.Length);
                            int slashIndex = relativePath.IndexOf('/');

                            if (slashIndex == -1)
                            {
                                results.Add(CreateItem(path, sizeStr, modifiedStr, isDir, attributes, archivePath));
                            }
                            else
                            {
                                string topLevel = relativePath.Substring(0, slashIndex);
                                if (!results.Exists(x => x.Name == topLevel))
                                {
                                    string fullVirtualPath = normalizedTarget + topLevel;
                                    results.Add(CreateSynthesizedDirectory(topLevel, archivePath, fullVirtualPath));
                                }
                            }
                        }
                    }
                }
            }

            return results;
        }

        private FileSystemItem CreateItem(string fullPath, string sizeStr, string modifiedStr, bool isDir, string attributes, string archivePath)
        {
            long size = 0;
            long.TryParse(sizeStr, out size);

            // Attributes 'D' also indicates directory
            if (attributes != null && attributes.Contains("D")) isDir = true;

            string name = Path.GetFileName(fullPath.TrimEnd('/')); // Handle cases where path ends with slash

            return new FileSystemItem
            {
                Name = name,
                // The Path needs to be a special "Archive Path" that the FileListService can recognize later.
                // Format: zip://[ArchiveFilePath]|[InnerPath]
                // Example: zip://C:\Data\Test.zip|Folder/File.txt
                Path = $"{ProtocolManager.ZipProtocol}{archivePath}|{fullPath}",
                Type = isDir ? "文件夹" : Path.GetExtension(name),
                Size = isDir ? "" : FormatFileSize(size), // We don't verify folder size inside archive yet
                SizeBytes = size,
                ModifiedDate = modifiedStr, // 7z returns YYYY-MM-DD HH:MM:SS
                IsDirectory = isDir,
                // We can parse date to DateTime if needed, skipping for now or doing simple parse
                ModifiedDateTime = DateTime.TryParse(modifiedStr, out DateTime dt) ? dt : DateTime.MinValue
            };
        }

        private FileSystemItem CreateSynthesizedDirectory(string name, string archivePath, string explicitPath = null)
        {
            string innerPath = explicitPath ?? name;
            return new FileSystemItem
            {
                Name = name,
                Path = $"{ProtocolManager.ZipProtocol}{archivePath}|{innerPath}",
                Type = "文件夹",
                IsDirectory = true,
                Size = "",
                ModifiedDate = "",
                SizeBytes = 0
            };
        }

        private string FormatFileSize(long bytes)
        {
            // Reusing logic from FileListService or keeping simple
            string[] sizes = { "B", "KB", "MB", "GB", "TB" };
            double len = bytes;
            int order = 0;
            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len = len / 1024;
            }
            return $"{len:0.##} {sizes[order]}";
        }
    }
}

