namespace ClaudeUsageWin.Models;

public record UsageData
{
    // ── From API ──────────────────────────────────────────────────
    public int       FiveHourPct     { get; init; }
    public DateTime? FiveHourResetAt { get; init; }
    public int       WeeklyPct       { get; init; }
    public DateTime? WeeklyResetAt   { get; init; }

    // ── From API or local stats ───────────────────────────────────
    public int    TodayMessages  { get; init; }
    public long   TodayTokens   { get; init; }
    public string Plan           { get; init; } = "free";

    // ── Local-only extras (from stats-cache.json) ─────────────────
    /// <summary>True when the data came from local files instead of the API.</summary>
    public bool  IsLocalOnly    { get; init; } = false;
    public int   WeeklyMessages { get; init; } = 0;
    public long  WeeklyTokens  { get; init; } = 0;
}
