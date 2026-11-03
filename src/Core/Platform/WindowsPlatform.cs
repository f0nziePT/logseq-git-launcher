using System.Diagnostics;

namespace LogseqGitLauncher.Core.Platform;

public sealed class WindowsPlatform : IPlatform
{
    public string SettingsDirectory =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "LogseqGitLauncher");

    public string GitExecutable => "git";
    public string LogseqPickerName => "Logseq.exe";
    public bool LogseqIsFolder => false;

    public string? DetectLogseq()
    {
        var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        string[] candidates =
        [
            Path.Combine(local, "Logseq", "Logseq.exe"),
            Path.Combine(local, "Programs", "Logseq", "Logseq.exe"),
            Path.Combine(programFiles, "Logseq", "Logseq.exe"),
        ];
        return candidates.FirstOrDefault(File.Exists);
    }

    public bool IsLogseqRunning() => ProcessWait.Exists("Logseq");

    public async Task LaunchAndWaitAsync(string logseqPath, string workingDirectory, CancellationToken cancellationToken)
    {
        using var process = Process.Start(new ProcessStartInfo(logseqPath)
        {
            UseShellExecute = false,
            WorkingDirectory = workingDirectory,
        }) ?? throw new InvalidOperationException("Logseq did not start.");
        await process.WaitForExitAsync(cancellationToken);
        await ProcessWait.UntilGoneAsync("Logseq", cancellationToken);
    }
}
