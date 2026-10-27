using System.Text.Json;
using System.Text.Json.Serialization;
using System.IO;

namespace SunAlanPet.Windows.Models;

public enum PetRole { Sun, Alan }

public sealed class ClipDefinition
{
    [JsonPropertyName("id")] public int Id { get; init; }
    [JsonPropertyName("label")] public string Label { get; init; } = "";
    [JsonPropertyName("file")] public string File { get; init; } = "";
    [JsonPropertyName("duration")] public double Duration { get; init; }
    [JsonPropertyName("answer")] public bool Answer { get; init; }
    [JsonPropertyName("defaultOff")] public bool DefaultOff { get; init; }

    public override string ToString() => $"{Id:00}  {Label}";
}

public sealed class PetDefinition
{
    public string Key { get; }
    public string DisplayName { get; }
    public string AssetDirectory { get; }
    public PetRole Role { get; }
    public IReadOnlyList<ClipDefinition> Clips { get; }
    public string FacePath { get; }

    private PetDefinition(string key, string displayName, string directory, PetRole role,
        IReadOnlyList<ClipDefinition> clips)
    {
        Key = key;
        DisplayName = displayName;
        AssetDirectory = directory;
        Role = role;
        Clips = clips;
        FacePath = Path.Combine(directory, "face.png");
    }

    public static PetDefinition Load(string key, string displayName, string relativeDirectory, PetRole role)
    {
        var directory = Path.Combine(AppContext.BaseDirectory, relativeDirectory.Replace('/', Path.DirectorySeparatorChar));
        var manifest = Path.Combine(directory, "clips.json");
        if (!File.Exists(manifest))
            throw new FileNotFoundException($"Missing asset manifest: {manifest}");
        var clips = JsonSerializer.Deserialize<List<ClipDefinition>>(File.ReadAllText(manifest), new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        }) ?? throw new InvalidDataException($"Empty asset manifest: {manifest}");
        if (clips.Count == 0) throw new InvalidDataException($"No clips in: {manifest}");
        return new PetDefinition(key, displayName, directory, role, clips);
    }

    public string AudioPath(ClipDefinition clip) => Path.Combine(AssetDirectory, "Clips", clip.File);
}
