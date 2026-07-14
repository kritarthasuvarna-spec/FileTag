using FileTag.Core;
using SharpShell.Attributes;
using SharpShell.SharpIconOverlayHandler;
using System.Drawing;
using System.Runtime.InteropServices;

namespace FileTag.ShellExtension;

[ComVisible(true)]
[Guid("B2C3D4E5-F6A7-8901-BCDE-F12345678901")]
public class IconOverlayHandler : SharpIconOverlayHandler
{
    private static readonly NoteRepository _repo = RepositoryLocator.Notes;

    protected override bool CanShowOverlay(string path, FILE_ATTRIBUTE attributes)
    {
        if (path.StartsWith(@"\\")) return false; // skip UNC
        try { return _repo.HasNote(FileKeyHelper.GetKey(path)); }
        catch { return false; }
    }

    protected override Icon GetOverlayIcon()
    {
        // 16x16 yellow dot
        var bmp = new Bitmap(16, 16);
        using var g = Graphics.FromImage(bmp);
        g.Clear(Color.Transparent);
        using var brush = new SolidBrush(Color.FromArgb(0xF9, 0xA8, 0x25));
        g.FillEllipse(brush, 6, 6, 10, 10);
        return Icon.FromHandle(bmp.GetHicon());
    }

    protected override int GetPriority() => 50;
}
