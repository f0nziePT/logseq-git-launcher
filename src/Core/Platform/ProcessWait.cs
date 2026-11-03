using System.Diagnostics;

namespace LogseqGitLauncher.Core.Platform;

public static class ProcessWait
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(1);

    /// <summary>
    /// Electron apps start a launcher that can exit while the real app keeps running, so waiting on the
    /// process we started is not enough. Wait until no process with this name has been seen for a few polls
    /// in a row (Logseq briefly has none while it restarts itself).
    /// </summary>
    public static async Task UntilGoneAsync(string processName, CancellationToken cancellationToken, int quietPolls = 3)
    {
        var quiet = 0;
        while (quiet < quietPolls)
        {
            await Task.Delay(PollInterval, cancellationToken);
            quiet = Exists(processName) ? 0 : quiet + 1;
        }
    }

    public static bool Exists(string processName)
    {
        var processes = Process.GetProcessesByName(processName);
        try { return processes.Length > 0; }
        finally { foreach (var p in processes) p.Dispose(); }
    }
}
