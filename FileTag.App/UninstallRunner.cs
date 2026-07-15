using System.Diagnostics;
using System.IO;
using FileTag.Core;
using Microsoft.Win32;

namespace FileTag.App;

/// <summary>
/// The actual uninstall work, UI-free so the wizard and the /S test path share
/// it. Order (spec): strip comments (both backends) → registry cleanup →
/// local data → install folder (scheduled — this exe lives inside it).
/// The log goes to %TEMP%, never the install folder: a log inside the folder
/// being deleted would erase its own record on completion.
/// </summary>
internal sealed class UninstallRunner
{
    private readonly IndexStore _index = new();
    private readonly IReadOnlyCollection<string> _paths;

    public int Stripped { get; private set; }
    public int Skipped { get; private set; }
    public int TaggedFileCount => _paths.Count;

    public UninstallRunner()
    {
        _paths = _index.GetPaths();
    }

    public void Run(Action<int, int, string>? onProgress = null)
    {
        Logger.Info($"uninstall started — {_paths.Count} indexed path(s)");

        int i = 0;
        foreach (string path in _paths)
        {
            i++;
            onProgress?.Invoke(i, _paths.Count, path);
            try
            {
                bool had = StorageRouter.HasComment(path);
                StorageRouter.Delete(path);
                if (had) { Stripped++; Logger.Info($"stripped: {path}"); }
                else { Skipped++; Logger.Warn($"skipped (moved/deleted): {path}"); }
            }
            catch (Exception ex)
            {
                Skipped++;
                Logger.Warn($"skipped ({ex.Message}): {path}");
            }
        }

        try
        {
            using var run = Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Run", writable: true);
            run?.DeleteValue("FileTag", throwOnMissingValue: false);
            Logger.Info("startup entry removed");
        }
        catch (Exception ex) { Logger.Error("startup entry: " + ex.Message); }

        try
        {
            Registry.CurrentUser.DeleteSubKeyTree(
                @"Software\Microsoft\Windows\CurrentVersion\Uninstall\FileTag", throwOnMissingSubKey: false);
            Logger.Info("Apps & Features entry removed");
        }
        catch (Exception ex) { Logger.Error("uninstall key: " + ex.Message); }

        try
        {
            Directory.Delete(IndexStore.DataDirectory, recursive: true);
            Logger.Info("local index/config removed");
        }
        catch (Exception ex) { Logger.Warn("data dir: " + ex.Message); }

        try
        {
            string settingsDir = Path.GetDirectoryName(Settings.SettingsService.SettingsPath)!;
            bool existed = Directory.Exists(settingsDir);
            if (existed) Directory.Delete(settingsDir, recursive: true);
            Logger.Info($"settings/logs removed (dir: {settingsDir}, existed: {existed}, still-there: {Directory.Exists(settingsDir)})");
        }
        catch (Exception ex) { Logger.Warn("settings dir: " + ex.Message); }

        Logger.Info($"uninstall summary — stripped: {Stripped}, skipped: {Skipped}");
    }

    /// <summary>Deletes the install folder after this process exits.</summary>
    public void ScheduleInstallFolderDeletion()
    {
        string installDir = AppContext.BaseDirectory.TrimEnd('\\');
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = $"/c ping -n 3 127.0.0.1 >nul & rd /s /q \"{installDir}\"",
                CreateNoWindow = true,
                UseShellExecute = false,
                WorkingDirectory = Path.GetTempPath(),
            });
            Logger.Info($"install folder deletion scheduled: {installDir}");
        }
        catch (Exception ex) { Logger.Error("folder deletion: " + ex.Message); }
    }

    /// <summary>Stops other running FileTag.App instances (not this one).</summary>
    public static void KillOtherInstances()
    {
        int self = Environment.ProcessId;
        foreach (var p in Process.GetProcessesByName("FileTag.App"))
        {
            try { if (p.Id != self) { p.Kill(); p.WaitForExit(3000); } }
            catch { }
            finally { p.Dispose(); }
        }
    }
}
