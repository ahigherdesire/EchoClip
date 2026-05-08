using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EchoClip.Data;
using EchoClip.Models;
using EchoClip.Services;
using Win32FileDialog = Microsoft.Win32.OpenFileDialog;

namespace EchoClip.ViewModels
{
    public partial class ClipManagerViewModel : ObservableObject
    {
        private readonly AppDatabase          _db;
        private readonly ClipStorageService   _storage;
        private readonly AudioPlaybackService _playback;
        private readonly AppSettings          _settings;

        [ObservableProperty] private ObservableCollection<AudioClip>      _clips      = new();
        [ObservableProperty] private ObservableCollection<ClipCategory>   _categories = new();
        [ObservableProperty] private AudioClip?      _selectedClip;
        [ObservableProperty] private ClipCategory?   _selectedCategory;
        [ObservableProperty] private string          _searchText = string.Empty;
        [ObservableProperty] private bool            _isRenaming;
        [ObservableProperty] private string          _renameText  = string.Empty;

        public ClipManagerViewModel(AppDatabase db, ClipStorageService storage,
            AudioPlaybackService playback, AppSettings settings)
        {
            _db       = db;
            _storage  = storage;
            _playback = playback;
            _settings = settings;
            Reload();
        }

        public void Reload()
        {
            Clips      = new ObservableCollection<AudioClip>(_db.GetAllClips());
            Categories = new ObservableCollection<ClipCategory>(_db.GetAllCategories());
        }

        // ── Playback ──────────────────────────────────────────────────────────

        [RelayCommand]
        public void Play(AudioClip? clip)
        {
            if (clip != null) _playback.PlayClip(clip);
        }

        [RelayCommand]
        public void Stop() => _playback.StopPlayback();

        // ── Clip management ───────────────────────────────────────────────────

        [RelayCommand]
        public void BeginRename(AudioClip? clip)
        {
            if (clip == null) return;
            SelectedClip = clip;
            RenameText   = clip.Name;
            IsRenaming   = true;
        }

        [RelayCommand]
        public void CommitRename()
        {
            if (SelectedClip == null || string.IsNullOrWhiteSpace(RenameText)) return;
            SelectedClip.Name = RenameText.Trim();
            _storage.UpdateClip(SelectedClip);
            IsRenaming = false;
            // Force list refresh for the name change
            var idx = Clips.IndexOf(SelectedClip);
            if (idx >= 0) { Clips.RemoveAt(idx); Clips.Insert(idx, SelectedClip); SelectedClip = Clips[idx]; }
        }

        [RelayCommand]
        public void CancelRename() => IsRenaming = false;

        [RelayCommand]
        public void Delete(AudioClip? clip)
        {
            if (clip == null) return;
            _storage.DeleteClip(clip);
            Clips.Remove(clip);
            if (SelectedClip == clip) SelectedClip = null;
        }

        [RelayCommand]
        public void Import()
        {
            var dlg = new Win32FileDialog
            {
                Title       = "Import Audio Files",
                Filter      = "Audio Files|*.wav;*.mp3;*.ogg;*.flac;*.m4a;*.aac|All Files|*.*",
                Multiselect = true
            };
            if (dlg.ShowDialog() != true) return;
            foreach (var f in dlg.FileNames)
            {
                try { Clips.Insert(0, _storage.ImportFile(f)); }
                catch { /* skip unsupported files */ }
            }
        }

        [RelayCommand]
        public void SaveClipChanges()
        {
            if (SelectedClip == null) return;
            _storage.UpdateClip(SelectedClip);
        }

        // ── Categories ────────────────────────────────────────────────────────

        [RelayCommand]
        public void AddCategory()
        {
            var cat = new ClipCategory { Name = "New Category" };
            cat.Id = _db.InsertCategory(cat);
            Categories.Add(cat);
        }

        [RelayCommand]
        public void DeleteCategory(ClipCategory? cat)
        {
            if (cat == null) return;
            _db.DeleteCategory(cat.Id);
            Categories.Remove(cat);
            foreach (var c in Clips.Where(x => x.CategoryId == cat.Id))
            {
                c.CategoryId = null;
                c.Category   = null;
            }
        }

        [RelayCommand]
        public void AssignCategory()
        {
            if (SelectedClip == null) return;
            SelectedClip.CategoryId = SelectedCategory?.Id;
            SelectedClip.Category   = SelectedCategory;
            _storage.UpdateClip(SelectedClip);
        }

        // ── Search ────────────────────────────────────────────────────────────

        public void ClearAll()
        {
            _storage.ClearAllClips(deleteFiles: true);
            Clips.Clear();
            SelectedClip = null;
        }

        partial void OnSearchTextChanged(string value)
        {
            var results = string.IsNullOrWhiteSpace(value)
                ? _db.GetAllClips()
                : _db.SearchClips(value);
            Clips = new ObservableCollection<AudioClip>(results);
        }
    }
}
