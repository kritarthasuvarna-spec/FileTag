<picture>
  <source media="(prefers-color-scheme: dark)" srcset="docs/brand/wordmark-dark.svg">
  <img alt="footnote" src="docs/brand/wordmark-light.svg" width="360">
</picture>

### The sticky note Windows forgot to build in.

Six months from now you'll open some file and think *"wait, why did I keep this?"*
FootNote answers that, right where the file lives, with no database and no setup.

Select a file. Press a hotkey. Type a thought. Done.
Click the file again next week, next year, on a different PC entirely, and the note is still there, riding along with the file itself.

**No app to open. No account to make. Nothing to configure before it works.**

---

## Why it's different

- **The note lives inside the file, not in some database that can go stale or get deleted.** On local NTFS drives it's written into an alternate data stream attached to the file, so renaming or moving the file carries the note along automatically.
- **It follows you across PCs, with zero setup.** Inside a Google Drive or OneDrive folder, or on a USB stick, FootNote quietly switches to a hidden companion file instead, so your sync engine carries the note along like any other file. It picks the right method per file. You never think about it.
- **Your files stay exactly as they were.** Content, size, modified date, none of it changes. Every other app opens the file exactly as before.
- **Nothing is ever silently overwritten.** Every note keeps a short internal history, so an accidental edit or sync conflict is always recoverable.
- **It gets out of your way.** No admin rights, no bloated installer, one small tray icon sitting at a few MB of RAM.

## Install

**Recommended:** download **`FootNote-Setup.exe`** and run it. A short wizard (Welcome, Location, Options, Done) installs it per user with no admin prompt.

**Portable alternative:** download the `.zip`, extract it anywhere permanent, and run `FootNote.App.exe` directly. It registers itself the same way.

Either way, FootNote shows up in **Settings → Apps** for uninstalling, and can start with Windows. Works on Windows 10 and 11, nothing else to install.

## Using FootNote

| You want to... | Do this |
|---|---|
| **Add a note to a file or folder** | Select it in Explorer or on the desktop, press **Shift + Alt + N** |
| **Add a note from any other app** | Copy the file (Ctrl+C), then press **Shift + Alt + N** anywhere |
| **Add a note to "some file somewhere"** | Press **Shift + Alt + N** with nothing selected, a file picker opens |
| **Read a note** | Click the file in Explorer or on the desktop, the bar appears |
| **Edit a note** | Click **Edit** on the bar, or press the hotkey again |
| **Remove a note** | Click **Delete** on the bar and confirm, you get 5 seconds to undo |
| **Save while typing** | **Ctrl + Enter**, or the Save button |
| **Cancel while typing** | **Esc**, or the Discard button |
| **Change how FootNote looks or behaves** | Right-click the tray icon → **Settings...** |

## Settings

Right-click the tray icon → **Settings...**. Changes apply instantly, there is no Save button, the same way Windows' own Settings app works.

- **Position** — dock the bar to the top or bottom of the screen, pin it to a specific monitor, or let it follow the active Explorer window.
- **Appearance** — pick an accent color from swatches or any hex value, with a live preview in the window.
- **Behavior** — set an auto-hide delay (2 to 15 seconds) or keep the bar until dismissed, toggle the slide animation.
- **Hotkey** — remap the shortcut by clicking the box and pressing the new combination. Conflicts with combos other apps already own are caught before you can save them.
- **Reset to Defaults** — one click, no questions asked.

Settings live in `%APPDATA%\FootNote\settings.json`.

Notes are capped at **500 characters**, a sticky note, not a document. Files without a note never show anything; the bar only appears when there's something to say.

## How it works

FootNote routes each note to one of two storage backends automatically:

- **Local NTFS drives** — the note is written to an alternate data stream named `FootNote.txt`, attached to the file. That's a native NTFS feature that's existed since the 90s. Invisible, no extra files, and renaming or moving the file carries the note along.
- **Google Drive, OneDrive, and non-NTFS drives** — the note is written to a hidden companion file next to the original (`report.xlsx` becomes `report.xlsx.footnote`). ADS streams don't survive a cloud round trip, but a plain file does, so your sync engine uploads it and FootNote finds it on your other PC. Cloud folders are detected locally, no accounts and no API calls involved.

Both backends store the same format: a small JSON history capped at 20 entries. A local index of file paths (never note text) is kept in `%LocalAppData%\FootNote` to keep things fast.

Two honest notes about the cloud path: the companion files are hidden in your own Explorer, but anyone you share that Drive or OneDrive folder with can see them from a browser, phone, or Mac, so the first note saved into a shared folder also drops a one-time `_FootNote_ReadMe.txt` explaining what they are. And cloud detection reads local Google and Microsoft configuration that isn't a public API, so if either vendor changes it, FootNote quietly falls back to local-only notes instead of breaking.

### Limitations you should know

- **Renaming a file in a cloud or USB folder orphans its note.** The companion file doesn't follow renames the way an ADS stream does. The note isn't lost, it's still there under the old name, but it won't display until re-linked manually.
- **Sending a file elsewhere** (email attachment, uploading to a web service, Dropbox) doesn't take the note with it, by design.
- **Cloud-only files** ("online-only," never downloaded locally) can be commented on, but only from the PC where you did it. The note file syncs down as usual.
- **Multi-select is ambiguous**, so the hotkey does nothing there on purpose.
- Editing the same file's note offline on two PCs can, rarely, create a sync conflict copy of the note, recoverable through the history, not auto-merged.
- Windows only.

## Recovering an accidentally deleted note

FootNote quietly keeps a tiny local backup of every note it has ever saved, gzip compressed, usually only a few hundred bytes even with heavy use. If you delete a note by mistake past the 5-second undo toast, or a note gets lost because its file moved somewhere its storage couldn't follow, right-click the tray icon → **Recover Notes...**. It lists anything backed up that no longer has a live note, with the exact text and when it was lost, and a Restore button that adds it right back without overwriting anything already on the file.

This backup lives in `%LocalAppData%\FootNote\notes-backup.json.gz` and is removed along with everything else on uninstall, same as your notes themselves.

## Uninstalling

**Settings → Apps → FootNote → Uninstall** (or run `Uninstall.exe` from the install folder). A short wizard tells you exactly how many tagged files it will clean, shows live progress, and finishes with a summary. It removes the app and strips every note it created, both stream notes and companion files, so your files are left exactly as FootNote found them. Files that were moved or deleted since their note was written are skipped and logged, not treated as failures.

## Logs

Everything FootNote does is logged in plain text, right-click the tray icon → **Open logs folder** (or go to `%APPDATA%\FootNote\logs\`). The app log rolls daily and keeps about a week; the installer writes `install.log` there too. The uninstaller's log goes to `%TEMP%\FootNote-uninstall.log`, since it can't write into a folder that's being deleted.

## Troubleshooting

- **The hotkey does nothing:** another app may already own Shift+Alt+N. FootNote warns about this once at startup via a tray balloon.
- **Input language flips when using the hotkey:** Shift+Alt is also Windows' own keyboard-language switcher if you have multiple input languages installed. Remove unused languages in Settings, or live with it.
- **Bar doesn't appear on click:** the passive bar shows in Explorer windows and on the desktop only, and only for files that actually have a note.
- **Diagnostics:** set the environment variable `FOOTNOTE_DEBUG=1` before starting the app for a log at `%LocalAppData%\FootNote\debug.log`.

---

*FootNote is free. It writes nothing outside its install folder, `%LocalAppData%\FootNote`, and the notes you ask it to create.*
