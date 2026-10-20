using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using UvcCaptureSim.Core.Configuration;
using UvcCaptureSim.Core.Engine;

namespace UvcCaptureSim.App.ViewModels;

public partial class MainViewModel : ViewModelBase, IDisposable
{
    private readonly SimulatorConfig _config;
    private readonly SynchronizationContext? _ui = SynchronizationContext.Current;
    private readonly CancellationTokenSource _lifetime = new();
    private int _disposed;

    public SimulatorEngine Engine { get; } = new();

    public ObservableCollection<MediaItem> Playlist { get; } = [];

    public IReadOnlyList<VirtualCameraBackend> Backends { get; } = Enum.GetValues<VirtualCameraBackend>();

    public IReadOnlyList<ScalingMode> ScalingModes { get; } = Enum.GetValues<ScalingMode>();

    /// <summary>Set by the window. True asks for images, false asks for videos.</summary>
    public Func<bool, Task<IReadOnlyList<string>>>? PickFilesAsync { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsIdle))]
    public partial bool IsRunning { get; set; }

    public bool IsIdle => !IsRunning;

    [ObservableProperty]
    public partial int Width { get; set; } = 1280;

    [ObservableProperty]
    public partial int Height { get; set; } = 720;

    [ObservableProperty]
    public partial double FramesPerSecond { get; set; } = 30;

    [ObservableProperty]
    public partial double SecondsPerImage { get; set; } = 5;

    [ObservableProperty]
    public partial bool LoopPlaylist { get; set; } = true;

    [ObservableProperty]
    public partial bool ShowOverlay { get; set; } = true;

    [ObservableProperty]
    public partial VirtualCameraBackend Backend { get; set; } = VirtualCameraBackend.DirectShowSoftcam;

    [ObservableProperty]
    public partial ScalingMode Scaling { get; set; } = ScalingMode.Letterbox;

    [ObservableProperty]
    public partial MediaItem? SelectedItem { get; set; }

    [ObservableProperty]
    public partial string StatusText { get; set; } = "Idle — configure a playlist and press Start.";

    [ObservableProperty]
    public partial string ConnectionText { get; set; } = string.Empty;

    public MainViewModel()
    {
        _config = SimulatorConfigStore.Load();
        Width = _config.Width;
        Height = _config.Height;
        FramesPerSecond = _config.FramesPerSecond;
        SecondsPerImage = _config.SecondsPerImage;
        LoopPlaylist = _config.LoopPlaylist;
        ShowOverlay = _config.ShowOverlay;
        Backend = _config.Backend;
        Scaling = _config.Scaling;
        foreach (var item in _config.Playlist)
            Playlist.Add(item);
        if (Playlist.Count == 0)
            Playlist.Add(new MediaItem { Kind = MediaKind.TestPattern });

        Engine.StatusChanged += OnEngineStatus;
        _ = WatchStatusAsync(_lifetime.Token);
    }

    [RelayCommand(CanExecute = nameof(CanStart))]
    private void Start()
    {
        ApplyToConfig();
        try
        {
            SimulatorConfigStore.Save(_config);
        }
        catch (Exception ex)
        {
            StatusText = "Could not save settings. " + ex.Message;
        }

        Engine.Start(_config);
        RefreshRuntimeStatus();
    }

    [RelayCommand(CanExecute = nameof(CanStop))]
    private async Task Stop()
    {
        await Engine.StopAsync();
        RefreshRuntimeStatus();
    }

    [RelayCommand]
    private async Task AddImages()
    {
        if (PickFilesAsync is null)
            return;

        foreach (var path in await PickFilesAsync(true))
            Playlist.Add(new MediaItem { Kind = MediaKind.Image, Path = path });
    }

    [RelayCommand]
    private async Task AddVideos()
    {
        if (PickFilesAsync is null)
            return;

        foreach (var path in await PickFilesAsync(false))
            Playlist.Add(new MediaItem { Kind = MediaKind.Video, Path = path });
    }

    [RelayCommand]
    private void AddTestPattern() => Playlist.Add(new MediaItem { Kind = MediaKind.TestPattern });

    [RelayCommand]
    private void RemoveSelected()
    {
        if (SelectedItem is not null)
            Playlist.Remove(SelectedItem);
    }

    [RelayCommand]
    private void MoveUp()
    {
        var index = SelectedItem is null ? -1 : Playlist.IndexOf(SelectedItem);
        if (index > 0)
            Playlist.Move(index, index - 1);
    }

    [RelayCommand]
    private void MoveDown()
    {
        var index = SelectedItem is null ? -1 : Playlist.IndexOf(SelectedItem);
        if (index >= 0 && index < Playlist.Count - 1)
            Playlist.Move(index, index + 1);
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 1)
            return;

        Engine.StatusChanged -= OnEngineStatus;
        _lifetime.Cancel();
        Engine.Dispose();
        _lifetime.Dispose();
    }

    partial void OnIsRunningChanged(bool value)
    {
        StartCommand.NotifyCanExecuteChanged();
        StopCommand.NotifyCanExecuteChanged();
    }

    private bool CanStart() => !IsRunning;

    private bool CanStop() => IsRunning;

    private void OnEngineStatus(string message) => Post(() => StatusText = message);

    private async Task WatchStatusAsync(CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));
        try
        {
            while (await timer.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false))
                Post(RefreshRuntimeStatus);
        }
        catch (OperationCanceledException)
        {
            // Window closed.
        }
    }

    private void RefreshRuntimeStatus()
    {
        if (Volatile.Read(ref _disposed) == 1)
            return;

        IsRunning = Engine.IsRunning;
        ConnectionText = Engine.IsRunning
            ? $"{Engine.BackendName}  ·  {(Engine.IsConnected ? "viewer connected" : "waiting for a viewer")}  ·  {Engine.CurrentItemName}"
            : string.Empty;
    }

    private void ApplyToConfig()
    {
        Width = Math.Clamp(Width, 4, 3840);
        Height = Math.Clamp(Height, 4, 2160);
        if (FramesPerSecond is <= 0 or > 240)
            FramesPerSecond = 30;
        if (SecondsPerImage <= 0)
            SecondsPerImage = 5;

        _config.Width = Width;
        _config.Height = Height;
        _config.FramesPerSecond = FramesPerSecond;
        _config.SecondsPerImage = SecondsPerImage;
        _config.LoopPlaylist = LoopPlaylist;
        _config.ShowOverlay = ShowOverlay;
        _config.Backend = Backend;
        _config.Scaling = Scaling;
        _config.Playlist = Playlist.ToList();
    }

    private void Post(Action action)
    {
        if (Volatile.Read(ref _disposed) == 1)
            return;

        if (_ui is null || SynchronizationContext.Current == _ui)
            action();
        else
            _ui.Post(_ =>
            {
                if (Volatile.Read(ref _disposed) == 0)
                    action();
            }, null);
    }
}
