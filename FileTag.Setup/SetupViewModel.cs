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

    // ---- existing-install detection (the fix for side-by-side installs) ----
    public string? ExistingVersion { get; private set; }
    public string? ExistingLocation { get; private set; }
    public string NewVersion { get; } =
        typeof(SetupViewModel).Assembly.GetName().Version?.ToString(3) ?? "0.0.0";

    /// <summary>Reads the Apps &amp; Features key. True if a usable install exists.</summary>
    public bool DetectExistingInstall()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Uninstall\FileTag");
            string? loc = key?.GetValue("InstallLocation") as string;
            if (string.IsNullOrEmpty(loc) || !Directory.Exists(loc)) return false;
            ExistingLocation = loc;
            ExistingVersion = key?.GetValue("DisplayVersion") as string ?? "?";
            return true;
        }
        catch { return false; }
    }

    /// <summary>True when the payload is newer than the installed version
    /// (System.Version comparison, never string compare).</summary>
    public bool IsUpgrade =>
        Version.TryParse(ExistingVersion, out var ex) && Version.TryParse(NewVersion, out var nw)
            ? nw > ex : true;

    /// <summary>Update-in-place: the target folder comes from the registry,
    /// never from user choice — Update cannot create a second install.
    /// Notes live in the tagged files and settings in %APPDATA%, so an
    /// update structurally cannot touch either.</summary>
    public void ConfigureAsUpdate()
    {
        InstallDir = ExistingLocation!;
        // Preserve the user's startup preference instead of resetting it.
        try
        {
            using var run = Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Run");
            LaunchOnStartup = run?.GetValue("FileTag") is not null;
        }
        catch { }
    }

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
            // The runtime check gets its own generous band (0-25%) with continuous
            // sub-progress during download — everything else is a discrete step
            // reported only once its work actually completed.
            CurrentStep = "Checking for the .NET Runtime…";
            Notify();
            await RuntimeInstaller.EnsureInstalledAsync((pct, label) =>
            {
                CurrentStep = label;
                ProgressValue = pct * 0.25;
                Notify();
            });
            Logger.Info("step ok: .NET Runtime present");

            await Step(1, 5, "Copying files…", ExtractPayload);
            await Step(2, 5, "Registering startup entry…", RegisterStartup);
            await Step(3, 5, "Creating Start Menu shortcut…", CreateStartMenuShortcut);
            await Step(4, 5, "Detecting Google Drive / OneDrive folders…", DetectCloudFolders);
            await Step(5, 5, "Writing Apps & Features entry…", WriteUninstallEntry);
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

    /// <summary>Steps after the runtime check evenly split the remaining 75% of the bar.</summary>
    private async Task Step(int number, int totalAfterRuntime, string label, Action work)
    {
        CurrentStep = label;
        ProgressValue = 25 + (number - 1) * (75.0 / totalAfterRuntime);
        Notify();
        await Task.Run(work);
        Logger.Info($"step ok: {label}");
    }

    private void ExtractPayload()
    {
        using Stream? payload = Assembly.GetExecutingAssembly()
            .GetManifestResourceStream("FileTag.Setup.payload.zip");
        if (payload is null) throw new InvalidOperationException("Setup payload missing — corrupted download?");

        StopRunningApp();

        Directory.CreateDirectory(InstallDir);
        using var zip = new ZipArchive(payload, ZipArchiveMode.Read);
        zip.ExtractToDirectory(InstallDir, overwriteFiles: true);

        // Installing over a just-uninstalled copy: cancel its pending folder deletion.
        try { File.Delete(Path.Combine(InstallDir, ".filetag-uninstall-pending")); } catch { }
    }

    /// <summary>Ask the running app to exit gracefully (named event it listens
    /// on), wait briefly, and only then fall back to killing the process.</summary>
    private static void StopRunningApp()
    {
        var procs = Process.GetProcessesByName("FileTag.App");
        if (procs.Length == 0) return;
        try
        {
            if (EventWaitHandle.TryOpenExisting("FileTag.App.ExitRequest", out var ev))
            {
                using (ev) ev.Set();
                foreach (var p in procs) { try { p.WaitForExit(5000); } catch { } }
                Logger.Info("running app asked to exit gracefully");
            }
        }
        catch { }
        foreach (var p in procs)
        {
            try { if (!p.HasExited) { p.Kill(); p.WaitForExit(3000); Logger.Warn("app killed (graceful exit timed out)"); } }
            catch { }
            finally { p.Dispose(); }
        }
    }

    private void CreateStartMenuShortcut()
    {
        var t = Type.GetTypeFromProgID("WScript.Shell")
            ?? throw new InvalidOperationException("WScript.Shell unavailable");
        dynamic shell = Activator.CreateInstance(t)!;
        dynamic sc = shell.CreateShortcut(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.Programs), "FileTag.lnk"));
        sc.TargetPath = InstalledExePath;
        sc.WorkingDirectory = InstallDir;
        sc.IconLocation = InstalledExePath;
        sc.Description = "FileTag — notes on your files";
        sc.Save();
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
