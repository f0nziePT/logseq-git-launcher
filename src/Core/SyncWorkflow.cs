using LogseqGitLauncher.Core.Platform;

namespace LogseqGitLauncher.Core;

public readonly record struct StepResult(bool Ok, string Message)
{
    public static StepResult Success(string message = "") => new(true, message);
    public static StepResult Failure(string message) => new(false, message);
}

/// <summary>
/// Pull, open Logseq, then commit and push when it closes. Each step is separate so the
/// caller can retry just the step that failed.
/// </summary>
public sealed class SyncWorkflow(Settings settings, IPlatform platform, Action<string> log)
{
    private readonly GitRunner _git = new(platform.GitExecutable, settings.GraphPath);

    public async Task<StepResult> PullAsync()
    {
        log("git pull --ff-only");
        var r = await _git.RunAsync("pull", "--ff-only");
        if (!string.IsNullOrEmpty(r.Output)) log(r.Output);
        return r.Success
            ? StepResult.Success()
            : StepResult.Failure("Pull failed, Logseq was not opened.");
    }

    public async Task<StepResult> OpenLogseqAsync(CancellationToken cancellationToken = default)
    {
        if (platform.IsLogseqRunning())
            return StepResult.Failure("Logseq is already running. Close it first so the sync can tell when you are done.");
        log("Opening Logseq, waiting for it to close...");
        try
        {
            await platform.LaunchAndWaitAsync(settings.LogseqPath, settings.GraphPath, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return StepResult.Failure($"Could not run Logseq: {ex.Message}");
        }
        log("Logseq closed.");
        return StepResult.Success();
    }

    /// <summary>Safe to repeat: commits only when something is staged, always pushes.</summary>
    public async Task<StepResult> CommitAndPushAsync()
    {
        var add = await _git.RunAsync("add", "-A");
        if (!add.Success) return StepResult.Failure($"git add failed:\n{add.Output}");

        // Exit code 1 means there are staged changes; anything above 1 is an error.
        var diff = await _git.RunAsync("diff", "--cached", "--quiet");
        if (diff.ExitCode == 1)
        {
            var message = "Sync " + DateTime.Now.ToString("yyyy-MM-dd HH:mm");
            log($"git commit -m \"{message}\"");
            var commit = await _git.RunAsync("commit", "-m", message);
            if (!string.IsNullOrEmpty(commit.Output)) log(commit.Output);
            if (!commit.Success) return StepResult.Failure("Commit failed.");
        }
        else if (diff.ExitCode == 0)
        {
            log("No changes to commit.");
        }
        else
        {
            return StepResult.Failure($"git diff failed:\n{diff.Output}");
        }

        log("git push");
        var push = await _git.RunAsync("push");
        if (!string.IsNullOrEmpty(push.Output)) log(push.Output);
        return push.Success
            ? StepResult.Success("Synced.")
            : StepResult.Failure("Push failed. Your changes are committed locally.");
    }
}
