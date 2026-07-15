using System.Diagnostics;
using System.IO;
using Microsoft.Win32;

namespace FileTag.App;

/// <summary>
/// Per-user (HKCU, no admin) self-registration: Run-at-startup value and the
/// Apps &amp; Features uninstall entry. Idempotent — re-run on every launch so the
/// registry always points at the current install location.
/// </summary>
internal static class InstallHelper
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string UninstallKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\FileTag";
    private const string AppName = "FileTag";

    public static string ExePath => Environment.ProcessPath ?? Process.GetCurrentProcess().MainModule!.FileName;

    /// <summary>Install folder currently recorded in Apps &amp; Features, or null.</summary>
    public static string? ReadRegisteredLocation()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(UninstallKeyPath);
            return key?.GetValue("InstallLocation") as string;
        }
        catch { return null; }
    }

    public static void RegisterAll(bool startWithWindows)
    {
        try
        {
            SetStartup(startWithWindows);
            WriteUninstallEntry();
        }
        catch { /* registry unavailable — app still functions this session */ }
    }

    public static void SetStartup(bool enabled)
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(RunKeyPath);
            if (enabled) key.SetValue(AppName, $"\"{ExePath}\"");
            else key.DeleteValue(AppName, throwOnMissingValue: false);
        }
        catch { }
    }

    private static void WriteUninstallEntry()
    {
        string dir = Path.GetDirectoryName(ExePath)!;
        string uninstaller = Path.Combine(dir, "Uninstall.exe");
        string version = typeof(InstallHelper).Assembly.GetName().Version?.ToString(3) ?? "1.0.0";

        using var key = Registry.CurrentUser.CreateSubKey(UninstallKeyPath);
        key.SetValue("DisplayName", AppName);
        key.SetValue("DisplayVersion", version);
        key.SetValue("Publisher", AppName);
        key.SetValue("DisplayIcon", ExePath);
        key.SetValue("InstallLocation", dir);
        key.SetValue("UninstallString", $"\"{uninstaller}\"");
        key.SetValue("NoModify", 1, RegistryValueKind.DWord);
        key.SetValue("NoRepair", 1, RegistryValueKind.DWord);
        try
        {
            long bytes = Directory.EnumerateFiles(dir).Sum(f => new FileInfo(f).Length);
            key.SetValue("EstimatedSize", (int)(bytes / 1024), RegistryValueKind.DWord);
        }
        catch { }
    }
}
