using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using LogseqGitLauncher.Core;
using LogseqGitLauncher.Core.Platform;

namespace LogseqGitLauncher;

public partial class SettingsWindow : Window
{
    private static readonly IBrush ErrorBrush = new SolidColorBrush(Color.Parse("#F27C84"));
    private static readonly IBrush WarningBrush = new SolidColorBrush(Color.Parse("#F5C26B"));

    private readonly IPlatform _platform;
    private readonly Settings _settings;
    private readonly SettingsStore _store;

    // Required by the XAML loader; the real constructor below is the one used.
    public SettingsWindow() : this(PlatformFactory.Create(), new Settings(), new SettingsStore("")) { }

    public SettingsWindow(IPlatform platform, Settings settings, SettingsStore store)
    {
        InitializeComponent();
        _platform = platform;
        _settings = settings;
        _store = store;

        LogseqLabel.Text = $"LOGSEQ ({platform.LogseqPickerName.ToUpperInvariant()})";
        Card.PointerPressed += (_, e) =>
        {
            if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
                BeginMoveDrag(e);
        };
        CloseButton.Click += (_, _) => Close(false);
        // Enter saves and Escape cancels (done by hand: Fluent restyles the default and cancel buttons).
        KeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter) Save();
            else if (e.Key == Key.Escape) Close(false);
        };
        GraphBox.Text = settings.GraphPath;
        LogseqBox.Text = string.IsNullOrWhiteSpace(settings.LogseqPath)
            ? platform.DetectLogseq() ?? ""
            : settings.LogseqPath;

        BrowseGraph.Click += async (_, _) => await PickGraphAsync();
        BrowseLogseq.Click += async (_, _) => await PickLogseqAsync();
        SaveButton.Click += (_, _) => Save();
        CancelButton.Click += (_, _) => Close(false);
        GraphBox.TextChanged += (_, _) => Validate();
        LogseqBox.TextChanged += (_, _) => Validate();
        Validate();
    }

    private async Task PickGraphAsync()
    {
        var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Select the Logseq graph folder",
            AllowMultiple = false,
        });
        if (folders.Count > 0)
            GraphBox.Text = folders[0].Path.LocalPath;
    }

    private async Task PickLogseqAsync()
    {
        if (_platform.LogseqIsFolder)
        {
            // A .app bundle is a folder as far as the file system is concerned.
            var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
            {
                Title = $"Select {_platform.LogseqPickerName}",
                AllowMultiple = false,
            });
            if (folders.Count > 0)
                LogseqBox.Text = folders[0].Path.LocalPath.TrimEnd('/');
        }
        else
        {
            var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = $"Select {_platform.LogseqPickerName}",
                AllowMultiple = false,
            });
            if (files.Count > 0)
                LogseqBox.Text = files[0].Path.LocalPath;
        }
    }

    private bool Validate()
    {
        var graph = GraphValidator.ValidateGraph(GraphBox.Text?.Trim());
        var logseqError = GraphValidator.ValidateLogseq(LogseqBox.Text?.Trim());
        var error = graph.Error ?? logseqError;

        MessageText.Text = error ?? graph.Warning ?? "";
        MessageText.Foreground = error is not null ? ErrorBrush : WarningBrush;
        SaveButton.IsEnabled = error is null;
        return error is null;
    }

    private void Save()
    {
        if (!Validate()) return;
        _settings.GraphPath = GraphBox.Text!.Trim();
        _settings.LogseqPath = LogseqBox.Text!.Trim();
        try
        {
            _store.Save(_settings);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            MessageText.Text = $"Could not save settings: {ex.Message}";
            MessageText.Foreground = ErrorBrush;
            return;
        }
        Close(true);
    }
}
