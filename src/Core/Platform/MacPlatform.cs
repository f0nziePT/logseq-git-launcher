using System.Diagnostics;

namespace LogseqGitLauncher.Core.Platform;

public sealed class MacPlatform : IPlatform
{
    private static string Home => Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

    public string SettingsDirectory =>
        Path.Combine(Home, "Library", "Application Support", "LogseqGitLauncher");

    // Apps started from Finder get a minimal PATH, so use the absolute path of the git shim.
    public string GitExecutable => "/usr/bin/git";
    public string LogseqPickerName => "Logseq.app";
    public bool LogseqIsFolder => true;

    public string? DetectLogseq()
    {
        string[] candidates =
        [
            "/Applications/Logseq.app",
            Path.Combine(Home, "Applications", "Logseq.app"),
        ];
        return candidates.FirstOrDefault(Directory.Exists);
    }

    public bool IsLogseqRunning() => ProcessWait.Exists("Logseq");

    public async Task LaunchAndWaitAsync(string logseqPath, string workingDirectory, CancellationToken cancellationToken)
    {
        // "open -W" returns only when the application quits.
        var psi = new ProcessStartInfo("/usr/bin/open") { UseShellExecute = false };
        psi.ArgumentList.Add("-W");
        psi.ArgumentList.Add("-a");
        psi.ArgumentList.Add(logseqPath);
        using var process = Process.Start(psi) ?? throw new InvalidOperationException("Logseq did not start.");
        await process.WaitForExitAsync(cancellationToken);
        await ProcessWait.UntilGoneAsync("Logseq", cancellationToken);
    }
}
