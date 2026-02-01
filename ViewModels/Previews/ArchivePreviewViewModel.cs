using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Input;
using YiboFile.ViewModels;
using YiboFile.Previews;
using System.Collections.Generic;
using System.IO.Compression;
using System.Text;
using System.Linq;

namespace YiboFile.ViewModels.Previews
{
    public class ArchiveEntryViewModel : BaseViewModel
    {
        public string Name { get; set; }
        public string Size { get; set; }
        public bool IsDirectory { get; set; }
        public string Icon => IsDirectory ? "📁" : "📄";
    }

    public class ArchivePreviewViewModel : BasePreviewViewModel
    {
        private ObservableCollection<ArchiveEntryViewModel> _entries = new();
        public ObservableCollection<ArchiveEntryViewModel> Entries
        {
            get => _entries;
            set => SetProperty(ref _entries, value);
        }

        private string _stats;
        public string Stats
        {
            get => _stats;
            set => SetProperty(ref _stats, value);
        }

        public ArchivePreviewViewModel()
        {
            OpenExternalCommand = new RelayCommand(() => PreviewHelper.OpenInDefaultApp(FilePath));
        }

        public async Task LoadAsync(string filePath, System.Threading.CancellationToken token = default)
        {
            FilePath = filePath;
            Title = Path.GetFileName(filePath);
            Icon = "📦";
            IsLoading = true;

            try
            {
                var extension = Path.GetExtension(filePath)?.ToLower();
                if (extension == ".zip" || extension == ".apk" || extension == ".jar")
                {
                    await LoadZipAsync(filePath, token);
                }
                else
                {
                    // For 7z/rar we still need the external 7z.exe logic
                    // We can either move that logic to a service or keep it here for now
                    Stats = "目前仅支持 ZIP/APK 预览。其他格式请使用外部程序。";
                }
            }
            catch (Exception ex)
            {
                Title = "Error loading archive";
                Stats = $"加载失败: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task LoadZipAsync(string filePath, System.Threading.CancellationToken token)
        {
            await Task.Run(() =>
            {
                try
                {
                    // Register GBK encoding for older ZIP files
                    System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);

                    using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                    using var archive = new ZipArchive(fs, ZipArchiveMode.Read, false, Encoding.GetEncoding("GBK"));

                    if (token.IsCancellationRequested) return;

                    // 取前 1000 个条目，防止特大压缩包导致的内存和 UI 卡顿
                    var entriesCount = archive.Entries.Count;
                    var entryVms = archive.Entries
                        .Take(1000)
                        .Select(e => new ArchiveEntryViewModel
                        {
                            Name = e.FullName,
                            Size = FormatFileSize(e.Length),
                            IsDirectory = e.FullName.EndsWith("/") || e.FullName.EndsWith("\\")
                        })
                        .ToList();

                    if (token.IsCancellationRequested) return;

                    System.Windows.Application.Current.Dispatcher.Invoke(() =>
                    {
                        if (token.IsCancellationRequested) return;

                        Entries.Clear();
                        foreach (var evm in entryVms) Entries.Add(evm);

                        string limitMsg = entriesCount > 1000 ? " (仅显示前1000个项目)" : "";
                    });
                }
                catch
                {
                    // Fallback to UTF8 if GBK fails or other errors
                    try
                    {
                        if (token.IsCancellationRequested) return;
                        using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                        using var archive = new ZipArchive(fs, ZipArchiveMode.Read, false, Encoding.UTF8);

                        var entryVms = archive.Entries
                            .Take(1000)
                            .Select(e => new ArchiveEntryViewModel { Name = e.FullName, Size = FormatFileSize(e.Length), IsDirectory = e.FullName.EndsWith("/") })
                            .ToList();

                        System.Windows.Application.Current.Dispatcher.Invoke(() =>
                        {
                            Entries.Clear();
                            foreach (var evm in entryVms) Entries.Add(evm);
                            Stats = "加载完成 (UTF8 编码)";
                        });
                    }
                    catch { }
                }
            });
        }

        private string FormatFileSize(long bytes)
        {
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

