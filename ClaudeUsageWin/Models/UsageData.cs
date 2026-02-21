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

    // ── Local-only basic extras ───────────────────────────────────
    public bool  IsLocalOnly    { get; init; } = false;
    public int   WeeklyMessages { get; init; } = 0;
    public long  WeeklyTokens  { get; init; } = 0;

    // ── Advanced stats (from session jsonl files) ─────────────────

    /// Input tokens today (non-cache)
    public long TodayInputTokens         { get; init; } = 0;
    /// Output tokens today
    public long TodayOutputTokens        { get; init; } = 0;
    /// Cache-read tokens today (cheap)
    public long TodayCacheReadTokens     { get; init; } = 0;
    /// Cache-write (creation) tokens today
    public long TodayCacheWriteTokens    { get; init; } = 0;

    /// Estimated USD cost for today's usage based on model pricing
    public double TodayCostUSD           { get; init; } = 0;

    /// Tokens per hour burn rate (based on today's session span)
    public double BurnRateTokensPerHour  { get; init; } = 0;

    /// Estimated USD cost per hour at current burn rate
    public double BurnRateCostPerHour    { get; init; } = 0;

    /// Per-model token totals today: model name → total tokens
    public Dictionary<string, long> ModelTokensToday    { get; init; } = [];

    /// Per-project message counts today: project display name → message count
    public Dictionary<string, int> ProjectMessagesToday { get; init; } = [];

    /// UTC time of the first assistant message today (for burn rate calculation)
    public DateTime? TodayFirstMessageAt { get; init; }
}
