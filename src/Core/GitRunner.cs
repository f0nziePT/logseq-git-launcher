using System.Diagnostics;
using System.Text;

namespace LogseqGitLauncher.Core;

public readonly record struct GitResult(int ExitCode, string Output)
{
    public bool Success => ExitCode == 0;
}

public sealed class GitRunner(string gitExecutable, string workingDirectory)
{
    public async Task<GitResult> RunAsync(params string[] args)
    {
        var psi = new ProcessStartInfo(gitExecutable)
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };
        foreach (var a in args)
            psi.ArgumentList.Add(a);
        // There is no console to type into: fail instead of hanging on a credential prompt.
        psi.Environment["GIT_TERMINAL_PROMPT"] = "0";

        using var process = new Process { StartInfo = psi };
        var output = new StringBuilder();
        var gate = new object();
        void Append(string? line)
        {
            if (line is null) return;
            lock (gate) output.AppendLine(line);
        }
        process.OutputDataReceived += (_, e) => Append(e.Data);
        process.ErrorDataReceived += (_, e) => Append(e.Data);

        try
        {
            process.Start();
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            return new GitResult(-1, $"Could not start git ('{gitExecutable}'): {ex.Message}");
        }
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        await process.WaitForExitAsync();
        lock (gate) return new GitResult(process.ExitCode, output.ToString().TrimEnd());
    }
}
