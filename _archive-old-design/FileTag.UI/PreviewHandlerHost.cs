using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace FileTag.UI;

/// <summary>
/// Hosts the sidebar as an IPreviewHandler so Explorer can embed it in the Details/Preview pane.
/// The shell calls Initialize → DoPreview; we forward the file path to SidebarPanel.
/// </summary>
[ComVisible(true)]
[Guid("C4D5E6F7-A8B9-0123-1234-567890000005")]
public class PreviewHandlerHost : IPreviewHandler, IInitializeWithFile
{
    private SidebarPanel? _panel;
    private HwndSource?   _source;
    private RECT          _rect;

    // IInitializeWithFile
    public void Initialize(string filePath, uint grfMode)
    {
        _panel = new SidebarPanel();
        _panel.Load(filePath);
    }

    // IPreviewHandler
    public void SetWindow(nint hwnd, ref RECT rect)
    {
        _rect = rect;
        EnsureHwnd(hwnd);
    }

    public void SetRect(ref RECT rect)
    {
        _rect = rect;
        if (_source != null)
            SetWindowPos(_source.Handle, nint.Zero,
                rect.Left, rect.Top,
                rect.Right - rect.Left, rect.Bottom - rect.Top,
                0x0014); // SWP_NOZORDER | SWP_NOACTIVATE
    }

    public void DoPreview()
    {
        if (_source == null || _panel == null) return;
        _panel.Visibility = Visibility.Visible;
    }

    public void Unload()
    {
        _source?.Dispose();
        _source = null;
    }

    public void SetFocus() { }
    public void QueryFocus(out nint phwnd) => phwnd = nint.Zero;
    public uint TranslateAccelerator(ref MSG pmsg) => 1; // S_FALSE

    private void EnsureHwnd(nint parentHwnd)
    {
        if (_source != null || _panel == null) return;

        var p = new HwndSourceParameters("FileTagPreview")
        {
            ParentWindow   = parentHwnd,
            WindowStyle    = 0x40000000 | 0x10000000, // WS_CHILD | WS_VISIBLE
            PositionX      = _rect.Left,
            PositionY      = _rect.Top,
            Width          = _rect.Right  - _rect.Left,
            Height         = _rect.Bottom - _rect.Top,
        };
        _source = new HwndSource(p) { RootVisual = _panel };
    }

    [DllImport("user32.dll")]
    private static extern bool SetWindowPos(nint hWnd, nint hWndInsertAfter,
        int X, int Y, int cx, int cy, uint uFlags);
}

[ComImport, Guid("8895b1c6-b41f-4c1c-a562-0d564250836f"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
public interface IPreviewHandler
{
    void SetWindow(nint hwnd, ref RECT rect);
    void SetRect(ref RECT rect);
    void DoPreview();
    void Unload();
    void SetFocus();
    void QueryFocus(out nint phwnd);
    [PreserveSig] uint TranslateAccelerator(ref MSG pmsg);
}

[ComImport, Guid("b7d14566-0509-4cce-a71f-0a554233bd9b"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
public interface IInitializeWithFile
{
    void Initialize([MarshalAs(UnmanagedType.LPWStr)] string pszFilePath, uint grfMode);
}

[StructLayout(LayoutKind.Sequential)]
public struct RECT { public int Left, Top, Right, Bottom; }

[StructLayout(LayoutKind.Sequential)]
public struct MSG
{
    public nint hwnd;
    public uint message;
    public nint wParam, lParam;
    public uint time;
    public System.Drawing.Point pt;
}
