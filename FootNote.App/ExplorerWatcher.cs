using System.Collections.Concurrent;
using System.Diagnostics;
using System.Windows.Automation;

namespace FootNote.App;

/// <summary>
/// Plumbing that says "the Explorer selection may have changed — re-evaluate".
/// Two signal sources, both cheap and both funneled into one callback:
///   1. UI Automation selection events (fire system-wide; filtered to explorer.exe
///      by process id and early-exited fast, per spec).
///   2. A foreground-window WinEvent hook (window/tab switches, window closes).
/// Subscribing at the UIA root means new Explorer windows and Win11 tabs are
/// covered automatically — nothing to re-attach per window.
/// </summary>
internal sealed class ExplorerWatcher : IDisposable
{
    private readonly Action _selectionMaybeChanged;
    private readonly ConcurrentDictionary<int, bool> _pidIsExplorer = new();
    private NativeMethods.WinEventDelegate? _winEventProc; // kept alive for the hook
    private IntPtr _hook;
    private volatile bool _disposed;

    public ExplorerWatcher(Action selectionMaybeChanged)
    {
        _selectionMaybeChanged = selectionMaybeChanged;

        // UIA subscriptions must not run on the STA UI thread (can deadlock).
        var t = new Thread(SubscribeUia) { IsBackground = true, Name = "FootNote.UIA" };
        t.SetApartmentState(ApartmentState.MTA);
        t.Start();

        // WinEvent hook needs a message pump — the WPF UI thread provides one.
        _winEventProc = OnWinEvent;
        _hook = NativeMethods.SetWinEventHook(
            NativeMethods.EVENT_SYSTEM_FOREGROUND, NativeMethods.EVENT_SYSTEM_FOREGROUND,
            IntPtr.Zero, _winEventProc, 0, 0, NativeMethods.WINEVENT_OUTOFCONTEXT);
    }

    private void SubscribeUia()
    {
        try
        {
            var root = AutomationElement.RootElement;
            Automation.AddAutomationEventHandler(SelectionItemPattern.ElementSelectedEvent,
                root, TreeScope.Subtree, OnUiaEvent);
            Automation.AddAutomationEventHandler(SelectionItemPattern.ElementAddedToSelectionEvent,
                root, TreeScope.Subtree, OnUiaEvent);
            Automation.AddAutomationEventHandler(SelectionItemPattern.ElementRemovedFromSelectionEvent,
                root, TreeScope.Subtree, OnUiaEvent);
            DebugLog.Write("uia: subscribed");
        }
        catch (Exception ex) { DebugLog.Write("uia subscribe EX: " + ex); }
    }

    private void OnUiaEvent(object sender, AutomationEventArgs e)
    {
        if (_disposed) return;
        try
        {
            // Early exit for non-Explorer noise before doing anything else.
            if (sender is AutomationElement el && !IsExplorerPid(el.Current.ProcessId)) return;
            _selectionMaybeChanged();
        }
        catch { /* element vanished — ignore */ }
    }

    private void OnWinEvent(IntPtr hook, uint evt, IntPtr hwnd, int idObject, int idChild, uint thread, uint time)
    {
        if (!_disposed) _selectionMaybeChanged();
    }

    private bool IsExplorerPid(int pid)
    {
        if (_pidIsExplorer.Count > 128) _pidIsExplorer.Clear(); // don't grow forever
        return _pidIsExplorer.GetOrAdd(pid, id =>
        {
            try
            {
                using var p = Process.GetProcessById(id);
                return p.ProcessName.Equals("explorer", StringComparison.OrdinalIgnoreCase);
            }
            catch { return false; }
        });
    }

    public void Dispose()
    {
        _disposed = true;
        if (_hook != IntPtr.Zero) { NativeMethods.UnhookWinEvent(_hook); _hook = IntPtr.Zero; }
        _winEventProc = null;
        // RemoveAllEventHandlers must also run off the STA thread.
        var t = new Thread(() => { try { Automation.RemoveAllEventHandlers(); } catch { } }) { IsBackground = true };
        t.SetApartmentState(ApartmentState.MTA);
        t.Start();
    }
}
