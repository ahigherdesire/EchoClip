using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EchoClip.Models;
using EchoClip.Services;
using NAudio.Wave;

namespace EchoClip.ViewModels
{
    public partial class SettingsViewModel : ObservableObject
    {
        private readonly MainViewModel _main;

        [ObservableProperty] private AppSettings                    _settings;
        [ObservableProperty] private ObservableCollection<string>  _inputDevices  = new();
        [ObservableProperty] private ObservableCollection<string>  _outputDevices = new();
        [ObservableProperty] private string                         _savedMessage  = string.Empty;

        public SettingsViewModel(AppSettings settings, MainViewModel main)
        {
            _settings = settings;
            _main     = main;
            RefreshDevices();
        }

        [RelayCommand]
        public void RefreshDevices()
        {
            InputDevices.Clear();
            for (int i = 0; i < WaveIn.DeviceCount; i++)
                InputDevices.Add(WaveIn.GetCapabilities(i).ProductName);

            OutputDevices.Clear();
            for (int i = 0; i < WaveOut.DeviceCount; i++)
                OutputDevices.Add(WaveOut.GetCapabilities(i).ProductName);
        }

        [RelayCommand]
        public void Save()
        {
            SettingsService.SetStartWithWindows(_settings.StartWithWindows);
            _main.ApplyNewSettings(_settings);
            SavedMessage = "Settings saved.";
        }

        [RelayCommand]
        public void BrowseClipsFolder()
        {
            var dlg = new System.Windows.Forms.FolderBrowserDialog
            {
                Description         = "Select folder for saved clips",
                UseDescriptionForTitle = true,
                SelectedPath        = _settings.ClipsDirectory
            };
            if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                Settings.ClipsDirectory = dlg.SelectedPath;
        }

        [RelayCommand]
        public void ClearAllRecordings()
        {
            var result = System.Windows.MessageBox.Show(
                "Delete all saved clips and their audio files? This cannot be undone.",
                "Clear All Recordings",
                System.Windows.MessageBoxButton.YesNo,
                System.Windows.MessageBoxImage.Warning);

            if (result != System.Windows.MessageBoxResult.Yes) return;

            _main.ClipManager.ClearAll();
            SavedMessage = "All recordings cleared.";
        }
    }
}
