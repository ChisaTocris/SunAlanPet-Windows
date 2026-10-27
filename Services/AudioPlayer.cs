using System.Windows.Media;
using System.IO;
using SunAlanPet.Windows.Models;

namespace SunAlanPet.Windows.Services;

public sealed class AudioPlayer : IDisposable
{
    private readonly MediaPlayer _player = new();
    private readonly object _gate = new();
    private bool _playing;
    private double _volume = 1;

    public bool IsPlaying { get { lock (_gate) return _playing; } }
    public event EventHandler? PlaybackEnded;

    public AudioPlayer()
    {
        _player.MediaEnded += (_, _) =>
        {
            lock (_gate) _playing = false;
            PlaybackEnded?.Invoke(this, EventArgs.Empty);
        };
        _player.MediaFailed += (_, _) =>
        {
            lock (_gate) _playing = false;
            PlaybackEnded?.Invoke(this, EventArgs.Empty);
        };
    }

    public double Volume
    {
        get => _volume;
        set { _volume = Math.Clamp(value, 0, 1); _player.Volume = _volume; }
    }

    public bool Play(string path)
    {
        if (!File.Exists(path)) return false;
        Stop();
        try
        {
            _player.Open(new Uri(path, UriKind.Absolute));
            _player.Volume = _volume;
            lock (_gate) _playing = true;
            _player.Play();
            return true;
        }
        catch
        {
            lock (_gate) _playing = false;
            return false;
        }
    }

    public async Task<bool> PlayAndWaitForOpenAsync(string path, TimeSpan timeout)
    {
        if (!File.Exists(path)) return false;
        Stop();
        var opened = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        void OnOpened(object? _, EventArgs __) => opened.TrySetResult(true);
        void OnFailed(object? _, ExceptionEventArgs __) => opened.TrySetResult(false);
        _player.MediaOpened += OnOpened;
        _player.MediaFailed += OnFailed;
        try
        {
            _player.Open(new Uri(path, UriKind.Absolute));
            _player.Volume = _volume;
            lock (_gate) _playing = true;
            _player.Play();
            var completed = await Task.WhenAny(opened.Task, Task.Delay(timeout));
            return completed == opened.Task && await opened.Task;
        }
        catch { return false; }
        finally
        {
            _player.MediaOpened -= OnOpened;
            _player.MediaFailed -= OnFailed;
        }
    }

    public void Stop()
    {
        try { _player.Stop(); } catch { }
        lock (_gate) _playing = false;
    }

    public void Dispose() { Stop(); _player.Close(); }
}
