using System;
using System.IO;
using NAudio.Wave;
using EchoClip.Data;
using EchoClip.Helpers;
using EchoClip.Models;

namespace EchoClip.Services
{
    /// <summary>
    /// Translates raw PCM buffers and file imports into AudioClip records on disk + in the database.
    /// The only place audio ever touches the filesystem is here — triggered by an explicit user action.
    /// </summary>
    public sealed class ClipStorageService
    {
        private readonly AppDatabase _db;
        private AppSettings          _settings;

        public ClipStorageService(AppDatabase db, AppSettings settings)
        {
            _db       = db;
            _settings = settings;
        }

        public void UpdateSettings(AppSettings s) => _settings = s;

        /// <summary>
        /// Saves raw PCM bytes captured from the ring buffer.
        /// Fades are applied before encoding; the result goes to ClipsDirectory.
        /// </summary>
        public AudioClip SaveBufferedAudio(byte[] pcm, WaveFormat fmt, string? customName = null)
        {
            Directory.CreateDirectory(_settings.ClipsDirectory);

            string ts   = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
            string name = customName ?? $"Clip_{ts}";
            string ext  = _settings.DefaultFormat.ToLower() is "mp3" ? "mp3" : "wav";
            string path = UniquePath(_settings.ClipsDirectory, ts, ext);

            byte[] ready = AudioConverter.ApplyFades(pcm, fmt, 0f, 0f);

            if (ext == "mp3")
                AudioConverter.SaveMp3(ready, fmt, path);
            else
                AudioConverter.SaveWav(ready, fmt, path);

            var clip = new AudioClip
            {
                Name          = name,
                FilePath      = path,
                Duration      = AudioConverter.GetDuration(ready.Length, fmt),
                CreatedAt     = DateTime.Now,
                Volume        = 1.0f,
                FileSizeBytes = new FileInfo(path).Length
            };
            clip.Id = _db.InsertClip(clip);
            return clip;
        }

        /// <summary>Imports an existing audio file by copying it into ClipsDirectory.</summary>
        public AudioClip ImportFile(string sourcePath)
        {
            Directory.CreateDirectory(_settings.ClipsDirectory);

            string baseName = Path.GetFileNameWithoutExtension(sourcePath);
            string ext      = Path.GetExtension(sourcePath);
            string destPath = UniquePath(_settings.ClipsDirectory, baseName, ext.TrimStart('.'));

            File.Copy(sourcePath, destPath);

            TimeSpan duration = TimeSpan.Zero;
            try
            {
                using var reader = new AudioFileReader(destPath);
                duration = reader.TotalTime;
            }
            catch { /* non-audio or unsupported: store zero duration */ }

            var clip = new AudioClip
            {
                Name          = Path.GetFileNameWithoutExtension(destPath),
                FilePath      = destPath,
                Duration      = duration,
                CreatedAt     = DateTime.Now,
                Volume        = 1.0f,
                FileSizeBytes = new FileInfo(destPath).Length
            };
            clip.Id = _db.InsertClip(clip);
            return clip;
        }

        public void UpdateClip(AudioClip clip)  => _db.UpdateClip(clip);
        public void DeleteClip(AudioClip clip, bool deleteFile = true)
        {
            _db.DeleteClip(clip.Id);
            if (deleteFile && File.Exists(clip.FilePath))
                File.Delete(clip.FilePath);
        }

        public void ClearAllClips(bool deleteFiles = true)
        {
            foreach (var clip in _db.GetAllClips())
            {
                if (deleteFiles && File.Exists(clip.FilePath))
                    File.Delete(clip.FilePath);
            }
            _db.DeleteAllClips();
        }

        private static string UniquePath(string dir, string baseName, string ext)
        {
            string path = Path.Combine(dir, $"{baseName}.{ext}");
            int n = 1;
            while (File.Exists(path))
                path = Path.Combine(dir, $"{baseName}_{n++}.{ext}");
            return path;
        }
    }
}
