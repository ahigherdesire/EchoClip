using System;
using System.IO;
using NAudio.Wave;
using NAudio.Lame;

namespace EchoClip.Helpers
{
    public static class AudioConverter
    {
        public static void SaveWav(byte[] pcm, WaveFormat fmt, string path)
        {
            using var writer = new WaveFileWriter(path, fmt);
            writer.Write(pcm, 0, pcm.Length);
        }

        public static void SaveMp3(byte[] pcm, WaveFormat fmt, string path, int bitRate = 128)
        {
            using var ms = new MemoryStream(pcm);
            using var reader = new RawSourceWaveStream(ms, fmt);
            using var writer = new LameMP3FileWriter(path, fmt, bitRate);
            reader.CopyTo(writer);
        }

        public static TimeSpan GetDuration(int byteCount, WaveFormat fmt)
        {
            int bytesPerSec = fmt.SampleRate * fmt.Channels * (fmt.BitsPerSample / 8);
            return bytesPerSec == 0 ? TimeSpan.Zero : TimeSpan.FromSeconds((double)byteCount / bytesPerSec);
        }

        /// <summary>
        /// Applies linear fade-in / fade-out ramps to 16-bit PCM in-place.
        /// Safe no-op for other bit depths.
        /// </summary>
        public static byte[] ApplyFades(byte[] pcm, WaveFormat fmt, float fadeInMs, float fadeOutMs)
        {
            if (fmt.BitsPerSample != 16 || (fadeInMs <= 0 && fadeOutMs <= 0))
                return pcm;

            var result = (byte[])pcm.Clone();
            int bytesPerFrame = fmt.Channels * 2;           // 16-bit → 2 bytes/sample
            int totalFrames   = result.Length / bytesPerFrame;
            int fadeInFrames  = (int)(fadeInMs  / 1000.0 * fmt.SampleRate);
            int fadeOutFrames = (int)(fadeOutMs / 1000.0 * fmt.SampleRate);

            for (int f = 0; f < totalFrames; f++)
            {
                float mult = 1f;
                if (f < fadeInFrames && fadeInFrames > 0)
                    mult = (float)f / fadeInFrames;
                else if (f >= totalFrames - fadeOutFrames && fadeOutFrames > 0)
                    mult = (float)(totalFrames - f) / fadeOutFrames;

                if (Math.Abs(mult - 1f) < 1e-4f) continue;

                for (int ch = 0; ch < fmt.Channels; ch++)
                {
                    int pos = f * bytesPerFrame + ch * 2;
                    short s = BitConverter.ToInt16(result, pos);
                    s = (short)Math.Clamp(s * mult, short.MinValue, short.MaxValue);
                    result[pos]     = (byte)(s & 0xFF);
                    result[pos + 1] = (byte)(s >> 8);
                }
            }
            return result;
        }
    }
}
