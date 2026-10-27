using System.Text.Json;
using System.IO;
using SunAlanPet.Windows.Models;

namespace SunAlanPet.Windows.Services;

public sealed class SettingsStore
{
    private readonly string _path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "SunAlanPetWindows", "settings.json");
    private readonly object _gate = new();
    private SettingsFile _file;

    public SettingsStore()
    {
        try
        {
            var json = File.Exists(_path) ? File.ReadAllText(_path) : "";
            _file = string.IsNullOrWhiteSpace(json)
                ? new SettingsFile()
                : JsonSerializer.Deserialize<SettingsFile>(json) ?? new SettingsFile();
        }
        catch { _file = new SettingsFile(); }
    }

    public PetSettings Get(string key, double defaultLeft, double defaultTop)
    {
        lock (_gate)
        {
            if (!_file.Pets.TryGetValue(key, out var value))
            {
                value = new PetSettings { Left = defaultLeft, Top = defaultTop };
                _file.Pets[key] = value;
            }
            return value;
        }
    }

    public void Save(string key, PetSettings settings)
    {
        lock (_gate)
        {
            _file.Pets[key] = settings;
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
                var temp = _path + ".tmp";
                File.WriteAllText(temp, JsonSerializer.Serialize(_file, new JsonSerializerOptions { WriteIndented = true }));
                File.Move(temp, _path, true);
            }
            catch { /* settings are best effort and must never stop the pet */ }
        }
    }
}
