using System;
using System.IO;
using EchoClip.Data;
using EchoClip.Models;
using EchoClip.Services;
using EchoClip.ViewModels;
using EchoClip.Views;
using WpfApp = System.Windows.Application;
using WpfStartup = System.Windows.StartupEventArgs;
using WpfExit = System.Windows.ExitEventArgs;
using WpfWindow = System.Windows.Window;
using WpfWindowState = System.Windows.WindowState;

namespace EchoClip
{
    public partial class App : WpfApp
    {
        private MainViewModel?    _vm;
        private SystemTrayService _tray = new();
        private MainWindow?       _window;

        protected override void OnStartup(WpfStartup e)
        {
            base.OnStartup(e);

            // ── Load settings ────────────────────────────────────────────────
            var settings = SettingsService.Load();

            // ── Consent gate (first-launch only) ────────────────────────────
            if (!settings.ConsentGiven)
            {
                var consent = new ConsentDialog();
                consent.ShowDialog();
                if (!consent.Accepted)
                {
                    Shutdown();
                    return;
                }
                settings.ConsentGiven = true;
                SettingsService.Save(settings);
            }

            // ── Database ────────────────────────────────────────────────────
            string dataDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "EchoClip");
            Directory.CreateDirectory(dataDir);
            var db = new AppDatabase(Path.Combine(dataDir, "echoclip.db"));

            // ── View-model ──────────────────────────────────────────────────
            _vm = new MainViewModel(settings, db);

            // ── Main window ─────────────────────────────────────────────────
            _window = new MainWindow(_vm);

            // ── System tray ─────────────────────────────────────────────────
            _tray.Initialize();
            _tray.ShowWindowRequested  += (_, _) => ShowMainWindow();
            _tray.StopPlaybackRequested+= (_, _) => _vm.StopPlayback();
            _tray.ExitRequested        += (_, _) => ExitApp();

            // Forward status to tray tooltip
            _vm.PropertyChanged += (_, args) =>
            {
                if (args.PropertyName is nameof(MainViewModel.StatusText))
                    _tray.SetStatus(_vm.StatusText);
            };

            _window.Show();
        }

        private void ShowMainWindow()
        {
            if (_window == null) return;
            _window.Show();
            _window.WindowState = WpfWindowState.Normal;
            _window.Activate();
        }

        private void ExitApp()
        {
            _vm?.Shutdown();
            _tray.Dispose();
            Shutdown();
        }

        protected override void OnExit(WpfExit e)
        {
            _vm?.Shutdown();
            _tray.Dispose();
            base.OnExit(e);
        }
    }
}
