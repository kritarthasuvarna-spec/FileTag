using System.IO;
using FileTag.Core;

namespace FileTag.App;

/// <summary>Opt-in diagnostics: set FILETAG_DEBUG=1 to log to %LocalAppData%\FileTag\debug.log.</summary>
internal static class DebugLog
{
    private static readonly bool Enabled =
        Environment.GetEnvironmentVariable("FILETAG_DEBUG") == "1";
    private static readonly object Gate = new();

    public static void Write(string message)
    {
        if (!Enabled) return;
        try
        {
            lock (Gate)
            {
                Directory.CreateDirectory(IndexStore.DataDirectory);
                File.AppendAllText(Path.Combine(IndexStore.DataDirectory, "debug.log"),
                    $"{DateTime.Now:HH:mm:ss.fff} {message}{Environment.NewLine}");
            }
        }
        catch { }
    }
}
