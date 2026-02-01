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

        public async Task LoadAsync(string filePath)
        {
            FilePath = filePath;
            Title = Path.GetFileName(filePath);
            Icon = "📦";
            IsLoading = true;

            try
            {
                var extension = Path.GetExtension(filePath)?.ToLower();
                if (extension == ".zip")
                {
                    await LoadZipAsync(filePath);
                }
                else
                {
                    // For 7z/rar we still need the external 7z.exe logic
                    // We can either move that logic to a service or keep it here for now
                    Stats = "目前仅支持 ZIP 预览。其他格式请使用外部程序。";
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

        private async Task LoadZipAsync(string filePath)
        {
            await Task.Run(() =>
            {
                try
                {
                    // Register GBK encoding for older ZIP files
                    System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);

                    using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                    using var archive = new ZipArchive(fs, ZipArchiveMode.Read, false, Encoding.GetEncoding("GBK"));

                    var entryVms = archive.Entries
                        .Select(e => new ArchiveEntryViewModel
                        {
                            Name = e.FullName,
                            Size = FormatFileSize(e.Length),
                            IsDirectory = e.FullName.EndsWith("/") || e.FullName.EndsWith("\\")
                        })
                        .ToList();

                    System.Windows.Application.Current.Dispatcher.Invoke(() =>
                    {
                        Entries.Clear();
                        foreach (var evm in entryVms) Entries.Add(evm);
                        Stats = $"文件数: {entryVms.Count(x => !x.IsDirectory)}, 目录数: {entryVms.Count(x => x.IsDirectory)}";
                    });
                }
                catch
                {
                    // Fallback to UTF8 if GBK fails
                    using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                    using var archive = new ZipArchive(fs, ZipArchiveMode.Read, false, Encoding.UTF8);
                    // Same as above...
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
