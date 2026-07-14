using System.Diagnostics;
using System.Runtime.InteropServices;
using FileTag.Core;
using Microsoft.Win32;

namespace FileTag.Uninstaller;

/// <summary>
/// Full cleanup, in this order (spec):
///   1. strip every FileTag comment stream listed in the local index
///      (files themselves untouched; moved/deleted files skipped + logged)
///   2. stop the running tray app
///   3. remove the Startup entry
///   4. remove the Apps &amp; Features registry key
///   5. delete the local index/config
///   6. delete the install folder (scheduled, since this exe lives inside it)
/// Never leaves orphaned FileTag data behind with no app to manage it.
/// </summary>
internal static class Program
{
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int MessageBoxW(IntPtr hWnd, string text, string caption, uint type);

    private const uint MB_YESNO = 0x4, MB_OK = 0x0, MB_ICONWARNING = 0x30, MB_ICONINFORMATION = 0x40;
    private const int IDYES = 6;

    private static readonly List<string> Log = new();

    [STAThread]
    private static int Main(string[] args)
    {
        bool silent = args.Any(a => a.Equals("/S", StringComparison.OrdinalIgnoreCase));

        int answer = silent ? IDYES : MessageBoxW(IntPtr.Zero,
            "This will remove FileTag and delete every comment it has attached to your files.\n" +
            "The files themselves are not touched.\n\nContinue?",
            "Uninstall FileTag", MB_YESNO | MB_ICONWARNING);
        if (answer != IDYES) return 1;

        // 1. Strip comments from both backends (do this while the index still exists).
        int stripped = 0, skipped = 0;
        try
        {
            var index = new IndexStore();
            foreach (string path in index.GetPaths())
            {
                try
                {
                    // Delete covers ADS and sidecar alike, and cleans up an
                    // orphaned sidecar even when the original file moved away.
                    bool had = StorageRouter.HasComment(path);
                    StorageRouter.Delete(path);
                    if (had) stripped++;
                    else { skipped++; Log.Add($"skipped (moved/deleted): {path}"); }
                }
                catch (Exception ex) { skipped++; Log.Add($"skipped ({ex.Message}): {path}"); }
            }
        }
        catch (Exception ex) { Log.Add("index unreadable: " + ex.Message); }

        // 2. Stop the tray app.
        foreach (var p in Process.GetProcessesByName("FileTag.App"))
        {
            try { p.Kill(); p.WaitForExit(3000); } catch { }
            finally { p.Dispose(); }
        }

        // 3 + 4. Registry cleanup.
        try
        {
            using var run = Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Run", writable: true);
            run?.DeleteValue("FileTag", throwOnMissingValue: false);
        }
        catch (Exception ex) { Log.Add("startup entry: " + ex.Message); }
        try
        {
            Registry.CurrentUser.DeleteSubKeyTree(
                @"Software\Microsoft\Windows\CurrentVersion\Uninstall\FileTag", throwOnMissingSubKey: false);
        }
        catch (Exception ex) { Log.Add("uninstall key: " + ex.Message); }

        // 5. Local data.
        try { Directory.Delete(IndexStore.DataDirectory, recursive: true); }
        catch (Exception ex) { Log.Add("data dir: " + ex.Message); }

        WriteLog();

        if (!silent)
            MessageBoxW(IntPtr.Zero,
                $"FileTag has been removed.\nComments stripped: {stripped}" +
                (skipped > 0 ? $" (skipped {skipped} moved/deleted files)" : ""),
                "Uninstall FileTag", MB_OK | MB_ICONINFORMATION);

        // 6. Delete the install folder after this process exits.
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
        }
        catch { }
        return 0;
    }

    private static void WriteLog()
    {
        if (Log.Count == 0) return;
        try
        {
            File.WriteAllLines(Path.Combine(Path.GetTempPath(), "FileTag-uninstall.log"),
                Log.Prepend($"FileTag uninstall {DateTime.Now:yyyy-MM-dd HH:mm:ss}"));
        }
        catch { }
    }
}
