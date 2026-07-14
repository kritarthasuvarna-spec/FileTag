namespace FileTag.App;

/// <summary>
/// Failure feedback (spec: "every time, not once"). All toasts here fire on
/// every occurrence and only ever from an active hotkey attempt or a save —
/// never from passive browsing, which would spam on every click.
/// </summary>
internal sealed class ToastManager
{
    private readonly TrayIcon _tray;

    public ToastManager(TrayIcon tray) => _tray = tray;

    public void SelectSingleFile() =>
        _tray.ShowBalloon("FileTag", "Select a single file to add a comment");

    public void LocationUnsupported() =>
        _tray.ShowBalloon("FileTag", "This location isn't supported yet");

    public void SaveFailed(string reason) =>
        _tray.ShowBalloon("FileTag", $"Couldn't save comment: {reason}");

    public void DataDamaged() =>
        _tray.ShowBalloon("FileTag", "Comment data looks damaged for this file");

    public void HotkeyConflict(string combo) =>
        _tray.ShowBalloon("FileTag", $"{combo} is taken by another app — change it in Settings.");
}
