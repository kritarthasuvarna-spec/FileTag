using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using FileTag.Core;

namespace FileTag.App;

/// <summary>
/// The bottom overlay bar. Three states: Hidden / Read / Edit.
/// Read mode never steals focus (WS_EX_NOACTIVATE); Edit mode does, deliberately.
/// Docked to the bottom work-area edge of whichever monitor hosts the Explorer
/// window that triggered it, positioned in raw pixels via SetWindowPos so
/// per-monitor DPI can't skew the math.
/// </summary>
public partial class OverlayBar : Window
{
    public enum BarState { Hidden, Read, Edit }

    public BarState State { get; private set; } = BarState.Hidden;
    public bool IsEditing => State == BarState.Edit;
    public string? CurrentPath { get; private set; }

    /// <summary>Raised when the user saves; the app persists and then calls CompleteSave.</summary>
    public event Action<string, string>? SaveRequested;

    private Note? _currentNote;
    private IntPtr _hwnd;
    private IntPtr _explorerHwnd;
    private bool _hiding;

    public OverlayBar()
    {
        InitializeComponent();
        SourceInitialized += (_, _) =>
        {
            _hwnd = new WindowInteropHelper(this).Handle;
            SetNoActivate(true);
        };
        SizeChanged += (_, _) => { if (State != BarState.Hidden) Reposition(); };
    }

    public IntPtr Handle => _hwnd;

    // ---- public state transitions -----------------------------------------

    public void ShowRead(string path, Note note, IntPtr explorerHwnd)
    {
        _explorerHwnd = explorerHwnd;
        // Already showing this file — just refresh content, no re-animation.
        bool alreadyVisible = State == BarState.Read && IsVisible;
        bool samePath = string.Equals(CurrentPath, path, StringComparison.OrdinalIgnoreCase);

        CurrentPath = path;
        _currentNote = note;
        State = BarState.Read;

        FileNameText.Text = Path.GetFileName(path);
        CommentText.Text = note.Text;
        TimestampText.Text = "edited " + note.ModifiedUtc.ToLocalTime().ToString("d MMM yyyy, HH:mm");
        CommentText.Visibility = Visibility.Visible;
        EditPanel.Visibility = Visibility.Collapsed;
        EditButton.Visibility = Visibility.Visible;

        SetNoActivate(true);
        if (!alreadyVisible || !samePath) ShowWithSlide();
        else Reposition();
    }

    public void ShowEdit(string path, Note? existing, IntPtr explorerHwnd)
    {
        _explorerHwnd = explorerHwnd;
        CurrentPath = path;
        _currentNote = existing;
        State = BarState.Edit;

        FileNameText.Text = Path.GetFileName(path);
        TimestampText.Text = existing is null ? "new comment"
            : "edited " + existing.ModifiedUtc.ToLocalTime().ToString("d MMM yyyy, HH:mm");
        CommentText.Visibility = Visibility.Collapsed;
        EditPanel.Visibility = Visibility.Visible;
        EditButton.Visibility = Visibility.Collapsed;

        EditBox.Text = existing?.Text ?? "";
        EditBox.CaretIndex = EditBox.Text.Length; // cursor at end, per spec
        UpdateCounter();

        SetNoActivate(false); // edit mode legitimately takes focus
        ShowWithSlide();
        Activate();
        // The foreground lock can deny Activate (e.g. hotkey fired over a
        // stubborn fullscreen app). Attach to the foreground thread's input
        // queue, which makes SetForegroundWindow succeed.
        if (NativeMethods.GetForegroundWindow() != _hwnd)
        {
            IntPtr fg = NativeMethods.GetForegroundWindow();
            uint fgThread = NativeMethods.GetWindowThreadProcessId(fg, out _);
            uint cur = NativeMethods.GetCurrentThreadId();
            if (NativeMethods.AttachThreadInput(cur, fgThread, true))
            {
                NativeMethods.SetForegroundWindow(_hwnd);
                NativeMethods.BringWindowToTop(_hwnd);
                NativeMethods.AttachThreadInput(cur, fgThread, false);
            }
        }
        EditBox.Focus();
    }

    public void HideBar()
    {
        if (State == BarState.Hidden || _hiding) return;
        State = BarState.Hidden;
        CurrentPath = null;
        _currentNote = null;
        HideWithSlide();
    }

    /// <summary>Called by the app after a save was persisted.</summary>
    public void CompleteSave(Note? savedNote)
    {
        ReturnFocusToExplorer();
        if (savedNote is null || CurrentPath is null) { HideBar(); return; }
        ShowRead(CurrentPath, savedNote, _explorerHwnd);
    }

