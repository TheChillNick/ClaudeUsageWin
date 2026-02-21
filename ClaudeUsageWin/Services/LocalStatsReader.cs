using System.IO;
using System.Text.Json.Nodes;
using ClaudeUsageWin.Models;

namespace ClaudeUsageWin.Services;

/// <summary>
/// Reads today's and weekly usage stats by scanning Claude Code session files
/// (~/.claude/projects/**/*.jsonl). Each "assistant" entry contains a timestamp,
/// model name, and full token usage breakdown.
///
/// Also computes per-model token totals, per-project message counts, cost estimates,
/// and burn rate — all from local files with no network calls.
///
/// Falls back to stats-cache.json if no session files are found.
/// </summary>
public static class LocalStatsReader
{
    private static readonly string ClaudeDir =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".claude");

    private static readonly string ProjectsDir = Path.Combine(ClaudeDir, "projects");
    private static readonly string StatsPath   = Path.Combine(ClaudeDir, "stats-cache.json");

    // ── Model pricing (per million tokens, USD) ───────────────────
    // Source: Anthropic pricing page, approximate, baked in for offline cost estimates
    private static readonly Dictionary<string, (double InputPer1M, double OutputPer1M, double CacheReadPer1M, double CacheWritePer1M)> Pricing = new()
    {
        ["claude-opus-4"]             = (15.00, 75.00, 1.50,  18.75),
        ["claude-opus-4-5"]           = (15.00, 75.00, 1.50,  18.75),
        ["claude-sonnet-4"]           = ( 3.00, 15.00, 0.30,   3.75),
        ["claude-sonnet-4-5"]         = ( 3.00, 15.00, 0.30,   3.75),
        ["claude-sonnet-4-6"]         = ( 3.00, 15.00, 0.30,   3.75),
        ["claude-sonnet-3-5"]         = ( 3.00, 15.00, 0.30,   3.75),
        ["claude-haiku-4-5"]          = ( 0.80,  4.00, 0.08,   1.00),
        ["claude-haiku-4-5-20251001"] = ( 0.80,  4.00, 0.08,   1.00),
        ["claude-haiku-3"]            = ( 0.25,  1.25, 0.03,   0.30),
        ["claude-haiku-3-5"]          = ( 0.80,  4.00, 0.08,   1.00),
    };

    // ── Public entry point ────────────────────────────────────────

    public static UsageData? TryRead()
    {
        var stats = ReadFromSessionFiles();

        if (stats.TodayMessages == 0 && stats.WeeklyMessages == 0)
        {
            var cached = TryReadFromCache();
            if (cached is not null) return cached;
        }

        if (stats.TodayMessages == 0 && stats.TodayTokens == 0 &&
            stats.WeeklyMessages == 0 && stats.WeeklyTokens == 0)
            return null;

        var plan = CredentialsReader.TryRead()?.SubscriptionType ?? "pro";

        Logger.Log($"LocalStats (sessions): today={stats.TodayMessages}msg/{stats.TodayTokens}tok " +
                   $"cost=${stats.TodayCostUSD:F4} burn={stats.BurnRateTokensPerHour:F0}tok/h " +
                   $"week={stats.WeeklyMessages}msg/{stats.WeeklyTokens}tok plan={plan}");

        return stats with { Plan = plan };
    }

    // ── Primary: scan ~/.claude/projects/**/*.jsonl ───────────────

    private static UsageData ReadFromSessionFiles()
    {
        if (!Directory.Exists(ProjectsDir))
            return new UsageData { IsLocalOnly = true };

        var todayUtc   = DateTime.UtcNow.Date;
        var weekAgoUtc = todayUtc.AddDays(-6);

        int  todayMsg = 0, weeklyMsg = 0;
        long todayTok = 0, weeklyTok = 0;
        long todayInput = 0, todayOutput = 0, todayCacheRead = 0, todayCacheWrite = 0;
        double todayCost = 0;
        DateTime? firstMsgAt = null;
        DateTime? lastMsgAt  = null;

        var modelTotals   = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
        var projectCounts = new Dictionary<string, int>(StringComparer.Ordinal);

        try
        {
            var files = Directory.GetFiles(ProjectsDir, "*.jsonl", SearchOption.AllDirectories);

            foreach (var file in files)
            {
                if (File.GetLastWriteTimeUtc(file).Date < weekAgoUtc) continue;

                // Project name = parent directory basename, prettified
                var projectSlug = Path.GetFileName(Path.GetDirectoryName(file) ?? "") ?? "";
                var projectName = SlugToProjectName(projectSlug);

                try
                {
                    foreach (var line in File.ReadLines(file))
                    {
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

                            var model  = node["message"]?["model"]?.GetValue<string>() ?? "unknown";
                            long inp   = usage["input_tokens"]?.GetValue<long>()                ?? 0;
                            long outp  = usage["output_tokens"]?.GetValue<long>()               ?? 0;
                            long cread = usage["cache_read_input_tokens"]?.GetValue<long>()     ?? 0;
                            long cwrit = usage["cache_creation_input_tokens"]?.GetValue<long>() ?? 0;
                            long total = inp + outp + cread + cwrit;

                            weeklyMsg++;
                            weeklyTok += total;

                            if (entryDate == todayUtc)
                            {
                                todayMsg++;
                                todayTok     += total;
                                todayInput   += inp;
                                todayOutput  += outp;
                                todayCacheRead  += cread;
                                todayCacheWrite += cwrit;

                                // Cost estimate
                                todayCost += EstimateCost(model, inp, outp, cread, cwrit);

                                // Model breakdown
                                var modelKey = NormalizeModelName(model);
                                modelTotals[modelKey] = modelTotals.GetValueOrDefault(modelKey) + total;

                                // Project counts
                                projectCounts[projectName] = projectCounts.GetValueOrDefault(projectName) + 1;

                                // Session span tracking
                                var tsUtc = ts.ToUniversalTime();
                                if (firstMsgAt is null || tsUtc < firstMsgAt) firstMsgAt = tsUtc;
                                if (lastMsgAt  is null || tsUtc > lastMsgAt)  lastMsgAt  = tsUtc;
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
            Logger.LogError("LocalStatsReader.ReadFromSessionFiles", ex);
        }

        // Burn rate: tokens / elapsed hours today
        double burnTokensPerHour = 0;
        double burnCostPerHour   = 0;
        if (firstMsgAt.HasValue && lastMsgAt.HasValue && todayMsg > 1)
        {
            var elapsed = (lastMsgAt.Value - firstMsgAt.Value).TotalHours;
            if (elapsed > 0.05) // ignore sessions < 3 minutes
            {
                burnTokensPerHour = todayTok / elapsed;
                burnCostPerHour   = todayCost / elapsed;
            }
        }

        // Sort and trim project map to top 10 by message count
        var topProjects = projectCounts
            .OrderByDescending(kv => kv.Value)
            .Take(10)
            .ToDictionary(kv => kv.Key, kv => kv.Value);

        return new UsageData
        {
            IsLocalOnly              = true,
            TodayMessages            = todayMsg,
            TodayTokens              = todayTok,
            WeeklyMessages           = weeklyMsg,
            WeeklyTokens             = weeklyTok,
            TodayInputTokens         = todayInput,
            TodayOutputTokens        = todayOutput,
            TodayCacheReadTokens     = todayCacheRead,
            TodayCacheWriteTokens    = todayCacheWrite,
            TodayCostUSD             = todayCost,
            BurnRateTokensPerHour    = burnTokensPerHour,
            BurnRateCostPerHour      = burnCostPerHour,
            ModelTokensToday         = modelTotals,
            ProjectMessagesToday     = topProjects,
            TodayFirstMessageAt      = firstMsgAt,
        };
    }

    // ── Helpers ───────────────────────────────────────────────────

    private static double EstimateCost(
        string model, long input, long output, long cacheRead, long cacheWrite)
    {
        var key = NormalizeModelName(model);
        if (!Pricing.TryGetValue(key, out var p))
            p = (3.00, 15.00, 0.30, 3.75); // default: Sonnet pricing

        return (input     * p.InputPer1M      / 1_000_000.0)
             + (output    * p.OutputPer1M     / 1_000_000.0)
             + (cacheRead * p.CacheReadPer1M  / 1_000_000.0)
             + (cacheWrite* p.CacheWritePer1M / 1_000_000.0);
    }

    private static string NormalizeModelName(string raw)
    {
        // Strip date suffix: claude-sonnet-4-6-20260101 → claude-sonnet-4-6
        var parts = raw.Split('-');
        // If last part is 8-digit date like 20260101, drop it
        if (parts.Length > 0 && parts[^1].Length == 8 &&
            long.TryParse(parts[^1], out _))
        {
            raw = string.Join('-', parts[..^1]);
        }
        return raw.ToLowerInvariant();
    }

    /// <summary>Convert project slug (C--Users--foo--my-project) to display name (my-project)</summary>
    private static string SlugToProjectName(string slug)
    {
        if (string.IsNullOrEmpty(slug)) return "Unknown";
        // Slugs replace path separators with '--', so last component is the project folder
        var parts = slug.Split("--");
        return parts[^1].Length > 0 ? parts[^1] : slug;
    }

    // ── Fallback: stats-cache.json ────────────────────────────────

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
                    var date   = day?["date"]?.GetValue<string>();
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
            Logger.Log($"LocalStats (cache): today={todayMsg}msg/{todayTok}tok week={weeklyMsg}msg/{weeklyTok}tok");

            return new UsageData
            {
                IsLocalOnly    = true,
                TodayMessages  = todayMsg,
                TodayTokens    = todayTok,
                WeeklyMessages = weeklyMsg,
                WeeklyTokens   = weeklyTok,
                Plan           = plan,
            };
        }
        catch (Exception ex)
        {
            Logger.LogError("LocalStatsReader.TryReadFromCache", ex);
            return null;
        }
    }
}
