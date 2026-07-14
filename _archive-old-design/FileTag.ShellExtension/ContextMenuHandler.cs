using FileTag.Core;
using SharpShell.Attributes;
using SharpShell.SharpContextMenu;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace FileTag.ShellExtension;

[ComVisible(true)]
[Guid("A1B2C3D4-E5F6-7890-ABCD-EF1234567890")]
[COMServerAssociation(AssociationType.AllFiles)]
[COMServerAssociation(AssociationType.Directory)]
public class ContextMenuHandler : SharpContextMenu
{
    private static readonly NoteRepository _repo = RepositoryLocator.Notes;

    protected override bool CanShowMenu()
    {
        return SelectedItemPaths.Count() == 1;
    }

    protected override ContextMenuStrip CreateMenu()
    {
        var path  = SelectedItemPaths.First();
        var key   = FileKeyHelper.GetKey(path);
        var hasNote = _repo.HasNote(key);

        var menu = new ContextMenuStrip();
        var item = new ToolStripMenuItem(hasNote ? "Edit Comment" : "Add Comment");
        item.Click += (_, _) => OpenSidebar(path, hasNote);
        menu.Items.Add(item);
        return menu;
    }

    private static void OpenSidebar(string path, bool hasNote)
    {
        // Launch FileTag.UI passing the path; UI reads mode from DB state
        var exe = Path.Combine(
            Path.GetDirectoryName(typeof(ContextMenuHandler).Assembly.Location)!,
            "..", "FileTag.UI", "FileTag.UI.exe");
        try
        {
            System.Diagnostics.Process.Start(exe, $"\"{path}\"");
        }
        catch { /* UI not found in dev — no-op */ }
    }
}
