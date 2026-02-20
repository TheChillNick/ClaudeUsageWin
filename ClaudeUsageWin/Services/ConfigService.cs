using System.IO;
using System.Text.Json;

namespace ClaudeUsageWin.Services;

public record AppConfig
{
    public string SessionKey       { get; init; } = "";
    public string OrgId            { get; init; } = "";
    public int    RefreshInterval  { get; init; } = 60;
    public string SubscriptionType { get; init; } = "free";

    // Threshold Notifications
    public int[]  NotifyThresholds   { get; init; } = [75, 90, 95];
    public bool   NotifyFiveHour     { get; init; } = true;
    public bool   NotifyWeekly       { get; init; } = true;

    // Appearance
    public int  OpacityPct   { get; init; } = 95;
    public bool AlwaysOnTop  { get; init; } = true;

    // Icon Styles
    public string IconStyle { get; init; } = "Percentage"; // "Percentage", "Bar", "Dot"

    // Launch at Startup
    public bool LaunchAtStartup { get; init; } = false;

    // Remaining vs Used Toggle
    public bool ShowRemaining { get; init; } = false;

    // Statusline
    public bool StatuslineInstalled { get; init; } = false;

    // Window sizes (remembered across sessions)
    public int    PopupWidth      { get; init; } = 340;
    public int    SettingsWidth   { get; init; } = 460;
    public int    SettingsHeight  { get; init; } = 560;
    public double PopupScale      { get; init; } = 1.0;

    // Window positions (double.NaN = not yet set = use default)
    public double PopupLeft      { get; init; } = double.NaN;
    public double PopupTop       { get; init; } = double.NaN;
    public double SettingsLeft   { get; init; } = double.NaN;
    public double SettingsTop    { get; init; } = double.NaN;
}

public static class ConfigService
{
    private static readonly string ConfigPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "ClaudeUsageWin", "config.json");

    public static AppConfig Load()
    {
        try
        {
            if (File.Exists(ConfigPath))
                return JsonSerializer.Deserialize<AppConfig>(File.ReadAllText(ConfigPath)) ?? new();
        }
        catch { }
        return new AppConfig();
    }

    public static void Save(AppConfig config)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(ConfigPath)!);
        File.WriteAllText(ConfigPath,
            JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true }));
    }
}
