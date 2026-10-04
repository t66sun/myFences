namespace MyFences.App.Ui;

internal static class Text
{
    public static string Language { get; set; } = "zh-CN";
    private static readonly Dictionary<string, (string En, string Zh)> Strings = new()
    {
        ["new"] = ("New group", "新建分区"), ["organize"] = ("Organize desktop", "整理桌面"),
        ["undo"] = ("Undo", "撤销"), ["toggle"] = ("Show / hide groups", "显示 / 隐藏分区"),
        ["settings"] = ("Settings", "设置"), ["quit"] = ("Quit and restore desktop", "退出并恢复桌面"),
        ["remove"] = ("Move out of group", "移出分区"), ["resume"] = ("Resume rule management", "恢复自动管理"),
        ["rename"] = ("Rename group", "重命名分区"), ["delete"] = ("Delete group", "删除分区"),
        ["collapse"] = ("Collapse group", "收起分区"), ["expand"] = ("Expand group", "展开分区"),
        ["empty"] = ("Drop desktop items here", "将桌面项目拖入这里"),
        ["emptyDetail"] = ("Files stay in their original locations.", "文件仍保留在原来的位置。"),
        ["missing"] = ("Missing file", "文件已移走或不存在"),
        ["welcomeTitle"] = ("A little order. More room to think.", "让桌面井然有序。"),
        ["welcomeBody"] = ("Create a group from the tray, drag in desktop items, or organize them with your rules.", "从托盘新建分区，拖入桌面项目，或按你的规则整理。"),
        ["rules"] = ("Organization rules", "整理规则"), ["rulesHint"] = ("Rules run when you click Organize. Manual placement always wins.", "点击「整理桌面」时才执行规则，手动摆放的位置始终优先。"),
        ["enabled"] = ("On", "启用"), ["ruleName"] = ("Rule name", "规则名称"),
        ["kind"] = ("Item type", "项目类型"), ["extensions"] = ("Extensions", "扩展名"),
        ["target"] = ("Target group", "目标分区"), ["addRule"] = ("Add rule", "添加规则"),
        ["removeRule"] = ("Remove", "移除"), ["up"] = ("Move up", "上移"), ["down"] = ("Move down", "下移"),
        ["appearance"] = ("Appearance & keyboard", "外观与快捷键"),
        ["language"] = ("Language", "界面语言"), ["color"] = ("Group color", "分区颜色"),
        ["opacity"] = ("Background opacity", "背景不透明度"), ["hotkey"] = ("Show / hide shortcut", "显示 / 隐藏快捷键"),
        ["hotkeyHint"] = ("Use Ctrl, Alt or Shift with one letter, number or function key.", "组合 Ctrl、Alt、Shift 与一个字母、数字或功能键。"),
        ["apply"] = ("Apply changes", "应用更改"), ["close"] = ("Close", "关闭"),
        ["all"] = ("Any item", "所有项目"), ["file"] = ("File", "文件"),
        ["folder"] = ("Folder", "文件夹"), ["shortcut"] = ("App / shortcut", "应用 / 快捷方式"),
        ["consentTitle"] = ("Allow MyFences to organize desktop icons?", "允许 MyFences 管理桌面图标？"),
        ["consentBody"] = ("MyFences needs to turn off Auto arrange icons and Align icons to grid while running. Your original settings and grouped icons will be restored when you quit. Files remain in their original locations.\n\nContinue?", "MyFences 运行时需要关闭桌面的「自动排列图标」和「将图标与网格对齐」。退出时会恢复原来的设置和已分组图标，文件路径保持不变。\n\n是否继续？"),
        ["recovered"] = ("Your desktop has been restored after the previous session.", "已恢复上次会话的桌面图标与排列设置。"),
        ["count"] = ("items", "项"), ["organized"] = ("items organized. Undo is available from the tray.", "项已整理，可从托盘撤销。"),
        ["unchanged"] = ("No items changed. Check your rules or manual assignments.", "没有项目需要调整，可检查规则或手动分组。"),
        ["paused"] = ("Desktop integration paused", "桌面管理已暂停"),
        ["pausedBody"] = ("Desktop icon alignment changed. MyFences restored your icons. Use Show / hide groups from the tray to resume.", "检测到桌面排列设置变化，MyFences 已恢复原生图标。可从托盘显示分区，重新启用管理。"),
        ["shortcutConflict"] = ("That shortcut is already in use. Choose another in Settings. The tray control remains available.", "快捷键已被其他程序使用，请在设置中更换；托盘显隐仍可使用。"),
        ["error"] = ("MyFences needs your attention", "MyFences 需要处理一个问题"),
        ["groupName"] = ("Group name", "分区名称"),
        ["groupRemoved"] = ("The group was removed. Its files remain available on your desktop.", "已删除分区，其中的文件仍可从桌面访问。"),
        ["preview"] = ("Preview · sample layout", "预览 · 示例布局"),
        ["footer"] = ("MyFences 0.1 · Local by design · MIT", "MyFences 0.1 · 本地保存 · MIT 开源"),
        ["defaultsOnly"] = ("Changes apply to all existing groups and new groups.", "更改应用于现有分区和新建分区。"),
        ["resumeAll"] = ("Resume rules for all manual items", "让所有手动项目恢复规则管理"),
        ["previewHint"] = ("Sample layout; desktop integration is not active.", "当前为示例布局预览，尚未接管桌面图标。"),
        ["extensionHint"] = ("Example: .pdf, .docx. Blank matches all extensions of the chosen type.", "例如 .pdf, .docx；留空表示匹配所选类型的所有扩展名。")
    };
    public static string Get(string key) => Strings.TryGetValue(key, out var value) ? Language.StartsWith("zh", StringComparison.OrdinalIgnoreCase) ? value.Zh : value.En : key;
}
