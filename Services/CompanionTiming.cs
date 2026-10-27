using SunAlanPet.Windows.Models;

namespace SunAlanPet.Windows.Services;

/// <summary>
/// Timing metadata derived from the unchanged SunPet companion clips.
/// The original macOS implementation measures each clip's leading silence
/// before applying the 0.3-second overlap. Keeping the measured values here
/// avoids a runtime decoder dependency while preserving that behavior on a
/// self-contained Windows build.
/// </summary>
public static class CompanionTiming
{
    public const double OverlapSeconds = 0.3;

    private static readonly IReadOnlyDictionary<int, double> LeadingSilenceSeconds =
        new Dictionary<int, double>
        {
            [1] = 0.250312,
            [2] = 0.00297917,
            [4] = 0.00110417,
            [11] = 0.0579375,
            [13] = 0.124333,
            [14] = 0.321604
        };

    public static IReadOnlyCollection<int> CompanionClipIds { get; } = LeadingSilenceSeconds.Keys.ToArray();

    public static double LeadingSilenceSecondsFor(ClipDefinition clip)
    {
        return LeadingSilenceSeconds.TryGetValue(clip.Id, out var value) ? value : 0;
    }
}
