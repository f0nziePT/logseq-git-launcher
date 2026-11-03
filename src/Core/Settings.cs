using System.Text.Json;

namespace LogseqGitLauncher.Core;

public sealed class Settings
{
    /// <summary>Folder of the Logseq graph; it is also the git repository root.</summary>
    public string GraphPath { get; set; } = "";

    /// <summary>Logseq.exe on Windows, Logseq.app on macOS.</summary>
    public string LogseqPath { get; set; } = "";
}

public sealed class SettingsStore(string directory)
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    public string FilePath => Path.Combine(directory, "settings.json");

    public Settings Load()
    {
        try
        {
            if (File.Exists(FilePath))
                return JsonSerializer.Deserialize<Settings>(File.ReadAllText(FilePath)) ?? new Settings();
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            // Corrupt or unreadable settings: start over with defaults.
        }
        return new Settings();
    }

    public void Save(Settings settings)
    {
        Directory.CreateDirectory(directory);
        File.WriteAllText(FilePath, JsonSerializer.Serialize(settings, Options));
    }
}
