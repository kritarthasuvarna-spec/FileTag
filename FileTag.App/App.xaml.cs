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

        // Uninstall mode: this exe hosts the uninstall wizard (launched by the
        // Uninstall.exe stub) so the wizard UI costs no extra WPF payload.
        if (e.Args.Any(a => a.Equals("--uninstall", StringComparison.OrdinalIgnoreCase)))
        {
            Logger.Init("Uninstall", Logger.UninstallLogPath);
            UninstallRunner.KillOtherInstances();
            if (e.Args.Any(a => a.Equals("/S", StringComparison.OrdinalIgnoreCase)))
            {
                var runner = new UninstallRunner();
                runner.Run();
                runner.ScheduleInstallFolderDeletion();
                Shutdown(0);
                return;
            }
            ShutdownMode = ShutdownMode.OnLastWindowClose;
            new UninstallWindow().Show();
            return;
        }

        _mutex = new Mutex(true, "FileTag.App.SingleInstance", out bool createdNew);
        if (!createdNew)
        {
            // Dying silently here reads as "the app is broken" — say something.
            System.Windows.MessageBox.Show(
                "FileTag is already running — look for the tag icon in the system tray.\n\n" +
                "To run this copy instead, exit the running one first (tray icon → Exit).",
                "FileTag", MessageBoxButton.OK, MessageBoxImage.Information);
            Shutdown();
            return;
        }

        Logger.Init("App");
        Logger.Info($"FileTag {typeof(App).Assembly.GetName().Version?.ToString(3)} starting from {InstallHelper.ExePath}");
        UninstallRunner.CancelPendingDeletionHere();
        ListenForExitRequests(); // lets Setup update in place with a graceful stop

        _index = new IndexStore();
        // Detect a new/changed install location before re-registering over it,
        // so the user gets visible feedback that "running the exe" worked.
        string? previousLocation = InstallHelper.ReadRegisteredLocation();
        string currentLocation = Path.GetDirectoryName(InstallHelper.ExePath)!;
        InstallHelper.RegisterAll(_index.StartWithWindows);
        SettingsService.Instance.Save(); // materialize defaults on first run

        _bar = new OverlayBar();
        _bar.SaveRequested += OnSaveRequested;
        _bar.DeleteConfirmed += OnDeleteConfirmed;

        _tray = new TrayIcon(_index, ExitApp, OpenSettings, OpenTutorial);
        _toasts = new ToastManager(_tray);

        _hotkey = new HotkeyManager();
        _hotkey.Pressed += OnHotkey;
        ApplyHotkeyFromSettings(warnOnFailure: true);
        DebugLog.Write($"startup: hotkey registered={_hotkey.Registered}");
        Logger.Info($"hotkey {SettingsService.Instance.Current.HotkeyDisplay} registered={_hotkey.Registered}");
        Logger.Info($"cloud sync roots: {string.Join(" | ", CloudFolderDetector.GetRoots())}");

        SettingsService.Instance.SettingsChanged += () => ApplyHotkeyFromSettings(warnOnFailure: false);

        // Selection events arrive in bursts from any thread; collapse them into
        // one evaluation ~120ms after the burst ends.
        _debounce = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromMilliseconds(120) };
        _debounce.Tick += (_, _) => { _debounce.Stop(); Evaluate(); };
        _watcher = new ExplorerWatcher(() => Dispatcher.BeginInvoke(() => { _debounce.Stop(); _debounce.Start(); }));

        if (!_index.FirstRunShown)
        {
            // A balloon is too easy to miss — show the interactive tutorial.
            OpenTutorial();
            _index.FirstRunShown = true;
        }
        else if (!string.Equals(previousLocation, currentLocation, StringComparison.OrdinalIgnoreCase))
        {
            // Not a first run, but the app moved (fresh extract, new folder):
            // without this, launching the exe looks like "nothing happened".
            _tray.ShowBalloon("FileTag is running",
                $"Now installed at {currentLocation}. Look for the tag icon in the system tray.");
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

    private TutorialWindow? _tutorial;

    private void OpenTutorial()
    {
        if (_tutorial is { IsLoaded: true }) { _tutorial.Activate(); return; }
        _tutorial = new TutorialWindow();
        _tutorial.Show();
        _tutorial.Activate();
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
            IntPtr fg = NativeMethods.GetForegroundWindow();
            if (fg == _bar.Handle) return; // user is interacting with the bar itself

            string cls = NativeMethods.GetWindowClass(fg);
            List<string>? sel = null;
            if (cls == "CabinetWClass") sel = ShellSelection.GetSelectedPaths(fg);
            else if (ShellSelection.IsDesktopClass(cls)) sel = ShellSelection.GetDesktopSelectedPaths();

            if (_bar.IsEditing)
            {
                // Unsaved typing is protected; an untouched editor is abandoned
                // once the selection moves to a different item.
                bool sameItem = sel is { Count: 1 }
                    && string.Equals(sel[0], _bar.CurrentPath, StringComparison.OrdinalIgnoreCase);
                if (_bar.IsEditingDirty || sameItem || sel is null) return;
            }

            if (sel is not null)
            {
                DebugLog.Write($"evaluate: fg={fg} cls={cls} sel=[{string.Join("; ", sel)}]");
                // Exactly one file or folder (multi-select is ambiguous)
                if (sel.Count == 1 && IsFileOrFolder(sel[0])
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

            if (sel.Count > 1) { _toasts.SelectSingleFile(sel.Count); return; } // multi-select is ambiguous

            string? path = sel.Count == 1 ? sel[0] : null;
            if (path is not null && SidecarHelper.IsSidecar(path)) return;    // never comment a sidecar
            if (path is not null && !IsFileOrFolder(path))
            {
                // Virtual/unresolvable item — log what it was and keep the
                // hotkey alive via the clipboard/picker chain, not a dead end.
                Logger.Warn($"hotkey: selection not resolvable as file/folder: \"{path}\"");
                path = null;
            }

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

            if (!IsFileOrFolder(path) || SidecarHelper.IsSidecar(path)) return;

            CancelPendingDeleteFor(path); // re-editing revives a pending delete

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
            _tutorial?.HotkeyVerified(); // completes the tutorial's verified step
        }
        catch (Exception ex) { DebugLog.Write("hotkey EX: " + ex); }
    }

    private static bool IsFileOrFolder(string path) =>
        File.Exists(path) || Directory.Exists(path);

    /// <summary>Exactly one existing file/folder on the clipboard (CF_HDROP), else null.</summary>
    private static string? TryClipboardFile()
    {
        try
        {
            if (!System.Windows.Clipboard.ContainsFileDropList()) return null;
            var files = System.Windows.Clipboard.GetFileDropList();
            if (files.Count == 1 && files[0] is string p && IsFileOrFolder(p)) return p;
        }
        catch { /* clipboard is flaky by nature */ }
        return null;
    }

    // ---- delete with undo grace period -------------------------------------
    // Confirming Delete hides the comment immediately, but the underlying
    // stream/sidecar deletion is held ~5s; Undo (the toast) just cancels it —
    // nothing has left the disk yet.
    private DispatcherTimer? _undoTimer;
    private string? _pendingDeletePath;

    private void OnDeleteConfirmed(string path)
    {
        CommitPendingDelete(); // a previous pending delete commits first
        _pendingDeletePath = path;
        _undoTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
        _undoTimer.Tick += (_, _) => { _undoTimer!.Stop(); CommitPendingDelete(); };
        _undoTimer.Start();
        _bar.HideBar();
        _tray.ShowBalloon("Comment deleted", "Click here to Undo", onClick: UndoPendingDelete);
        Logger.Info($"delete pending (5s undo): {path}");
    }

    private void CommitPendingDelete()
    {
        _undoTimer?.Stop();
        if (_pendingDeletePath is null) return;
        string p = _pendingDeletePath;
        _pendingDeletePath = null;
        try
        {
            StorageRouter.Delete(p);
            _index.RemovePath(p);
            Logger.Info($"delete committed: {p}");
        }
        catch (Exception ex) { Logger.Error($"delete failed: {p} — {ex.Message}"); }
    }

    private void UndoPendingDelete()
    {
        Dispatcher.BeginInvoke(() =>
        {
            if (_pendingDeletePath is null) return; // window expired
            Logger.Info($"delete undone: {_pendingDeletePath}");
            _undoTimer?.Stop();
            _pendingDeletePath = null;
            _tray.ShowBalloon("FileTag", "Comment restored.");
        });
    }

    private void CancelPendingDeleteFor(string path)
    {
        if (string.Equals(_pendingDeletePath, path, StringComparison.OrdinalIgnoreCase))
        {
            _undoTimer?.Stop();
            _pendingDeletePath = null;
            Logger.Info($"pending delete cancelled by new activity: {path}");
        }
    }

    private void OnSaveRequested(string path, string text)
    {
        try
        {
            CancelPendingDeleteFor(path); // saving over a pending delete keeps the new comment
            StorageRouter.Save(path, text); // empty text deletes the comment
            if (string.IsNullOrWhiteSpace(text))
            {
                _index.RemovePath(path);
                Logger.Info($"comment removed: {path}");
                _bar.CompleteSave(null);
            }
            else
            {
                _index.AddPath(path);
                Logger.Info($"comment saved ({StorageRouter.RouteFor(path).GetType().Name}): {path}");
                _bar.CompleteSave(StorageRouter.ReadLatest(path));
            }
        }
        catch (Exception ex)
        {
            _toasts.SaveFailed(ex.Message);
        }
    }

    public const string ExitEventName = "FileTag.App.ExitRequest";
    private EventWaitHandle? _exitEvent;

    private void ListenForExitRequests()
    {
        try
        {
            _exitEvent = new EventWaitHandle(false, EventResetMode.AutoReset, ExitEventName);
            var t = new Thread(() =>
            {
                _exitEvent.WaitOne();
                Logger.Info("graceful exit requested (updater)");
                Dispatcher.BeginInvoke(ExitApp);
            }) { IsBackground = true, Name = "FileTag.ExitListener" };
            t.Start();
        }
        catch { /* updater falls back to killing the process */ }
    }

    private void ExitApp()
    {
        CommitPendingDelete(); // don't lose an intended delete on exit
        Logger.Info("exit via tray menu");
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
