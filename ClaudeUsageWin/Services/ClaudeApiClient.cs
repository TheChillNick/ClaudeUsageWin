using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json.Nodes;
using System.IO;
using ClaudeUsageWin.Models;

namespace ClaudeUsageWin.Services;

public class ClaudeApiClient
{
    private readonly HttpClient _http;
    private const string Base = "https://claude.ai/api";

    // ── Factory methods ───────────────────────────────────────────

    public static ClaudeApiClient FromOAuth(string accessToken)
    {
        var http = BuildHttp(null);
        http.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", accessToken);
        return new ClaudeApiClient(http);
    }

    public static ClaudeApiClient FromSessionKey(string sessionKey)
    {
        // Use a CookieContainer — WinHttpHandler with IgnoreCookies strips
        // manually-set Cookie headers, so we must use UseSpecifiedCookieContainer.
        var jar = new CookieContainer();
        jar.Add(new Uri("https://claude.ai"),
                new Cookie("sessionKey", sessionKey));
        return new ClaudeApiClient(BuildHttp(jar));
    }

    private ClaudeApiClient(HttpClient http) => _http = http;

    // ── HTTP client builder ───────────────────────────────────────

    private static HttpClient BuildHttp(CookieContainer? jar)
    {
        var handler = new WinHttpHandler
        {
            AutomaticDecompression = System.Net.DecompressionMethods.All,
            AutomaticRedirection   = true,
            CookieUsePolicy        = jar is null
                                        ? CookieUsePolicy.IgnoreCookies
                                        : CookieUsePolicy.UseSpecifiedCookieContainer,
            CookieContainer        = jar ?? new CookieContainer(),
        };

        var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(20) };
        client.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent",
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/131.0.0.0 Safari/537.36");
        client.DefaultRequestHeaders.TryAddWithoutValidation("Accept",          "application/json, text/plain, */*");
        client.DefaultRequestHeaders.TryAddWithoutValidation("Accept-Language", "en-US,en;q=0.9");
        client.DefaultRequestHeaders.TryAddWithoutValidation("Origin",          "https://claude.ai");
        client.DefaultRequestHeaders.TryAddWithoutValidation("Referer",         "https://claude.ai/");
        client.DefaultRequestHeaders.TryAddWithoutValidation("sec-ch-ua",
            "\"Google Chrome\";v=\"131\", \"Chromium\";v=\"131\", \"Not_A Brand\";v=\"24\"");
        client.DefaultRequestHeaders.TryAddWithoutValidation("sec-ch-ua-mobile",          "?0");
        client.DefaultRequestHeaders.TryAddWithoutValidation("sec-ch-ua-platform",        "\"Windows\"");
        client.DefaultRequestHeaders.TryAddWithoutValidation("Sec-Fetch-Dest",            "empty");
        client.DefaultRequestHeaders.TryAddWithoutValidation("Sec-Fetch-Mode",            "cors");
        client.DefaultRequestHeaders.TryAddWithoutValidation("Sec-Fetch-Site",            "same-origin");
        client.DefaultRequestHeaders.TryAddWithoutValidation("anthropic-client-platform", "web_claude_ai");
        return client;
    }

    // ── API: detect org / account ID ─────────────────────────────
    //
    // Returns the ID to use in subsequent API calls.
    // For Teams/Enterprise users: account.memberships[0].organization.uuid
    // For personal Pro users:     account.uuid  (personal "org" namespace)

    public async Task<string?> GetOrgIdAsync()
    {
        try
        {
            var resp = await _http.GetAsync($"{Base}/auth/session");
            var body = await resp.Content.ReadAsStringAsync();

            Logger.Log($"GetOrgId: status={resp.StatusCode} body={body[..Math.Min(300, body.Length)]}");

            if (!resp.IsSuccessStatusCode) return null;

            var node = JsonNode.Parse(body);

            // Try team/org membership first
            var orgId = node?["account"]?["memberships"]?[0]?["organization"]?["uuid"]
                            ?.GetValue<string>();

            // Fall back to personal account UUID (personal Pro plans have no memberships)
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

    // ── API: fetch usage data ─────────────────────────────────────

    public async Task<UsageData?> GetUsageAsync(string orgId)
    {
        try
        {
            var resp = await _http.GetAsync($"{Base}/organizations/{orgId}/usage");
            var body = await resp.Content.ReadAsStringAsync();

            Logger.Log($"GetUsage: status={resp.StatusCode} body={body[..Math.Min(400, body.Length)]}");

            if (!resp.IsSuccessStatusCode) return null;

            var n = JsonNode.Parse(body);
            if (n is null) return null;

            var fh = n["five_hour_window"];
            var wk = n["weekly"];
            var td = n["today"];

            return new UsageData
            {
                FiveHourPct     = fh?["utilization_pct"]?.GetValue<int>()  ?? 0,
                FiveHourResetAt = ParseDate(fh?["reset_at"]?.GetValue<string>()),
                WeeklyPct       = wk?["utilization_pct"]?.GetValue<int>()  ?? 0,
                WeeklyResetAt   = ParseDate(wk?["reset_at"]?.GetValue<string>()),
                TodayMessages   = td?["message_count"]?.GetValue<int>()    ?? 0,
                TodayTokens     = td?["token_count"]?.GetValue<long>()     ?? 0,
                Plan            = n["plan"]?.GetValue<string>()?.ToLower() ?? "free",
            };
        }
        catch (Exception ex)
        {
            Logger.LogError("GetUsage exception", ex);
            return null;
        }
    }

    private static DateTime? ParseDate(string? iso) =>
        DateTime.TryParse(iso, null,
            System.Globalization.DateTimeStyles.RoundtripKind, out var dt)
            ? dt.ToLocalTime() : null;
}
