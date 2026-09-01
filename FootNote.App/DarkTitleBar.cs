using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace FootNote.App;

/// <summary>
/// Every FootNote window is dark-themed, but WPF draws the native title bar
/// light by default — call <see cref="Apply"/> right after InitializeComponent
/// so the chrome matches the content instead of flashing white on top of it.
/// </summary>
public static class DarkTitleBar
{
    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

    private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
    private const int DWMWA_USE_IMMERSIVE_DARK_MODE_OLD = 19; // pre-20H1 builds

    public static void Apply(Window window)
    {
        window.SourceInitialized += (_, _) =>
        {
            IntPtr hwnd = new WindowInteropHelper(window).Handle;
            int enabled = 1;
            if (DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref enabled, sizeof(int)) != 0)
                DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE_OLD, ref enabled, sizeof(int));
        };
    }
}
