using System.IO;
using System.Text.Json.Nodes;
using ClaudeUsageWin.Models;

namespace ClaudeUsageWin.Services;

/// <summary>
/// Reads today's and weekly usage stats by scanning Claude Code session files
/// (~/.claude/projects/**/*.jsonl). Each "assistant" entry in those files
/// represents one AI response and contains a timestamp plus token usage.
///
/// Falls back to stats-cache.json for any days not covered by live session files.
/// </summary>
public static class LocalStatsReader
{
    private static readonly string ClaudeDir =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".claude");

    private static readonly string ProjectsDir = Path.Combine(ClaudeDir, "projects");

    private static readonly string StatsPath = Path.Combine(ClaudeDir, "stats-cache.json");

    public static UsageData? TryRead()
    {
        var (todayMsg, todayTok, weeklyMsg, weeklyTok) = ReadFromSessionFiles();

        // If session files had nothing (e.g. no Claude Code usage at all), try stats-cache.json
        if (todayMsg == 0 && weeklyMsg == 0)
        {
            var cached = TryReadFromCache();
            if (cached is not null) return cached;
        }

        if (todayMsg == 0 && todayTok == 0 && weeklyMsg == 0 && weeklyTok == 0)
            return null;

        var plan = CredentialsReader.TryRead()?.SubscriptionType ?? "pro";

        Logger.Log($"LocalStats (sessions): today={todayMsg}msg/{todayTok}tok, " +
                   $"week={weeklyMsg}msg/{weeklyTok}tok, plan={plan}");

        return new UsageData
        {
            TodayMessages  = todayMsg,
            TodayTokens    = todayTok,
            WeeklyMessages = weeklyMsg,
            WeeklyTokens   = weeklyTok,
            Plan           = plan,
            IsLocalOnly    = true,
        };
    }

    // ── Primary: scan ~/.claude/projects/**/*.jsonl ───────────────

    private static (int todayMsg, long todayTok, int weeklyMsg, long weeklyTok)
        ReadFromSessionFiles()
    {
        if (!Directory.Exists(ProjectsDir))
            return (0, 0, 0, 0);

        var todayUtc   = DateTime.UtcNow.Date;
        var weekAgoUtc = todayUtc.AddDays(-6); // 7 days inclusive

        int  todayMsg = 0, weeklyMsg = 0;
        long todayTok = 0, weeklyTok = 0;

        try
        {
            var files = Directory.GetFiles(ProjectsDir, "*.jsonl", SearchOption.AllDirectories);

            foreach (var file in files)
            {
                // Skip files untouched in the last 7 days (fast filesystem check)
                if (File.GetLastWriteTimeUtc(file).Date < weekAgoUtc)
                    continue;

                try
                {
                    foreach (var line in File.ReadLines(file))
                    {
                        // Fast pre-filter before JSON parse
                        if (!line.Contains("\"assistant\"") || !line.Contains("\"output_tokens\""))
                            continue;

                        try
                        {
                            var node = JsonNode.Parse(line);
                            if (node?["type"]?.GetValue<string>() != "assistant") continue;

                            var tsStr = node["timestamp"]?.GetValue<string>();
                            if (!DateTime.TryParse(tsStr, null,
                                    System.Globalization.DateTimeStyles.RoundtripKind, out var ts))
                                continue;

                            var entryDate = ts.ToUniversalTime().Date;
                            if (entryDate < weekAgoUtc || entryDate > todayUtc) continue;

                            var usage = node["message"]?["usage"];
                            if (usage is null) continue;

                            long tokens = (usage["input_tokens"]?.GetValue<long>()                ?? 0)
                                        + (usage["output_tokens"]?.GetValue<long>()               ?? 0)
                                        + (usage["cache_creation_input_tokens"]?.GetValue<long>() ?? 0)
                                        + (usage["cache_read_input_tokens"]?.GetValue<long>()     ?? 0);

                            weeklyMsg++;
                            weeklyTok += tokens;

                            if (entryDate == todayUtc)
                            {
                                todayMsg++;
                                todayTok += tokens;
                            }
                        }
                        catch { /* skip malformed lines */ }
                    }
                }
                catch { /* skip unreadable files */ }
            }
        }
        catch (Exception ex)
        {
            Logger.LogError("LocalStatsReader.ReadFromSessionFiles failed", ex);
        }

        return (todayMsg, todayTok, weeklyMsg, weeklyTok);
    }

    // ── Fallback: stats-cache.json (may be stale) ─────────────────

    private static UsageData? TryReadFromCache()
    {
        try
        {
            if (!File.Exists(StatsPath)) return null;

            var root = JsonNode.Parse(File.ReadAllText(StatsPath));
            if (root is null) return null;

            var today   = DateTime.Now.ToString("yyyy-MM-dd");
            var weekAgo = DateTime.Now.AddDays(-6).ToString("yyyy-MM-dd");

            int  todayMsg = 0, weeklyMsg = 0;
            long todayTok = 0, weeklyTok = 0;

            var dailyActivity = root["dailyActivity"]?.AsArray();
            if (dailyActivity is not null)
            {
                foreach (var day in dailyActivity)
                {
                    var date  = day?["date"]?.GetValue<string>();
                    if (date is null) continue;
                    var count = day?["messageCount"]?.GetValue<int>() ?? 0;
                    if (date == today) todayMsg = count;
                    if (string.Compare(date, weekAgo, StringComparison.Ordinal) >= 0)
                        weeklyMsg += count;
                }
            }

            var dailyTokens = root["dailyModelTokens"]?.AsArray();
            if (dailyTokens is not null)
            {
                foreach (var day in dailyTokens)
                {
                    var date = day?["date"]?.GetValue<string>();
                    if (date is null) continue;
                    var models = day?["tokensByModel"]?.AsObject();
                    if (models is null) continue;

                    long dayTotal = 0;
                    foreach (var kvp in models) dayTotal += kvp.Value?.GetValue<long>() ?? 0;

                    if (date == today) todayTok = dayTotal;
                    if (string.Compare(date, weekAgo, StringComparison.Ordinal) >= 0)
                        weeklyTok += dayTotal;
                }
            }

            if (todayMsg == 0 && weeklyMsg == 0) return null;

            var plan = CredentialsReader.TryRead()?.SubscriptionType ?? "pro";
            Logger.Log($"LocalStats (cache): today={todayMsg}msg/{todayTok}tok, " +
                       $"week={weeklyMsg}msg/{weeklyTok}tok");

            return new UsageData
            {
                TodayMessages  = todayMsg,
                TodayTokens    = todayTok,
                WeeklyMessages = weeklyMsg,
                WeeklyTokens   = weeklyTok,
                Plan           = plan,
                IsLocalOnly    = true,
            };
        }
        catch (Exception ex)
        {
            Logger.LogError("LocalStatsReader.TryReadFromCache failed", ex);
            return null;
        }
    }
}
