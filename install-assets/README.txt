FileTag - quick reference
==========================

WHAT IT DOES
  Attaches a personal note to any file or folder. The note is stored with
  the item itself (invisibly on local drives, as a tiny hidden companion
  file in Google Drive / OneDrive folders so it syncs across your PCs).

USING IT
  Add / edit a note ....... select a file or folder, press Shift+Alt+N
                            (remappable in Settings)
  See a note .............. just click the item in Explorer or the desktop
  Save while typing ....... Ctrl+Enter          Cancel: Esc
  Delete a note ........... Delete button on the bar; 5 seconds to Undo

TRAY MENU (right-click the tag icon)
  Settings...  |  Show tutorial  |  What's New  |  Recover Notes...  |
  Open logs folder  |  Exit

RECOVERING A DELETED NOTE
  FileTag keeps a tiny local backup (gzip, usually a few hundred bytes) of
  every note it has ever saved, specifically so an accidental delete is
  recoverable. Tray menu -> Recover Notes... lists anything backed up that
  no longer has a live comment, with a one-click Restore.

WHERE THINGS LIVE
  This folder ............. the app and its uninstaller only
  Settings ................ %APPDATA%\FileTag\settings.json
  Logs .................... %APPDATA%\FileTag\logs
  Your notes .............. inside/next to the files you tagged - never here

UNINSTALL
  Windows Settings -> Apps -> FileTag -> Uninstall (or Uninstall.exe here).
  Removes the app AND strips every note it created; your files themselves
  are never touched.

MORE
  Full illustrated guide, downloads, and updates: see the project page
  linked from "Check for updates" in the tray menu.
