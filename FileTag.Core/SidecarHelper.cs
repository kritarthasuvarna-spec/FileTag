namespace FileTag.Core;

/// <summary>
/// <see cref="ICommentStore"/> backed by a hidden companion file —
/// "report.xlsx" → "report.xlsx.filetag" in the same folder.
/// Used where ADS isn't available (FAT32/exFAT) or wouldn't survive
/// (Google Drive / OneDrive synced folders: the sync engine uploads a plain
/// file like any other, which is what makes a comment appear on another PC).
/// </summary>
public sealed class SidecarHelper : ICommentStore
{
    public const string Suffix = ".filetag";

    public static string SidecarPath(string filePath) => filePath + Suffix;

    /// <summary>Sidecar files themselves must never be commented or surfaced.</summary>
    public static bool IsSidecar(string path) =>
        path.EndsWith(Suffix, StringComparison.OrdinalIgnoreCase);

    public bool HasComment(string filePath)
    {
        try { return File.Exists(SidecarPath(filePath)); }
        catch { return false; }
    }

    public NoteHistory? ReadHistory(string filePath)
    {
        try
        {
            string sp = SidecarPath(filePath);
            return NoteFormat.Parse(File.ReadAllText(sp), File.GetLastWriteTimeUtc(sp));
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
        string sp = SidecarPath(filePath);
        try
        {
            if (!File.Exists(sp)) return;
            File.SetAttributes(sp, FileAttributes.Normal);
            File.Delete(sp);
        }
        catch { /* locked/inaccessible — best effort */ }
    }
}
