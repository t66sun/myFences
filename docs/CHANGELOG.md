# Change history

## 0.2.1

- Exclude desktop groups, Settings, naming dialogs and drop previews from Alt+Tab using native tool-window styling, without WPF hidden owner windows.

Validation: the live Windows switcher check reproduced the old failure and passed after the fix, including hide/show and absence of hidden owners. Release build and 19 core tests passed.

## 0.2.0

- Desktop icons dragged out of groups are placed near the release point in group order, avoiding occupied desktop cells and keeping positions visible.
- Drag feedback previews desktop placement; references from other folders are removed from the group without moving files or creating shortcuts.
- A batch desktop drop can be undone together with grouping order and desktop position recovery.
- Optional desktop background menu creates a named group near the command's cursor position and forwards commands to the running instance.
- Release notes are read from the recorded commit rather than repeating the initial-delivery description.

Validation: 19 core tests and the zero-warning Release build passed. Real Shell checks at 200% DPI verified preview/placement, mixed references, batch Undo and recovery. The user confirmed final desktop menu, Escape, drag-out/Undo and File Explorer copy acceptance on 2026-10-06. This version uses the signed IExplorerCommand identity package. Detailed final acceptance evidence is recorded in VALIDATION.md.

## 0.1.2

Initial repository delivery: desktop groups, layout persistence, undo, desktop icon recovery, Chinese/English UI and desktop integration diagnostics. Established project governance and commit-based portable/source packaging.

Validation: Release build passed with zero warnings and errors; nine core tests passed; self-contained development publish and clean-commit delivery archives passed verification.
