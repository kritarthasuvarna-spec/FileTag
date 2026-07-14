using System.IO;
using System.Windows;
using System.Windows.Threading;
using FileTag.App.Settings;
using FileTag.Core;

namespace FileTag.App;

/// <summary>
/// Coordinator. Owns all components and the one evaluation routine that decides
/// what the overlay bar shows for the current Explorer selection.
/// </summary>
public partial class App : System.Windows.Application
{
    private Mutex? _mutex;
    private IndexStore _index = null!;
    private OverlayBar _bar = null!;
    private TrayIcon _tray = null!;
    private ToastManager _toasts = null!;
    private HotkeyManager _hotkey = null!;
    private ExplorerWatcher _watcher = null!;
    private DispatcherTimer _debounce = null!;
    private SettingsWindow? _settingsWindow;
    private string _appliedHotkey = "";

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _mutex = new Mutex(true, "FileTag.App.SingleInstance", out bool createdNew);
        if (!createdNew) { Shutdown(); return; }

        _index = new IndexStore();
        InstallHelper.RegisterAll(_index.StartWithWindows);
        SettingsService.Instance.Save(); // materialize defaults on first run

        _bar = new OverlayBar();
        _bar.SaveRequested += OnSaveRequested;

        _tray = new TrayIcon(_index, ExitApp, OpenSettings);
        _toasts = new ToastManager(_tray);

        _hotkey = new HotkeyManager();
        _hotkey.Pressed += OnHotkey;
        ApplyHotkeyFromSettings(warnOnFailure: true);
        DebugLog.Write($"startup: hotkey registered={_hotkey.Registered}");

        SettingsService.Instance.SettingsChanged += () => ApplyHotkeyFromSettings(warnOnFailure: false);

