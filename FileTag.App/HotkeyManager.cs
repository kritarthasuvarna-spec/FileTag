using System.Windows.Interop;

namespace FileTag.App;

/// <summary>Global Shift+Alt+N via RegisterHotKey on a message-only window.</summary>
internal sealed class HotkeyManager : IDisposable
{
    private const int HotkeyId = 0xF11E;
    private const uint VK_N = 0x4E;

    private readonly HwndSource _source;
    public event Action? Pressed;
    public bool Registered { get; }

    public HotkeyManager()
    {
        var p = new HwndSourceParameters("FileTagHotkeyWindow")
        {
            Width = 0,
            Height = 0,
            ParentWindow = new IntPtr(-3), // HWND_MESSAGE
        };
        _source = new HwndSource(p);
        _source.AddHook(WndProc);
        Registered = NativeMethods.RegisterHotKey(_source.Handle, HotkeyId,
            NativeMethods.MOD_SHIFT | NativeMethods.MOD_ALT | NativeMethods.MOD_NOREPEAT, VK_N);
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == NativeMethods.WM_HOTKEY && wParam.ToInt32() == HotkeyId)
        {
            Pressed?.Invoke();
            handled = true;
        }
        return IntPtr.Zero;
    }

    public void Dispose()
    {
        if (Registered) NativeMethods.UnregisterHotKey(_source.Handle, HotkeyId);
        _source.Dispose();
    }
}
