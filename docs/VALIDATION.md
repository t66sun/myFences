# Validation

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
