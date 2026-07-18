using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace FileTag.Core;

/// <summary>
/// Locates OneDrive and Google Drive Desktop sync roots so the router can
/// send those paths to sidecar storage (ADS doesn't survive cloud sync).
/// Detection is purely local — env vars, registry, volume labels; no API
/// calls, no accounts, no internet. Scanned once, then refreshed lazily on a
/// slow interval (accounts don't change often) — never on the hot path.
/// </summary>
public static class CloudFolderDetector
{
    private static readonly TimeSpan RefreshInterval = TimeSpan.FromMinutes(15);
    private static readonly object Gate = new();
    private static string[] _roots = [];
    private static DateTime _lastScanUtc = DateTime.MinValue;
    private static string[]? _testOverride;

    public static bool IsInCloudFolder(string path) => GetRootFor(path) is not null;

    /// <summary>The cloud sync root containing this path, or null.</summary>
    public static string? GetRootFor(string path)
    {
        string full;
        try { full = Path.GetFullPath(path); }
        catch { return null; }

        foreach (string root in GetRoots())
        {
            if (full.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                return root;
        }
        return null;
    }

    /// <summary>Current sync roots, each normalized with a trailing separator.</summary>
    public static IReadOnlyList<string> GetRoots()
    {
        if (_testOverride is not null) return _testOverride;
        lock (Gate)
        {
            if (DateTime.UtcNow - _lastScanUtc > RefreshInterval)
            {
                _roots = Scan();
                _lastScanUtc = DateTime.UtcNow;
            }
            return _roots;
        }
    }

    /// <summary>Test hook: pin the root list (null = resume auto-detection).</summary>
    public static void SetRootsForTesting(string[]? roots) =>
        _testOverride = roots?.Select(Normalize).ToArray();

    private static string[] Scan()
    {
        // Honest fragility note: every source below is undocumented local
        // config, not a stable contract — Google and Microsoft can change
        // these internals without warning. Each source therefore fails
        // independently and silently; if everything fails the root list is
        // simply empty and StorageRouter degrades gracefully to plain NTFS
        // routing — comments keep working locally, they just lose the
        // cross-device behavior instead of the app breaking.
        var roots = new List<string>();

        // --- OneDrive: environment variables -------------------------------
        foreach (string env in new[] { "OneDrive", "OneDriveConsumer", "OneDriveCommercial" })
        {
            string? v = Environment.GetEnvironmentVariable(env);
            if (!string.IsNullOrEmpty(v) && Directory.Exists(v)) roots.Add(v);
        }

        // --- OneDrive: per-account registry (covers renamed/multiple accounts)
        try
        {
            using var accounts = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\OneDrive\Accounts");
            if (accounts is not null)
            {
                foreach (string sub in accounts.GetSubKeyNames())
                {
                    using var key = accounts.OpenSubKey(sub);
                    if (key?.GetValue("UserFolder") is string folder
                        && folder.Length > 0 && Directory.Exists(folder))
                        roots.Add(folder);
                }
            }
        }
        catch { }

        // --- Google Drive Desktop: mount points from DriveFS preferences ----
        try
        {
            using var gd = Registry.CurrentUser.OpenSubKey(@"Software\Google\DriveFS");
            if (gd?.GetValue("PerAccountPreferences") is string prefs && prefs.Length > 0)
            {
                // Best-effort: pull every mount_point_path out of the JSON blob.
                foreach (Match m in Regex.Matches(prefs, "\"mount_point_path\"\\s*:\\s*\"([^\"]+)\""))
                {
                    string p = m.Groups[1].Value.Replace("\\\\", "\\");
                    if (p.Length == 1) p += ":\\"; // bare drive letter form ("G")
                    if (Directory.Exists(p)) roots.Add(p);
                }
            }
        }
        catch { }

        // --- Google Drive Desktop: mounted-drive fallback by volume label ---
        try
        {
            foreach (var drive in DriveInfo.GetDrives())
            {
                try
                {
                    if (drive.IsReady && drive.VolumeLabel.Contains("Google Drive", StringComparison.OrdinalIgnoreCase))
                        roots.Add(drive.RootDirectory.FullName);
                }
                catch { }
            }
        }
        catch { }

        return roots.Select(Normalize).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    }

    private static string Normalize(string root)
    {
        string full = Path.GetFullPath(root);
        return full.EndsWith(Path.DirectorySeparatorChar) ? full : full + Path.DirectorySeparatorChar;
    }
}
