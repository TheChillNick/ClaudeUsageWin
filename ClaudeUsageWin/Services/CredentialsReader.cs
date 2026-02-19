using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace ClaudeUsageWin.Services;

public record ClaudeCredentials(
    string AccessToken,
    string RefreshToken,
    long   ExpiresAt,
    string SubscriptionType,
    string RateLimitTier
);

public static class CredentialsReader
{
    private static readonly string CredPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".claude", ".credentials.json");

    public static ClaudeCredentials? TryRead()
    {
        try
        {
            if (!File.Exists(CredPath)) return null;

            var json  = File.ReadAllText(CredPath);
            var root  = JsonNode.Parse(json)?["claudeAiOauth"];
            if (root is null) return null;

            var token   = root["accessToken"]?.GetValue<string>();
            var refresh = root["refreshToken"]?.GetValue<string>();
            var expires = root["expiresAt"]?.GetValue<long>() ?? 0;
            var subType = root["subscriptionType"]?.GetValue<string>() ?? "free";
            var tier    = root["rateLimitTier"]?.GetValue<string>() ?? "";

            if (string.IsNullOrEmpty(token)) return null;

            return new ClaudeCredentials(token, refresh ?? "", expires, subType, tier);
        }
        catch { return null; }
    }

    public static bool IsExpired(ClaudeCredentials creds)
    {
        // expiresAt is milliseconds since epoch
        var expiry = DateTimeOffset.FromUnixTimeMilliseconds(creds.ExpiresAt);
        return DateTimeOffset.UtcNow >= expiry - TimeSpan.FromMinutes(5);
    }

    public static async Task<ClaudeCredentials?> TryRefreshAsync(ClaudeCredentials expired)
    {
        try
        {
            if (string.IsNullOrEmpty(expired.RefreshToken))
            {
                Logger.Log("TokenRefresh: no refresh token available");
                return null;
            }

            Logger.Log("TokenRefresh: attempting token refresh");

            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
            http.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent",
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/131.0.0.0 Safari/537.36");
            http.DefaultRequestHeaders.TryAddWithoutValidation("Accept", "application/json");
            http.DefaultRequestHeaders.TryAddWithoutValidation("Origin", "https://claude.ai");
            http.DefaultRequestHeaders.TryAddWithoutValidation("Referer", "https://claude.ai/");

            var payload = JsonSerializer.Serialize(new { refresh_token = expired.RefreshToken });
            var content = new StringContent(payload, Encoding.UTF8, "application/json");

            var resp = await http.PostAsync("https://claude.ai/api/auth/token/refresh", content);
            var body = await resp.Content.ReadAsStringAsync();

            if (!resp.IsSuccessStatusCode)
            {
                Logger.LogError($"TokenRefresh: failed status={resp.StatusCode} body={body[..Math.Min(200, body.Length)]}");
                return null;
            }

            var node = JsonNode.Parse(body);
            var newToken   = node?["accessToken"]?.GetValue<string>();
            var newExpires = node?["expiresAt"]?.GetValue<long>() ?? 0;

            if (string.IsNullOrEmpty(newToken) || newExpires == 0)
            {
                Logger.LogError("TokenRefresh: response missing accessToken or expiresAt");
                return null;
            }

            // Update the credentials file on disk
            var fileJson = File.ReadAllText(CredPath);
            var fileRoot = JsonNode.Parse(fileJson);
            var oauth = fileRoot?["claudeAiOauth"];
            if (oauth is not null)
            {
                oauth["accessToken"] = newToken;
                oauth["expiresAt"]   = newExpires;
                var options = new JsonSerializerOptions { WriteIndented = true };
                File.WriteAllText(CredPath, fileRoot!.ToJsonString(options));
            }

            Logger.Log("TokenRefresh: success, credentials updated on disk");

            return new ClaudeCredentials(
                newToken,
                expired.RefreshToken,
                newExpires,
                expired.SubscriptionType,
                expired.RateLimitTier
            );
        }
        catch (Exception ex)
        {
            Logger.LogError($"TokenRefresh: exception {ex.Message}");
            return null;
        }
    }
}
