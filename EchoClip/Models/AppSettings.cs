namespace EchoClip.Models
{
    public class AppSettings
    {
        public string SaveHotkey { get; set; } = "F8";
        public string EmergencyMuteHotkey { get; set; } = "F9";
        public string InputDeviceId { get; set; } = string.Empty;
        public string VirtualMicDeviceId { get; set; } = string.Empty;
        public string MonitorDeviceId { get; set; } = string.Empty;
        public string ClipsDirectory { get; set; } = string.Empty;
        public string DefaultFormat { get; set; } = "wav";
        public float GlobalVolume { get; set; } = 1.0f;
        public int BufferSeconds { get; set; } = 10;
        public bool MinimizeToTray { get; set; } = true;
        public bool StartWithWindows { get; set; } = false;
        public bool ConsentGiven { get; set; } = false;
        public bool EnableDiscordDetection { get; set; } = true;
        public bool BufferOnlyDuringCall { get; set; } = true;
        public string DiscordClientId { get; set; } = string.Empty;
    }
}
