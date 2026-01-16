using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace YiboFile.Services.ClipboardHistory
{
    /// <summary>
    /// 剪切板内容类型
    /// </summary>
    public enum ClipboardItemType
    {
        Files,
        Text
    }

    /// <summary>
    /// 剪切板历史记录项
    /// </summary>
    public class ClipboardHistoryItem
    {
        public ClipboardItemType Type { get; set; }
        public DateTime Timestamp { get; set; }
        public List<string> Files { get; set; } = new();
        public string Text { get; set; } = string.Empty;
        public bool IsCut { get; set; }

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

        private const int MaxHistoryCount = 20;
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

        /// <summary>
        /// 剪切板内容变化事件
        /// </summary>
        public event Action<ClipboardHistoryItem> ClipboardChanged;

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
                System.Diagnostics.Debug.WriteLine("[ClipboardHistoryService] Started listening");
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
            System.Diagnostics.Debug.WriteLine("[ClipboardHistoryService] Stopped listening");
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
                    // 检查文本
                    else if (System.Windows.Clipboard.ContainsText())
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
                            }
                        }

                        // 添加到历史
                        History.Insert(0, item);

                        // 限制数量
                        while (History.Count > MaxHistoryCount)
                            History.RemoveAt(History.Count - 1);

                        ClipboardChanged?.Invoke(item);
                        System.Diagnostics.Debug.WriteLine($"[ClipboardHistoryService] Added {item.Type}: {item.Preview}");
                    }
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ClipboardHistoryService] Error: {ex.Message}");
            }
        }

        /// <summary>
        /// 将历史项粘贴到剪切板
        /// </summary>
        public bool SetToClipboard(ClipboardHistoryItem item)
        {
            try
            {
                if (item.Type == ClipboardItemType.Files)
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
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ClipboardHistoryService] SetToClipboard error: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 删除历史项
        /// </summary>
        public void RemoveItem(ClipboardHistoryItem item)
        {
            History.Remove(item);
        }

        /// <summary>
        /// 清空历史
        /// </summary>
        public void ClearHistory()
        {
            History.Clear();
        }

        public void Dispose()
        {
            StopListening();
        }
    }
}

