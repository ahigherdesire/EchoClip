using System;
using System.Collections.Generic;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EchoClip.Data;
using EchoClip.Models;
using EchoClip.Services;

namespace EchoClip.ViewModels
{
    public partial class MainViewModel : ObservableObject
    {
        private readonly AudioCaptureService   _capture;
        private readonly AudioPlaybackService  _playback;
        private readonly DiscordDetectionService _discord;
        private readonly HotkeyService         _hotkeys;
        private readonly ClipStorageService    _storage;
        private readonly AppDatabase           _db;

        [ObservableProperty] private string  _statusText         = "Ready";
        [ObservableProperty] private bool    _isBuffering;
        [ObservableProperty] private bool    _isInVoiceCall;
        [ObservableProperty] private bool    _isDiscordRunning;
        [ObservableProperty] private bool    _isPlaying;
        [ObservableProperty] private string? _nowPlayingName;
        [ObservableProperty] private float _globalVolume = 1.0f;

        // Not [ObservableProperty] — mutated internally; XAML binds to Settings.XXX via SettingsVm
        private AppSettings _settings;

        public AppSettings Settings => _settings;

        private int _saveHotkeyId  = -1;
        private int _muteHotkeyId  = -1;
        private int _tabIndex;

        public int TabIndex
        {
            get => _tabIndex;
            set => SetProperty(ref _tabIndex, value);
        }

        public ClipManagerViewModel ClipManager { get; }
        public SettingsViewModel    SettingsVm  { get; }

        public MainViewModel(AppSettings settings, AppDatabase db)
        {
            _settings = settings;
            _db       = db;
            _capture  = new AudioCaptureService(settings);
            _playback = new AudioPlaybackService(settings);
            _discord  = new DiscordDetectionService(settings.DiscordClientId);
            _hotkeys  = new HotkeyService();
            _storage  = new ClipStorageService(db, settings);

            ClipManager = new ClipManagerViewModel(db, _storage, _playback, settings);
            SettingsVm  = new SettingsViewModel(settings, this);

            WireEvents();

            if (settings.EnableDiscordDetection)
                _discord.Start();
            else
                // Without Discord detection, buffer whenever the app is running
                if (!settings.BufferOnlyDuringCall) StartBuffering();
        }

        private void WireEvents()
        {
            _capture.StatusChanged               += (_, s) => Dispatch(() => StatusText = s);
            _discord.DiscordRunningChanged        += (_, v) => Dispatch(() => OnDiscordRunningChanged(v));
            _discord.VoiceCallStateChanged        += (_, v) => Dispatch(() => OnVoiceCallStateChanged(v));
            _discord.StatusChanged                += (_, s) => Dispatch(() => StatusText = s);
            _playback.PlaybackStarted             += (_, _) => Dispatch(() => { IsPlaying = true; });
            _playback.PlaybackStopped             += (_, _) => Dispatch(() => { IsPlaying = false; NowPlayingName = null; });
            _playback.StatusChanged               += (_, s) => Dispatch(() => StatusText = s);
        }

        public void InitializeHotkeys(Window window)
        {
            _hotkeys.Initialize(window);
            ReRegisterHotkeys();
        }

        private readonly Dictionary<int, int> _clipHotkeyIds = new(); // hotkeyId → clipId

