using System;
using NAudio.Wave;
using EchoClip.Helpers;
using EchoClip.Models;

namespace EchoClip.Services
{
    /// <summary>
    /// Captures microphone audio into a rolling in-memory ring buffer.
    /// Nothing is written to disk until the user explicitly triggers a save.
    /// Start/Stop are controlled externally (by Discord detection or the user's manual toggle).
    /// </summary>
    public sealed class AudioCaptureService : IDisposable
    {
        private WaveInEvent?          _waveIn;
        private CircularAudioBuffer?  _ringBuf;
        private AppSettings           _settings;

        public bool        IsBuffering    { get; private set; }
        public WaveFormat? CaptureFormat  { get; private set; }

        public event EventHandler<string>? StatusChanged;

        public AudioCaptureService(AppSettings settings) => _settings = settings;

        public void UpdateSettings(AppSettings settings)
        {
            _settings = settings;
            if (IsBuffering) { Stop(); Start(); }
        }

        public void Start()
        {
            if (IsBuffering) return;

            // 44.1 kHz 16-bit stereo — universally compatible with NAudio / VB-CABLE
            CaptureFormat = new WaveFormat(44100, 16, 2);
            int bytesPerSec = CaptureFormat.SampleRate * CaptureFormat.Channels * (CaptureFormat.BitsPerSample / 8);
            _ringBuf = new CircularAudioBuffer(bytesPerSec * Math.Max(1, _settings.BufferSeconds));

            _waveIn = new WaveInEvent
            {
                WaveFormat        = CaptureFormat,
                BufferMilliseconds = 100
            };

            // Find device by name (WinMM truncates to 32 chars — acceptable)
            if (!string.IsNullOrEmpty(_settings.InputDeviceId))
            {
                for (int i = 0; i < WaveIn.DeviceCount; i++)
                {
                    if (WaveIn.GetCapabilities(i).ProductName == _settings.InputDeviceId)
                    {
                        _waveIn.DeviceNumber = i;
                        break;
                    }
                }
            }

            _waveIn.DataAvailable += (_, e) => _ringBuf.Write(e.Buffer, 0, e.BytesRecorded);
            _waveIn.StartRecording();
            IsBuffering = true;
            StatusChanged?.Invoke(this, "Audio buffer started");
        }

        public void Stop()
        {
            if (!IsBuffering) return;
            _waveIn?.StopRecording();
            _waveIn?.Dispose();
            _waveIn = null;
            IsBuffering = false;
            StatusChanged?.Invoke(this, "Audio buffer stopped");
        }

        /// <summary>Returns the last <paramref name="seconds"/> of buffered PCM, or null if no data.</summary>
        public byte[]? GetBufferedAudio(int seconds = -1)
        {
            if (_ringBuf == null || CaptureFormat == null) return null;
            int secs = seconds < 0 ? _settings.BufferSeconds : seconds;
            int bytesPerSec = CaptureFormat.SampleRate * CaptureFormat.Channels * (CaptureFormat.BitsPerSample / 8);
            return _ringBuf.ReadLast(bytesPerSec * secs);
        }

        public void ClearBuffer() => _ringBuf?.Clear();

        public void Dispose() { Stop(); _ringBuf = null; }
    }
}
