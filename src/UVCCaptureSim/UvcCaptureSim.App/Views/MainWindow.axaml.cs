using Avalonia.Controls;
using Avalonia.Platform.Storage;
using UvcCaptureSim.App.ViewModels;

namespace UvcCaptureSim.App.Views;

public partial class MainWindow : Window
{
    private readonly PreviewPresenter _preview;
    private MainViewModel? _viewModel;

    public MainWindow()
    {
        InitializeComponent();
        _preview = new PreviewPresenter(LiveImage);
        DataContextChanged += OnDataContextChanged;
    }

    protected override void OnClosed(EventArgs e)
    {
        if (_viewModel is not null)
        {
            _viewModel.Engine.FrameRendered -= _preview.OnFrameRendered;
            _viewModel.PickFilesAsync = null;
        }

        _preview.Dispose();
        _viewModel?.Dispose();
        _viewModel = null;
        base.OnClosed(e);
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (_viewModel is not null)
        {
            _viewModel.Engine.FrameRendered -= _preview.OnFrameRendered;
            _viewModel.PickFilesAsync = null;
        }

        _viewModel = DataContext as MainViewModel;
        if (_viewModel is null)
            return;

        _viewModel.PickFilesAsync = PickFilesAsync;
        _viewModel.Engine.FrameRendered += _preview.OnFrameRendered;
    }

    private async Task<IReadOnlyList<string>> PickFilesAsync(bool images)
    {
        var storage = StorageProvider;
        if (storage is null)
            return [];

        var fileType = images
            ? new FilePickerFileType("Images")
            {
                Patterns = ["*.png", "*.jpg", "*.jpeg", "*.bmp", "*.gif", "*.webp"],
            }
            : new FilePickerFileType("Videos")
            {
                Patterns = ["*.mp4", "*.mov", "*.mkv", "*.avi", "*.webm", "*.m4v"],
            };

        var files = await storage.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            AllowMultiple = true,
            Title = images ? "Add images" : "Add videos",
            FileTypeFilter = [fileType],
        });

        var paths = new List<string>();
        foreach (var file in files)
        {
            var path = file.TryGetLocalPath();
            if (!string.IsNullOrEmpty(path))
                paths.Add(path);
        }

        return paths;
    }
}
