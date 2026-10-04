using System.Text.Json;

namespace MyFences.Core;

public enum ItemKind { File, Folder, Shortcut }
public enum AssignmentSource { Manual, Rule }
public sealed record DesktopEntry(string Path, ItemKind Kind);

public sealed class FenceGroup
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "New group";
    public double X { get; set; } = 120;
    public double Y { get; set; } = 100;
    public double Width { get; set; } = 360;
    public double Height { get; set; } = 280;
    public bool Collapsed { get; set; }
    public string Color { get; set; } = "#253344";
    public double Opacity { get; set; } = .72;
}

public sealed class ItemReference
{
    public string Path { get; set; } = "";
    public Guid? GroupId { get; set; }
    public AssignmentSource Source { get; set; }
}

public sealed class ClassificationRule
{
    public bool Enabled { get; set; } = true;
    public string Name { get; set; } = "";
    public ItemKind? Kind { get; set; }
    public string Extensions { get; set; } = "";
    public Guid TargetGroupId { get; set; }
    public string TargetName { get; set; } = "";
    public bool Matches(DesktopEntry entry)
    {
        if (!Enabled || (Kind.HasValue && Kind != entry.Kind)) return false;
        var extensions = Extensions.Split([',', ';', ' ', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return extensions.Length == 0 || extensions.Any(e => string.Equals(e.StartsWith('.') ? e : "." + e, System.IO.Path.GetExtension(entry.Path), StringComparison.OrdinalIgnoreCase));
    }
}

public sealed class AppSettings
{
    public bool DesktopPermissionGranted { get; set; }
    public string Language { get; set; } = "zh-CN";
    public string Hotkey { get; set; } = "Ctrl+Alt+H";
    public string DefaultColor { get; set; } = "#253344";
    public double DefaultOpacity { get; set; } = .72;
}

public sealed class AppState
{
    public int Version { get; set; } = 1;
    public List<FenceGroup> Groups { get; set; } = [];
    // Ungrouped manual references are retained so Organize cannot override them.
    public List<ItemReference> Items { get; set; } = [];
    public List<ClassificationRule> Rules { get; set; } = [];
    public AppSettings Settings { get; set; } = new();
    public AppState Clone() => JsonSerializer.Deserialize<AppState>(JsonSerializer.Serialize(this))!;

    public static AppState Create(string language)
    {
        var zh = language.StartsWith("zh", StringComparison.OrdinalIgnoreCase);
        var names = zh ? new[] { "应用", "文件夹", "文档", "图片" } : new[] { "Apps", "Folders", "Documents", "Pictures" };
        var state = new AppState { Settings = new() { Language = zh ? "zh-CN" : "en" } };
        state.Rules = [
            new() { Name = names[0], TargetName = names[0], Kind = ItemKind.Shortcut, TargetGroupId = Guid.NewGuid() },
            new() { Name = names[1], TargetName = names[1], Kind = ItemKind.Folder, TargetGroupId = Guid.NewGuid() },
            new() { Name = names[2], TargetName = names[2], Kind = ItemKind.File, Extensions = ".pdf,.doc,.docx,.xls,.xlsx,.ppt,.pptx,.txt,.md,.rtf,.csv", TargetGroupId = Guid.NewGuid() },
            new() { Name = names[3], TargetName = names[3], Kind = ItemKind.File, Extensions = ".png,.jpg,.jpeg,.gif,.bmp,.webp,.heic,.tif,.tiff,.svg", TargetGroupId = Guid.NewGuid() }
        ];
        return state;
    }
}
