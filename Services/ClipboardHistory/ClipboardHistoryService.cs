using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using System.Text.Json;
using System.IO;

namespace YiboFile.Services.ClipboardHistory
{
    /// <summary>
    /// 剪切板内容类型
    /// </summary>
    public enum ClipboardItemType
    {
        Files,
        Text,
        Image
    }

    /// <summary>剪切板内容过滤类型</summary>
    public enum ClipboardFilterType
    {
        All,
        Files,
        Images,
        Text,
        Pinned
    }

    /// <summary>面板宽度布局模式</summary>
    public enum ClipboardLayoutMode
    {
        Compact,   // 紧凑（< 550px）: 纯列表
        Wide       // 宽屏（≥ 550px）: 列表 + 内联预览
    }

    /// <summary>
    /// 剪切板历史记录项
    /// </summary>
    public class ClipboardHistoryItem : System.ComponentModel.INotifyPropertyChanged
    {
        public event System.ComponentModel.PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([System.Runtime.CompilerServices.CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(propertyName));
        }

        public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];
        public ClipboardItemType Type { get; set; }
        public DateTime Timestamp { get; set; }
        public List<string> Files { get; set; } = new();
        public string Text { get; set; } = string.Empty;
        public bool IsCut { get; set; }

        private bool _isPinned;
        public bool IsPinned
        {
            get => _isPinned;
            set { if (_isPinned != value) { _isPinned = value; OnPropertyChanged(); } }
        }

        public string ThumbnailCachePath { get; set; }
        public long? TotalFileSize { get; set; }

        [System.Text.Json.Serialization.JsonIgnore]
        public bool IsImage => Type == ClipboardItemType.Files
            && Files.Count == 1
            && _imageExtensions.Contains(System.IO.Path.GetExtension(Files[0]).ToLowerInvariant());

        [System.Text.Json.Serialization.JsonIgnore]
        public bool IsFolder => Type == ClipboardItemType.Files
            && Files.Count == 1
            && System.IO.Directory.Exists(Files[0]);

        public bool IsScreenCapture { get; set; }

        [System.Text.Json.Serialization.JsonIgnore]
        public string TimeGroup
        {
            get
            {
                var today = DateTime.Today;
                if (Timestamp.Date == today) return "今天";
                if (Timestamp.Date == today.AddDays(-1)) return "昨天";
                if (Timestamp.Date >= today.AddDays(-(int)today.DayOfWeek)) return "本周";
                return "更早";
            }
        }

        [System.Text.Json.Serialization.JsonIgnore]
        public string TypeDisplayName => Type switch
        {
            ClipboardItemType.Files when IsImage => "图片",
            ClipboardItemType.Files when IsFolder => "文件夹",
            ClipboardItemType.Files => $"文件 ({Files.Count})",
            ClipboardItemType.Text => "文本",
            ClipboardItemType.Image => "截屏图片",
            _ => "未知"
        };

        [System.Text.Json.Serialization.JsonIgnore]
        public string FileSizeText => TotalFileSize switch
        {
            null => "",
            < 1024 => $"{TotalFileSize} B",
            < 1024 * 1024 => $"{TotalFileSize / 1024.0:F1} KB",
            < 1024L * 1024 * 1024 => $"{TotalFileSize / (1024.0 * 1024):F1} MB",
            _ => $"{TotalFileSize / (1024.0 * 1024 * 1024):F2} GB"
        };

        [System.Text.Json.Serialization.JsonIgnore]
        public string FirstFilePath => Files?.FirstOrDefault() ?? "";

