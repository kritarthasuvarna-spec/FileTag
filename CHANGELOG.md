# Changelog (dev-facing)

Record of what was actually implemented, tested, fixed, or deferred per
release. User-facing notes live in `FootNote.App/Assets/PatchNotes.json`.

## 5.6.0 — 2026-09-01
- Product rebrand: FileTag → FootNote. All projects, namespaces, assemblies,
  the solution file, icons, and documentation renamed. 🏷 swapped to 📝
  throughout the UI.
- Storage identifiers renamed too, not just branding: ADS stream
  `FileTag.txt` → `FootNote.txt`, sidecar suffix `.filetag` → `.footnote`.
  Both backends (AdsHelper, SidecarHelper) keep read-fallback to the legacy
  name plus a `MigrateLegacy()` that renames a found legacy stream/sidecar
  to the new name — exposed per-file via `StorageRouter.MigrateLegacy`.
- RebrandMigration.cs (Core, new): one-time move of
  `%LocalAppData%\FileTag` → `...\FootNote` and `%AppData%\FileTag` →
  `...\FootNote` (settings, index, notes-backup, logs all travel intact),
  plus cleanup of the old per-user registry entries (Run value, Apps &
  Features key) once the new ones are written. Runs as the literal first
  statement in both App.xaml.cs OnStartup and SetupViewModel.RunAsync —
  before Logger.Init, which would otherwise create the new folder first
  and make the migration think there was nothing to move.
  `Directory.Move` retries briefly (5x, 300ms) before falling back to
  copy+delete, to ride out a transient file-handle release race right
  after the previous build's process exits.
- Setup: existing-install detection, the Update path's registry reads, and
  StopRunningApp all transitionally recognize both "FootNote" and the old
  "FileTag" naming, so an in-place update over a real FileTag install is
  detected as an upgrade (not a fresh side-by-side install) and the old
  build is asked to exit gracefully before Setup touches its files.
- Verified for real against genuine pre-rebrand user data (real settings
  with non-default values, real index entries, a real ADS-stream note):
  full launch-time migration correctly moved both data folders, preserved
  settings and index content exactly, migrated the per-file note, and left
  no legacy folders or streams behind.

## 5.5.0 — 2026-09-01
- FootNote.App switched from self-contained to framework-dependent publish
  (build.ps1: `--self-contained false`, dropped EnableCompressionInSingleFile/
  IncludeNativeLibrariesForSelfExtract which only apply to self-contained).
  Result: portable zip 68.7MB -> 5.8MB, FootNote.App.exe itself 0.82MB.
  FootNote.Setup and Uninstall.exe deliberately stay self-contained — Setup
  has to be runnable on a bare machine with nothing installed, before it can
  even check what App.exe needs.
- RuntimeInstaller.cs (Setup): detects Microsoft.WindowsDesktop.App 8.x via
  %ProgramFiles%\dotnet\shared; if missing, downloads the official evergreen
  installer (aka.ms/dotnet/8.0/windowsdesktop-runtime-win-x64.exe) with live
  byte-progress, runs it silently (/install /quiet /norestart), and only
  then proceeds — visible step in the UI, no manual download required from
  the user (explicit user decision: transparent but automatic, not silent-
  and-hidden, not a manual redirect-and-stop).
  Test hook: FOOTNOTE_FORCE_RUNTIME_MISSING=1 forces the download path
  without touching the real installed runtime.
- Setup progress bar reworked to a continuous 0-25% band for the runtime
  check/download/install, remaining 5 steps evenly splitting 25-100%.
