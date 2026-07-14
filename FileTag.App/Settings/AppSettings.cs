using System.Text.Json.Serialization;

namespace FileTag.App.Settings;

/// <summary>
/// POCO matching %APPDATA%\FileTag\settings.json. SchemaVersion exists so
/// future fields can migrate safely instead of resetting the file.
/// </summary>
public sealed class AppSettings
{
    public int SchemaVersion { get; set; } = 1;

    // --- Position -----------------------------------------------------------
    /// <summary>"Top" or "Bottom".</summary>
    public string ScreenEdge { get; set; } = "Bottom";

    /// <summary>-1 = Auto (same monitor as the active Explorer window); otherwise a display index.</summary>
    public int MonitorIndex { get; set; } = -1;

    // --- Appearance -----------------------------------------------------------
    /// <summary>Hex color for accent elements (Save button etc.).</summary>
    public string AccentColor { get; set; } = "#4F8EF7";

    // --- Behavior --------------------------------------------------------------
    /// <summary>Seconds before the read-mode bar hides itself (2–15).</summary>
    public int AutoHideSeconds { get; set; } = 7;

    /// <summary>True = never auto-hide; bar stays until dismissed or deselected.</summary>
    public bool StayUntilDismissed { get; set; }

    public bool SlideAnimation { get; set; } = true;

    // --- Hotkey -----------------------------------------------------------------
    public bool HotkeyCtrl { get; set; }
    public bool HotkeyShift { get; set; } = true;
    public bool HotkeyAlt { get; set; } = true;
    /// <summary>"A"–"Z", "0"–"9", or "F1"–"F24".</summary>
    public string HotkeyKey { get; set; } = "N";

    [JsonIgnore]
    public string HotkeyDisplay
    {
        get
        {
            var parts = new List<string>(4);
            if (HotkeyCtrl) parts.Add("Ctrl");
            if (HotkeyShift) parts.Add("Shift");
            if (HotkeyAlt) parts.Add("Alt");
            parts.Add(HotkeyKey);
            return string.Join("+", parts);
        }
    }

    [JsonIgnore]
    public bool IsBottomEdge => !ScreenEdge.Equals("Top", StringComparison.OrdinalIgnoreCase);
}
