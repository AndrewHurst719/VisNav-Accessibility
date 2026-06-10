using System.IO;

namespace VisNav.App;

/// <summary>
/// Minimal append-only logger to %AppData%\VisNav\debug.log, enabled only when the
/// environment variable VISNAV_DEBUG is set. Used to diagnose the native paths.
/// </summary>
internal static class Diagnostics
{
    private static readonly bool Enabled =
        !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("VISNAV_DEBUG"));

    private static readonly string LogPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "VisNav", "debug.log");

    private static readonly object Gate = new();

    public static void Log(string message)
    {
        if (!Enabled)
            return;
        try
        {
            lock (Gate)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(LogPath)!);
                File.AppendAllText(LogPath, $"{DateTime.Now:HH:mm:ss.fff}  {message}{Environment.NewLine}");
            }
        }
        catch
        {
            // Never let logging break the app.
        }
    }
}
