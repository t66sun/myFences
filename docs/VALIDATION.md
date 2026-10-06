# Validation

## 0.2.0 acceptance

- Release solution build passed with zero warnings and errors; 19 core tests passed.
- On the real Windows desktop at 192 DPI (200%), DesktopDropCheck passed:
  previewed and actual pixels matched, the preview did not block hit testing or
  steal focus, two desktop icons and one external reference were released as one
  ordered batch, Undo restored membership/order/original recovery coordinates,
  an invalid group target changed nothing, and normal exit/recovery did not move
  successfully released icons back to obsolete capture positions.
- The native check verified the external file contents and desktop file list
  remained unchanged. Its finally block restored original test-icon locations
  and arrangement flags. It exercises Controller/Shell calls, not mouse OLE input.
- Existing controller checks passed: real desktop attachment/exposure, newly
  added group exposure, collapse/ungroup Undo and exit restoration.
- MenuCommandCheck passed: command acknowledgement, screen coordinates and WPF
  UI-thread dispatch. A stream disposal bug discovered by this check was fixed.
- The user confirmed the signed IExplorerCommand prototype was visible in the
  Windows 11 first-level desktop background menu and clicked it. The native log
  recorded state-desktop, title and invoke. The certificate required explicitly
  authorized LocalMachine TrustedPeople trust; CurrentUser trust alone failed.
  Signed package installation and COM activation succeeded without Developer Mode.
- DesktopMenuCheck passed: first registration, same-version external-location
  update, removal of package and metadata, and re-enabling. The check reads the
  effective external path through the Windows AppModel API, rather than assuming
  a PowerShell package property exists.
- Native Invoke initially failed with 0x80070002: the packaged COM surrogate
  could not read the unpackaged app's HKCU executable setting. The command now
  derives MyFences.exe from its registered module directory and reads the localized
  caption from a UTF-16 title file written by Enable. The same registered COM
  probe then returned S_OK and opened the naming window in the existing instance.
- Computer Use verified cancel leaves the saved group count unchanged; confirming
  a name creates a saved group visible in live Windows desktop composition.
  Startup via --new-group when the app is absent displayed only the naming window,
  with no initial Settings window. Cancellation created no group.
- Computer Use verified the normal Settings menu path-update button and its
  resulting status. Real mouse dragging in a preview group reordered the icons;
  Ctrl+Z restored their prior order. This is a real OLE interaction but does not
  prove desktop release or external-application FileDrop.
- RenderCheck generated the actual WPF menu settings page in Chinese and English
  at 100%, 125%, 150% and 200%. Both language layouts were visually inspected;
  status, trust explanation and actions fit without clipping.
On 2026-10-06, the user confirmed all final acceptance checks passed: actual desktop menu naming/cancellation/visible creation; Escape cancellation; mixed batch drag-out with preview, nearby icon placement and Ctrl+Z; and Ctrl-drag FileDrop copying to File Explorer while retaining the source file. An administrator-run 0.1.2 instance initially blocked IPC; it was stopped, its icons restored and the candidate restarted with normal privileges in an isolated layout. The original user layout hash was preserved.

Free-positioning exit checks preserve the drop point. Restoring a user's original
Auto Arrange/Align to Grid settings can subsequently cause Windows to rearrange
icons; MyFences continues restoring those settings rather than leaving them disabled.

## Earlier desktop integration evidence

Repository initialization (0.1.2): Release solution build passed with zero warnings
and errors; all nine core tests passed; self-contained win-x64 development publish
and portable/source ZIP generation succeeded. The final delivery script exports
the committed tree and validates source ZIP entries against that export.

Visibility correction: the original wallpaper WorkerW host was covered by the
desktop icon view on Windows 11 25H2. Groups now attach inside SHELLDLL_DefView,
above SysListView32. The native ancestor/sibling hit-test regression failed on
the original implementation and passes after the correction. A parent handle
and correct dimensions alone are not proof that a desktop group is exposed.

The first correction still left groups absent from the actual desktop picture.
Attaching after Window.Show changed the layered window's parent/styles after
its initial presentation. Attachment now runs in SourceInitialized, before
Window.Show presents it. Live DWM desktop composition showed no group before
this change and showed the saved group after it, including a normal launch
without diagnostic bitmap rendering. The --desktop-view Windows check opens
that compositor view; it does not replace the actual desktop with a mockup.

Environment: Windows 11 Pro 25H2 x64, build 26200.9168; .NET SDK 10.0.401.

Completed:

- Release builds of application and Windows checks, with zero compiler warnings.
- Nine core tests: priority and matching, manual grouping/ungrouping overrides, lazy groups, deletion, multiple-item ordering, batch undo, persistence, corrupt-layout retention, and undo after physical rename/move.
- Actual Windows Shell connection, desktop child-window attachment, disabling managed arrangement flags, hiding a real filesystem icon, restoring its exact position, and restoring arrangement flags.
- Actual transparent desktop attachment at the current 200% system DPI (192 DPI), plus recovery using a new session object reading the persisted journal.
- Actual app controller: real group dimensions at 200% DPI, collapse/undo, ungroup/undo, and normal exit restoring the original icon position and flags.
- Self-contained published EXE launch and the same Shell/journal integration check, without DOTNET_ROOT.
- Real WPF Chinese/English settings and group rendering, including empty groups, missing long filenames, narrow groups, and settings renders at 100/125/150/200% bitmap scales.
- Interactive preview collapse/expand controls and keyboard undo.

Render scale checks do not establish native desktop positioning at all system DPI settings.

Remaining manual beta acceptance:

- System DPI 100%, 125%, 150%, 200%: title movement, all resize edges, clipping, collapse and restored positions.
- Explorer/desktop incoming drag; single/multiple-item reorder and cross-group drag; desktop ungrouping; Ctrl/Shift copy/move/link outbound to Explorer and applications; Escape cancellation.
- Native Shell context-menu open, rename, properties and mixed desktop locations; missing file handling.
- Explorer restart, app process termination followed by journal recovery, logoff, and manual desktop alignment changes.
- Long-session behavior and hotkey conflicts.

The source includes `tests/MyFences.WindowsChecks`, whose default check briefly captures one real icon and restores it in a `finally` block. It writes its result to the configured data directory. `--controller-check` exercises the actual controller with an isolated one-icon layout. `--render-check` writes actual WPF render artifacts to `.local/render-check`.
