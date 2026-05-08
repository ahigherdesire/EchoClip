# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Build commands

dotnet is at `C:\Program Files\dotnet\dotnet.exe` — it is not on the system PATH.

```powershell
# Restore packages
& "C:\Program Files\dotnet\dotnet.exe" restore EchoClip\EchoClip.csproj

# Build (Debug)
& "C:\Program Files\dotnet\dotnet.exe" build EchoClip\EchoClip.csproj --no-restore

# Publish single-file release exe
& "C:\Program Files\dotnet\dotnet.exe" publish EchoClip\EchoClip.csproj `
    -c Release -r win-x64 --self-contained `
    -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true `
    -o publish\
```

No test project exists yet. No linter is configured.

## Architecture

The app is a single WPF project (`EchoClip\EchoClip.csproj`, `net8.0-windows`) with both `UseWPF` and `UseWindowsForms` enabled. Because both are enabled, **namespace collisions are common** — always qualify ambiguous types explicitly (e.g. `System.Windows.Application`, `System.Windows.Controls.UserControl`, `System.Windows.Media.Brush`) rather than relying on `using` directives.

### Startup flow (`App.xaml.cs`)

`App.OnStartup` is the composition root:
1. `SettingsService.Load()` → deserialises `%AppData%\EchoClip\settings.json`
2. Shows `ConsentDialog` on first launch; exits if declined
3. Opens `AppDatabase` (`%AppData%\EchoClip\echoclip.db`, SQLite via raw ADO.NET — no EF)
4. Constructs `MainViewModel` (which owns and wires all services)
5. Shows `MainWindow`, initialises `SystemTrayService`

### Service layer

All services are instantiated and owned by `MainViewModel`. They are not DI-registered.

| Service | Role |
|---|---|
| `AudioCaptureService` | NAudio `WaveInEvent` → `CircularAudioBuffer` (in-memory ring buffer, never touches disk) |
| `AudioPlaybackService` | `AudioFileReader` → `VolumeSampleProvider` → `FadeInOutSampleProvider` → `WaveOutEvent` (virtual mic + optional monitor device) |
| `DiscordDetectionService` | Polls for `Discord.exe` process; if a Client ID is configured, connects to `\\.\pipe\discord-ipc-{0-9}` and subscribes to `VOICE_CONNECTION_STATUS` events |
| `HotkeyService` | Win32 `RegisterHotKey` via `HwndSource.AddHook`; must be initialised after `OnSourceInitialized` fires on the main window |
| `ClipStorageService` | Writes PCM→WAV/MP3 to disk (only on user save), copies imported files, delegates metadata to `AppDatabase` |
| `SettingsService` | Static: JSON load/save + registry `StartWithWindows` key |
| `SystemTrayService` | WinForms `NotifyIcon` with a context menu |

### Audio pipeline

```
Physical mic → WaveInEvent → CircularAudioBuffer (ring, in memory)
                                    │  hotkey F8
                                    ▼
                             AudioConverter.SaveWav/Mp3 → disk
                                    │  play command
                                    ▼
                             AudioFileReader → Volume → Fade
                                    ├──► WaveOut (CABLE Input = virtual mic → Discord)
                                    └──► WaveOut (monitor speaker, optional)
```

`CircularAudioBuffer` (`Helpers/CircularAudioBuffer.cs`) is a thread-safe byte ring buffer guarded by `ReaderWriterLockSlim`. `ReadLast(n)` reassembles chronological order across the wrap boundary.

### ViewModel layer

`MainViewModel` is the root VM (data context of `MainWindow`). It:
- Owns all services and wires their events onto the UI thread via `System.Windows.Application.Current.Dispatcher.Invoke`
- Registers/unregisters global hotkeys through `HotkeyService`, including per-clip hotkeys stored in `AudioClip.HotkeyBinding`
- Exposes `ClipManager` (`ClipManagerViewModel`) and `SettingsVm` (`SettingsViewModel`) as child VMs

`_settings` in `MainViewModel` is a **plain private field** (not `[ObservableProperty]`) to avoid MVVMTK0034; exposed via a read-only `Settings` property. All other observable state uses CommunityToolkit source-generated properties — reference the generated property name (PascalCase) inside the class, not the backing field.

### XAML / binding notes

- `Resources/Styles.xaml` — dark theme palette, button styles, list item styles, tab nav styles
- `Resources/Converters.xaml` — instances of converters from `Helpers/Converters.cs`
- Both resource dictionaries are merged into `App.xaml` **and** individually into each View that needs them
- `ClipManagerView` sets `DataContext="{Binding SelectedClip}"` on its detail `StackPanel`, then reaches back to the VM using `ElementName=DetailScroll` bindings (the ScrollViewer above still has the VM as its DataContext)
- `ListBox.ContextMenu` binds via `PlacementTarget.DataContext.XxxCommand` — ContextMenu is outside the visual tree so `RelativeSource AncestorType` to the ListBox doesn't work

### Persistent data locations (runtime, not in repo)

| Path | Content |
|---|---|
| `%AppData%\EchoClip\settings.json` | All `AppSettings` fields |
| `%AppData%\EchoClip\echoclip.db` | SQLite: `clips` + `categories` tables |
| `Documents\EchoClip\Clips\` (default) | Saved WAV/MP3 files |

### Namespace collision patterns to watch for

Because `UseWindowsForms=true` pulls in the Forms namespace globally:

- `Application` → use `System.Windows.Application`
- `UserControl` → use `System.Windows.Controls.UserControl`
- `Brush` → use `System.Windows.Media.Brush`
- `Binding` → use `System.Windows.Data.Binding`
- `OpenFileDialog` → use `Microsoft.Win32.OpenFileDialog`
