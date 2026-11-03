namespace LogseqGitLauncher.Core;

public static class GraphValidator
{
    /// <summary>Error is set when the folder cannot be used; Warning is advisory only.</summary>
    public readonly record struct Result(string? Error, string? Warning)
    {
        public bool IsValid => Error is null;
    }

    public static Result ValidateGraph(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return new Result("Choose the graph folder.", null);
        if (!Directory.Exists(path))
            return new Result("The graph folder does not exist.", null);

        // .git is a directory normally, a file for worktrees and submodules.
        var git = Path.Combine(path, ".git");
        if (!Directory.Exists(git) && !File.Exists(git))
            return new Result("This folder is not a git repository (no .git found).", null);

        var looksLikeGraph = Directory.Exists(Path.Combine(path, "pages"))
                             || Directory.Exists(Path.Combine(path, "journals"))
                             || Directory.Exists(Path.Combine(path, "logseq"));
        return new Result(null, looksLikeGraph ? null : "No pages, journals or logseq folder found; this may not be a Logseq graph.");
    }

    public static string? ValidateLogseq(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return "Choose the Logseq application.";
        // On macOS Logseq.app is a directory, on Windows Logseq.exe is a file.
        if (!File.Exists(path) && !Directory.Exists(path))
            return "The Logseq application was not found.";
        return null;
    }
}
