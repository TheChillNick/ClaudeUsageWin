using System.IO;
using System.Text.Json.Nodes;
using ClaudeUsageWin.Models;

namespace ClaudeUsageWin.Services;

/// <summary>
/// Reads usage data from ~/.claude/stats-cache.json — the local file
/// that Claude Code CLI maintains. Works even when the claude.ai API
/// is unavailable (e.g. Cloudflare challenge blocking HTTP clients).
/// </summary>
public static class LocalStatsReader
{
    private static readonly string StatsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".claude", "stats-cache.json");

    public static UsageData? TryRead()
    {
        try
        {
            if (!File.Exists(StatsPath)) return null;

            var json = File.ReadAllText(StatsPath);
            var root = JsonNode.Parse(json);
            if (root is null) return null;

            var today   = DateTime.Now.ToString("yyyy-MM-dd");
            var weekAgo = DateTime.Now.AddDays(-6).ToString("yyyy-MM-dd"); // 7 days incl. today

            int  todayMessages   = 0;
            int  weeklyMessages  = 0;
            long todayTokens     = 0;
            long weeklyTokens    = 0;

            // ── Daily message counts ──────────────────────────────
            var dailyActivity = root["dailyActivity"]?.AsArray();
            if (dailyActivity is not null)
            {
                foreach (var day in dailyActivity)
                {
                    var date  = day?["date"]?.GetValue<string>();
                    if (date is null) continue;
                    var count = day?["messageCount"]?.GetValue<int>() ?? 0;
                    if (date == today) todayMessages = count;
                    if (string.Compare(date, weekAgo, StringComparison.Ordinal) >= 0)
                        weeklyMessages += count;
                }
            }

            // ── Daily token counts ────────────────────────────────
            var dailyTokens = root["dailyModelTokens"]?.AsArray();
            if (dailyTokens is not null)
            {
                foreach (var day in dailyTokens)
                {
                    var date = day?["date"]?.GetValue<string>();
                    if (date is null) continue;
                    var modelTokens = day?["tokensByModel"]?.AsObject();
                    if (modelTokens is null) continue;

                    long dayTotal = 0;
                    foreach (var kvp in modelTokens)
                        dayTotal += kvp.Value?.GetValue<long>() ?? 0;

                    if (date == today) todayTokens = dayTotal;
                    if (string.Compare(date, weekAgo, StringComparison.Ordinal) >= 0)
                        weeklyTokens += dayTotal;
                }
            }

            // ── Plan from credentials ─────────────────────────────
            var creds = CredentialsReader.TryRead();
            var plan  = creds?.SubscriptionType ?? "free";

            Logger.Log($"LocalStats: today={todayMessages}msg/{todayTokens}tok, " +
                       $"week={weeklyMessages}msg/{weeklyTokens}tok, plan={plan}");

            return new UsageData
            {
                TodayMessages  = todayMessages,
                TodayTokens    = todayTokens,
                WeeklyMessages = weeklyMessages,
                WeeklyTokens   = weeklyTokens,
                Plan           = plan,
                IsLocalOnly    = true,
            };
        }
        catch (Exception ex)
        {
            Logger.LogError("LocalStatsReader.TryRead failed", ex);
            return null;
        }
    }
}