    // ---- UI handlers -------------------------------------------------------

    private void EditButton_Click(object sender, RoutedEventArgs e)
    {
        if (CurrentPath is not null) ShowEdit(CurrentPath, _currentNote, _explorerHwnd);
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e) => HideBar();

    private void SaveButton_Click(object sender, RoutedEventArgs e) => DoSave();

    private void DiscardButton_Click(object sender, RoutedEventArgs e) => DoDiscard();

    private void EditBox_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == Key.Escape) { DoDiscard(); e.Handled = true; }
        else if (e.Key == Key.Enter && Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
        {
            DoSave(); e.Handled = true;
        }
    }

    private void EditBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        => UpdateCounter();

    private void UpdateCounter()
    {
        int n = EditBox.Text.Length;
        CharCounter.Text = $"{n}/{StorageRouter.MaxCommentLength}";
        CharCounter.Foreground = n >= 450
            ? new SolidColorBrush(System.Windows.Media.Color.FromRgb(0xF2, 0x6B, 0x6B))
            : new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x9B, 0x9B, 0xA8));
    }

    private void DoSave()
    {
        if (CurrentPath is null) return;
        SaveRequested?.Invoke(CurrentPath, EditBox.Text.Trim());
    }

    private void DoDiscard()
    {
        ReturnFocusToExplorer();
        if (_currentNote is not null && CurrentPath is not null)
            ShowRead(CurrentPath, _currentNote, _explorerHwnd);
        else
            HideBar();
    }

    private void ReturnFocusToExplorer()
    {
        SetNoActivate(true);
        if (_explorerHwnd != IntPtr.Zero) NativeMethods.SetForegroundWindow(_explorerHwnd);
    }

    // ---- window mechanics ----------------------------------------------------

    private void SetNoActivate(bool on)
    {
        if (_hwnd == IntPtr.Zero) return;
        int ex = NativeMethods.GetWindowLong(_hwnd, NativeMethods.GWL_EXSTYLE);
        ex |= NativeMethods.WS_EX_TOOLWINDOW;
        ex = on ? ex | NativeMethods.WS_EX_NOACTIVATE : ex & ~NativeMethods.WS_EX_NOACTIVATE;
        NativeMethods.SetWindowLong(_hwnd, NativeMethods.GWL_EXSTYLE, ex);
    }

    private void ShowWithSlide()
    {
        _hiding = false;
        Show();
        Reposition();
        double from = Math.Max(ActualHeight, 40);
        SlideTransform.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(from, 0,
            TimeSpan.FromMilliseconds(150)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } });
    }

    private void HideWithSlide()
    {
        _hiding = true;
        double to = Math.Max(ActualHeight, 40);
        var anim = new DoubleAnimation(0, to, TimeSpan.FromMilliseconds(150))
        { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn } };
        anim.Completed += (_, _) => { if (_hiding) { Hide(); _hiding = false; } };
        SlideTransform.BeginAnimation(TranslateTransform.YProperty, anim);
    }

    /// <summary>Dock to the bottom work-area edge of the monitor hosting the Explorer window.</summary>
    private void Reposition()
    {
        if (_hwnd == IntPtr.Zero) return;

        IntPtr anchor = _explorerHwnd != IntPtr.Zero ? _explorerHwnd : _hwnd;
        IntPtr monitor = NativeMethods.MonitorFromWindow(anchor, NativeMethods.MONITOR_DEFAULTTONEAREST);
        var mi = new NativeMethods.MONITORINFO { cbSize = System.Runtime.InteropServices.Marshal.SizeOf<NativeMethods.MONITORINFO>() };
        if (!NativeMethods.GetMonitorInfo(monitor, ref mi)) return;

        double scale = 1.0;
        if (NativeMethods.GetDpiForMonitor(monitor, 0 /* MDT_EFFECTIVE_DPI */, out uint dpiX, out _) == 0)
            scale = dpiX / 96.0;

        int workWidthPx = mi.rcWork.Right - mi.rcWork.Left;
        int barWidthPx = (int)(Math.Min(900, (workWidthPx / scale) * 0.75) * scale);
        int barHeightPx = (int)Math.Ceiling(ActualHeight * scale);
        if (barHeightPx <= 0) barHeightPx = (int)(64 * scale);

        int x = mi.rcWork.Left + (workWidthPx - barWidthPx) / 2;
        int y = mi.rcWork.Bottom - barHeightPx;

        NativeMethods.SetWindowPos(_hwnd, NativeMethods.HWND_TOPMOST, x, y, barWidthPx, barHeightPx,
            NativeMethods.SWP_NOACTIVATE);
    }
}
