namespace LogseqGitLauncher.Core.Platform;

public interface IPlatform
{
    /// <summary>Folder where settings.json is kept.</summary>
    string SettingsDirectory { get; }

    /// <summary>Path or command name of git.</summary>
    string GitExecutable { get; }

    /// <summary>Text for the "browse for Logseq" button, e.g. "Logseq.exe" or "Logseq.app".</summary>
    string LogseqPickerName { get; }

    /// <summary>True if the Logseq picker selects a folder (a .app bundle) instead of a file.</summary>
    bool LogseqIsFolder { get; }

    /// <summary>Looks for Logseq in the usual install locations.</summary>
    string? DetectLogseq();

    bool IsLogseqRunning();

    /// <summary>Starts Logseq and completes when it has been closed.</summary>
    Task LaunchAndWaitAsync(string logseqPath, string workingDirectory, CancellationToken cancellationToken);
}
