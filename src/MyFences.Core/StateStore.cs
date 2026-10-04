using System.Text.Json;

namespace MyFences.Core;

public sealed class StateStore(string directory)
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };
    public string DirectoryPath { get; } = directory;
    public string LayoutPath => Path.Combine(DirectoryPath, "layout.json");
    public AppState Load(string language)
    {
        if (!File.Exists(LayoutPath)) return AppState.Create(language);
        var state = JsonSerializer.Deserialize<AppState>(File.ReadAllText(LayoutPath), Options) ?? throw new InvalidDataException("The layout is empty.");
        if (state.Version != 1 || state.Groups.Select(g => g.Id).Distinct().Count() != state.Groups.Count || state.Items.GroupBy(i => i.Path, StringComparer.OrdinalIgnoreCase).Any(g => g.Count() > 1))
            throw new InvalidDataException("The layout format is invalid or unsupported.");
        return state;
    }
    public void Save(AppState state) => WriteAtomic(LayoutPath, state);
    public static void WriteAtomic<T>(string path, T value)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temporary = path + ".tmp";
        using (var stream = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None))
        { JsonSerializer.Serialize(stream, value, Options); stream.Flush(true); }
        File.Move(temporary, path, true);
    }
}
