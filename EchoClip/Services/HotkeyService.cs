using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Interop;
using EchoClip.Helpers;

namespace EchoClip.Services
{
    /// <summary>
    /// Registers and dispatches global hotkeys via Win32 RegisterHotKey.
    /// Hotkeys fire even when the app window is minimised or not focused.
    /// Must be initialised with a visible Window before registering any keys.
    /// </summary>
    public sealed class HotkeyService : IDisposable
    {
        private readonly Dictionary<int, Action> _callbacks = new();
        private HwndSource? _hwnd;
        private int _nextId = 0xEC00;   // arbitrary base in user-defined range

        public void Initialize(Window window)
        {
            var helper = new WindowInteropHelper(window);
            _hwnd = HwndSource.FromHwnd(helper.EnsureHandle());
            _hwnd.AddHook(WndProc);
        }

        /// <summary>Registers a hotkey. Returns the assigned ID (use to unregister), or -1 on failure.</summary>
        public int Register(uint modifiers, uint virtualKey, Action callback)
        {
            if (_hwnd == null) throw new InvalidOperationException("Call Initialize(window) first.");
            int id = _nextId++;
            if (NativeHelpers.RegisterHotKey(_hwnd.Handle, id, modifiers | NativeHelpers.MOD_NOREPEAT, virtualKey))
            {
                _callbacks[id] = callback;
                return id;
            }
            return -1;
        }

        public void Unregister(int id)
        {
            if (id < 0 || _hwnd == null) return;
            NativeHelpers.UnregisterHotKey(_hwnd.Handle, id);
            _callbacks.Remove(id);
        }

        public void UnregisterAll()
        {
            if (_hwnd == null) return;
            foreach (var id in _callbacks.Keys)
                NativeHelpers.UnregisterHotKey(_hwnd.Handle, id);
            _callbacks.Clear();
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == NativeHelpers.WM_HOTKEY &&
                _callbacks.TryGetValue(wParam.ToInt32(), out var cb))
            {
                cb();
                handled = true;
            }
            return IntPtr.Zero;
        }

        /// <summary>
        /// Parses a string like "Ctrl+Alt+F8" into (modifiers, virtualKey).
        /// Supported modifiers: Ctrl/Control, Alt, Shift, Win/Windows.
        /// Key names: F1-F12, A-Z, 0-9, Space, Enter, Esc, Home, End, Insert, Delete, PgUp, PgDn.
        /// </summary>
        public static (uint modifiers, uint vk) ParseHotkey(string hotkeyString)
        {
            if (string.IsNullOrWhiteSpace(hotkeyString)) return (0, 0);

            uint mods = NativeHelpers.MOD_NONE;
            var parts = hotkeyString.Split('+', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            string key = parts[^1];

            foreach (var part in parts[..^1])
            {
                mods |= part.ToUpperInvariant() switch
                {
                    "CTRL" or "CONTROL" => NativeHelpers.MOD_CONTROL,
                    "ALT"               => NativeHelpers.MOD_ALT,
                    "SHIFT"             => NativeHelpers.MOD_SHIFT,
                    "WIN" or "WINDOWS"  => NativeHelpers.MOD_WIN,
                    _                   => 0
                };
            }

            uint vk = key.ToUpperInvariant() switch
            {
                "F1"  => 0x70, "F2"  => 0x71, "F3"  => 0x72, "F4"  => 0x73,
                "F5"  => 0x74, "F6"  => 0x75, "F7"  => 0x76, "F8"  => 0x77,
                "F9"  => 0x78, "F10" => 0x79, "F11" => 0x7A, "F12" => 0x7B,
                "SPACE"              => 0x20,
                "ENTER" or "RETURN"  => 0x0D,
                "ESC" or "ESCAPE"    => 0x1B,
                "TAB"                => 0x09,
                "HOME"               => 0x24,
                "END"                => 0x23,
                "PGUP" or "PAGEUP"   => 0x21,
                "PGDN" or "PAGEDOWN" => 0x22,
                "INSERT"             => 0x2D,
                "DELETE" or "DEL"    => 0x2E,
                "LEFT"               => 0x25,
                "UP"                 => 0x26,
                "RIGHT"              => 0x27,
                "DOWN"               => 0x28,
                _ when key.Length == 1 => (uint)char.ToUpperInvariant(key[0]),
                _                      => 0
            };

            return (mods, vk);
        }

        public static string FormatHotkey(uint modifiers, uint vk)
        {
            var parts = new List<string>();
            if ((modifiers & NativeHelpers.MOD_CONTROL) != 0) parts.Add("Ctrl");
            if ((modifiers & NativeHelpers.MOD_ALT)     != 0) parts.Add("Alt");
            if ((modifiers & NativeHelpers.MOD_SHIFT)   != 0) parts.Add("Shift");
            if ((modifiers & NativeHelpers.MOD_WIN)     != 0) parts.Add("Win");
            parts.Add(vk >= 0x70 && vk <= 0x7B ? $"F{vk - 0x6F}" : ((char)vk).ToString());
            return string.Join("+", parts);
        }

        public void Dispose() => UnregisterAll();
    }
}
