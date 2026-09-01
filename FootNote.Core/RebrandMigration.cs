namespace FootNote.Core;

/// <summary>
/// One-time migration from this app's previous life as "FileTag" to its
/// current name, "FootNote". Moves the two local data folders wholesale
/// (so settings, the index, logs, and the notes-backup safety net all
/// survive intact) and cleans up the old per-user registry entries so
/// Apps &amp; Features and the Startup list don't end up with duplicates.
///
/// Idempotent and safe to call from both the app and Setup — whichever
/// runs first does the work; the other finds nothing left to migrate.
/// Never throws: a failed migration should never block a normal launch.
/// </summary>
public static class RebrandMigration
{
    private static string LegacyLocalDataDir => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FileTag");

    private static string LegacyRoamingDataDir => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "FileTag");

    private static string RoamingDataDir => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "FootNote");

    /// <summary>Moves %LocalAppData%\FileTag → …\FootNote and
    /// %AppData%\FileTag → …\FootNote, if the old folders exist and the
    /// new ones don't yet. Returns true if anything was migrated.</summary>
    public static bool MigrateDataFolders()
    {
        bool migrated = false;
        migrated |= MoveIfNeeded(LegacyLocalDataDir, IndexStore.DataDirectory);
        migrated |= MoveIfNeeded(LegacyRoamingDataDir, RoamingDataDir);
        return migrated;
    }

    private static bool MoveIfNeeded(string oldDir, string newDir)
    {
        if (!Directory.Exists(oldDir) || Directory.Exists(newDir)) return false;

        // The app/process that owned the old folder may have only just exited
        // (Setup's StopRunningApp, or the user closing the old build seconds
        // ago) — a file handle or an AV/indexer scan can still be releasing
        // for a moment, so a straight Directory.Move can transiently fail.
        // Retry briefly before falling back to copy+delete.
        Exception? lastError = null;
        for (int attempt = 1; attempt <= 5; attempt++)
        {
            try
            {
                Directory.Move(oldDir, newDir); // same volume (both under the user profile) — fast, atomic
                return true;
            }
            catch (Exception ex)
            {
                lastError = ex;
                Thread.Sleep(300);
            }
        }
        Logger.Warn($"migration: Directory.Move retries exhausted for {oldDir} -> {newDir}: {lastError?.Message}");

        // Cross-volume profile redirection or a still-held lock — fall back to copy + delete.
        try
        {
            if (Directory.Exists(newDir)) return false;
            CopyDirectory(oldDir, newDir);
            Directory.Delete(oldDir, recursive: true);
            return true;
        }
        catch (Exception ex)
        {
            Logger.Error($"migration: copy+delete fallback also failed for {oldDir} -> {newDir}: {ex}");
            return false;
        }
    }

    private static void CopyDirectory(string source, string dest)
    {
        Directory.CreateDirectory(dest);
        foreach (string dir in Directory.GetDirectories(source, "*", SearchOption.AllDirectories))
            Directory.CreateDirectory(dir.Replace(source, dest));
        foreach (string file in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
            File.Copy(file, file.Replace(source, dest), overwrite: true);
    }

    /// <summary>Removes the old FileTag-named per-user registry entries (Apps &amp;
    /// Features key and Run value) after the new FootNote-named ones are written,
    /// so nothing shows up twice. Safe no-op if they were never there.</summary>
    public static void CleanupLegacyRegistry()
    {
        try
        {
            using var run = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Run", writable: true);
            run?.DeleteValue("FileTag", throwOnMissingValue: false);
        }
        catch { }
        try
        {
            Microsoft.Win32.Registry.CurrentUser.DeleteSubKeyTree(
                @"Software\Microsoft\Windows\CurrentVersion\Uninstall\FileTag", throwOnMissingSubKey: false);
        }
        catch { }
    }
}
