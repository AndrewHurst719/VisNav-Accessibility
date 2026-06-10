using System.Text.Json;
using System.Text.Json.Serialization;

namespace VisNav.Core.Settings;

/// <summary>
/// Loads and saves <see cref="VisNavSettings"/> as JSON. Missing or corrupt files
/// fall back to defaults rather than throwing, so the app always starts. The file
/// path is injectable to keep the service unit-testable.
/// </summary>
public sealed class SettingsService
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly string _path;

    public SettingsService(string? settingsPath = null)
    {
        _path = settingsPath ?? DefaultPath;
    }

    /// <summary>Full path to <c>%AppData%\VisNav\settings.json</c>.</summary>
    public static string DefaultPath => System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "VisNav",
        "settings.json");

    /// <summary>The resolved settings file path this service reads from and writes to.</summary>
    public string Path => _path;

    /// <summary>
    /// Reads settings from disk. Returns defaults when the file is absent,
    /// empty, or cannot be parsed.
    /// </summary>
    public VisNavSettings Load()
    {
        try
        {
            if (!File.Exists(_path))
                return new VisNavSettings();

            var json = File.ReadAllText(_path);
            if (string.IsNullOrWhiteSpace(json))
                return new VisNavSettings();

            return JsonSerializer.Deserialize<VisNavSettings>(json, Options) ?? new VisNavSettings();
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            // Corrupt or unreadable file — start from defaults instead of crashing.
            return new VisNavSettings();
        }
    }

    /// <summary>Writes settings to disk, creating the containing directory if needed.</summary>
    public void Save(VisNavSettings settings)
    {
        var dir = System.IO.Path.GetDirectoryName(_path);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        var json = JsonSerializer.Serialize(settings, Options);
        File.WriteAllText(_path, json);
    }
}
