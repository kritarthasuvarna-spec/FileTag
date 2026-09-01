# Changelog (dev-facing)

Record of what was actually implemented, tested, fixed, or deferred per
release. User-facing notes live in `FootNote.App/Assets/PatchNotes.json`.

## 1.0.0 — 2026-09-02
- First public release of FootNote. Fresh version history starting here;
  prior internal builds (including the FileTag-branded ones) are folded
  into this release rather than tracked individually.
- Core: dual storage backend (NTFS alternate data stream on local drives,
  hidden companion file on Google Drive/OneDrive/non-NTFS drives), version
  history per note, cloud sync detection, folder notes.
  Overlay bar, global hotkey (Shift+Alt+N, remappable, conflict-checked),
  delete with 5-second undo, Recover Notes backup/restore.
  Settings window with live-apply position/appearance/behavior options,
  interactive first-run tutorial.
  Per-user Setup wizard and matching uninstaller, plain-text logging
  across install/app/uninstall, dark titlebar and Manrope font throughout.
