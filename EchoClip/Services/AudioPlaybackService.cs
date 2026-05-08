using System;
using System.IO;
using System.Threading.Tasks;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using EchoClip.Models;

namespace EchoClip.Services
{
    /// <summary>
    /// Routes a clip through a selected virtual audio device (e.g. VB-CABLE) so it appears
    /// as microphone input inside Discord. Optionally mirrors to a monitor output device.
    ///
    /// Audio pipeline: FileReader → VolumeSampleProvider → [FadeInOut] → WaveOut (virtual mic)
    ///                                                                  → WaveOut (monitor, optional)
    /// </summary>
    public sealed class AudioPlaybackService : IDisposable
    {
        private WaveOutEvent? _vmicOut;
        private WaveOutEvent? _monOut;
        private AppSettings   _settings;

        public bool       IsPlaying    { get; private set; }
        public AudioClip? CurrentClip  { get; private set; }

        public event EventHandler?        PlaybackStarted;
        public event EventHandler?        PlaybackStopped;
        public event EventHandler<string>? StatusChanged;

        public AudioPlaybackService(AppSettings settings) => _settings = settings;

        public void UpdateSettings(AppSettings s) => _settings = s;

        public void PlayClip(AudioClip clip)
        {
            StopPlayback();
            if (!File.Exists(clip.FilePath))
            {
                StatusChanged?.Invoke(this, $"File not found: {clip.FilePath}");
                return;
            }

            try
            {
                CurrentClip = clip;
                float finalVol = Math.Clamp(clip.Volume * _settings.GlobalVolume, 0f, 2f);

                // ── Virtual mic output ────────────────────────────────────────
                var vmicReader = new AudioFileReader(clip.FilePath) { Volume = finalVol };
                ISampleProvider vmicChain = BuildChain(vmicReader, clip);

                _vmicOut = CreateWaveOut(_settings.VirtualMicDeviceId);
                _vmicOut.Init(vmicChain);
                _vmicOut.PlaybackStopped += OnStopped;

                // ── Monitor output (optional) ─────────────────────────────────
                if (!string.IsNullOrEmpty(_settings.MonitorDeviceId))
                {
                    var monReader = new AudioFileReader(clip.FilePath) { Volume = finalVol };
                    _monOut = CreateWaveOut(_settings.MonitorDeviceId);
                    _monOut.Init(monReader);
                    _monOut.Play();
                }

                _vmicOut.Play();
                IsPlaying = true;
                PlaybackStarted?.Invoke(this, EventArgs.Empty);
                StatusChanged?.Invoke(this, $"Playing: {clip.Name}");

                // Schedule fade-out near end of clip if configured
                if (clip.FadeOutMs > 0 && vmicChain is FadeInOutSampleProvider fade)
                {
                    using var probe = new AudioFileReader(clip.FilePath);
                    var totalMs = probe.TotalTime.TotalMilliseconds;
                    var delayMs = (int)Math.Max(0, totalMs - clip.FadeOutMs - 200);
                    Task.Delay(delayMs).ContinueWith(_ =>
                    {
                        if (IsPlaying) fade.BeginFadeOut(clip.FadeOutMs);
                    });
                }
            }
            catch (Exception ex)
            {
                StatusChanged?.Invoke(this, $"Playback error: {ex.Message}");
                Cleanup();
            }
        }

        public void StopPlayback() => Cleanup();

        private ISampleProvider BuildChain(AudioFileReader reader, AudioClip clip)
        {
            if (clip.FadeInMs <= 0 && clip.FadeOutMs <= 0)
                return reader;

            var fade = new FadeInOutSampleProvider(reader, initiallySilent: clip.FadeInMs > 0);
            if (clip.FadeInMs > 0) fade.BeginFadeIn(clip.FadeInMs);
            return fade;
        }

        private static WaveOutEvent CreateWaveOut(string deviceName)
        {
            var wo = new WaveOutEvent();
            if (!string.IsNullOrEmpty(deviceName))
            {
                for (int i = 0; i < WaveOut.DeviceCount; i++)
                {
                    if (WaveOut.GetCapabilities(i).ProductName == deviceName)
                    {
                        wo.DeviceNumber = i;
                        break;
                    }
                }
            }
            return wo;
        }

        private void OnStopped(object? sender, StoppedEventArgs e)
        {
            Cleanup();
            PlaybackStopped?.Invoke(this, EventArgs.Empty);
            StatusChanged?.Invoke(this, "Playback finished");
        }

        private void Cleanup()
        {
            IsPlaying   = false;
            CurrentClip = null;

            _vmicOut?.Stop();
            _vmicOut?.Dispose();
            _vmicOut = null;

            _monOut?.Stop();
            _monOut?.Dispose();
            _monOut = null;
        }

        public static string[] GetOutputDeviceNames()
        {
            var names = new string[WaveOut.DeviceCount];
            for (int i = 0; i < WaveOut.DeviceCount; i++)
                names[i] = WaveOut.GetCapabilities(i).ProductName;
            return names;
        }

        public static string[] GetInputDeviceNames()
        {
            var names = new string[WaveIn.DeviceCount];
            for (int i = 0; i < WaveIn.DeviceCount; i++)
                names[i] = WaveIn.GetCapabilities(i).ProductName;
            return names;
        }

        public void Dispose() => Cleanup();
    }
}
