using System;
using System.Threading;

namespace EchoClip.Helpers
{
    /// <summary>
    /// Thread-safe ring buffer for raw PCM bytes. Incoming audio continuously overwrites
    /// the oldest data so the buffer always holds the most recent N seconds of audio.
    /// </summary>
    public sealed class CircularAudioBuffer
    {
        private readonly byte[] _buf;
        private int _writePos;
        private long _totalWritten;
        private readonly ReaderWriterLockSlim _lock = new();

        public int Capacity => _buf.Length;

        public CircularAudioBuffer(int capacityBytes)
        {
            _buf = new byte[capacityBytes];
        }

        public void Write(byte[] data, int offset, int count)
        {
            _lock.EnterWriteLock();
            try
            {
                int remaining = count;
                int src = offset;
                while (remaining > 0)
                {
                    int chunk = Math.Min(remaining, _buf.Length - _writePos);
                    Buffer.BlockCopy(data, src, _buf, _writePos, chunk);
                    _writePos = (_writePos + chunk) % _buf.Length;
                    src += chunk;
                    remaining -= chunk;
                }
                _totalWritten += count;
            }
            finally { _lock.ExitWriteLock(); }
        }

        /// <summary>Returns up to <paramref name="byteCount"/> most-recent bytes in chronological order.</summary>
        public byte[] ReadLast(int byteCount)
        {
            _lock.EnterReadLock();
            try
            {
                long available = Math.Min(_totalWritten, _buf.Length);
                int readCount = (int)Math.Min(byteCount, available);
                var result = new byte[readCount];

                int startPos = (_writePos - readCount + _buf.Length) % _buf.Length;
                int first = Math.Min(readCount, _buf.Length - startPos);
                Buffer.BlockCopy(_buf, startPos, result, 0, first);
                if (first < readCount)
                    Buffer.BlockCopy(_buf, 0, result, first, readCount - first);

                return result;
            }
            finally { _lock.ExitReadLock(); }
        }

        public void Clear()
        {
            _lock.EnterWriteLock();
            try
            {
                Array.Clear(_buf, 0, _buf.Length);
                _writePos = 0;
                _totalWritten = 0;
            }
            finally { _lock.ExitWriteLock(); }
        }
    }
}
