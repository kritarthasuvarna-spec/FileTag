namespace FootNote.Core;

/// <summary>
/// <see cref="ICommentStore"/> backed by a hidden companion file —
/// "report.xlsx" → "report.xlsx.footnote" in the same folder.
/// Used where ADS isn't available (FAT32/exFAT) or wouldn't survive
/// (Google Drive / OneDrive synced folders: the sync engine uploads a plain
/// file like any other, which is what makes a comment appear on another PC).
/// </summary>
public sealed class SidecarHelper : ICommentStore
{
    public const string Suffix = ".footnote";

    /// <summary>Suffix from the app's previous life as FileTag. Never written
    /// again, only read as a fallback and migrated away from — see <see cref="MigrateLegacy"/>.</summary>
    public const string LegacySuffix = ".filetag";

    public static string SidecarPath(string filePath) => filePath + Suffix;
    public static string LegacySidecarPath(string filePath) => filePath + LegacySuffix;

    /// <summary>Sidecar files (current or legacy) must never be commented or surfaced.</summary>
    public static bool IsSidecar(string path) =>
        path.EndsWith(Suffix, StringComparison.OrdinalIgnoreCase) ||
        path.EndsWith(LegacySuffix, StringComparison.OrdinalIgnoreCase);

    public bool HasComment(string filePath)
    {
        try { return File.Exists(SidecarPath(filePath)) || File.Exists(LegacySidecarPath(filePath)); }
        catch { return false; }
    }

    public NoteHistory? ReadHistory(string filePath)
    {
        try
        {
            string sp = SidecarPath(filePath);
            if (File.Exists(sp)) return NoteFormat.Parse(File.ReadAllText(sp), File.GetLastWriteTimeUtc(sp));
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { return null; }

        try
        {
            string legacy = LegacySidecarPath(filePath);
            return NoteFormat.Parse(File.ReadAllText(legacy), File.GetLastWriteTimeUtc(legacy));
        }
        catch (IOException) { return null; }
        catch (UnauthorizedAccessException) { return null; }
    }

    public void WriteHistory(string filePath, NoteHistory history)
    {
        string sp = SidecarPath(filePath);
        // Overwriting a hidden file fails outright, so unhide before writing.
        if (File.Exists(sp)) File.SetAttributes(sp, FileAttributes.Normal);
        File.WriteAllText(sp, NoteFormat.Serialize(history));
        File.SetAttributes(sp, FileAttributes.Hidden);
    }

    public void Delete(string filePath)
    {
        // Works even when the original file is gone (orphaned sidecar cleanup).
        foreach (string sp in new[] { SidecarPath(filePath), LegacySidecarPath(filePath) })
        {
            try
            {
                if (!File.Exists(sp)) continue;
                File.SetAttributes(sp, FileAttributes.Normal);
                File.Delete(sp);
            }
            catch { /* locked/inaccessible — best effort */ }
        }
    }

    /// <summary>One-time rebrand migration: if a legacy .filetag sidecar exists and
    /// the new one doesn't, rename it. Safe to call repeatedly. Never throws.</summary>
    public bool MigrateLegacy(string filePath)
    {
        try
        {
            string legacy = LegacySidecarPath(filePath);
            string current = SidecarPath(filePath);
            if (!File.Exists(legacy) || File.Exists(current)) return false;
            File.SetAttributes(legacy, FileAttributes.Normal);
            File.Move(legacy, current);
            File.SetAttributes(current, FileAttributes.Hidden);
            return true;
        }
        catch { return false; }
    }
}
