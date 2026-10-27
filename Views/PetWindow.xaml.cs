using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using SunAlanPet.Windows.Models;
using SunAlanPet.Windows.Services;

namespace SunAlanPet.Windows.Views;

public partial class PetWindow : Window
{
    private const double CaptionSlotHeight = 44;
    private readonly PetDefinition _definition;
    private readonly SettingsStore _settingsStore;
    private readonly PetSettings _settings;
    private readonly AudioPlayer _audio = new();
    private readonly BridgeService _bridge;
    private readonly Random _random = new();
    private readonly HashSet<int> _playedThisRound = new();
    private readonly HashSet<int> _companionPlayed = new();
    private Point _dragStart;
    private bool _dragging;
    private bool _didDrag;
    private bool _closing;
    private Point _windowStart;
    private DispatcherTimer? _companionTimer;

    public PetWindow? OtherPet { get; set; }

    public PetWindow(PetDefinition definition, SettingsStore settingsStore, BridgeService bridge)
    {
        InitializeComponent();
        _definition = definition;
        _settingsStore = settingsStore;
        _bridge = bridge;
        var workArea = SystemParameters.WorkArea;
        var defaultLeft = definition.Role == PetRole.Alan ? workArea.Left + workArea.Width - 230 : workArea.Left + 40;
        var defaultTop = workArea.Top + workArea.Height - 220;
        _settings = settingsStore.Get(definition.Key, defaultLeft, defaultTop);
        _audio.Volume = _settings.Volume;
        Width = Math.Clamp(_settings.Size, 72, 400);
        Height = Width + CaptionSlotHeight;
        Left = _settings.Left;
        Top = _settings.Top;
        Visibility = _settings.Visible ? Visibility.Visible : Visibility.Hidden;
        var image = new BitmapImage();
        image.BeginInit(); image.UriSource = new Uri(Path.GetFullPath(definition.FacePath));
        image.CacheOption = BitmapCacheOption.OnLoad; image.EndInit();
        PetImage.Source = image;
        _audio.PlaybackEnded += (_, _) => Dispatcher.BeginInvoke(new Action(() =>
        {
            // A rapid click can stop one clip while opening the next one. Only
            // clear the new caption if no replacement clip is still playing.
            if (!_audio.IsPlaying) StopSpeakingVisual();
        }));
        _bridge.Spoke += OnBridgeSpoke;
        Closed += (_, _) => Dispose();
    }

