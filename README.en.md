# MyFences 0.1.2

[简体中文](README.md) · MIT · Windows 11 x64 · C# / WPF / .NET 10

Translucent desktop groups with original Windows file icons. Groups store path references; organizing and moving between groups do not move files on disk. This is a single-monitor beta.

## Use

Extract the portable ZIP and launch `MyFences.exe`. No separate .NET runtime or administrator privileges are required. Right-click the tray icon to create a group, or click **Organize desktop** in Settings to apply classification rules. Drag files from the desktop or Explorer into groups; drag the title to move, the edges to resize, and the arrow to collapse.

`Ctrl+Alt+H` toggles all groups. Ungrouped desktop icons remain visible. Exit through the tray menu to restore native icon positions and desktop arrangement settings.

The first capture asks to disable Auto Arrange and Align to Grid. Grouped native icons are temporarily positioned off-screen, with their original coordinates journaled before any changes. No files are moved or deleted. Changing desktop alignment manually restores native icons and pauses capture; use the tray toggle to resume.

Classification runs only when you click Organize. Rules match item type and extensions, in priority order. Manual placement, including manual ungrouping, wins until **Resume rule management** is selected. Groups support multiple selection, reordering, transfers, `Ctrl+A`, and `Ctrl+Z`. Undo retains the latest 50 group operations in the current session. Standard outbound Windows drag-and-drop can physically copy or move files; group undo does not reverse those operations.

Double-click opens the default Windows app. Right-click provides native file actions and grouping commands. Renamed files update their references; missing files are dimmed and can be ungrouped. Settings offer Chinese/English, group color, opacity, and a configurable global shortcut.

## Recovery

Layout: `%LOCALAPPDATA%\MyFences\layout.json`. Before capture, `desktop-recovery.json` records positions and arrangement flags. Normal exit restores them; the next launch recovers after an abnormal exit. To restore separately, stop any running instance and execute:

```powershell
.\MyFences.exe --restore-desktop
```

The saved layout is retained. Startup errors are logged in `last-error.txt`. `MYFENCES_DATA_DIR` overrides the data directory for development; recovery must use the same directory.

## Build

With .NET 10 SDK:

```powershell
dotnet build MyFences.slnx -c Release
dotnet test tests/MyFences.Tests -c Release
.\scripts\release.ps1
```

The release script exports the recorded Git commit and builds portable Windows x64 and source ZIPs under `release/v0.1.2/`, with a changelog and a SHA-256 manifest. Development packaging from the current working tree uses `scripts/publish.ps1` and writes to `.local/`. Project policy is in [AGENTS.md](AGENTS.md), design in [docs/DESIGN.md](docs/DESIGN.md), and reference notes in `research/`. Upstream checkouts remain local and are not Git submodules.

`--preview` shows a sample layout without capturing desktop icons. The WindowsChecks project tests real Shell attachment and temporary icon capture/restoration; use a separate data directory and an interactive desktop session. Its `--render-check` option renders the real WPF controls at multiple scales, which does not substitute for OS DPI tests.

See [validation](docs/VALIDATION.md) for completed checks and remaining acceptance work. Multi-monitor profiles, virtual desktops, folder portals, search, background classification, installers, and updates are outside version 0.1.

Licensed under [MIT](LICENSE). See [references and dependencies](THIRD_PARTY.md). This independently implemented project is not affiliated with Stardock.
