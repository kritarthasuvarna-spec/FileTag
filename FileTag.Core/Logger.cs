namespace FileTag.Core;

/// <summary>
/// One shared logger for Setup, the tray app, and Uninstall — same format
/// everywhere: "timestamp | LEVEL | component | message". Plain text, opens
/// in Notepad, no tooling required. Never throws: logging must not be able
/// to break the thing it's observing.
///
/// App log rolls daily (or at 5 MB) into date-stamped files under
/// %APPDATA%\FileTag\logs\, trimmed to ~7 days. Setup writes install.log in
/// the same folder; Uninstall writes to %TEMP% (a log inside the folder
/// being deleted would erase its own record).
/// </summary>
public static class Logger
{
    private const long MaxSizeBytes = 5 * 1024 * 1024;
    private const int KeepDays = 7;

    public static string LogsDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "FileTag", "logs");

    public static string AppLogPath => Path.Combine(LogsDirectory, "filetag.log");
    public static string InstallLogPath => Path.Combine(LogsDirectory, "install.log");
    public static string UninstallLogPath => Path.Combine(Path.GetTempPath(), "FileTag-uninstall.log");

    private static readonly object Gate = new();
    private static string _component = "App";
    private static string? _file;

    /// <summary>Set once at process start. Null path = the rolling app log.</summary>
    public static void Init(string component, string? filePath = null)
    {
        lock (Gate)
        {
            _component = component;
            _file = filePath ?? AppLogPath;
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(_file)!);
                if (_file == AppLogPath) Rotate();
            }
            catch { }
        }
    }

    public static void Info(string message) => Write("INFO", message);
    public static void Warn(string message) => Write("WARN", message);
    public static void Error(string message) => Write("ERROR", message);

    private static void Write(string level, string message)
    {
        lock (Gate)
        {
            if (_file is null) Init(_component);
            try
            {
                File.AppendAllText(_file!,
                    $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} | {level,-5} | {_component} | {message}{Environment.NewLine}");
            }
            catch { }
        }
    }

    /// <summary>Daily/size rollover for the app log, plus 7-day cleanup.</summary>
    private static void Rotate()
    {
        try
        {
            var fi = new FileInfo(AppLogPath);
            if (fi.Exists && (fi.LastWriteTime.Date < DateTime.Now.Date || fi.Length > MaxSizeBytes))
            {
                string archived = Path.Combine(LogsDirectory,
                    $"filetag-{fi.LastWriteTime:yyyyMMdd-HHmmss}.log");
                if (!File.Exists(archived)) File.Move(AppLogPath, archived);
            }

            foreach (var old in new DirectoryInfo(LogsDirectory).GetFiles("filetag-*.log"))
            {
                if (DateTime.Now - old.LastWriteTime > TimeSpan.FromDays(KeepDays))
                    old.Delete();
            }
        }
        catch { }
    }
}
