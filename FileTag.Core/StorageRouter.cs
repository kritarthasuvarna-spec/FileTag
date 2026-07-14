using System.Collections.Concurrent;

namespace FileTag.Core;

/// <summary>
/// Picks the storage backend per path and exposes the only comment API the
/// rest of the app uses. Routing rule (spec):
///
///   inside a cloud-sync folder → sidecar   (survives sync, shows up cross-device)
///   local NTFS drive           → ADS       (invisible, zero extra files)
///   non-NTFS drive             → sidecar   (FAT32/exFAT USB sticks etc.)
///
/// Reads are resilient to files that moved between worlds (e.g. dragged from a
/// local folder into OneDrive with the ADS comment still attached): the routed
/// backend is tried first, then the other. Saving migrates the comment to the
/// correct backend for where the file lives now.
/// </summary>
public static class StorageRouter
{
    public const int MaxCommentLength = 500;

    private static readonly AdsHelper Ads = new();
    private static readonly SidecarHelper Sidecar = new();
    private static readonly ConcurrentDictionary<string, bool> NtfsCache =
        new(StringComparer.OrdinalIgnoreCase);

    public static ICommentStore RouteFor(string filePath)
    {
        if (CloudFolderDetector.IsInCloudFolder(filePath)) return Sidecar;
        return IsNtfs(filePath) ? Ads : Sidecar;
    }

    private static ICommentStore OtherThan(ICommentStore store) =>
        ReferenceEquals(store, Ads) ? Sidecar : Ads;

    public static bool HasComment(string filePath)
    {
        var routed = RouteFor(filePath);
        return routed.HasComment(filePath) || OtherThan(routed).HasComment(filePath);
    }

    /// <summary>Newest entry as the UI-facing <see cref="Note"/>, or null.</summary>
    public static Note? ReadLatest(string filePath)
    {
        var latest = ReadHistory(filePath)?.Latest;
        return latest is null ? null : new Note { Text = latest.Text, ModifiedUtc = latest.At };
    }

    public static NoteHistory? ReadHistory(string filePath)
    {
        var routed = RouteFor(filePath);
        return routed.ReadHistory(filePath) ?? OtherThan(routed).ReadHistory(filePath);
    }

    /// <summary>Appends a new history entry and persists. Empty text deletes the comment.</summary>
    public static void Save(string filePath, string text)
    {
        text = text?.Trim() ?? "";
        if (text.Length == 0) { Delete(filePath); return; }
        if (text.Length > MaxCommentLength) text = text[..MaxCommentLength];

        var routed = RouteFor(filePath);
        var other = OtherThan(routed);

        var history = routed.ReadHistory(filePath) ?? other.ReadHistory(filePath) ?? new NoteHistory();
        history.Append(text, DateTime.UtcNow);
        routed.WriteHistory(filePath, history);

        // The comment now lives in the right backend — drop any stale copy.
        if (other.HasComment(filePath)) other.Delete(filePath);
    }

    /// <summary>Removes the comment from both backends. Files are untouched.</summary>
    public static void Delete(string filePath)
    {
        Ads.Delete(filePath);
        Sidecar.Delete(filePath);
    }

    public static bool IsNtfs(string filePath)
    {
        try
        {
            string? root = Path.GetPathRoot(Path.GetFullPath(filePath));
            if (string.IsNullOrEmpty(root)) return false;
            return NtfsCache.GetOrAdd(root, r =>
            {
                try { return new DriveInfo(r).DriveFormat.Equals("NTFS", StringComparison.OrdinalIgnoreCase); }
                catch { return false; }
            });
        }
        catch { return false; }
    }
}
