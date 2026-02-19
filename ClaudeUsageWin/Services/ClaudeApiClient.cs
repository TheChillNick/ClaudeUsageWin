using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Nodes;
using ClaudeUsageWin.Models;

namespace ClaudeUsageWin.Services;

public class ClaudeApiClient
{
    private readonly HttpClient _http;

    // OAuth Bearer token → api.anthropic.com (bypasses Cloudflare, no org ID needed)
    private const string OAuthUsageUrl = "https://api.anthropic.com/api/oauth/usage";
    // Session key cookie → claude.ai (may be Cloudflare-blocked)
    private const string ClaudeBase    = "https://claude.ai/api";

    // ── Factory methods ───────────────────────────────────────────

    /// <summary>Creates a client that calls api.anthropic.com with OAuth Bearer token.
    /// This endpoint is NOT protected by Cloudflare.</summary>
    public static ClaudeApiClient FromOAuth(string accessToken)
    {
        var handler = new WinHttpHandler
        {
            AutomaticDecompression = System.Net.DecompressionMethods.All,
            AutomaticRedirection   = true,
            CookieUsePolicy        = CookieUsePolicy.IgnoreCookies,
        };
        var http = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(20) };
        http.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", accessToken);
        http.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent",     "claude-code/2.1.5");
        http.DefaultRequestHeaders.TryAddWithoutValidation("anthropic-beta", "oauth-2025-04-20");
        http.DefaultRequestHeaders.TryAddWithoutValidation("Accept",         "application/json");
        return new ClaudeApiClient(http);
    }

    /// <summary>Creates a client that uses a session key cookie against claude.ai.</summary>
    public static ClaudeApiClient FromSessionKey(string sessionKey)
    {
        var jar = new CookieContainer();
        jar.Add(new Uri("https://claude.ai"), new Cookie("sessionKey", sessionKey));
        return new ClaudeApiClient(BuildSessionHttp(jar));
    }

    private ClaudeApiClient(HttpClient http) => _http = http;

    // ── OAuth direct usage ────────────────────────────────────────
    // GET https://api.anthropic.com/api/oauth/usage
    // No org ID needed. Returns: { five_hour: { utilization, resets_at }, seven_day: {...} }

    public async Task<UsageData?> GetOAuthUsageAsync(string? plan = null)
    {
        try
        {
            var resp = await _http.GetAsync(OAuthUsageUrl);
            var body = await resp.Content.ReadAsStringAsync();
            Logger.Log($"GetOAuthUsage: status={resp.StatusCode} body={body[..Math.Min(400, body.Length)]}");
            if (!resp.IsSuccessStatusCode) return null;

            var n = JsonNode.Parse(body);
            if (n is null) return null;

            var fh = n["five_hour"];
            var wk = n["seven_day"];

            return new UsageData
            {
                FiveHourPct     = ParsePct(fh?["utilization"]),
                FiveHourResetAt = ParseDate(fh?["resets_at"]?.GetValue<string>()),
                WeeklyPct       = ParsePct(wk?["utilization"]),
                WeeklyResetAt   = ParseDate(wk?["resets_at"]?.GetValue<string>()),
                Plan            = NormalizePlan(plan),
            };
        }
        catch (Exception ex)
        {
            Logger.LogError("GetOAuthUsage exception", ex);
            return null;
        }
    }

    // ── Session key: detect org ID ────────────────────────────────

    public async Task<string?> GetOrgIdAsync()
    {
        try
        {
            var resp = await _http.GetAsync($"{ClaudeBase}/auth/session");
            var body = await resp.Content.ReadAsStringAsync();
            Logger.Log($"GetOrgId: status={resp.StatusCode} body={body[..Math.Min(300, body.Length)]}");
            if (!resp.IsSuccessStatusCode) return null;

            var node = JsonNode.Parse(body);
            var orgId = node?["account"]?["memberships"]?[0]?["organization"]?["uuid"]
                            ?.GetValue<string>();
            if (string.IsNullOrEmpty(orgId))
                orgId = node?["account"]?["uuid"]?.GetValue<string>();

            Logger.Log($"GetOrgId: resolved id={orgId ?? "(null)"}");
            return orgId;
        }
        catch (Exception ex)
        {
            Logger.LogError("GetOrgId exception", ex);
            return null;
        }
    }

    // ── Session key: fetch usage data ─────────────────────────────
    // GET https://claude.ai/api/organizations/{orgId}/usage
    // Returns: { five_hour: { utilization, resets_at }, seven_day: { utilization, resets_at } }

    public async Task<UsageData?> GetUsageAsync(string orgId)
    {
        try
        {
            var resp = await _http.GetAsync($"{ClaudeBase}/organizations/{orgId}/usage");
            var body = await resp.Content.ReadAsStringAsync();
            Logger.Log($"GetUsage: status={resp.StatusCode} body={body[..Math.Min(400, body.Length)]}");
            if (!resp.IsSuccessStatusCode) return null;

            var n = JsonNode.Parse(body);
            if (n is null) return null;

            var fh = n["five_hour"];
            var wk = n["seven_day"];

            return new UsageData
            {
                FiveHourPct     = ParsePct(fh?["utilization"]),
                FiveHourResetAt = ParseDate(fh?["resets_at"]?.GetValue<string>()),
                WeeklyPct       = ParsePct(wk?["utilization"]),
                WeeklyResetAt   = ParseDate(wk?["resets_at"]?.GetValue<string>()),
                Plan            = NormalizePlan(n["plan"]?.GetValue<string>()),
            };
        }
        catch (Exception ex)
        {
            Logger.LogError("GetUsage exception", ex);
            return null;
        }
    }

    // ── Helpers ───────────────────────────────────────────────────

    /// <summary>The utilization field can be int, double, or string (e.g. "42" or "42.5%").</summary>
    private static int ParsePct(JsonNode? node)
    {
        if (node is null) return 0;
        try
        {
            var kind = node.GetValueKind();
            if (kind == JsonValueKind.Number)
                return (int)Math.Round(node.GetValue<double>());
            if (kind == JsonValueKind.String)
            {
                var s = node.GetValue<string>().Trim().TrimEnd('%');
                return double.TryParse(s, System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture, out var d)
                    ? (int)Math.Round(d) : 0;
            }
        }
        catch { }
        return 0;
    }

    private static DateTime? ParseDate(string? iso) =>
        DateTime.TryParse(iso, null,
            System.Globalization.DateTimeStyles.RoundtripKind, out var dt)
            ? dt.ToLocalTime() : null;

    private static string NormalizePlan(string? raw)
    {
        if (string.IsNullOrEmpty(raw)) return "pro";
        var lower = raw.ToLowerInvariant();
        if (lower.Contains("max"))  return "max";
        if (lower.Contains("pro"))  return "pro";
        if (lower.Contains("free")) return "free";
        return lower;
    }

    private static HttpClient BuildSessionHttp(CookieContainer jar)
    {
        var handler = new WinHttpHandler
        {
            AutomaticDecompression = System.Net.DecompressionMethods.All,
            AutomaticRedirection   = true,
            CookieUsePolicy        = CookieUsePolicy.UseSpecifiedCookieContainer,
            CookieContainer        = jar,
        };
        var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(20) };
        client.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent",
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/131.0.0.0 Safari/537.36");
        client.DefaultRequestHeaders.TryAddWithoutValidation("Accept",          "application/json, text/plain, */*");
        client.DefaultRequestHeaders.TryAddWithoutValidation("Accept-Language", "en-US,en;q=0.9");
        client.DefaultRequestHeaders.TryAddWithoutValidation("Origin",          "https://claude.ai");
        client.DefaultRequestHeaders.TryAddWithoutValidation("Referer",         "https://claude.ai/");
        client.DefaultRequestHeaders.TryAddWithoutValidation("anthropic-client-platform", "web_claude_ai");
        return client;
    }
}
