using System;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace EchoClip.Services
{
    /// <summary>
    /// Detects Discord process presence and, when a Discord application client_id is configured,
    /// connects to Discord's local RPC pipe to track voice-call state accurately.
    ///
    /// Without a client_id the service falls back to process detection only; the UI lets users
    /// manually toggle buffering in that mode.
    ///
    /// Discord IPC protocol: named pipe \\.\pipe\discord-ipc-{0-9}, little-endian header
    /// [opcode:4][length:4] followed by UTF-8 JSON.
    /// </summary>
    public sealed class DiscordDetectionService : IDisposable
    {
        private readonly string            _clientId;
        private CancellationTokenSource?   _cts;
        private Task?                      _task;

        public bool IsDiscordRunning  { get; private set; }
        public bool IsInVoiceCall     { get; private set; }

        public event EventHandler<bool>?    DiscordRunningChanged;
        public event EventHandler<bool>?    VoiceCallStateChanged;
        public event EventHandler<string>?  StatusChanged;

        public DiscordDetectionService(string clientId = "") => _clientId = clientId;

        public void Start()
        {
            _cts  = new CancellationTokenSource();
            _task = Task.Run(() => MonitorAsync(_cts.Token));
        }

        public void Stop() => _cts?.Cancel();

        private async Task MonitorAsync(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                bool wasRunning = IsDiscordRunning;
                IsDiscordRunning = IsDiscordUp();

                if (IsDiscordRunning != wasRunning)
                {
                    DiscordRunningChanged?.Invoke(this, IsDiscordRunning);
                    StatusChanged?.Invoke(this, IsDiscordRunning ? "Discord detected" : "Discord closed");
                    if (!IsDiscordRunning) SetVoiceState(false);
                }

                if (IsDiscordRunning && !string.IsNullOrEmpty(_clientId))
                    await TryIpcAsync(ct).ConfigureAwait(false);
                else if (IsDiscordRunning)
                    StatusChanged?.Invoke(this, "Discord running — configure Client ID for call detection");

                try { await Task.Delay(3000, ct).ConfigureAwait(false); }
                catch (OperationCanceledException) { break; }
            }
        }

        private static bool IsDiscordUp()
        {
            var procs = Process.GetProcessesByName("Discord");
            bool found = procs.Length > 0;
            foreach (var p in procs) p.Dispose();
            return found;
        }

        // ── Discord IPC ───────────────────────────────────────────────────────

        private async Task TryIpcAsync(CancellationToken ct)
        {
            for (int i = 0; i <= 9 && !ct.IsCancellationRequested; i++)
            {
                try
                {
                    using var pipe = new NamedPipeClientStream(".", $"discord-ipc-{i}",
                        PipeDirection.InOut, PipeOptions.Asynchronous);

                    await pipe.ConnectAsync(400, ct).ConfigureAwait(false);

                    await IpcSendAsync(pipe, 0,
                        JsonSerializer.Serialize(new { v = 1, client_id = _clientId })).ConfigureAwait(false);

                    var hello = await IpcReadAsync(pipe, ct).ConfigureAwait(false);
                    if (hello == null) continue;

                    await IpcSendAsync(pipe, 1, JsonSerializer.Serialize(new
                    {
                        cmd = "SUBSCRIBE",
                        evt = "VOICE_CONNECTION_STATUS",
                        args = new { },
                        nonce = Guid.NewGuid().ToString("N")
                    })).ConfigureAwait(false);

                    // Read a short burst of events then let the outer loop reconnect
                    for (int j = 0; j < 10 && !ct.IsCancellationRequested; j++)
                    {
                        var msg = await IpcReadAsync(pipe, ct).ConfigureAwait(false);
                        if (msg == null) break;
                        ParseEvent(msg);
                        await Task.Delay(200, ct).ConfigureAwait(false);
                    }
                    break;
                }
                catch (OperationCanceledException) { throw; }
                catch { /* pipe not available on this index */ }
            }
        }

        private void ParseEvent(string json)
        {
            try
            {
                var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;
                if (root.TryGetProperty("evt", out var evtProp) &&
                    evtProp.GetString() == "VOICE_CONNECTION_STATUS" &&
                    root.TryGetProperty("data", out var data) &&
                    data.TryGetProperty("state", out var state))
                {
                    SetVoiceState(state.GetString() == "VOICE_CONNECTED");
                }
            }
            catch { /* ignore bad JSON */ }
        }

        private void SetVoiceState(bool inCall)
        {
            if (IsInVoiceCall == inCall) return;
            IsInVoiceCall = inCall;
            VoiceCallStateChanged?.Invoke(this, inCall);
            StatusChanged?.Invoke(this, inCall ? "Voice call active" : "Voice call ended");
        }

        private static async Task IpcSendAsync(PipeStream pipe, int opCode, string payload)
        {
            var data   = Encoding.UTF8.GetBytes(payload);
            var header = new byte[8];
            BitConverter.TryWriteBytes(header.AsSpan(0), opCode);
            BitConverter.TryWriteBytes(header.AsSpan(4), data.Length);
            await pipe.WriteAsync(header).ConfigureAwait(false);
            await pipe.WriteAsync(data).ConfigureAwait(false);
        }

        private static async Task<string?> IpcReadAsync(PipeStream pipe, CancellationToken ct)
        {
            var header = new byte[8];
            int n = await pipe.ReadAsync(header.AsMemory(), ct).ConfigureAwait(false);
            if (n < 8) return null;

            int length = BitConverter.ToInt32(header, 4);
            if (length is <= 0 or > 1_048_576) return null;

            var body = new byte[length];
            n = await pipe.ReadAsync(body.AsMemory(), ct).ConfigureAwait(false);
            return n < length ? null : Encoding.UTF8.GetString(body);
        }

        public void Dispose() { Stop(); _cts?.Dispose(); }
    }
}
