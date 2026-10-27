namespace SunAlanPet.Windows.Models;

public sealed class PetSettings
{
    public double Left { get; set; }
    public double Top { get; set; }
    public double Size { get; set; } = 150;
    public double Volume { get; set; } = 1;
    public PlaybackMode Mode { get; set; } = PlaybackMode.Random;
    public int? PinnedClipId { get; set; }
    public bool Visible { get; set; } = true;
    public HashSet<int>? EnabledClipIds { get; set; }
    public bool BridgeEnabled { get; set; } = true;
}

public enum PlaybackMode { Random, Fixed }

public sealed class SettingsFile
{
    public Dictionary<string, PetSettings> Pets { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}
