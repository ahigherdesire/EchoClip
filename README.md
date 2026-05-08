# EchoClip

A Windows desktop app for managing short audio clips during Discord voice calls.  
Press a hotkey to save the last 10 seconds of microphone audio, then replay saved clips into Discord through a virtual microphone.

---

## Features

| Feature | Detail |
|---|---|
| **Rolling buffer** | Keeps the last N seconds (default 10) of mic audio in memory — nothing is written to disk until you press save |
| **Hotkey clip saving** | Press your configured hotkey (default `F8`) to save the current buffer as a WAV or MP3 file |
| **Discord detection** | Detects when Discord is open and (optionally) when you join a voice call, automatically starting/stopping the buffer |
| **Clip manager** | Rename, delete, import, organise into folders, search, and assign per-clip hotkeys |
| **Virtual mic routing** | Plays clips through a virtual audio device (VB-CABLE) so they appear as microphone input in Discord |
| **Monitor output** | Optionally hear clips through your own speakers while they play |
| **Fades** | Per-clip fade-in/out in milliseconds to avoid audio pops |
| **System tray** | Minimises to tray; stop playback or open the window from the tray icon |
| **Privacy first** | Audio never leaves your disk; no cloud sync unless you add it |

---

## Requirements

| | |
|---|---|
| **OS** | Windows 10 / 11 (x64) |
| **Runtime** | [.NET 8 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/8.0) |
| **Virtual mic** | [VB-CABLE Virtual Audio Device](https://vb-audio.com/Cable/) (free) — required for Discord routing |
| **SDK (build only)** | .NET 8 SDK |

---

## Build

```powershell
# Clone / enter the repo
cd "EchoClip"

# Restore packages (requires internet access the first time)
& "C:\Program Files\dotnet\dotnet.exe" restore EchoClip\EchoClip.csproj

# Build (Debug)
& "C:\Program Files\dotnet\dotnet.exe" build EchoClip\EchoClip.csproj

# Build (Release, self-contained, single file)
& "C:\Program Files\dotnet\dotnet.exe" publish EchoClip\EchoClip.csproj `
    -c Release -r win-x64 `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -o publish\
```

The output binary `EchoClip.exe` will be in `publish\`.

---

## First-Run Setup

1. **Install VB-CABLE** — Download and run the installer from vb-audio.com. Two new devices appear: *CABLE Input* and *CABLE Output*.
2. **Configure Discord** — In Discord → Settings → Voice & Video → Input Device, select **CABLE Output** (the virtual cable's output side becomes Discord's mic input).
3. **Launch EchoClip** — Accept the privacy/consent notice.
4. **Open Settings** — Choose:
   - *Input (Microphone)* — your real physical microphone
   - *Virtual Mic (VB-CABLE)* — **CABLE Input**
   - *Monitor Output* — your headphones/speakers (optional, lets you hear what you play)
5. **Save settings** and return to the Clips tab.

Now, when you press **F8** during a Discord call, the last 10 seconds are saved. Select a clip and press **Play** to route it into Discord.

---

## Discord Voice-Call Detection

EchoClip can automatically start the audio buffer only when you are in a Discord voice call.

**Without a Client ID (default):** EchoClip detects the Discord process by name. You can control the buffer manually with the **Start Buffer / Stop Buffer** button, or disable `Buffer audio only during Discord voice call` in Settings to always buffer while EchoClip is running.

**With a Client ID (accurate detection):** Register a Discord application at [discord.com/developers](https://discord.com/developers), paste the Application ID into Settings → *Discord App Client ID*, and save. EchoClip will connect to Discord's local RPC pipe and receive real-time voice connection events.

---

## Audio Routing Pipeline

```
Physical mic
     │
     ▼
EchoClip capture (WaveIn)
     │
     ├── Ring buffer (in memory, ≤ 10 s)
     │         │  hotkey pressed
     │         ▼
     │    Save to disk (WAV / MP3)
     │
     │  play command
     ▼
AudioFileReader
     │
  Volume × fade
     │
     ├──► CABLE Input (virtual mic) ──► Discord sees it as microphone
     │
     └──► Monitor speaker (optional) ──► you hear it locally
```

---

## Hotkeys

| Hotkey | Default | Action |
|---|---|---|
| Save clip | `F8` | Saves last N seconds of buffer to a new clip |
| Emergency mute | `F9` | Immediately stops any clip playback |
| Per-clip hotkey | configurable | Plays the assigned clip from anywhere |

Hotkey format: `F8`, `Ctrl+Shift+S`, `Alt+F2`, etc. Modifiers: `Ctrl`, `Alt`, `Shift`, `Win`.

---

## Project Structure

```
EchoClip/
├── EchoClip.sln
├── README.md
└── EchoClip/
    ├── App.xaml / App.xaml.cs          # startup, tray wiring, consent gate
    ├── MainWindow.xaml                  # header, tabs, status bar
    ├── Data/
    │   └── AppDatabase.cs              # SQLite metadata store
    ├── Helpers/
    │   ├── CircularAudioBuffer.cs      # thread-safe PCM ring buffer
    │   ├── AudioConverter.cs           # WAV/MP3 encoding + fade ramps
    │   ├── Converters.cs               # WPF value converters
    │   └── NativeHelpers.cs            # RegisterHotKey P/Invoke
    ├── Models/
    │   ├── AudioClip.cs
    │   ├── ClipCategory.cs
    │   └── AppSettings.cs
    ├── Resources/
    │   ├── Styles.xaml                 # dark theme, buttons, list styles
    │   └── Converters.xaml             # converter instances
    ├── Services/
    │   ├── AudioCaptureService.cs      # mic capture → ring buffer
    │   ├── AudioPlaybackService.cs     # clip → virtual mic + monitor
    │   ├── DiscordDetectionService.cs  # process + IPC voice detection
    │   ├── HotkeyService.cs            # global hotkey registration
    │   ├── ClipStorageService.cs       # disk I/O for clips
    │   ├── SettingsService.cs          # JSON settings persistence
    │   └── SystemTrayService.cs        # WinForms NotifyIcon
    ├── ViewModels/
    │   ├── MainViewModel.cs
    │   ├── ClipManagerViewModel.cs
    │   └── SettingsViewModel.cs
    └── Views/
        ├── ClipManagerView.xaml
        ├── SettingsView.xaml
        └── ConsentDialog.xaml
```

---

## Privacy

- The rolling buffer stays **in memory only**. It is discarded when you stop the buffer or close the app.
- Audio is written to disk **only** when you press the save hotkey.
- All clips are stored locally in `Documents\EchoClip\Clips\` by default.
- The app **never contacts any server**. No telemetry, no cloud sync.
- You can delete all clips and metadata at any time from Settings → *Clear All Recordings*.

---

## Troubleshooting

**Clips don't play in Discord**  
Confirm Discord's Input Device is set to *CABLE Output* (not your real mic).

**VB-CABLE not in device list**  
Click the refresh button in Settings after installing VB-CABLE. You may need to restart EchoClip.

**Hotkey doesn't work**  
Another application may have registered the same hotkey. Try a different combination (e.g. `Ctrl+F8`).

**Buffer never starts automatically**  
Without a Discord Client ID, automatic detection only checks whether the Discord process is running, not whether you're in a call. Either disable *Buffer only during Discord voice call* in Settings, or provide a Client ID.

**MP3 encoding produces silence / errors**  
MP3 encoding uses NAudio.Lame which wraps libmp3lame. Use WAV format if you see encoding errors.
