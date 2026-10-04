# MyFences 0.1.2 — Windows 11 桌面整理工具

[English](README.en.md) · MIT · C# / WPF / .NET 10 · Windows 11 x64

半透明桌面分区，保留 Windows 原生文件图标。分区保存文件路径引用，整理和分区间拖动不会移动磁盘上的文件。当前为单显示器测试版。

## 开始使用

1. 解压便携 ZIP，双击 `MyFences.exe`。不需要另外安装 .NET，也不需要管理员权限。
2. 右键系统托盘的 MyFences 图标，选择“新建分区”；或在设置中点击“整理桌面”，按规则创建所需分区。
3. 将桌面或资源管理器中的文件拖入分区。拖动标题栏移动分区，拖动边缘调整大小，点击标题栏箭头折叠。
4. `Ctrl+Alt+H` 显示或隐藏全部分区；未分组的桌面图标仍保留。快捷键可在设置中修改。
5. 退出请使用托盘菜单“退出”，应用会恢复已接管的原生桌面图标和排列设置。

首次接管桌面文件时会说明并请求关闭“自动排列图标”和“将图标与网格对齐”。应用把已分组的原生图标坐标暂时移到屏幕外；没有移动或删除文件。桌面排列设置被手动改变后，应用恢复图标并暂停接管，可从托盘重新显示分区。

## 整理与拖放

- 只有点击“整理桌面”才执行分类，没有后台自动分类。预置应用、文件夹、文档、图片规则；支持项目类型、扩展名与规则优先级，第一条匹配规则生效。
- 手动分组与手动移出分区优先。右键文件选择“恢复规则管理”，或在设置中恢复全部，然后下一次整理才会重新分类。
- 分区内支持多选、排序、分区间拖放；拖回空白桌面可解除分组。`Ctrl+A` 全选，`Ctrl+Z` 撤销分区操作，托盘菜单也提供撤销。撤销历史保留本次运行最近 50 次操作。
- 双击通过 Windows 默认应用打开；右键显示 Windows 文件菜单和分区操作。文件被重命名时更新引用；缺失文件显示淡化图标，可移出分区。
- 拖出到资源管理器或其他应用使用标准 Windows 文件拖放，可能复制、移动或创建快捷方式。**分区撤销不会撤销这些物理文件操作。**
- 设置提供中英文、颜色、透明度和快捷键。外观变化应用于全部分区。

## 保存与恢复

布局保存在 `%LOCALAPPDATA%\MyFences\layout.json`。图标位置与桌面设置在修改前记录到同目录的 `desktop-recovery.json`。正常退出恢复并删除恢复记录；异常退出后下次启动先恢复，再加载布局。

需要单独恢复时，先结束仍在运行的 MyFences，再在程序所在目录运行：

```powershell
.\MyFences.exe --restore-desktop
```

恢复过程不会清空布局。遇到启动错误时查看同目录 `last-error.txt`。测试和开发可通过 `MYFENCES_DATA_DIR` 指定独立数据目录；恢复时必须使用相同目录。

## 从源码构建

安装 .NET 10 SDK，在项目根目录执行：

```powershell
dotnet build MyFences.slnx -c Release
dotnet test tests/MyFences.Tests -c Release
.\scripts\release.ps1
```

本地交付脚本从已提交的干净内容构建，在 `release/v0.1.2/` 生成便携版 ZIP、源码 ZIP、变更说明和记录 commit 与 SHA-256 的 `release.json`。开发中只打包当前工作区时使用 `scripts/publish.ps1`，输出位于被忽略的 `.local/`。便携版附带 .NET 运行时，未启用 WPF 不支持的裁剪。

目录职责与后续提交政策见 [AGENTS.md](AGENTS.md)，设计说明见 [docs/DESIGN.md](docs/DESIGN.md)。研究笔记位于 `research/`；参考项目克隆仅保留在本地，不作为子模块提交。没有授权时不推送远端；`push` 授权不包含 Git tag 或 GitHub Release。

不接管桌面的示例预览：

```powershell
.\MyFences.exe --preview
```

Windows 集成检查会短暂接管一个真实图标，并在检查结束前恢复原位置及桌面设置。请在可交互的桌面会话中运行，使用独立数据目录：

```powershell
$env:MYFENCES_DATA_DIR = "$PWD\.local\integration"
dotnet run --project tests/MyFences.WindowsChecks -c Release
```

`--render-check` 生成实际 WPF 控件的中英文与多倍率渲染截图；它不等于切换系统 DPI 的验收。

## 版本范围与验证

已验证的内容见 [验收记录](docs/VALIDATION.md)。当前支持 Windows 11 x64 单显示器。多显示器、虚拟桌面配置、文件夹门户、搜索、后台分类、安装器和自动更新未包含在 0.1 中。完整外部拖放矩阵、Explorer 重启以及所有系统 DPI 档位仍需实机验收。

程序与源码使用 [MIT License](LICENSE)。参考项目与系统依赖说明见 [THIRD_PARTY.md](THIRD_PARTY.md)。本项目独立实现，不与 Stardock 关联。
