namespace FootNote.Core;

/// <summary>
/// A storage backend for file comments. Two implementations exist —
/// <see cref="AdsHelper"/> (NTFS alternate data streams) and
/// <see cref="SidecarHelper"/> (hidden companion files) — and
/// <see cref="StorageRouter"/> picks one per path. Nothing above the router
/// may care which backend owns a file.
/// </summary>
public interface ICommentStore
{
    /// <summary>Cheap existence check — gates whether the overlay bar shows at all.</summary>
    bool HasComment(string filePath);

    /// <summary>Full history, or null if none / unreadable.</summary>
    NoteHistory? ReadHistory(string filePath);

    /// <summary>Persists the history (creates or replaces).</summary>
    void WriteHistory(string filePath, NoteHistory history);

    /// <summary>Removes the comment storage. The commented file is untouched. No-op if absent.</summary>
    void Delete(string filePath);
}
