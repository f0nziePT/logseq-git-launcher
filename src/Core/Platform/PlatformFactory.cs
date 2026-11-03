namespace LogseqGitLauncher.Core.Platform;

public static class PlatformFactory
{
    public static IPlatform Create()
    {
        if (OperatingSystem.IsWindows()) return new WindowsPlatform();
        if (OperatingSystem.IsMacOS()) return new MacPlatform();
        throw new PlatformNotSupportedException("Only Windows and macOS are supported.");
    }
}