        // Selection events arrive in bursts from any thread; collapse them into
        // one evaluation ~120ms after the burst ends.
        _debounce = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromMilliseconds(120) };
        _debounce.Tick += (_, _) => { _debounce.Stop(); Evaluate(); };
        _watcher = new ExplorerWatcher(() => Dispatcher.BeginInvoke(() => { _debounce.Stop(); _debounce.Start(); }));

        if (!_index.FirstRunShown)
        {
            _tray.ShowBalloon("FileTag is running",
                $"Select a file and press {SettingsService.Instance.Current.HotkeyDisplay} to add a comment.");
            _index.FirstRunShown = true;
        }

        _ = UpdateChecker.CheckAsync(_tray);

        if (Environment.GetEnvironmentVariable("FILETAG_OPEN_SETTINGS") == "1")
            OpenSettings(); // debug/test aid
    }

    private void ApplyHotkeyFromSettings(bool warnOnFailure)
    {
        var s = SettingsService.Instance.Current;
        if (s.HotkeyDisplay == _appliedHotkey && _hotkey.Registered) return;
        bool ok = _hotkey.Apply(s.HotkeyCtrl, s.HotkeyShift, s.HotkeyAlt, s.HotkeyKey);
        if (ok) _appliedHotkey = s.HotkeyDisplay;
        else if (warnOnFailure) _toasts.HotkeyConflict(s.HotkeyDisplay);
    }

    private void OpenSettings()
    {
        if (_settingsWindow is { IsLoaded: true })
        {
            _settingsWindow.Activate();
            return;
        }
        _settingsWindow = new SettingsWindow(new SettingsViewModel(_hotkey.Probe));
        _settingsWindow.Show();
        _settingsWindow.Activate();
    }

    /// <summary>Decide what the bar should show for the current foreground Explorer selection.</summary>
    private void Evaluate()
    {
        try
        {
            if (_bar.IsEditing) return; // never yank an in-progress edit

            IntPtr fg = NativeMethods.GetForegroundWindow();
            if (fg == _bar.Handle) return; // user is interacting with the bar itself

            string cls = NativeMethods.GetWindowClass(fg);
            List<string>? sel = null;
            if (cls == "CabinetWClass") sel = ShellSelection.GetSelectedPaths(fg);
            else if (ShellSelection.IsDesktopClass(cls)) sel = ShellSelection.GetDesktopSelectedPaths();

            if (sel is not null)
            {
                DebugLog.Write($"evaluate: fg={fg} cls={cls} sel=[{string.Join("; ", sel)}]");
                // Exactly one *file* (multi-select is ambiguous; folders are out of scope)
                if (sel.Count == 1 && File.Exists(sel[0])
                    && !SidecarHelper.IsSidecar(sel[0]) && StorageRouter.HasComment(sel[0]))
                {
                    var note = StorageRouter.ReadLatest(sel[0]);
                    DebugLog.Write($"evaluate: note={(note is null ? "null" : "ok")} -> ShowRead");
                    if (note is not null)
                    {
                        _index.AddPath(sel[0]); // keep the cache warm as a side effect
                        _bar.ShowRead(sel[0], note, fg);
                        return;
                    }
                }
            }
            _bar.HideBar();
        }
        catch (Exception ex) { DebugLog.Write("evaluate EX: " + ex); }
    }

    private void OnHotkey()
    {
        try
        {
            IntPtr fg = NativeMethods.GetForegroundWindow();
            string cls = NativeMethods.GetWindowClass(fg);
            DebugLog.Write($"hotkey: fg={fg} cls={cls}");

            // Allow the hotkey while the bar itself has focus (re-enter edit).
            if (fg == _bar.Handle && _bar.CurrentPath is not null)
            {
                _bar.ShowEdit(_bar.CurrentPath, StorageRouter.ReadLatest(_bar.CurrentPath), IntPtr.Zero);
                return;
            }

            // 1. Foreground selection: Explorer window or the desktop.
            List<string> sel = new();
            IntPtr anchor = IntPtr.Zero;
            if (cls == "CabinetWClass") { sel = ShellSelection.GetSelectedPaths(fg); anchor = fg; }
            else if (ShellSelection.IsDesktopClass(cls)) { sel = ShellSelection.GetDesktopSelectedPaths(); anchor = fg; }
            DebugLog.Write($"hotkey: sel=[{string.Join("; ", sel)}]");

            if (sel.Count > 1) { _toasts.SelectSingleFile(); return; }        // multi-select is ambiguous
            if (sel.Count == 1 && !File.Exists(sel[0])) { _toasts.SelectSingleFile(); return; } // folder/virtual item
            if (sel.Count == 1 && SidecarHelper.IsSidecar(sel[0])) return;   // never comment a sidecar

            string? path = sel.Count == 1 ? sel[0] : null;

            // 2. Nothing selected anywhere → a file copied to the clipboard counts.
            path ??= TryClipboardFile();

            // 3. Last resort: never a dead key — let the user pick any file.
            if (path is null)
            {
                var dlg = new Microsoft.Win32.OpenFileDialog
                {
                    Title = "FileTag — choose a file to comment",
                    CheckFileExists = true,
                };
                if (dlg.ShowDialog() != true) return;
                path = dlg.FileName;
            }

            if (!File.Exists(path) || SidecarHelper.IsSidecar(path)) return;

            // Existing comment that can't be parsed/read → warn, don't overwrite blindly.
            var existing = StorageRouter.ReadLatest(path);
            if (existing is null && StorageRouter.HasComment(path))
            {
                _toasts.DataDamaged();
                return;
            }

            // Any drive works now: NTFS gets an ADS stream, everything else
            // (FAT32 sticks, cloud-sync folders) gets a hidden sidecar file.
            _bar.ShowEdit(path, existing, anchor);
        }
        catch (Exception ex) { DebugLog.Write("hotkey EX: " + ex); }
    }

    /// <summary>Exactly one existing file on the clipboard (CF_HDROP), else null.</summary>
    private static string? TryClipboardFile()
    {
        try
        {
            if (!System.Windows.Clipboard.ContainsFileDropList()) return null;
            var files = System.Windows.Clipboard.GetFileDropList();
            if (files.Count == 1 && File.Exists(files[0])) return files[0];
        }
        catch { /* clipboard is flaky by nature */ }
        return null;
    }

    private void OnSaveRequested(string path, string text)
    {
        try
        {
            StorageRouter.Save(path, text); // empty text deletes the comment
            if (string.IsNullOrWhiteSpace(text))
            {
                _index.RemovePath(path);
                _bar.CompleteSave(null);
            }
            else
            {
                _index.AddPath(path);
                _bar.CompleteSave(StorageRouter.ReadLatest(path));
            }
        }
        catch (Exception ex)
        {
            _toasts.SaveFailed(ex.Message);
        }
    }

    private void ExitApp()
    {
        _watcher?.Dispose();
        _hotkey?.Dispose();
        _tray?.Dispose();
        Shutdown();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _mutex?.Dispose();
        base.OnExit(e);
    }
}
