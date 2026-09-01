using System.Diagnostics;
using System.IO;
using System.Net.Http;
using FileTag.Core;

namespace FileTag.Setup;

/// <summary>
/// Detects and, if missing, silently installs the .NET 8 Desktop Runtime that
/// the (now framework-dependent) FileTag.App needs. Setup itself stays
/// self-contained specifically so it can run this check on a bare machine
/// that has nothing installed yet — a chicken-and-egg problem if Setup
/// needed the runtime it's supposed to be checking for.
/// </summary>
public static class RuntimeInstaller
{
    /// <summary>Microsoft's evergreen redirect for the latest 8.0.x Desktop Runtime (x64).
    /// This is the address Microsoft publishes specifically for bootstrappers like this one —
    /// it always resolves to the current patch release, never a stale/vulnerable old build.</summary>
    private const string DownloadUrl = "https://aka.ms/dotnet/8.0/windowsdesktop-runtime-win-x64.exe";

    /// <summary>Test hook: force the "not installed" path without touching the real runtime.</summary>
    public static bool ForceMissingForTesting { get; set; } =
        Environment.GetEnvironmentVariable("FILETAG_FORCE_RUNTIME_MISSING") == "1";

    public static bool IsInstalled()
    {
        if (ForceMissingForTesting) return false;
        try
        {
            string sharedFx = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                "dotnet", "shared", "Microsoft.WindowsDesktop.App");
            if (!Directory.Exists(sharedFx)) return false;

            return Directory.EnumerateDirectories(sharedFx)
                .Select(d => Path.GetFileName(d))
                .Select(name => Version.TryParse(name, out var v) ? v : null)
                .Any(v => v is { Major: 8 });
        }
        catch { return false; } // can't tell — treat as missing, the installer is idempotent anyway
    }

    /// <summary>Downloads (reporting 0-90% via <paramref name="onProgress"/>) then silently
    /// installs (90-100%) the runtime. Throws with a clear message on failure.</summary>
    public static async Task EnsureInstalledAsync(Action<int, string> onProgress)
    {
        if (IsInstalled() && !ForceMissingForTesting)
        {
            onProgress(100, "The .NET Runtime is already installed.");
            return;
        }

        string tempExe = Path.Combine(Path.GetTempPath(), $"windowsdesktop-runtime-{Guid.NewGuid():N}.exe");
        try
        {
            onProgress(0, "Downloading the .NET Runtime (one-time, ~55 MB)…");
            using (var http = new HttpClient { Timeout = TimeSpan.FromMinutes(5) })
            using (var response = await http.GetAsync(DownloadUrl, HttpCompletionOption.ResponseHeadersRead))
            {
                response.EnsureSuccessStatusCode();
                long? total = response.Content.Headers.ContentLength;
                await using var input = await response.Content.ReadAsStreamAsync();
                await using var output = File.Create(tempExe);

                var buffer = new byte[81920];
                long readSoFar = 0;
                int read;
                while ((read = await input.ReadAsync(buffer)) > 0)
                {
                    await output.WriteAsync(buffer.AsMemory(0, read));
                    readSoFar += read;
                    if (total is > 0)
                    {
                        int pct = (int)(readSoFar * 90 / total.Value);
                        onProgress(Math.Clamp(pct, 0, 90), "Downloading the .NET Runtime (one-time, ~55 MB)…");
                    }
                }
            }

            onProgress(92, "Installing the .NET Runtime…");
            Logger.Info("running windowsdesktop-runtime installer silently");
            var psi = new ProcessStartInfo(tempExe, "/install /quiet /norestart")
            {
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            using var proc = Process.Start(psi)!;
            await proc.WaitForExitAsync();

            // 0 = success; 3010 = success, reboot recommended (not required for the app to run).
            if (proc.ExitCode is not (0 or 3010))
                throw new InvalidOperationException($".NET Runtime installer exited with code {proc.ExitCode}.");

            Logger.Info($".NET Runtime installer finished (exit code {proc.ExitCode})");
            onProgress(100, "The .NET Runtime is installed.");
        }
        finally
        {
            try { File.Delete(tempExe); } catch { }
        }
    }
}
