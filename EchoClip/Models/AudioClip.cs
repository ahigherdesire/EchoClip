using System;

namespace EchoClip.Models
{
    public class AudioClip
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string FilePath { get; set; } = string.Empty;
        public TimeSpan Duration { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public int? CategoryId { get; set; }
        public float Volume { get; set; } = 1.0f;
        public string? HotkeyBinding { get; set; }
        public float FadeInMs { get; set; } = 0f;
        public float FadeOutMs { get; set; } = 0f;
        public long FileSizeBytes { get; set; }

        public ClipCategory? Category { get; set; }

        public string DurationDisplay => Duration.TotalHours >= 1
            ? Duration.ToString(@"h\:mm\:ss")
            : Duration.ToString(@"m\:ss");

        public string SizeDisplay => FileSizeBytes switch
        {
            >= 1_048_576 => $"{FileSizeBytes / 1_048_576.0:F1} MB",
            >= 1_024 => $"{FileSizeBytes / 1_024.0:F0} KB",
            _ => $"{FileSizeBytes} B"
        };
    }
}
