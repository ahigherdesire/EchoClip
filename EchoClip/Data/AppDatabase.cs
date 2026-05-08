using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Data.Sqlite;
using EchoClip.Models;

namespace EchoClip.Data
{
    /// <summary>
    /// SQLite-backed persistence for clip metadata and categories.
    /// All audio data stays on disk as files; only metadata lives here.
    /// </summary>
    public class AppDatabase : IDisposable
    {
        private readonly SqliteConnection _conn;

        public AppDatabase(string dbPath)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(dbPath)!);
            _conn = new SqliteConnection($"Data Source={dbPath}");
            _conn.Open();
            ApplySchema();
        }

        private void ApplySchema()
        {
            Exec(@"
                CREATE TABLE IF NOT EXISTS categories (
                    id    INTEGER PRIMARY KEY AUTOINCREMENT,
                    name  TEXT NOT NULL,
                    color TEXT NOT NULL DEFAULT '#5865F2'
                );
                CREATE TABLE IF NOT EXISTS clips (
                    id              INTEGER PRIMARY KEY AUTOINCREMENT,
                    name            TEXT    NOT NULL,
                    file_path       TEXT    NOT NULL,
                    duration_ms     INTEGER NOT NULL DEFAULT 0,
                    created_at      TEXT    NOT NULL,
                    category_id     INTEGER,
                    volume          REAL    NOT NULL DEFAULT 1.0,
                    hotkey_binding  TEXT,
                    fade_in_ms      REAL    NOT NULL DEFAULT 0,
                    fade_out_ms     REAL    NOT NULL DEFAULT 0,
                    file_size_bytes INTEGER NOT NULL DEFAULT 0,
                    FOREIGN KEY (category_id) REFERENCES categories(id) ON DELETE SET NULL
                );");
        }

        // ── Categories ────────────────────────────────────────────────────────

        public int InsertCategory(ClipCategory cat)
        {
            using var cmd = Cmd("INSERT INTO categories(name,color) VALUES(@n,@c); SELECT last_insert_rowid();");
            cmd.Parameters.AddWithValue("@n", cat.Name);
            cmd.Parameters.AddWithValue("@c", cat.Color);
            return Convert.ToInt32(cmd.ExecuteScalar());
        }

        public List<ClipCategory> GetAllCategories()
        {
            var list = new List<ClipCategory>();
            using var cmd = Cmd("SELECT id,name,color FROM categories ORDER BY name");
            using var r = cmd.ExecuteReader();
            while (r.Read())
                list.Add(new ClipCategory { Id = r.GetInt32(0), Name = r.GetString(1), Color = r.GetString(2) });
            return list;
        }

        public void UpdateCategory(ClipCategory cat)
        {
            using var cmd = Cmd("UPDATE categories SET name=@n,color=@c WHERE id=@id");
            cmd.Parameters.AddWithValue("@n", cat.Name);
            cmd.Parameters.AddWithValue("@c", cat.Color);
            cmd.Parameters.AddWithValue("@id", cat.Id);
            cmd.ExecuteNonQuery();
        }

        public void DeleteCategory(int id)
        {
            using var cmd = Cmd("DELETE FROM categories WHERE id=@id");
            cmd.Parameters.AddWithValue("@id", id);
            cmd.ExecuteNonQuery();
        }

        // ── Clips ─────────────────────────────────────────────────────────────

        public int InsertClip(AudioClip clip)
        {
            using var cmd = Cmd(@"
                INSERT INTO clips(name,file_path,duration_ms,created_at,category_id,volume,
                                  hotkey_binding,fade_in_ms,fade_out_ms,file_size_bytes)
                VALUES(@name,@path,@dur,@created,@cat,@vol,@hotkey,@fi,@fo,@size);
                SELECT last_insert_rowid();");
            cmd.Parameters.AddWithValue("@name",    clip.Name);
            cmd.Parameters.AddWithValue("@path",    clip.FilePath);
            cmd.Parameters.AddWithValue("@dur",     (long)clip.Duration.TotalMilliseconds);
            cmd.Parameters.AddWithValue("@created", clip.CreatedAt.ToString("O"));
            cmd.Parameters.AddWithValue("@cat",     clip.CategoryId.HasValue ? (object)clip.CategoryId.Value : DBNull.Value);
            cmd.Parameters.AddWithValue("@vol",     clip.Volume);
            cmd.Parameters.AddWithValue("@hotkey",  clip.HotkeyBinding ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@fi",      clip.FadeInMs);
            cmd.Parameters.AddWithValue("@fo",      clip.FadeOutMs);
            cmd.Parameters.AddWithValue("@size",    clip.FileSizeBytes);
            return Convert.ToInt32(cmd.ExecuteScalar());
        }

        public List<AudioClip> GetAllClips() => QueryClips(
            "SELECT c.*,cat.name,cat.color FROM clips c LEFT JOIN categories cat ON c.category_id=cat.id ORDER BY c.created_at DESC",
            null);

        public List<AudioClip> SearchClips(string q) => QueryClips(
            "SELECT c.*,cat.name,cat.color FROM clips c LEFT JOIN categories cat ON c.category_id=cat.id WHERE c.name LIKE @q ORDER BY c.created_at DESC",
            cmd => cmd.Parameters.AddWithValue("@q", $"%{q}%"));

        public void UpdateClip(AudioClip clip)
        {
            using var cmd = Cmd(@"
                UPDATE clips SET name=@name,category_id=@cat,volume=@vol,
                hotkey_binding=@hotkey,fade_in_ms=@fi,fade_out_ms=@fo WHERE id=@id");
            cmd.Parameters.AddWithValue("@name",   clip.Name);
            cmd.Parameters.AddWithValue("@cat",    clip.CategoryId.HasValue ? (object)clip.CategoryId.Value : DBNull.Value);
            cmd.Parameters.AddWithValue("@vol",    clip.Volume);
            cmd.Parameters.AddWithValue("@hotkey", clip.HotkeyBinding ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@fi",     clip.FadeInMs);
            cmd.Parameters.AddWithValue("@fo",     clip.FadeOutMs);
            cmd.Parameters.AddWithValue("@id",     clip.Id);
            cmd.ExecuteNonQuery();
        }

        public void DeleteClip(int id)
        {
            using var cmd = Cmd("DELETE FROM clips WHERE id=@id");
            cmd.Parameters.AddWithValue("@id", id);
            cmd.ExecuteNonQuery();
        }

        public void DeleteAllClips()
        {
            Exec("DELETE FROM clips");
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private List<AudioClip> QueryClips(string sql, Action<SqliteCommand>? paramBinder)
        {
            var list = new List<AudioClip>();
            using var cmd = Cmd(sql);
            paramBinder?.Invoke(cmd);
            using var r = cmd.ExecuteReader();
            while (r.Read())
            {
                var clip = new AudioClip
                {
                    Id             = r.GetInt32(0),
                    Name           = r.GetString(1),
                    FilePath       = r.GetString(2),
                    Duration       = TimeSpan.FromMilliseconds(r.GetInt64(3)),
                    CreatedAt      = DateTime.Parse(r.GetString(4)),
                    CategoryId     = r.IsDBNull(5)  ? null : r.GetInt32(5),
                    Volume         = (float)r.GetDouble(6),
                    HotkeyBinding  = r.IsDBNull(7)  ? null : r.GetString(7),
                    FadeInMs       = (float)r.GetDouble(8),
                    FadeOutMs      = (float)r.GetDouble(9),
                    FileSizeBytes  = r.GetInt64(10)
                };
                if (!r.IsDBNull(11))
                    clip.Category = new ClipCategory
                    {
                        Id    = clip.CategoryId!.Value,
                        Name  = r.GetString(11),
                        Color = r.GetString(12)
                    };
                list.Add(clip);
            }
            return list;
        }

        private SqliteCommand Cmd(string sql) => new(sql, _conn);

        private void Exec(string sql)
        {
            using var cmd = Cmd(sql);
            cmd.ExecuteNonQuery();
        }

        public void Dispose() => _conn?.Dispose();
    }
}
