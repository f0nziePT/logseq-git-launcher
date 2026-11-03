using System.Diagnostics;
using LogseqGitLauncher.Core;
using LogseqGitLauncher.Core.Platform;
using Xunit;

namespace LogseqGitLauncher.Core.Tests;

public sealed class SyncWorkflowTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "lgl-" + Guid.NewGuid().ToString("N"));
    private readonly string _remote;
    private readonly string _graph;
    private readonly List<string> _log = [];

    public SyncWorkflowTests()
    {
        _remote = Path.Combine(_root, "remote.git");
        _graph = Path.Combine(_root, "graph");
        Directory.CreateDirectory(_root);
        Git(_root, "init", "--bare", "-b", "main", _remote);
        Git(_root, "clone", _remote, _graph);
        Git(_graph, "config", "user.email", "t@example.com");
        Git(_graph, "config", "user.name", "Test");
        Directory.CreateDirectory(Path.Combine(_graph, "pages"));
        File.WriteAllText(Path.Combine(_graph, "pages", "a.md"), "first");
        Git(_graph, "add", "-A");
        Git(_graph, "commit", "-m", "init");
        Git(_graph, "push", "-u", "origin", "HEAD:main");
    }

    [Fact]
    public async Task Edits_made_while_logseq_runs_are_committed_and_pushed()
    {
        var platform = new FakePlatform(() => File.WriteAllText(Path.Combine(_graph, "pages", "b.md"), "new"));
        var wf = new SyncWorkflow(new Settings { GraphPath = _graph, LogseqPath = "x" }, platform, _log.Add);

        Assert.True((await wf.PullAsync()).Ok);
        Assert.True((await wf.OpenLogseqAsync()).Ok);
        Assert.True((await wf.CommitAndPushAsync()).Ok);

        Assert.Equal(GitOut(_graph, "rev-parse", "HEAD"), GitOut(_remote, "rev-parse", "main"));
        Assert.StartsWith("Sync ", GitOut(_graph, "log", "-1", "--format=%s"));
    }

    [Fact]
    public async Task No_changes_means_no_new_commit()
    {
        var before = GitOut(_graph, "rev-parse", "HEAD");
        var wf = new SyncWorkflow(new Settings { GraphPath = _graph }, new FakePlatform(() => { }), _log.Add);

        Assert.True((await wf.CommitAndPushAsync()).Ok);
        Assert.Equal(before, GitOut(_graph, "rev-parse", "HEAD"));
    }

    [Fact]
    public async Task Pull_fails_when_branches_have_diverged()
    {
        // Another clone pushes a commit, then the local graph commits something different.
        var other = Path.Combine(_root, "other");
        Git(_root, "clone", _remote, other);
        Git(other, "config", "user.email", "o@example.com");
        Git(other, "config", "user.name", "Other");
        File.WriteAllText(Path.Combine(other, "x.md"), "x");
        Git(other, "add", "-A");
        Git(other, "commit", "-m", "other");
        Git(other, "push");
        File.WriteAllText(Path.Combine(_graph, "y.md"), "y");
        Git(_graph, "add", "-A");
        Git(_graph, "commit", "-m", "local");

        var wf = new SyncWorkflow(new Settings { GraphPath = _graph }, new FakePlatform(() => { }), _log.Add);
        Assert.False((await wf.PullAsync()).Ok);
    }

    [Fact]
    public async Task Refuses_to_start_when_logseq_is_already_running()
    {
        var wf = new SyncWorkflow(new Settings { GraphPath = _graph }, new FakePlatform(() => { }) { Running = true }, _log.Add);
        Assert.False((await wf.OpenLogseqAsync()).Ok);
    }

    [Fact]
    public void Validator_requires_a_git_folder()
    {
        Assert.True(GraphValidator.ValidateGraph(_graph).IsValid);
        Assert.Null(GraphValidator.ValidateGraph(_graph).Warning);
        Assert.False(GraphValidator.ValidateGraph(_root).IsValid);
        Assert.False(GraphValidator.ValidateGraph("").IsValid);
    }

    [Fact]
    public void Settings_round_trip()
    {
        var store = new SettingsStore(Path.Combine(_root, "cfg"));
        Assert.Equal("", store.Load().GraphPath);
        store.Save(new Settings { GraphPath = "g", LogseqPath = "l" });
        var loaded = store.Load();
        Assert.Equal("g", loaded.GraphPath);
        Assert.Equal("l", loaded.LogseqPath);
    }

    private static void Git(string dir, params string[] args) => GitOut(dir, args);

    private static string GitOut(string dir, params string[] args)
    {
        var psi = new ProcessStartInfo("git") { WorkingDirectory = dir, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var a in args) psi.ArgumentList.Add(a);
        using var p = Process.Start(psi)!;
        var output = p.StandardOutput.ReadToEnd();
        var error = p.StandardError.ReadToEnd();
        p.WaitForExit();
        if (p.ExitCode != 0) throw new Exception($"git {string.Join(' ', args)} failed: {error}");
        return output.Trim();
    }

    public void Dispose()
    {
        try
        {
            foreach (var f in Directory.EnumerateFiles(_root, "*", SearchOption.AllDirectories))
                File.SetAttributes(f, FileAttributes.Normal);
            Directory.Delete(_root, true);
        }
        catch (IOException) { }
    }

    private sealed class FakePlatform(Action whileRunning) : IPlatform
    {
        public bool Running { get; init; }
        public string SettingsDirectory => Path.GetTempPath();
        public string GitExecutable => "git";
        public string LogseqPickerName => "fake";
        public bool LogseqIsFolder => false;
        public string? DetectLogseq() => null;
        public bool IsLogseqRunning() => Running;

        public Task LaunchAndWaitAsync(string logseqPath, string workingDirectory, CancellationToken cancellationToken)
        {
            whileRunning();
            return Task.CompletedTask;
        }
    }
}
