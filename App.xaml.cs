using System.Windows;
using System.IO;
using SunAlanPet.Windows.Models;
using SunAlanPet.Windows.Services;
using SunAlanPet.Windows.Views;

namespace SunAlanPet.Windows;

public partial class App : Application
{
    private SettingsStore? _settings;
    private PetWindow? _sunWindow;
    private PetWindow? _alanWindow;
    private BridgeService? _bridge;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        try
        {
            var sun = PetDefinition.Load("Sun", "太阳/太对了", "Assets/SunPet", PetRole.Sun);
            var alan = PetDefinition.Load("Alan", "邵艾伦/薄肌", "Assets/ShaolanPet", PetRole.Alan);
            if (e.Args.Any(x => string.Equals(x, "--self-test", StringComparison.OrdinalIgnoreCase)))
            {
                var resultPath = e.Args.SkipWhile(x => !string.Equals(x, "--self-test-result", StringComparison.OrdinalIgnoreCase)).Skip(1).FirstOrDefault()
                    ?? Path.Combine(Path.GetTempPath(), $"SunAlanPet-self-test-{Environment.ProcessId}.txt");
                _ = CompleteSelfTestAsync(sun, alan, resultPath);
                return;
            }

            _settings = new SettingsStore();
            _bridge = new BridgeService();
            _sunWindow = new PetWindow(sun, _settings, _bridge);
            _alanWindow = new PetWindow(alan, _settings, _bridge);
            _sunWindow.OtherPet = _alanWindow;
            _alanWindow.OtherPet = _sunWindow;
            _sunWindow.Closed += (_, _) => OnPetClosed(_alanWindow);
            _alanWindow.Closed += (_, _) => OnPetClosed(_sunWindow);
            _sunWindow.Show();
            _alanWindow.Show();

            // A stale settings file must not strand both transparent windows
            // hidden; the visible pet's menu is the recovery path thereafter.
            if (_sunWindow.Visibility != Visibility.Visible && _alanWindow.Visibility != Visibility.Visible)
            {
                _sunWindow.SetVisible(true);
            }
        }
        catch (Exception ex)
        {
            File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "startup-error.txt"), ex.ToString());
            Shutdown(1);
        }
    }

    private async Task CompleteSelfTestAsync(PetDefinition sun, PetDefinition alan, string resultPath)
    {
        try
        {
            var bridge = new BridgeService();
            var received = false;
            bridge.Spoke += (_, signal) => received = signal.From == PetRole.Alan && signal.Clip.Id == alan.Clips[0].Id;
            bridge.Announce(PetRole.Alan, alan.Clips[0]);
            if (!received) throw new InvalidOperationException("Bridge dispatch failed");
            var expected = new[] { (sun, 28), (alan, 37) };
            foreach (var (pet, count) in expected)
            {
                if (pet.Clips.Count != count) throw new InvalidDataException($"{pet.Key} clip count {pet.Clips.Count}, expected {count}");
                if (!File.Exists(pet.FacePath)) throw new FileNotFoundException(pet.FacePath);
                foreach (var clip in pet.Clips)
                    if (!File.Exists(pet.AudioPath(clip))) throw new FileNotFoundException(pet.AudioPath(clip));
            }
            foreach (var id in CompanionTiming.CompanionClipIds)
            {
                var clip = sun.Clips.FirstOrDefault(x => x.Id == id)
                    ?? throw new InvalidDataException($"Missing Sun companion clip {id}");
                var lead = CompanionTiming.LeadingSilenceSecondsFor(clip);
                if (lead < 0 || lead > clip.Duration * 0.6)
                    throw new InvalidDataException($"Invalid companion lead for clip {id}: {lead:0.######}s");
            }
            using var sunAudio = new AudioPlayer();
            using var alanAudio = new AudioPlayer();
            if (!await sunAudio.PlayAndWaitForOpenAsync(sun.AudioPath(sun.Clips[0]), TimeSpan.FromSeconds(3))) throw new InvalidOperationException("Sun m4a MediaOpened/MediaFailed check failed");
            sunAudio.Stop();
            if (!await alanAudio.PlayAndWaitForOpenAsync(alan.AudioPath(alan.Clips[0]), TimeSpan.FromSeconds(3))) throw new InvalidOperationException("Alan wav MediaOpened/MediaFailed check failed");
            alanAudio.Stop();
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(resultPath))!);
            File.WriteAllText(resultPath, $"PASS assets Sun={sun.Clips.Count}, Alan={alan.Clips.Count}; bridge=ok; companion-lead={CompanionTiming.CompanionClipIds.Count}; media=sun-m4a-opened,alan-wav-opened");
            Shutdown(0);
        }
        catch (Exception ex)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(resultPath))!);
            File.WriteAllText(resultPath, "FAIL " + ex);
            Shutdown(1);
        }
    }

    private void OnPetClosed(PetWindow? other)
    {
        if (other is { IsLoaded: true }) other.Close();
        Shutdown();
    }
}
