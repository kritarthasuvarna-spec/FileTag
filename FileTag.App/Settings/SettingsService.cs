using System.IO;
using System.Text.Json;
using System.Windows.Threading;

namespace FileTag.App.Settings;

/// <summary>
/// Singleton owner of <see cref="AppSettings"/>. Live-apply model: every
/// change raises <see cref="SettingsChanged"/> immediately (components apply
/// it on the spot) while disk writes are debounced ~300ms so dragging a
/// slider doesn't hammer I/O. There is no Save/Cancel — like Windows Settings.
/// </summary>
public sealed class SettingsService
{
    public static SettingsService Instance { get; } = new();

    public static string SettingsPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "FileTag", "settings.json");

    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = true };

    public AppSettings Current { get; private set; }

    /// <summary>Raised on the UI thread after any setting changes.</summary>
    public event Action? SettingsChanged;

    private DispatcherTimer? _saveDebounce;

    private SettingsService()
    {
        Current = Load();
    }

    private static AppSettings Load()
    {
        try
        {
            if (File.Exists(SettingsPath))
            {
                var s = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(SettingsPath), JsonOpts);
                if (s is not null)
                {
                    // schemaVersion 1 is current; future migrations hook in here.
                    s.AutoHideSeconds = Math.Clamp(s.AutoHideSeconds, 2, 15);
                    return s;
                }
            }
        }
        catch { /* corrupt settings are not worth crashing over */ }
        return new AppSettings();
    }

    /// <summary>Call after mutating <see cref="Current"/>: applies live, saves debounced.</summary>
    public void NotifyChanged()
    {
        SettingsChanged?.Invoke();

        _saveDebounce ??= CreateDebounce();
        _saveDebounce.Stop();
        _saveDebounce.Start();
    }

    public void ResetToDefaults()
    {
        Current = new AppSettings();
        NotifyChanged();
    }

    private DispatcherTimer CreateDebounce()
    {
        var t = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300) };
        t.Tick += (_, _) => { t.Stop(); Save(); };
        return t;
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
            File.WriteAllText(SettingsPath, JsonSerializer.Serialize(Current, JsonOpts));
        }
        catch { /* read-only profile etc. — settings still apply for this session */ }
    }
}
