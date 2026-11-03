using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using LogseqGitLauncher.Core;
using LogseqGitLauncher.Core.Platform;

namespace LogseqGitLauncher;

public partial class MainWindow : Window
{
    private enum Step { Pull, Logseq, CommitPush }
    private enum State { Busy, Editing, Done, Error }

    private static readonly IBrush Teal = new SolidColorBrush(Color.Parse("#14B8A6"));
    private static readonly IBrush Violet = new SolidColorBrush(Color.Parse("#8B7CF6"));
    private static readonly IBrush Red = new SolidColorBrush(Color.Parse("#E5484D"));

    private readonly IPlatform _platform = PlatformFactory.Create();
    private readonly SettingsStore _store;
    private readonly DispatcherTimer _clock = new() { Interval = TimeSpan.FromSeconds(1) };
    private DateTime _editingSince;
    private Settings _settings;
    private bool _running;
    private bool _hasRun;
    private Step _failedStep;

    public MainWindow()
    {
        InitializeComponent();
        // LOGSEQ_LAUNCHER_DATA lets you keep a separate set of settings (also handy for testing).
        _store = new SettingsStore(Environment.GetEnvironmentVariable("LOGSEQ_LAUNCHER_DATA") is { Length: > 0 } dir
            ? dir
            : _platform.SettingsDirectory);
        _settings = _store.Load();

        Card.PointerPressed += (_, e) =>
        {
            if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
                BeginMoveDrag(e);
        };
        _clock.Tick += (_, _) => TitleText.Text = FormatElapsed(DateTime.Now - _editingSince);

        RetryButton.Click += async (_, _) => await RunAsync(_failedStep);
        SettingsButton.Click += async (_, _) => await OpenSettingsAsync();
        MinimizeButton.Click += (_, _) => WindowState = WindowState.Minimized;
        DetailsButton.Click += (_, _) => ShowDetails(!LogPanel.IsVisible);
        CloseButton.Click += (_, _) => Close();
        Opened += async (_, _) => await StartAsync();
        Closing += (_, e) =>
        {
            // Closing while Logseq is open would skip the commit and push.
            if (!_running) return;
            Log("Sync is still running; it finishes after Logseq is closed.");
            ShowDetails(true);
            e.Cancel = true;
        };
    }

    private async Task StartAsync()
    {
        if (!SettingsAreValid() && !await OpenSettingsAsync())
        {
            SetState(State.Error, "Setup needed", "Choose the graph folder in Settings to begin.");
            return;
        }
        await RunAsync(Step.Pull);
    }

    private bool SettingsAreValid() =>
        GraphValidator.ValidateGraph(_settings.GraphPath).IsValid
        && GraphValidator.ValidateLogseq(_settings.LogseqPath) is null;

    private async Task<bool> OpenSettingsAsync()
    {
        if (_running) return false;
        var dialog = new SettingsWindow(_platform, _settings, _store);
        var saved = await dialog.ShowDialog<bool>(this);
        if (saved)
        {
            _settings = _store.Load();
            // Settings were the missing piece: continue from where the sync stopped.
            if (!_hasRun) await RunAsync(Step.Pull);
            else if (RetryButton.IsVisible) await RunAsync(_failedStep);
        }
        return saved;
    }

    private async Task RunAsync(Step from)
    {
        if (_running) return;
        _running = true;
        _hasRun = true;
        RetryButton.IsVisible = false;
        SettingsButton.IsEnabled = false;
        try
        {
            var workflow = new SyncWorkflow(_settings, _platform, Log);

            if (from <= Step.Pull)
            {
                SetState(State.Busy, "Pulling", "Getting the latest changes");
                if (!await Report(workflow.PullAsync(), Step.Pull)) return;
            }
            if (from <= Step.Logseq)
            {
                _editingSince = DateTime.Now;
                SetState(State.Editing, FormatElapsed(TimeSpan.Zero), "Logseq is open. Close it to sync your changes.");
                _clock.Start();
                WindowState = WindowState.Minimized;
                var opened = await workflow.OpenLogseqAsync();
                _clock.Stop();
                WindowState = WindowState.Normal;
                Activate();
                if (!await Report(Task.FromResult(opened), Step.Logseq)) return;
            }
            SetState(State.Busy, "Syncing", "Committing and pushing");
            if (!await Report(workflow.CommitAndPushAsync(), Step.CommitPush)) return;

            SetState(State.Done, "Synced", $"Last sync at {DateTime.Now:HH:mm}");
        }
        finally
        {
            _running = false;
            SettingsButton.IsEnabled = true;
        }
    }

    /// <summary>Shows a failure and offers Retry; returns whether the step succeeded.</summary>
    private async Task<bool> Report(Task<StepResult> task, Step step)
    {
        var result = await task;
        if (result.Ok) return true;
        _failedStep = step;
        Log(result.Message);
        SetState(State.Error, "Needs attention", result.Message);
        RetryButton.IsVisible = true;
        ShowDetails(true);
        return false;
    }

    private void SetState(State state, string title, string subtitle)
    {
        TitleText.Text = title;
        SubtitleText.Text = subtitle;
        Ring.Stroke = state switch
        {
            State.Error => Red,
            State.Editing => Violet,
            _ => Teal,
        };
        var spinning = state == State.Busy;
        Ring.Classes.Set("spin", spinning);
        // A full circle for a settled state; a short arc that rotates while busy.
        Ring.SweepAngle = spinning ? 100 : state == State.Editing ? 270 : 359.9;
    }

    private void ShowDetails(bool show)
    {
        LogPanel.IsVisible = show;
        Chevron.RenderTransform = new ScaleTransform(1, show ? -1 : 1);
        ToolTip.SetTip(DetailsButton, show ? "Hide details" : "Show details");
    }

    private static string FormatElapsed(TimeSpan t) => $"{(int)t.TotalHours:00}:{t.Minutes:00}:{t.Seconds:00}";

    private void Log(string line) => Dispatcher.UIThread.Post(() =>
    {
        LogBox.Text += (LogBox.Text?.Length > 0 ? Environment.NewLine : "") + line;
        LogBox.CaretIndex = LogBox.Text!.Length;
    });
}
