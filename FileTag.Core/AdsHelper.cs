namespace FileTag.Core;

/// <summary>
/// <see cref="ICommentStore"/> backed by an NTFS alternate data stream —
/// "&lt;path&gt;:FileTag.txt" — the default for files on local NTFS drives.
/// The comment lives inside the file, so renames and moves within NTFS carry
/// it automatically, and no extra file ever appears next to the original.
///
/// Writing or deleting a stream would normally bump the host file's
/// LastWriteTime, so original timestamps are captured and restored around
/// every mutation — the file must look untouched.
/// </summary>
public sealed class AdsHelper : ICommentStore
{
    public const string StreamName = "FileTag.txt";

    private static string StreamPath(string filePath) => filePath + ":" + StreamName;

    public bool HasComment(string filePath)
    {
        try { return File.Exists(StreamPath(filePath)); }
        catch { return false; }
    }

    public NoteHistory? ReadHistory(string filePath)
    {
        try
        {
            string raw = File.ReadAllText(StreamPath(filePath));
            return NoteFormat.Parse(raw, File.GetLastWriteTimeUtc(filePath));
        }
        catch (IOException) { return null; }
        catch (UnauthorizedAccessException) { return null; }
    }

    public void WriteHistory(string filePath, NoteHistory history)
    {
        string json = NoteFormat.Serialize(history);
        WithTimestampsPreserved(filePath, () => File.WriteAllText(StreamPath(filePath), json));
    }

    public void Delete(string filePath)
    {
        string sp = StreamPath(filePath);
        try
        {
            if (!File.Exists(sp)) return;
            WithTimestampsPreserved(filePath, () => File.Delete(sp));
        }
        catch { /* stream gone or inaccessible — nothing to manage */ }
    }

    private static void WithTimestampsPreserved(string filePath, Action action)
    {
        DateTime created = default, written = default, accessed = default;
        bool captured = false;
        try
        {
            created = File.GetCreationTimeUtc(filePath);
            written = File.GetLastWriteTimeUtc(filePath);
            accessed = File.GetLastAccessTimeUtc(filePath);
            captured = true;
        }
        catch { /* best effort */ }

        action();

        if (!captured) return;
        try
        {
            File.SetCreationTimeUtc(filePath, created);
            File.SetLastWriteTimeUtc(filePath, written);
            File.SetLastAccessTimeUtc(filePath, accessed);
        }
        catch { /* best effort */ }
    }
}
