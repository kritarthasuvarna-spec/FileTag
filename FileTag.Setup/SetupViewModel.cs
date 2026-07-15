using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Runtime.CompilerServices;
using FileTag.Core;
using Microsoft.Win32;

namespace FileTag.Setup;

/// <summary>
/// Drives the install: real per-step progress (each step reported only when
/// its work actually completed), everything logged to install.log.
/// </summary>
public sealed class SetupViewModel : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    public string InstallDir { get; set; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FileTag");

    public bool LaunchOnStartup { get; set; } = true;
    public bool LaunchAfterInstall { get; set; } = true;

    public string CurrentStep { get; private set; } = "";
    public double ProgressValue { get; private set; }
    public string? FailureMessage { get; private set; }

    public string InstalledExePath => Path.Combine(InstallDir, "FileTag.App.exe");

    /// <summary>Runs all install steps on a background thread. Returns success.</summary>
    public async Task<bool> RunAsync()
    {
        Logger.Init("Setup", Logger.InstallLogPath);
        Logger.Info($"install started — target: {InstallDir}, startup: {LaunchOnStartup}");
        try
        {
            await Step(1, "Copying files…", ExtractPayload);
            await Step(2, "Registering startup entry…", RegisterStartup);
            await Step(3, "Detecting Google Drive / OneDrive folders…", DetectCloudFolders);
            await Step(4, "Writing Apps & Features entry…", WriteUninstallEntry);
            CurrentStep = "Done";
            ProgressValue = 100;
            Notify();
            Logger.Info("install completed successfully");
            return true;
        }
        catch (Exception ex)
        {
            FailureMessage = ex.Message;
            Logger.Error($"install failed at \"{CurrentStep}\": {ex}");
            Notify();
            return false;
        }
    }

    private async Task Step(int number, string label, Action work)
    {
        CurrentStep = label;
        ProgressValue = (number - 1) * 25;
        Notify();
        await Task.Run(work);
        Logger.Info($"step ok: {label}");
    }

    private void ExtractPayload()
    {
        using Stream? payload = Assembly.GetExecutingAssembly()
            .GetManifestResourceStream("FileTag.Setup.payload.zip");
        if (payload is null) throw new InvalidOperationException("Setup payload missing — corrupted download?");

        // A previous instance may be running from the target folder.
        foreach (var p in Process.GetProcessesByName("FileTag.App"))
        {
            try { p.Kill(); p.WaitForExit(3000); } catch { }
            finally { p.Dispose(); }
        }

        Directory.CreateDirectory(InstallDir);
        using var zip = new ZipArchive(payload, ZipArchiveMode.Read);
        zip.ExtractToDirectory(InstallDir, overwriteFiles: true);
    }

    private void RegisterStartup()
    {
        // The app re-asserts this on every launch from IndexStore.StartWithWindows,
        // so record the user's choice there too — not just in the Run key.
        var index = new IndexStore();
        index.StartWithWindows = LaunchOnStartup;

        using var key = Registry.CurrentUser.CreateSubKey(
            @"Software\Microsoft\Windows\CurrentVersion\Run");
        if (LaunchOnStartup) key.SetValue("FileTag", $"\"{InstalledExePath}\"");
        else key.DeleteValue("FileTag", throwOnMissingValue: false);
    }

    private void DetectCloudFolders()
    {
        var roots = CloudFolderDetector.GetRoots();
        Logger.Info(roots.Count == 0
            ? "no cloud-sync folders detected"
            : $"cloud-sync roots: {string.Join(" | ", roots)}");
    }

    private void WriteUninstallEntry()
    {
        string version = typeof(SetupViewModel).Assembly.GetName().Version?.ToString(3) ?? "0.0.0";
        using var key = Registry.CurrentUser.CreateSubKey(
            @"Software\Microsoft\Windows\CurrentVersion\Uninstall\FileTag");
        key.SetValue("DisplayName", "FileTag");
        key.SetValue("DisplayVersion", version);
        key.SetValue("Publisher", "FileTag");
        key.SetValue("DisplayIcon", InstalledExePath);
        key.SetValue("InstallLocation", InstallDir);
        key.SetValue("UninstallString", $"\"{Path.Combine(InstallDir, "Uninstall.exe")}\"");
        key.SetValue("NoModify", 1, RegistryValueKind.DWord);
        key.SetValue("NoRepair", 1, RegistryValueKind.DWord);
        try
        {
            long bytes = Directory.EnumerateFiles(InstallDir).Sum(f => new FileInfo(f).Length);
            key.SetValue("EstimatedSize", (int)(bytes / 1024), RegistryValueKind.DWord);
        }
        catch { }
    }

    public void LaunchApp(bool openSettings)
    {
        try
        {
            var psi = new ProcessStartInfo(InstalledExePath) { UseShellExecute = false };
            if (openSettings) psi.EnvironmentVariables["FILETAG_OPEN_SETTINGS"] = "1";
            Process.Start(psi);
            Logger.Info("app launched" + (openSettings ? " (settings opened)" : ""));
        }
        catch (Exception ex) { Logger.Error("launch failed: " + ex.Message); }
    }

    private void Notify() => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(string.Empty));
}
