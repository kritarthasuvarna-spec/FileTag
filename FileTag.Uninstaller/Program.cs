using System.Diagnostics;
using System.Runtime.InteropServices;

namespace FileTag.Uninstaller;

/// <summary>
/// Tiny stub: Apps &amp; Features points here, but the actual uninstall wizard
/// (Confirm → Progress → Finish) lives inside FileTag.App.exe --uninstall so
/// the wizard UI doesn't require shipping a second WPF runtime (~80 MB).
/// Arguments (e.g. /S for automated testing) are passed through.
/// </summary>
internal static class Program
{
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int MessageBoxW(IntPtr hWnd, string text, string caption, uint type);

    [STAThread]
    private static int Main(string[] args)
    {
        string appExe = Path.Combine(AppContext.BaseDirectory, "FileTag.App.exe");
        if (!File.Exists(appExe))
        {
            MessageBoxW(IntPtr.Zero,
                "FileTag.App.exe was not found next to the uninstaller.\n" +
                "Delete the FileTag folder manually to remove the app.",
                "Uninstall FileTag", 0x10 /* MB_ICONERROR */);
            return 1;
        }

        var psi = new ProcessStartInfo(appExe) { UseShellExecute = false };
        psi.ArgumentList.Add("--uninstall");
        foreach (string a in args) psi.ArgumentList.Add(a);
        Process.Start(psi);
        return 0;
    }
}
