using System.IO;
using System.Text.Json;
using ClaudeUsageWin.Models;

namespace ClaudeUsageWin.Services;

public record HistoryPoint(DateTime Timestamp, int FiveHourPct, int WeeklyPct);

public static class UsageHistory
{
    private static readonly string HistPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "ClaudeUsageWin", "history.json");
    private const int MaxPoints = 24; // keep last 24 data points

    public static List<HistoryPoint> Load()
    {
        try {
            if (!File.Exists(HistPath)) return new();
            var json = File.ReadAllText(HistPath);
            return JsonSerializer.Deserialize<List<HistoryPoint>>(json) ?? new();
        } catch { return new(); }
    }

    public static void Append(UsageData data)
    {
        var pts = Load();
        pts.Add(new HistoryPoint(DateTime.Now, data.FiveHourPct, data.WeeklyPct));
        if (pts.Count > MaxPoints) pts.RemoveRange(0, pts.Count - MaxPoints);
        Save(pts);
    }

    private static void Save(List<HistoryPoint> pts)
    {
        try {
            Directory.CreateDirectory(Path.GetDirectoryName(HistPath)!);
            File.WriteAllText(HistPath, JsonSerializer.Serialize(pts));
        } catch { }
    }
}
