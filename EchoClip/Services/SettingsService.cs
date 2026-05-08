using System;
using System.IO;
using System.Text.Json;
using EchoClip.Models;
using Microsoft.Win32;

namespace EchoClip.Services
{
    public static class SettingsService
    {
        private static readonly string SettingsDir =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "EchoClip");

        private static string SettingsPath => Path.Combine(SettingsDir, "settings.json");

        public static AppSettings Load()
        {
            try
            {
                if (File.Exists(SettingsPath))
                {
                    var json = File.ReadAllText(SettingsPath);
                    var s = JsonSerializer.Deserialize<AppSettings>(json);
                    if (s != null) return s;
                }
            }
            catch { /* fall through to defaults */ }

            return new AppSettings
            {
                ClipsDirectory = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                    "EchoClip", "Clips")
            };
        }

        public static void Save(AppSettings settings)
        {
            Directory.CreateDirectory(SettingsDir);
            var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(SettingsPath, json);
        }

        public static void SetStartWithWindows(bool enable)
        {
            const string key = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
            using var reg = Registry.CurrentUser.OpenSubKey(key, writable: true);
            if (reg == null) return;
            if (enable)
                reg.SetValue("EchoClip", $"\"{Environment.ProcessPath}\"");
            else
                reg.DeleteValue("EchoClip", throwOnMissingValue: false);
        }
    }
}