        private void ReRegisterHotkeys()
        {
            if (_saveHotkeyId >= 0) _hotkeys.Unregister(_saveHotkeyId);
            if (_muteHotkeyId >= 0) _hotkeys.Unregister(_muteHotkeyId);

            // Unregister all per-clip hotkeys
            foreach (var id in _clipHotkeyIds.Keys) _hotkeys.Unregister(id);
            _clipHotkeyIds.Clear();

            if (!string.IsNullOrWhiteSpace(_settings.SaveHotkey))
            {
                var (m, vk) = HotkeyService.ParseHotkey(_settings.SaveHotkey);
                if (vk != 0) _saveHotkeyId = _hotkeys.Register(m, vk, OnSavePressed);
            }
            if (!string.IsNullOrWhiteSpace(_settings.EmergencyMuteHotkey))
            {
                var (m, vk) = HotkeyService.ParseHotkey(_settings.EmergencyMuteHotkey);
                if (vk != 0) _muteHotkeyId = _hotkeys.Register(m, vk, OnEmergencyMute);
            }

            // Register per-clip hotkeys
            foreach (var clip in _db.GetAllClips())
            {
                if (string.IsNullOrWhiteSpace(clip.HotkeyBinding)) continue;
                var (m, vk) = HotkeyService.ParseHotkey(clip.HotkeyBinding);
                if (vk == 0) continue;
                var localClip = clip;
                int id = _hotkeys.Register(m, vk, () =>
                {
                    _playback.PlayClip(localClip);
                    Dispatch(() => { IsPlaying = true; NowPlayingName = localClip.Name; });
                });
                if (id >= 0) _clipHotkeyIds[id] = clip.Id;
            }
        }

        // ── Hotkey callbacks ──────────────────────────────────────────────────

        private void OnSavePressed()
        {
            if (!_capture.IsBuffering)
            {
                Dispatch(() => StatusText = "Buffer not active — start buffering first");
                return;
            }
            var pcm = _capture.GetBufferedAudio();
            if (pcm == null || pcm.Length == 0)
            {
                Dispatch(() => StatusText = "No audio captured yet");
                return;
            }
            try
            {
                var clip = _storage.SaveBufferedAudio(pcm, _capture.CaptureFormat!);
                Dispatch(() =>
                {
                    ClipManager.Clips.Insert(0, clip);
                    StatusText = $"Saved: {clip.Name}";
                });
            }
            catch (Exception ex)
            {
                Dispatch(() => StatusText = $"Save failed: {ex.Message}");
            }
        }

        private void OnEmergencyMute()
        {
            _playback.StopPlayback();
            Dispatch(() => StatusText = "Playback stopped");
        }

        // ── Discord callbacks ─────────────────────────────────────────────────

        private void OnDiscordRunningChanged(bool running)
        {
            IsDiscordRunning = running;
            if (!running) StopBuffering();
            else if (!_settings.BufferOnlyDuringCall) StartBuffering();
        }

        private void OnVoiceCallStateChanged(bool inCall)
        {
            IsInVoiceCall = inCall;
            if (_settings.BufferOnlyDuringCall)
            {
                if (inCall) StartBuffering();
                else StopBuffering();
            }
        }

        // ── Buffer commands ───────────────────────────────────────────────────

        [RelayCommand]
        public void StartBuffering()
        {
            if (_capture.IsBuffering) return;
            _capture.Start();
            IsBuffering = true;
        }

        [RelayCommand]
        public void StopBuffering()
        {
            if (!_capture.IsBuffering) return;
            _capture.Stop();
            IsBuffering = false;
        }

        [RelayCommand]
        public void ToggleBuffering()
        {
            if (IsBuffering) StopBuffering();
            else StartBuffering();
        }

        [RelayCommand]
        public void StopPlayback() => _playback.StopPlayback();

        // ── Misc ──────────────────────────────────────────────────────────────

        partial void OnGlobalVolumeChanged(float value)
        {
            _settings.GlobalVolume = value;
            _playback.UpdateSettings(_settings);
        }

        public void ApplyNewSettings(AppSettings s)
        {
            _settings = s;
            _capture.UpdateSettings(s);
            _playback.UpdateSettings(s);
            _storage.UpdateSettings(s);
            ReRegisterHotkeys();
            SettingsService.Save(s);
        }

        public void Shutdown()
        {
            _discord.Stop();
            _capture.Stop();
            _playback.StopPlayback();
            _hotkeys.UnregisterAll();
            _hotkeys.Dispose();
            _capture.Dispose();
            _playback.Dispose();
            _discord.Dispose();
            _db.Dispose();
        }

        private static void Dispatch(Action a) =>
            System.Windows.Application.Current?.Dispatcher.Invoke(a);
    }
}
