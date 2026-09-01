# 📝 FootNote

**Attach a personal note to any file on your PC — stored invisibly with the
file itself, synced across your PCs when the file lives in a Google Drive or
OneDrive folder.**

Ever looked at a file six months later and wondered *"why did I keep this?"* —
FootNote lets you answer that, right where the file lives. Select a file, press
a hotkey, type a note. Click the file later and the note slides up from the
bottom of your screen.

- **No central database.** On local NTFS drives the note is stored *inside*
  the file (alternate data stream), so it follows the file through renames
  and moves — automatically.
- **Works on any drive, syncs across devices.** In Google Drive / OneDrive
  folders and on FAT32/exFAT USB sticks, the note is stored as a tiny hidden
  companion file — which your sync engine uploads like any other file, so
  the note appears on your other PCs too. FootNote picks the right storage
  per file; you never have to think about it.
- **No clutter.** The file's content, size shown in apps, and modified date
  are untouched. Every app opens the file exactly as before.
- **Nothing ever overwritten.** Notes keep a small internal version history
  (last 20 edits), so a sync conflict or accidental edit is recoverable.
- **No admin rights, no installer wizard, no background bloat** — one tray
  icon, a few MB of RAM.

---

## Install

**Recommended:** download **`FootNote-Setup-vX.Y.Z.exe`** and run it — a short
wizard (Welcome → Location → Options → Done) that installs per-user with no
admin prompt.

**Portable alternative:** download `FootNote-vX.Y.Z-win-x64.zip`, extract it to
a permanent folder, and run **`FootNote.App.exe`** — it registers itself the
same way.

Either way FootNote appears in **Settings → Apps** for uninstalling, and can
start with Windows. Requires Windows 10 or 11; nothing else needed (the .NET
runtime is bundled).

## Using FootNote

| You want to… | Do this |
|---|---|
| **Add a note to a file or folder** | Select it (Explorer or desktop) and press **Shift + Alt + N** |
| **Add a note from any other app** | Copy the file (Ctrl+C), then press **Shift + Alt + N** anywhere |
| **Add a note to "some file somewhere"** | Press **Shift + Alt + N** with nothing selected — a file picker opens |
| **Read a note** | Just click the file in Explorer or on the desktop — the bar appears |
| **Edit a note** | Click **Edit** on the bar, or press the hotkey again |
| **Remove a note** | Click **Delete** on the bar, confirm — you get 5 seconds to Undo via the toast |
| **Save while typing** | **Ctrl + Enter** (or the Save button) |
| **Cancel while typing** | **Esc** (or the Discard button) |
| **Change how FootNote looks/behaves** | Tray icon → **Settings…** |

## Settings

Right-click the tray icon → **Settings…**. Changes apply instantly — there is
no Save button, exactly like Windows' own Settings app.

- **Position** — dock the bar to the bottom or top of the screen; pin it to a
  specific monitor or let it follow the active Explorer window.
- **Appearance** — accent color (swatches or any hex value), with a live
  preview right in the window.
- **Behavior** — auto-hide delay (2–15 s) or "stay until dismissed"; slide
  animation on/off.
- **Hotkey** — remap the shortcut: click the box, press the new combination.
  Conflicts with combos other apps already own are detected before accepting.
- **Reset to Defaults** — one click, no questions asked.

Settings live in `%APPDATA%\FootNote\settings.json`.

Notes are capped at **500 characters** — a sticky note, not a document.
Files without notes never show anything; the bar only appears when there is
something to say.

## How it works (the honest details)

FootNote routes each note to one of two storage backends, automatically:

- **Local NTFS drives** → the note is written to an *alternate data stream*
  named `FootNote.txt` attached to the file — a native NTFS feature that has
  existed since the 90s. Invisible, zero extra files, and renaming or moving
  the file carries the note along.
- **Google Drive / OneDrive folders and non-NTFS drives** → the note is
  written to a hidden companion file next to the original
  (`report.xlsx` → `report.xlsx.footnote`). ADS streams don't survive a cloud
  round-trip, but a plain file does: your sync engine uploads it, and FootNote
  on another PC finds it there. Cloud folders are detected locally (env vars,
  registry, drive labels) — no accounts, no API calls, no internet needed.

Both backends store the same format: a small JSON version history (capped at
20 entries). A local index (paths only, never note text) is kept in
`%LocalAppData%\FootNote` so future features like search stay fast.

Two honest notes about the cloud path: the companion files are hidden in
*your* Explorer, but people you share a Drive/OneDrive folder with can see
them (web UI, phones, Macs) — so the first note saved into a sync folder
also drops a one-time `_FootNote_ReadMe.txt` at the sync root explaining
what they are. And cloud-folder detection reads local Google/Microsoft
config that isn't a public API; if either vendor changes it, FootNote
quietly falls back to local-only notes rather than breaking.

### Limitations you should know

- **Renaming a file in a cloud/USB folder orphans its note** — the companion
  file doesn't follow renames the way ADS does. The note isn't lost (it's
  still there under the old name) but won't display until re-linked manually.
- **Sending a file elsewhere** (email attachment, uploading to a web service,
  Dropbox, etc.) doesn't take the note along — by design.
- **Cloud-only files** ("online-only", never downloaded) can be commented,
  but on the PC where you do it; the note file syncs as usual.
- **Multi-select** is ambiguous, so the hotkey does nothing there on purpose.
- Rare: editing the same file's note offline on two PCs creates a sync
  conflict copy of the note file — recoverable (version history), not
  auto-merged.
