using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using FileTag.App.Settings;
using FileTag.Core;
using Color = System.Windows.Media.Color;
using ColorConverter = System.Windows.Media.ColorConverter;

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

    private readonly DispatcherTimer _autoHide = new();

    public OverlayBar()
    {
        InitializeComponent();
        SourceInitialized += (_, _) =>
        {
            _hwnd = new WindowInteropHelper(this).Handle;
            SetNoActivate(true);
        };
        SizeChanged += (_, _) => { if (State != BarState.Hidden) Reposition(); };

        _autoHide.Tick += (_, _) =>
        {
            // Don't hide under the user's cursor — check again in a bit.
            if (IsMouseOver) return;
            _autoHide.Stop();
            if (State == BarState.Read) HideBar();
        };

        ApplySettings();
        SettingsService.Instance.SettingsChanged += () =>
        {
            ApplySettings();
            if (State != BarState.Hidden) Reposition();
        };
    }

    /// <summary>Pulls live values from AppSettings (accent color, edge shape).</summary>
    private void ApplySettings()
    {
        var s = SettingsService.Instance.Current;
        try
        {
            Resources["AccentBrush"] = new SolidColorBrush(
                (Color)ColorConverter.ConvertFromString(s.AccentColor));
        }
        catch { /* invalid hex — keep previous accent */ }
        RootBorder.CornerRadius = s.IsBottomEdge
            ? new CornerRadius(12, 12, 0, 0)
            : new CornerRadius(0, 0, 12, 12);
        RootBorder.BorderThickness = s.IsBottomEdge
            ? new Thickness(1, 1, 1, 0)
            : new Thickness(1, 0, 1, 1);
    }

    private void RestartAutoHide()
    {
        _autoHide.Stop();
        var s = SettingsService.Instance.Current;
        if (s.StayUntilDismissed) return;
        _autoHide.Interval = TimeSpan.FromSeconds(Math.Clamp(s.AutoHideSeconds, 2, 15));
        _autoHide.Start();
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
        RestartAutoHide();
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

        _autoHide.Stop(); // an edit in progress never auto-hides
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
        _autoHide.Stop();
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

    /// <summary>Off-screen offset for the slide: positive when docked bottom, negative for top.</summary>
    private double SlideOffset()
    {
        double h = Math.Max(ActualHeight, 40);
        return SettingsService.Instance.Current.IsBottomEdge ? h : -h;
    }

    private void ShowWithSlide()
    {
        _hiding = false;
        Show();
        Reposition();
        if (!SettingsService.Instance.Current.SlideAnimation)
        {
            SlideTransform.BeginAnimation(TranslateTransform.YProperty, null);
            SlideTransform.Y = 0;
            return;
        }
        SlideTransform.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(SlideOffset(), 0,
            TimeSpan.FromMilliseconds(150)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } });
    }

    private void HideWithSlide()
    {
        if (!SettingsService.Instance.Current.SlideAnimation) { Hide(); return; }
        _hiding = true;
        var anim = new DoubleAnimation(0, SlideOffset(), TimeSpan.FromMilliseconds(150))
        { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn } };
        anim.Completed += (_, _) => { if (_hiding) { Hide(); _hiding = false; } };
        SlideTransform.BeginAnimation(TranslateTransform.YProperty, anim);
    }

    /// <summary>Dock to the configured work-area edge of the target monitor
    /// (Auto = the monitor hosting the Explorer window; or a manual pick).</summary>
    private void Reposition()
    {
        if (_hwnd == IntPtr.Zero) return;
        var settings = SettingsService.Instance.Current;

        IntPtr monitor = ResolveMonitor(settings.MonitorIndex);
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
        int y = settings.IsBottomEdge ? mi.rcWork.Bottom - barHeightPx : mi.rcWork.Top;

        NativeMethods.SetWindowPos(_hwnd, NativeMethods.HWND_TOPMOST, x, y, barWidthPx, barHeightPx,
            NativeMethods.SWP_NOACTIVATE);
    }

    private IntPtr ResolveMonitor(int monitorIndex)
    {
        if (monitorIndex >= 0)
        {
            var screens = System.Windows.Forms.Screen.AllScreens;
            if (monitorIndex < screens.Length)
            {
                var b = screens[monitorIndex].Bounds;
                return NativeMethods.MonitorFromPoint(
                    new NativeMethods.POINT { X = b.Left + b.Width / 2, Y = b.Top + b.Height / 2 },
                    NativeMethods.MONITOR_DEFAULTTONEAREST);
            }
        }
        IntPtr anchor = _explorerHwnd != IntPtr.Zero ? _explorerHwnd : _hwnd;
        return NativeMethods.MonitorFromWindow(anchor, NativeMethods.MONITOR_DEFAULTTONEAREST);
    }
}
