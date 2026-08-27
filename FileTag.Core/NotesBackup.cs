using System.IO.Compression;
using System.Text.Json;

namespace FileTag.Core;

/// <summary>
/// Last-resort local backup of every note FileTag has ever saved — a compact,
/// gzip-compressed mirror kept purely so an accidental delete (or a note lost
/// to a stripped ADS stream, e.g. the file copied to a non-NTFS drive) is
/// recoverable. This is NOT the source of truth — the tagged file's own ADS
/// stream or sidecar is — it exists only to be read by the recovery UI.
///
/// Deleted notes are kept here (marked, not erased): surviving deletion is
/// the entire point of a backup. Text compresses extremely well, so even a
/// heavily-used install stays a small fraction of a megabyte gzipped.
/// </summary>
public static class NotesBackup
{
    private static string? _testPath;

    public static string BackupPath => _testPath ?? Path.Combine(IndexStore.DataDirectory, "notes-backup.json.gz");

    /// <summary>Test hook: redirect the backup file (null = resume the real one).</summary>
    public static void SetPathForTesting(string? path) => _testPath = path;

    public sealed class Entry
    {
        public string Path { get; set; } = "";
        public NoteHistory History { get; set; } = new();
        /// <summary>Null while the note is live; set when StorageRouter.Delete removed it.</summary>
        public DateTime? DeletedAtUtc { get; set; }
    }

    private static readonly object Gate = new();

    /// <summary>Call after every successful save — mirrors the file's current full history.</summary>
    public static void RecordSave(string path, NoteHistory history)
    {
        try
        {
            lock (Gate)
            {
                var entries = Load();
                var existing = entries.FirstOrDefault(e => string.Equals(e.Path, path, StringComparison.OrdinalIgnoreCase));
                if (existing is not null)
                {
                    existing.History = Clone(history);
                    existing.DeletedAtUtc = null; // saving again un-deletes it
                }
                else
                {
                    entries.Add(new Entry { Path = path, History = Clone(history) });
                }
                Save(entries);
            }
        }
        catch { /* backup is best-effort — must never block the real save */ }
    }

    /// <summary>Call when a note is actually deleted — keeps the last known text, marks when.</summary>
    public static void RecordDelete(string path)
    {
        try
        {
            lock (Gate)
            {
                var entries = Load();
                var existing = entries.FirstOrDefault(e => string.Equals(e.Path, path, StringComparison.OrdinalIgnoreCase));
                if (existing is null) return; // nothing was ever backed up for this path
                existing.DeletedAtUtc = DateTime.UtcNow;
                Save(entries);
            }
        }
        catch { }
    }

    /// <summary>Everything in the backup, newest activity first. Never throws.</summary>
    public static List<Entry> LoadAll()
    {
        try
        {
            lock (Gate)
            {
                return Load()
                    .OrderByDescending(e => e.DeletedAtUtc ?? e.History.Latest?.At ?? DateTime.MinValue)
                    .ToList();
            }
        }
        catch { return new(); }
    }

    private static NoteHistory Clone(NoteHistory h) => new()
    {
        History = h.History.Select(e => new NoteEntry { Text = e.Text, At = e.At }).ToList(),
    };

    private static List<Entry> Load()
    {
        if (!File.Exists(BackupPath)) return new();
        using var fs = File.OpenRead(BackupPath);
        using var gz = new GZipStream(fs, CompressionMode.Decompress);
        return JsonSerializer.Deserialize(gz, FileTagJsonContext.Default.ListEntry) ?? new();
    }

    private static void Save(List<Entry> entries)
    {
        Directory.CreateDirectory(IndexStore.DataDirectory);
        // Write to a temp file first so a crash mid-write can't corrupt the backup itself.
        string tmp = BackupPath + ".tmp";
        using (var fs = File.Create(tmp))
        using (var gz = new GZipStream(fs, CompressionLevel.Optimal))
        {
            JsonSerializer.Serialize(gz, entries, FileTagJsonContext.Default.ListEntry);
        }
        File.Copy(tmp, BackupPath, overwrite: true);
        File.Delete(tmp);
    }
}