- Windows only.

## Updating

FootNote checks GitHub quietly at startup and shows a small tray notification
if a newer release exists — nothing is ever installed automatically. To
update: exit FootNote (tray icon → Exit), extract the new zip over the old
folder, run `FootNote.App.exe` again. Notes and settings are unaffected.

## Recovering an accidentally deleted note

FootNote quietly keeps a tiny local backup of every note it has ever saved —
gzip-compressed, usually only a few hundred bytes even with heavy use. If you
delete a note by mistake (past the 5-second undo toast), or a note is lost
because its file moved somewhere its storage couldn't follow, right-click the
tray icon → **Recover Notes…**. It lists anything backed-up that no longer
has a live comment, with the exact text and when it was lost, and a Restore
button that adds it right back — never overwriting anything already on the
file.

This backup lives in `%LocalAppData%\FootNote\notes-backup.json.gz` and is
removed along with everything else on uninstall, same as your notes
themselves.

## Uninstalling

**Settings → Apps → FootNote → Uninstall** (or run `Uninstall.exe` from the
install folder). A short wizard tells you exactly how many tagged files it
will clean, shows live progress, and finishes with a summary. It removes the
app **and strips every note it created** — both stream notes and companion
files — your files are left exactly as FootNote found them, and nothing is
left behind on your disk. Files that were moved or deleted since their note
was written are skipped and logged, not treated as failures.

## Logs

Everything FootNote does is logged in plain text you can open in Notepad:
tray icon → **Open logs folder** (or `%APPDATA%\FootNote\logs\`). The app log
rolls daily and keeps about a week; the installer writes `install.log` there
too. The uninstaller's log goes to `%TEMP%\FootNote-uninstall.log` (it can't
live in a folder that's being deleted).

## Troubleshooting

- **The hotkey does nothing:** another app may own Shift+Alt+N. FootNote warns
  about this once at startup via a tray balloon.
- **Input language flips when using the hotkey:** Shift+Alt is also Windows'
  keyboard-language switcher if you have multiple input languages installed.
  Remove unused languages in Settings, or live with it.
- **Bar doesn't appear on click:** the passive bar shows in Explorer windows
  and on the desktop only — and only for files that actually have a note.
- **Diagnostics:** set the environment variable `FOOTNOTE_DEBUG=1` before
  starting the app to get a log at `%LocalAppData%\FootNote\debug.log`.

## Version history

- **5.6.0** — Renamed from FileTag to FootNote. Existing notes, settings, and
  the index all migrate automatically on first launch of the new build —
  nothing to do, nothing lost.
- **5.5.0** — The installer got 90%+ smaller (68.7MB → 5.8MB portable, app
  itself is 0.8MB). FootNote now relies on the .NET 8 Desktop Runtime already
  on your PC — if it's missing, Setup downloads and installs it automatically
  (one clearly-labeled step, no separate download needed from you).
- **5.4.0** — **Recover Notes**: a compact local backup of every note ever
  saved (gzip, a few hundred bytes even with heavy use), kept specifically to
  survive accidental deletes. Restore any backed-up note from the tray menu.
- **5.1.0** — **Delete with confirm and undo**: a Delete button on the bar,
  inline confirmation (Esc or clicking away always cancels), and a 5-second
  undo toast before anything actually leaves the disk. Translucency fixed —
  real blur-behind that clips to the panel's rounded corners.
- **5.0.0** — **Panel Appearance** (bar/pill style, background color, corner
  radius, size, font scale, opt-in blur) and the **interactive first-run
  tutorial** with verified hotkey step and embedded demo animation.
- **4.2.1** — The bar now always follows your selection: clicking another
  item updates or hides it immediately. An editor with unsaved typing is
  still protected; an untouched one is released.
- **4.2.0** — Notes can now be attached to **folders** as well as files, on
  both backends. Unresolvable selections fall through to the file picker
  instead of a dead-end message.
- **4.1.0** — One-time explanatory `_FootNote_ReadMe.txt` dropped at cloud
  sync roots so collaborators aren't puzzled by `.footnote` files; hardened
  cloud-detection fallback (degrades to local-only notes, never breaks).
- **4.0.0** — **Setup wizard** (`FootNote-Setup.exe`: Welcome / Location /
  Options / real progress / Finish), **uninstall wizard** with live tagged-file
  count and per-file progress, and a **shared logging system** across
  install / app / uninstall with an "Open logs folder" tray shortcut.
- **3.0.0** — **Settings window** (live-apply, no Save button): screen edge,
  monitor, accent color, auto-hide timing, animation toggle, and a fully
  remappable hotkey with conflict detection. Clear failure toasts every time
  a hotkey attempt or save can't proceed.
- **2.0.0** — Notes now work on **every** drive and **sync across PCs**:
  automatic dual storage (ADS on local NTFS, hidden companion files in
  Google Drive/OneDrive folders and on FAT32/exFAT drives), internal version
  history so nothing is silently overwritten, old notes migrate seamlessly.
- **1.1.0** — Hotkey changed to Shift+Alt+N. Works everywhere: desktop icons,
  clipboard-copied files, and a file-picker fallback so the hotkey is never a
  dead key. Focus fix for stubborn foreground apps.
- **1.0.0** — First release: Explorer integration, overlay bar, ADS storage,
  tray icon, auto-start, full uninstaller.

---

*FootNote is free. It writes nothing outside its install folder,
`%LocalAppData%\FootNote`, and the notes you ask it to create.*