        private static readonly HashSet<string> _imageExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".webp", ".ico", ".svg", ".tiff", ".tif"
        };

        /// <summary>
        /// 获取预览文本
        /// </summary>
        public string Preview
        {
            get
            {
                if (Type == ClipboardItemType.Files)
                {
                    if (Files.Count == 1)
                        return System.IO.Path.GetFileName(Files[0]);
                    return $"{System.IO.Path.GetFileName(Files[0])} 等 {Files.Count} 个项目";
                }
                else if (Type == ClipboardItemType.Image)
                {
                    return "图片数据";
                }
                else
                {
                    var text = Text.Length > 100 ? Text.Substring(0, 100) + "..." : Text;
                    return text.Replace("\r\n", " ").Replace("\n", " ");
                }
            }
        }

        /// <summary>
        /// 获取时间描述
        /// </summary>
        public string TimeAgo
        {
            get
            {
                var span = DateTime.Now - Timestamp;
                if (span.TotalSeconds < 60) return "刚刚";
                if (span.TotalMinutes < 60) return $"{(int)span.TotalMinutes} 分钟前";
                if (span.TotalHours < 24) return $"{(int)span.TotalHours} 小时前";
                return Timestamp.ToString("MM-dd HH:mm");
            }
        }
    }

    /// <summary>
    /// 剪切板历史记录服务
    /// </summary>
    public class ClipboardHistoryService : IDisposable
    {
        private static ClipboardHistoryService _instance;
        public static ClipboardHistoryService Instance => _instance ??= new ClipboardHistoryService();

        private AppConfig Config => Services.Config.ConfigurationService.Instance.Config;
        private string HistoryFilePath => Path.Combine(ConfigManager.GetBaseDirectory(), "clipboard_history.json");

        private int MaxHistoryCount => Config.ClipboardMaxHistory;
        private IntPtr _hwnd;
        private HwndSource _hwndSource;
        private bool _isListening;

        // Win32 API
        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool AddClipboardFormatListener(IntPtr hwnd);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool RemoveClipboardFormatListener(IntPtr hwnd);

        private const int WM_CLIPBOARDUPDATE = 0x031D;

        /// <summary>
        /// 历史记录列表
        /// </summary>
        public ObservableCollection<ClipboardHistoryItem> History { get; } = new();

        /// <summary>
        /// 仅文件类型的历史记录
        /// </summary>
        public IEnumerable<ClipboardHistoryItem> FileHistory => History.Where(h => h.Type == ClipboardItemType.Files);

        /// <summary>
        /// 仅文本类型的历史记录
        /// </summary>
        public IEnumerable<ClipboardHistoryItem> TextHistory => History.Where(h => h.Type == ClipboardItemType.Text);



        private ClipboardHistoryService() { }

        /// <summary>
        /// 开始监听剪切板变化
        /// </summary>
        public void StartListening(Window window)
        {
            if (_isListening) return;

            var helper = new WindowInteropHelper(window);
            _hwnd = helper.Handle;

            if (_hwnd == IntPtr.Zero)
            {
                // 窗口尚未完全初始化，延迟处理
                window.Loaded += (s, e) => StartListening(window);
                return;
            }

            _hwndSource = HwndSource.FromHwnd(_hwnd);
            _hwndSource?.AddHook(WndProc);

            if (AddClipboardFormatListener(_hwnd))
            {
                _isListening = true;
            }
        }

        /// <summary>
        /// 停止监听
        /// </summary>
        public void StopListening()
        {
            if (!_isListening) return;

            _hwndSource?.RemoveHook(WndProc);

            if (_hwnd != IntPtr.Zero)
            {
                RemoveClipboardFormatListener(_hwnd);
            }

            _isListening = false;
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == WM_CLIPBOARDUPDATE)
            {
                OnClipboardChanged();
                handled = true;
            }
            return IntPtr.Zero;
        }

        private void OnClipboardChanged()
        {
            try
            {
                Application.Current?.Dispatcher?.Invoke(() =>
                {
                    ClipboardHistoryItem item = null;

                    // 检查文件
                    if (System.Windows.Clipboard.ContainsFileDropList())
                    {
                        var files = System.Windows.Clipboard.GetFileDropList();
                        if (files.Count > 0)
                        {
                            var fileList = new List<string>();
                            foreach (string file in files) fileList.Add(file);

                            // 检测是否为剪切操作
                            bool isCut = false;
                            if (System.Windows.Clipboard.ContainsData("Preferred DropEffect"))
                            {
                                var data = System.Windows.Clipboard.GetData("Preferred DropEffect");
                                if (data is System.IO.MemoryStream ms)
                                {
                                    var bytes = ms.ToArray();
                                    if (bytes.Length >= 4)
                                    {
                                        int effect = BitConverter.ToInt32(bytes, 0);
                                        isCut = (effect == 2);
                                    }
                                }
                            }

                            item = new ClipboardHistoryItem
                            {
                                Type = ClipboardItemType.Files,
                                Timestamp = DateTime.Now,
                                Files = fileList,
                                IsCut = isCut
                            };
                        }
                    }
                    // 检查图片/截屏
                    else if (Config.ClipboardCaptureScreenshots && System.Windows.Clipboard.ContainsImage())
                    {
                        var bitmapSource = System.Windows.Clipboard.GetImage();
                        if (bitmapSource != null)
                        {
                            bitmapSource.Freeze();
                            var savedPath = ClipboardThumbnailService.SaveScreenCapture(bitmapSource);
                            if (savedPath != null)
                            {
                                var thumbPath = Path.Combine(
                                    Path.GetDirectoryName(savedPath) ?? "",
                                    $"thumb_{Path.GetFileName(savedPath)}");

                                item = new ClipboardHistoryItem
                                {
                                    Type = ClipboardItemType.Image,
                                    Timestamp = DateTime.Now,
                                    Files = new List<string> { savedPath },
                                    IsScreenCapture = true,
                                    ThumbnailCachePath = File.Exists(thumbPath) ? thumbPath : savedPath,
                                    TotalFileSize = new System.IO.FileInfo(savedPath).Length
                                };
                            }
                        }
                    }
                    // 检查文本
                    else if (Config.ClipboardCaptureText && System.Windows.Clipboard.ContainsText())
                    {
                        var text = System.Windows.Clipboard.GetText();
                        if (!string.IsNullOrWhiteSpace(text))
                        {
                            item = new ClipboardHistoryItem
                            {
                                Type = ClipboardItemType.Text,
                                Timestamp = DateTime.Now,
                                Text = text
                            };
                        }
                    }

                    if (item != null)
                    {
                        if (item.Type == ClipboardItemType.Files && !item.IsScreenCapture)
                        {
                            try
                            {
                                long total = 0;
                                foreach (var f in item.Files)
                                {
                                    if (File.Exists(f)) total += new System.IO.FileInfo(f).Length;
                                }
                                item.TotalFileSize = total > 0 ? total : null;
                            }
                            catch { }

                            if (item.IsImage)
                            {
                                item.ThumbnailCachePath = ClipboardThumbnailService.GenerateFileThumbnail(item.Files[0]);
                            }
                        }

                        // 去重：检查是否与最近一条相同
                        if (History.Count > 0)
                        {
                            var last = History[0];
                            if (last.Type == item.Type)
                            {
                                if (item.Type == ClipboardItemType.Text && last.Text == item.Text)
                                    return;
                                if (item.Type == ClipboardItemType.Files &&
                                    last.Files.SequenceEqual(item.Files))
                                    return;
                                if (item.Type == ClipboardItemType.Image && last.TotalFileSize == item.TotalFileSize)
                                    return; // 对图片采取一个简单的判断
                            }
                        }

                        // 添加到历史
                        History.Insert(0, item);

                        // 限制数量
                        while (History.Count > MaxHistoryCount)
                            History.RemoveAt(History.Count - 1);

                        ScheduleSave();
                    }
                });
            }
            catch (Exception)
            {
            }
        }

        /// <summary>
        /// 将历史项粘贴到剪切板
        /// </summary>
        public bool SetToClipboard(ClipboardHistoryItem item)
        {
            try
            {
                if (item.Type == ClipboardItemType.Files || item.Type == ClipboardItemType.Image && item.Files.Count > 0)
                {
                    var data = new DataObject();
                    var fileDropList = new System.Collections.Specialized.StringCollection();
                    fileDropList.AddRange(item.Files.ToArray());
                    data.SetFileDropList(fileDropList);

                    // 设置操作类型
                    int effect = item.IsCut ? 2 : 5;
                    var ms = new System.IO.MemoryStream(BitConverter.GetBytes(effect));
                    data.SetData("Preferred DropEffect", ms);

                    System.Windows.Clipboard.SetDataObject(data, true);
                }
                else
                {
                    System.Windows.Clipboard.SetText(item.Text);
                }
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// 删除历史项
        /// </summary>
        public void RemoveItem(ClipboardHistoryItem item)
        {
            History.Remove(item);
            ScheduleSave();
        }

        /// <summary>
        /// 清空历史
        /// </summary>
        public void ClearHistory()
        {
            History.Clear();
            ScheduleSave();
        }

        public void Dispose()
        {
            StopListening();
        }

        private DispatcherTimer _saveDebounceTimer;

        private void ScheduleSave()
        {
            if (!Config.ClipboardPersistHistory) return;

            if (_saveDebounceTimer == null)
            {
                _saveDebounceTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
                _saveDebounceTimer.Tick += (s, e) =>
                {
                    _saveDebounceTimer.Stop();
                    SaveHistory();
                };
            }
            _saveDebounceTimer.Stop();
            _saveDebounceTimer.Start();
        }

        public void SaveHistory()
        {
            try
            {
                var data = new ClipboardHistoryData
                {
                    Version = 1,
                    LastCleanup = DateTime.Now,
                    Items = History.Select(ClipboardHistoryItemDto.FromDomain).ToList()
                };
                var json = JsonSerializer.Serialize(data, new JsonSerializerOptions
                {
                    WriteIndented = true,
                    Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                });
                File.WriteAllText(HistoryFilePath, json);
            }
            catch (Exception ex)
            {
                Services.Core.FileLogger.Log($"[ClipboardHistory] 保存失败: {ex.Message}");
            }
        }

        public void LoadHistory()
        {
            try
            {
                if (!Config.ClipboardPersistHistory || !File.Exists(HistoryFilePath)) return;

                var json = File.ReadAllText(HistoryFilePath);
                var data = JsonSerializer.Deserialize<ClipboardHistoryData>(json);
                if (data?.Items == null) return;

                History.Clear();
                foreach (var dto in data.Items)
                {
                    History.Add(dto.ToDomain());
                }
            }
            catch (Exception ex)
            {
                Services.Core.FileLogger.Log($"[ClipboardHistory] 加载失败: {ex.Message}");
            }
        }

        public void CleanExpiredItems()
        {
            if (!Config.ClipboardAutoClean || Config.ClipboardRetentionDays <= 0) return;

            var cutoff = DateTime.Now.AddDays(-Config.ClipboardRetentionDays);
            var expired = History.Where(h => !h.IsPinned && h.Timestamp < cutoff).ToList();

            foreach (var item in expired)
            {
                History.Remove(item);
            }

            if (expired.Count > 0)
            {
                ScheduleSave();
                Services.Core.FileLogger.Log($"[ClipboardHistory] 已清理 {expired.Count} 条过期记录");
            }

            ClipboardThumbnailService.CleanupThumbnails(Config.ClipboardRetentionDays);
        }

        public void TogglePin(ClipboardHistoryItem item)
        {
            if (item == null) return;
            item.IsPinned = !item.IsPinned;
            ScheduleSave();
        }
    }
}

