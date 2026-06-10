using VisNav.Core.Settings;
using Xunit;

namespace VisNav.Core.Tests;

public sealed class SettingsServiceTests : IDisposable
{
    private readonly string _tempPath;

    public SettingsServiceTests()
    {
        _tempPath = Path.Combine(
            Path.GetTempPath(),
            "VisNavTests",
            Guid.NewGuid().ToString("N"),
            "settings.json");
    }

    public void Dispose()
    {
        var dir = Path.GetDirectoryName(_tempPath);
        if (dir is not null && Directory.Exists(dir))
            Directory.Delete(dir, recursive: true);
    }

    [Fact]
    public void Load_WhenFileMissing_ReturnsDefaults()
    {
        var service = new SettingsService(_tempPath);

        var settings = service.Load();

        Assert.False(settings.Crosshair.Enabled);
        Assert.Equal(2.0, settings.Magnifier.ZoomFactor);
        Assert.Equal(CrosshairColor.Green, settings.Crosshair.Color);
        Assert.Equal(40, settings.Crosshair.OuterRadius);
    }

    [Fact]
    public void Save_ThenLoad_RoundTripsAllSections()
    {
        var service = new SettingsService(_tempPath);
        var original = new VisNavSettings
        {
            Crosshair = { Enabled = true, Color = CrosshairColor.NeonPink, OuterRadius = 75, OuterThickness = 5 },
            Magnifier = { Enabled = true, ZoomFactor = 3.5, LensWidth = 600, LensHeight = 400 },
            Narrator = { Enabled = true, VoiceName = "Microsoft David", SpeechRate = -2 },
        };

        service.Save(original);
        var loaded = service.Load();

        Assert.True(loaded.Crosshair.Enabled);
        Assert.Equal(CrosshairColor.NeonPink, loaded.Crosshair.Color);
        Assert.Equal(75, loaded.Crosshair.OuterRadius);
        Assert.Equal(5, loaded.Crosshair.OuterThickness);
        Assert.Equal(3.5, loaded.Magnifier.ZoomFactor);
        Assert.Equal(600, loaded.Magnifier.LensWidth);
        Assert.Equal(400, loaded.Magnifier.LensHeight);
        Assert.True(loaded.Narrator.Enabled);
        Assert.Equal("Microsoft David", loaded.Narrator.VoiceName);
        Assert.Equal(-2, loaded.Narrator.SpeechRate);
    }

    [Fact]
    public void Save_CreatesMissingDirectory()
    {
        var service = new SettingsService(_tempPath);

        service.Save(new VisNavSettings());

        Assert.True(File.Exists(_tempPath));
    }

    [Fact]
    public void Load_WhenFileCorrupt_ReturnsDefaults()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_tempPath)!);
        File.WriteAllText(_tempPath, "{ this is not valid json ]");
        var service = new SettingsService(_tempPath);

        var settings = service.Load();

        Assert.False(settings.Magnifier.Enabled);
        Assert.Equal(2.0, settings.Magnifier.ZoomFactor);
    }

    [Fact]
    public void Load_WhenFileEmpty_ReturnsDefaults()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_tempPath)!);
        File.WriteAllText(_tempPath, "   ");
        var service = new SettingsService(_tempPath);

        var settings = service.Load();

        Assert.Equal(CrosshairColor.Green, settings.Crosshair.Color);
    }
}
