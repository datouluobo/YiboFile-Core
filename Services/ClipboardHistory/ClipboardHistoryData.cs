using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace YiboFile.Services.ClipboardHistory
{
    /// <summary>
    /// 剪切板历史持久化数据格式
    /// </summary>
    public class ClipboardHistoryData
    {
        [JsonPropertyName("version")]
        public int Version { get; set; } = 1;

        [JsonPropertyName("lastCleanup")]
        public DateTime LastCleanup { get; set; } = DateTime.Now;

        [JsonPropertyName("items")]
        public List<ClipboardHistoryItemDto> Items { get; set; } = new();
    }

    /// <summary>
    /// 序列化 DTO（避免序列化计算属性）
    /// </summary>
    public class ClipboardHistoryItemDto
    {
        [JsonPropertyName("id")]
        public string Id { get; set; }

        [JsonPropertyName("type")]
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public ClipboardItemType Type { get; set; }

        [JsonPropertyName("timestamp")]
        public DateTime Timestamp { get; set; }

        [JsonPropertyName("files")]
        public List<string> Files { get; set; }

        [JsonPropertyName("text")]
        public string Text { get; set; }

        [JsonPropertyName("isCut")]
        public bool IsCut { get; set; }

        [JsonPropertyName("isPinned")]
        public bool IsPinned { get; set; }

        [JsonPropertyName("isScreenCapture")]
        public bool IsScreenCapture { get; set; }

        [JsonPropertyName("thumbnailPath")]
        public string ThumbnailCachePath { get; set; }

        [JsonPropertyName("totalFileSize")]
        public long? TotalFileSize { get; set; }

        // ── DTO ↔ Domain 转换 ──
        public static ClipboardHistoryItemDto FromDomain(ClipboardHistoryItem item) => new()
        {
            Id = item.Id ?? Guid.NewGuid().ToString("N")[..8],
            Type = item.Type,
            Timestamp = item.Timestamp,
            Files = item.Files,
            Text = item.Text,
            IsCut = item.IsCut,
            IsPinned = item.IsPinned,
            IsScreenCapture = item.IsScreenCapture,
            ThumbnailCachePath = item.ThumbnailCachePath,
            TotalFileSize = item.TotalFileSize
        };

        public ClipboardHistoryItem ToDomain() => new()
        {
            Id = Id,
            Type = Type,
            Timestamp = Timestamp,
            Files = Files ?? new(),
            Text = Text ?? string.Empty,
            IsCut = IsCut,
            IsPinned = IsPinned,
            IsScreenCapture = IsScreenCapture,
            ThumbnailCachePath = ThumbnailCachePath,
            TotalFileSize = TotalFileSize
        };
    }
}