    private void OnLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 1)
        {
            _dragStart = GetScreenDip(e);
            _windowStart = new Point(Left, Top);
            _dragging = true;
            _didDrag = false;
            CaptureMouse();
        }
        e.Handled = true;
    }

    private void OnMouseMove(object sender, MouseEventArgs e)
    {
        if (!_dragging || e.LeftButton != MouseButtonState.Pressed) return;
        var current = GetScreenDip(e);
        var dx = current.X - _dragStart.X;
        var dy = current.Y - _dragStart.Y;
        if (Math.Abs(dx) > 4 || Math.Abs(dy) > 4) _didDrag = true;
        if (_didDrag) { Left = _windowStart.X + dx; Top = _windowStart.Y + dy; }
    }

    private void OnLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_dragging)
        {
            _dragging = false;
            ReleaseMouseCapture();
            if (!_didDrag) PlaySelectedOrRandom(announce: true);
            Persist();
        }
        e.Handled = true;
    }

    private Point GetScreenDip(MouseEventArgs e)
    {
        var screenPixels = PointToScreen(e.GetPosition(this));
        var transform = PresentationSource.FromVisual(this)?.CompositionTarget?.TransformFromDevice;
        return transform?.Transform(screenPixels) ?? screenPixels;
    }

    private void OnRightButtonUp(object sender, MouseButtonEventArgs e)
    {
        BuildMenu().IsOpen = true;
        e.Handled = true;
    }

    private ContextMenu BuildMenu()
    {
        var menu = new ContextMenu();
        var play = new MenuItem { Header = "播放方式" };
        var random = new MenuItem { Header = "随机一句", IsCheckable = true, IsChecked = _settings.Mode == PlaybackMode.Random };
        var fixedMode = new MenuItem { Header = "固定播一句", IsCheckable = true, IsChecked = _settings.Mode == PlaybackMode.Fixed };
        random.Click += (_, _) => { _settings.Mode = PlaybackMode.Random; Persist(); };
        fixedMode.Click += (_, _) => { _settings.Mode = PlaybackMode.Fixed; if (_settings.PinnedClipId is null) _settings.PinnedClipId = _definition.Clips[0].Id; Persist(); };
        play.Items.Add(random); play.Items.Add(fixedMode); menu.Items.Add(play);
        var choose = new MenuItem { Header = "挑一句" };
        foreach (var clip in _definition.Clips)
        {
            var item = new MenuItem { Header = clip.ToString(), Tag = clip.Id, IsCheckable = true,
                IsChecked = _settings.PinnedClipId == clip.Id };
            item.Click += (_, _) => { _settings.PinnedClipId = clip.Id; _settings.Mode = PlaybackMode.Fixed; Persist(); PlayClip(clip, true); };
            choose.Items.Add(item);
        }
        menu.Items.Add(choose);
        var rotation = new MenuItem { Header = "轮播内容" };
        var enabled = _settings.EnabledClipIds ?? _definition.Clips.Where(x => !x.DefaultOff).Select(x => x.Id).ToHashSet();
        foreach (var clip in _definition.Clips)
        {
            var item = new MenuItem { Header = clip.ToString(), IsCheckable = true, IsChecked = enabled.Contains(clip.Id) };
            item.Click += (_, _) =>
            {
                _settings.EnabledClipIds ??= _definition.Clips.Where(x => !x.DefaultOff).Select(x => x.Id).ToHashSet();
                if (item.IsChecked) _settings.EnabledClipIds.Add(clip.Id);
                else if (_settings.EnabledClipIds.Count > 1) _settings.EnabledClipIds.Remove(clip.Id);
                else item.IsChecked = true;
                Persist();
            };
            rotation.Items.Add(item);
        }
        menu.Items.Add(rotation);
        if (_definition.Role == PetRole.Sun)
        {
            var bridge = new MenuItem { Header = "捧哏联动", IsCheckable = true, IsChecked = _settings.BridgeEnabled };
            bridge.Click += (_, _) =>
            {
                _settings.BridgeEnabled = bridge.IsChecked;
                if (!bridge.IsChecked) _companionTimer?.Stop();
                Persist();
            };
            menu.Items.Add(bridge);
        }
        var volume = new MenuItem { Header = $"音量 {_settings.Volume:P0}" };
        foreach (var value in new[] { 0.25, 0.5, 0.75, 1.0 }) { var item = new MenuItem { Header = $"{value:P0}", IsCheckable = true, IsChecked = Math.Abs(_settings.Volume - value) < 0.01 }; item.Click += (_, _) => { _settings.Volume = value; _audio.Volume = value; Persist(); }; volume.Items.Add(item); }
        menu.Items.Add(volume);
        var size = new MenuItem { Header = "人物大小" };
        foreach (var value in new[] { 100d, 150d, 200d, 260d }) { var item = new MenuItem { Header = $"{value:0} px", IsCheckable = true, IsChecked = Math.Abs(Width - value) < 1 }; item.Click += (_, _) => { Width = value; Height = value + CaptionSlotHeight; _settings.Size = value; Persist(); }; size.Items.Add(item); }
        menu.Items.Add(size);
        var reset = new MenuItem { Header = "移回屏幕右下角" }; reset.Click += (_, _) => { var wa = SystemParameters.WorkArea; Left = wa.Right - Width - 40; Top = wa.Bottom - Height - 40; Persist(); }; menu.Items.Add(reset);
        var visibility = new MenuItem { Header = "隐藏人物" }; visibility.Click += (_, _) => { if (OtherPet is not null && OtherPet.Visibility != Visibility.Visible) OtherPet.SetVisible(true); SetVisible(false); }; menu.Items.Add(visibility);
        if (OtherPet is not null)
        {
            var otherVisibility = new MenuItem { Header = $"{(OtherPet._definition.Role == PetRole.Alan ? "邵艾伦" : "孙宇晨")}：{(OtherPet.Visibility == Visibility.Visible ? "隐藏" : "显示")}" };
            otherVisibility.Click += (_, _) =>
            {
                OtherPet.SetVisible(OtherPet.Visibility != Visibility.Visible);
            };
            menu.Items.Add(otherVisibility);
        }
        menu.Items.Add(new Separator());
        var quit = new MenuItem { Header = "退出" }; quit.Click += (_, _) => { _closing = true; Application.Current.Shutdown(); }; menu.Items.Add(quit);
        return menu;
    }

    private void PlaySelectedOrRandom(bool announce)
    {
        ClipDefinition? clip = null;
        if (_settings.Mode == PlaybackMode.Fixed && _settings.PinnedClipId is int pinned)
            clip = _definition.Clips.FirstOrDefault(x => x.Id == pinned);
        clip ??= NextRandomClip();
        if (clip is not null) PlayClip(clip, announce);
    }

    private ClipDefinition? NextRandomClip()
    {
        var enabled = _settings.EnabledClipIds;
        var candidates = _definition.Clips.Where(x => enabled is null ? !x.DefaultOff : enabled.Contains(x.Id)).ToList();
        if (enabled is null && _definition.Role == PetRole.Sun) candidates = _definition.Clips.ToList();
        if (candidates.Count == 0) candidates = _definition.Clips.ToList();
        if (_playedThisRound.Count >= candidates.Count) _playedThisRound.Clear();
        var remaining = candidates.Where(x => !_playedThisRound.Contains(x.Id)).ToList();
        if (remaining.Count == 0) return null;
        var clip = remaining[_random.Next(remaining.Count)]; _playedThisRound.Add(clip.Id); return clip;
    }

    private void PlayClip(ClipDefinition clip, bool announce)
    {
        StopSpeakingVisual();
        if (!_audio.Play(_definition.AudioPath(clip))) return;
        StartSpeakingVisual(clip.Label);
        if (announce) _bridge.Announce(_definition.Role, clip);
    }

    private void OnBridgeSpoke(object? sender, PetSpoke signal)
    {
        if (_definition.Role != PetRole.Sun || !_settings.BridgeEnabled || signal.From != PetRole.Alan || _audio.IsPlaying) return;
        if (signal.Hop >= 1) return;
        var answerPool = _definition.Clips.Where(x => CompanionTiming.CompanionClipIds.Contains(x.Id)).ToList();
        if (answerPool.Count == 0) return;
        if (_companionPlayed.Count >= answerPool.Count) _companionPlayed.Clear();
        var answer = answerPool.Where(x => !_companionPlayed.Contains(x.Id)).OrderBy(_ => _random.Next()).First();
        _companionPlayed.Add(answer.Id);
        var lead = CompanionTiming.LeadingSilenceSecondsFor(answer);
        var delay = Math.Max(0.02, signal.Clip.Duration - CompanionTiming.OverlapSeconds - lead);
        _companionTimer?.Stop();
        _companionTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(delay) };
        _companionTimer.Tick += (_, _) => { _companionTimer!.Stop(); if (!_audio.IsPlaying) { PlayClip(answer, false); _bridge.Announce(PetRole.Sun, answer, signal.Hop + 1, signal.Root); } };
        _companionTimer.Start();
    }

    private void Persist()
    {
        _settings.Left = Left;
        _settings.Top = Top;
        _settings.Size = Width;
        _settingsStore.Save(_definition.Key, _settings);
    }

    public void SetVisible(bool visible)
    {
        if (!visible)
        {
            _companionTimer?.Stop();
            _audio.Stop();
            StopSpeakingVisual();
        }
        Visibility = visible ? Visibility.Visible : Visibility.Hidden;
        _settings.Visible = visible;
        Persist();
    }

    private void StartSpeakingVisual(string label)
    {
        CaptionText.Text = label;
        CaptionBubble.Visibility = Visibility.Visible;

        var rock = new DoubleAnimation(-3.2, 3.2, TimeSpan.FromSeconds(0.28))
        {
            AutoReverse = true,
            RepeatBehavior = RepeatBehavior.Forever
        };
        PetRotation.BeginAnimation(RotateTransform.AngleProperty, rock);

        var bob = new DoubleAnimation(0, 3, TimeSpan.FromSeconds(0.28))
        {
            AutoReverse = true,
            RepeatBehavior = RepeatBehavior.Forever
        };
        PetTranslation.BeginAnimation(TranslateTransform.YProperty, bob);
    }

    private void StopSpeakingVisual()
    {
        PetRotation.BeginAnimation(RotateTransform.AngleProperty, null);
        PetTranslation.BeginAnimation(TranslateTransform.YProperty, null);
        PetRotation.Angle = 0;
        PetTranslation.Y = 0;
        CaptionBubble.Visibility = Visibility.Collapsed;
        CaptionText.Text = string.Empty;
    }

    private void Dispose()
    {
        if (!_closing) Persist();
        _bridge.Spoke -= OnBridgeSpoke; _companionTimer?.Stop(); StopSpeakingVisual(); _audio.Dispose();
    }
}
