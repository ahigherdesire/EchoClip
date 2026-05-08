using System;
using System.Drawing;
using System.Windows;
using System.Windows.Forms;
using Application = System.Windows.Application;

namespace EchoClip.Services
{
    /// <summary>
    /// System tray icon with a right-click context menu for quick access.
    /// Uses Windows Forms NotifyIcon (requires UseWindowsForms in .csproj).
    /// </summary>
    public sealed class SystemTrayService : IDisposable
    {
        private NotifyIcon? _icon;

        public event EventHandler? ShowWindowRequested;
        public event EventHandler? ExitRequested;
        public event EventHandler? StopPlaybackRequested;

        public void Initialize()
        {
            _icon = new NotifyIcon
            {
                Text    = "EchoClip",
                Icon    = ResolveIcon(),
                Visible = true
            };

            _icon.DoubleClick += (_, _) => ShowWindowRequested?.Invoke(this, EventArgs.Empty);

            var menu = new ContextMenuStrip();

            var show = new ToolStripMenuItem("Show EchoClip");
            show.Click += (_, _) => ShowWindowRequested?.Invoke(this, EventArgs.Empty);
            menu.Items.Add(show);

            menu.Items.Add(new ToolStripSeparator());

            var stop = new ToolStripMenuItem("Stop Playback");
            stop.Click += (_, _) => StopPlaybackRequested?.Invoke(this, EventArgs.Empty);
            menu.Items.Add(stop);

            menu.Items.Add(new ToolStripSeparator());

            var exit = new ToolStripMenuItem("Exit");
            exit.Click += (_, _) => ExitRequested?.Invoke(this, EventArgs.Empty);
            menu.Items.Add(exit);

            _icon.ContextMenuStrip = menu;
        }

        public void SetStatus(string text)
        {
            if (_icon == null) return;
            // NotifyIcon.Text is capped at 127 chars
            string full = $"EchoClip — {text}";
            _icon.Text = full.Length > 127 ? full[..127] : full;
        }

        public void ShowBalloon(string title, string message, int durationMs = 3000) =>
            _icon?.ShowBalloonTip(durationMs, title, message, ToolTipIcon.Info);

        private static Icon ResolveIcon()
        {
            try
            {
                string path = System.IO.Path.Combine(
                    AppDomain.CurrentDomain.BaseDirectory, "Assets", "tray.ico");
                if (System.IO.File.Exists(path))
                    return new Icon(path);
            }
            catch { }
            return SystemIcons.Application;
        }

        public void Dispose()
        {
            if (_icon != null)
            {
                _icon.Visible = false;
                _icon.Dispose();
                _icon = null;
            }
        }
    }
}