- Verified for real, not simulated: normal install with runtime present
  (near-instant skip, confirmed via install.log); forced-missing path
  actually downloaded and silently ran the real windowsdesktop-runtime
  installer end to end (exit code 0, ~3.5 min real-world, logged); app
  launches and the full passive-display/hotkey pipeline still works
  correctly post-switch (UI Automation + WPF unaffected by the publish
  mode change). 75 storage-suite checks still green (untouched by this
  change — Core wasn't touched).

## 5.4.0 — 2026-08-28
- NotesBackup (Core): gzip-compressed local mirror of every note ever saved,
  written on every StorageRouter.Save; entries survive StorageRouter.Delete
  (marked deleted, not erased) — that's the entire point of a last-resort
  backup. ~200 bytes per real entry observed in practice.
- RecoverNotesWindow (tray → "Recover Notes…"): lists backup entries whose
  file still exists but has no live comment right now; Restore re-saves the
  text (non-destructive — appends to history like any normal save).
- Startup backfill: mirrors every currently-indexed note into the backup once
  per launch (background thread), so pre-existing notes get protected too.
- Uninstall now also removes notes-backup.json.gz (consistent with "strips
  everything" — uninstall already deliberately erases all live notes).
- Test isolation fix: NotesBackup.SetPathForTesting added and the console
  suite now redirects for its ENTIRE run, not partway through — an earlier
  version of this change briefly leaked temp-test entries into the real
  %LocalAppData%\FootNote\notes-backup.json.gz on this dev machine before
  the fix; cleaned up, verified a clean re-run touches zero real files.
- Verified with a full live cycle through the real UI: delete a note, confirm,
  wait out the 5s grace period so it truly commits, open Recover Notes, see
  the exact text and deletion time, click Restore, confirm the note is back
  on the actual file and re-indexed. 75 storage-suite checks green (64 + 11
  new backup tests).

## 5.3.0 — 2026-07-27
- Patch Notes system: embedded `PatchNotes.json` (App + Setup), `PatchNotesWindow`
  ("What's New" in tray menu), filtered "what's new since vX" on Setup's update
  Finish screen (old version exclusive → new inclusive, System.Version compare).
- Setup "Uninstall only": launches the existing uninstall wizard and exits —
  no longer auto-chains into a fresh install.
- Delete grace period index timing: index entry removed at confirm (keeps the
  uninstaller's live count accurate), re-added on Undo or on save/edit revival.
- Added this CHANGELOG.md; process rule adopted (entry per release).
- Tested: build clean; storage suite 55/55; silent update path exercised.

## 5.2.0 — 2026-07-27
- Existing-install detection in Setup (registry DisplayVersion/InstallLocation);
  Update-in-place locked to registry folder; silent /S updates in place.
- Graceful app shutdown via named event `FootNote.App.ExitRequest`; kill fallback.
- Clean 4-file install layout (app, Uninstall.exe, README.txt, LICENSE.txt from
  install-assets/); Start Menu shortcut (both install paths; removed on uninstall).
- Tested live: 5.1.0→5.2.0 update in place, graceful-exit signal, layout, shortcut.
- Deviation: exe stays `FootNote.App.exe` (spec says FootNote.exe) — rename ripples
  through stub/process checks/existing installs for cosmetic gain.

## 5.1.0 — 2026-07-19
- Delete button (red hover) + inline confirm state (Esc/deactivate/selection
  change always cancel) + 5s undo grace (physical delete deferred; toast undo).
- Translucency fixed: ACCENT_ENABLE_BLURBEHIND (acrylic state broken on Win11
  layered windows) + rounded window region clipping.

## 5.0.0 — 2026-07-19
- Panel Appearance settings (bar/pill, panel color, radius 0–24, size preset,
  font scale, opt-in blur); settings schema v2.
- TutorialWindow (verified hotkey step, embedded demo GIF via WpfAnimatedGif,
  tray "Show tutorial"); GIF generated by tools/gen-demo-gif.ps1; Setup Finish
  plays the same GIF; tray tooltip updated.

## 4.2.x — 2026-07-19
- 4.2.0: folder comments on both backends (native stream-existence check —
  File.Exists is false for directory streams); unresolvable selections fall
  through to clipboard/picker; toast wording.
- 4.2.1: bar always follows selection; clean edits released, dirty protected.

## 4.1.x — 2026-07-18/19
- 4.1.0: one-time `_FootNote_ReadMe.txt` at cloud sync roots (Google letter-mounts
  target "My Drive"); documented cloud-detection graceful degradation.
- 4.1.1: manifest-based uninstall cleanup — only FootNote-owned files deleted,
  folder removed only if empty; data-dir cleanup file-specific.

## 4.0.0 — 2026-07-15
- Shared Logger (Core): `timestamp | LEVEL | component | message`; rolling app
  log (~7 days/5MB); install.log; uninstall log in %TEMP%; tray "Open logs folder".
- FootNote.Setup wizard (embedded payload zip; /S silent; WPF single-file needs
  IncludeNativeLibrariesForSelfExtract — crashes 0xC000041D without it).
- Uninstall wizard hosted in `FootNote.App --uninstall` (Uninstall.exe = stub).
- Sentinel-guarded scheduled folder deletion (reinstall race fix); relocation
  balloon; second-instance message.

## 3.0.0 — 2026-07-14
- Live-apply Settings (MVVM, %APPDATA% settings.json schema v1): edge, monitor,
  accent, auto-hide, animation, hotkey remap with RegisterHotKey probe.
- ToastManager failure toasts (each logged ERROR).

## 2.0.0 — 2026-07-14
- Dual storage behind ICommentStore (AdsHelper + SidecarHelper), StorageRouter,
  CloudFolderDetector (OneDrive registry/env; Google DriveFS registry/volume
  label); 20-entry versioned history; legacy v1 format migration; fallback reads
  across backends; 42-test console suite.

## 1.x — 2026-07-14
- 1.0.0: ADS storage, overlay bar, UIA watcher, tray, hotkey, self-registration,
  silent uninstaller, zip distribution. Defender/ADS spike passed day one.
- 1.1.0: hotkey → Shift+Alt+N (user request; spec default remains remappable);
  desktop selection (IShellWindows.FindWindowSW dual interface), clipboard
  fallback, picker fallback; AttachThreadInput focus fix.

## Deferred (per spec "NOT in v1")
Color tags, search, export, bulk manager, icon overlay badge, view-history UI,
full dark mode, Mac/Linux, cloud-provider APIs. Outstanding test: two-PC sync.
