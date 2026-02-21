using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace ClaudeUsageWin.Services;

/// <summary>
/// Manages the Claude Code terminal statusline integration on Windows.
///
/// Architecture (ported from the macOS reference project):
///   • App writes a small JSON cache (%APPDATA%\ClaudeUsageWin\statusline-cache.json)
///     on every successful API refresh — no network call from the script.
///   • A PowerShell script (~/.claude/statusline-command.ps1) reads the cache +
///     a config file (~/.claude/statusline-config.txt) and outputs an ANSI-colored
///     status line that Claude Code displays at the top of each terminal session.
///   • ~/.claude/settings.json is patched with statusLine.command pointing at the script.
///
/// Output example:
///   my-project │ ⎇ feature/new-ui │ Usage: 47% ▓▓▓▓▓░░░░░ → Reset: 4:15 PM
/// </summary>
public static class StatuslineService
{
    // ── Paths ─────────────────────────────────────────────────────

    private static readonly string ClaudeDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".claude");

    private static readonly string ScriptPath = Path.Combine(ClaudeDir, "statusline-command.ps1");
    private static readonly string ConfigPath  = Path.Combine(ClaudeDir, "statusline-config.txt");
    private static readonly string SettingsPath = Path.Combine(ClaudeDir, "settings.json");

    private static readonly string CachePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "ClaudeUsageWin", "statusline-cache.json");

    // ── Public API ────────────────────────────────────────────────

    public static bool IsInstalled => File.Exists(ScriptPath);

    /// <summary>
    /// Called by App after every successful API refresh.
    /// Writes latest usage data so the PS1 script can read it without network calls.
    /// </summary>
    public static void WriteCache(int fiveHourPct, DateTime? resetAt)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(CachePath)!);
            var cache = new JsonObject
            {
                ["pct"]     = fiveHourPct,
                ["resetAt"] = resetAt?.ToUniversalTime().ToString("o"),
            };
            File.WriteAllText(CachePath,
                cache.ToJsonString(new JsonSerializerOptions { WriteIndented = false }));
        }
        catch (Exception ex)
        {
            Logger.LogError("StatuslineService.WriteCache", ex);
        }
    }

    public static void Install()
    {
        Directory.CreateDirectory(ClaudeDir);
        File.WriteAllText(ScriptPath, GetPowerShellScript());
        EnsureDefaultConfig();
        PatchSettings(enabled: true);
        Logger.Log("Statusline: installed → " + ScriptPath);
    }

    public static void Uninstall()
    {
        PatchSettings(enabled: false);
        Logger.Log("Statusline: uninstalled");
    }

    // ── Component config (read/write statusline-config.txt) ───────

    public static StatuslineConfig ReadConfig()
    {
        var cfg = new StatuslineConfig();
        if (!File.Exists(ConfigPath)) return cfg;

        try
        {
            foreach (var line in File.ReadAllLines(ConfigPath))
            {
                var parts = line.Split('=', 2);
                if (parts.Length != 2) continue;
                bool on = parts[1].Trim() == "1";
                cfg = parts[0].Trim() switch
                {
                    "SHOW_DIRECTORY"    => cfg with { ShowDirectory   = on },
                    "SHOW_BRANCH"       => cfg with { ShowBranch      = on },
                    "SHOW_USAGE"        => cfg with { ShowUsage       = on },
                    "SHOW_PROGRESS_BAR" => cfg with { ShowProgressBar = on },
                    "SHOW_RESET_TIME"   => cfg with { ShowResetTime   = on },
                    _                   => cfg,
                };
            }
        }
        catch (Exception ex) { Logger.LogError("StatuslineService.ReadConfig", ex); }

        return cfg;
    }

    public static void WriteConfig(StatuslineConfig cfg)
    {
        try
        {
            Directory.CreateDirectory(ClaudeDir);
            File.WriteAllText(ConfigPath,
                $"SHOW_DIRECTORY={B(cfg.ShowDirectory)}\n" +
                $"SHOW_BRANCH={B(cfg.ShowBranch)}\n" +
                $"SHOW_USAGE={B(cfg.ShowUsage)}\n" +
                $"SHOW_PROGRESS_BAR={B(cfg.ShowProgressBar)}\n" +
                $"SHOW_RESET_TIME={B(cfg.ShowResetTime)}\n");
        }
        catch (Exception ex) { Logger.LogError("StatuslineService.WriteConfig", ex); }

        static string B(bool v) => v ? "1" : "0";
    }

    // ── Internals ─────────────────────────────────────────────────

    private static void EnsureDefaultConfig()
    {
        if (!File.Exists(ConfigPath))
            WriteConfig(new StatuslineConfig());
    }

    private static void PatchSettings(bool enabled)
    {
        try
        {
            JsonNode? root = null;
            if (File.Exists(SettingsPath))
            {
                var text = File.ReadAllText(SettingsPath);
                if (!string.IsNullOrWhiteSpace(text))
                    root = JsonNode.Parse(text);
            }
            root ??= new JsonObject();

            if (enabled)
            {
                // Use forward slashes — PowerShell accepts both and it avoids JSON escaping pain
                var fwd = ScriptPath.Replace('\\', '/');
                root["statusLine"] = new JsonObject
                {
                    ["type"]    = "command",
                    ["command"] = $"powershell -NoProfile -ExecutionPolicy Bypass -File \"{fwd}\"",
                    ["padding"] = 0,
                };
            }
            else
            {
                if (root is JsonObject obj) obj.Remove("statusLine");
            }

            File.WriteAllText(SettingsPath,
                root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
        }
        catch (Exception ex) { Logger.LogError("StatuslineService.PatchSettings", ex); }
    }

    // ── PowerShell script (ported from the macOS bash reference) ──

    private static string GetPowerShellScript()
    {
        // Cache and config paths embedded at write time so the script is self-contained
        var cacheFwd    = CachePath.Replace('\\', '/');
        var configFwd   = ConfigPath.Replace('\\', '/');

        return $@"# Claude Usage Win — statusline for Claude Code on Windows
# Generated by ClaudeUsageWin. Reference: github.com/hamed-elfayome/Claude-Usage-Tracker

$configFile = '{configFwd}'
$cacheFile  = '{cacheFwd}'

# ── Read component config ────────────────────────────────────────
$showDir    = 1; $showBranch = 1; $showUsage = 1; $showBar = 1; $showReset = 1
if (Test-Path $configFile) {{
    Get-Content $configFile | ForEach-Object {{
        if ($_ -match '^SHOW_DIRECTORY=(.+)$')    {{ $showDir    = $Matches[1].Trim() }}
        if ($_ -match '^SHOW_BRANCH=(.+)$')        {{ $showBranch = $Matches[1].Trim() }}
        if ($_ -match '^SHOW_USAGE=(.+)$')         {{ $showUsage  = $Matches[1].Trim() }}
        if ($_ -match '^SHOW_PROGRESS_BAR=(.+)$')  {{ $showBar    = $Matches[1].Trim() }}
        if ($_ -match '^SHOW_RESET_TIME=(.+)$')    {{ $showReset  = $Matches[1].Trim() }}
    }}
}}

# ── Read stdin JSON (Claude Code sends current_dir etc.) ─────────
$inputJson = $null
try {{
    $raw = [Console]::In.ReadToEnd()
    if ($raw) {{ $inputJson = $raw | ConvertFrom-Json }}
}} catch {{ }}
$currentDir = ''
if ($inputJson -and $inputJson.current_dir) {{
    $currentDir = [System.IO.Path]::GetFileName($inputJson.current_dir.TrimEnd('/\'))
}}

# ── Read usage cache ─────────────────────────────────────────────
$pct = $null; $resetAt = $null
if (Test-Path $cacheFile) {{
    try {{
        $cache   = Get-Content $cacheFile -Raw | ConvertFrom-Json
        $pct     = [int]$cache.pct
        $resetAt = $cache.resetAt
    }} catch {{ }}
}}

# ── ANSI helpers ─────────────────────────────────────────────────
$ESC   = [char]27
$blue  = ""$ESC[0;34m""
$green = ""$ESC[0;32m""
$gray  = ""$ESC[0;90m""
$rst   = ""$ESC[0m""
$sep   = ""${{gray}} │ ${{rst}}""

function Get-UsageColor([int]$p) {{
    if ($p -le 10) {{ return ""$ESC[38;5;22m""  }}  # dark green
    if ($p -le 20) {{ return ""$ESC[38;5;28m""  }}  # soft green
    if ($p -le 30) {{ return ""$ESC[38;5;34m""  }}  # medium green
    if ($p -le 40) {{ return ""$ESC[38;5;100m"" }}  # green-yellow
    if ($p -le 50) {{ return ""$ESC[38;5;142m"" }}  # olive
    if ($p -le 60) {{ return ""$ESC[38;5;178m"" }}  # muted yellow
    if ($p -le 70) {{ return ""$ESC[38;5;172m"" }}  # yellow-orange
    if ($p -le 80) {{ return ""$ESC[38;5;166m"" }}  # darker orange
    if ($p -le 90) {{ return ""$ESC[38;5;160m"" }}  # dark red
    return ""$ESC[38;5;124m""                        # deep red
}}

# ── Build parts ──────────────────────────────────────────────────
$parts = [System.Collections.Generic.List[string]]::new()

# Directory
if ($showDir -eq '1' -and $currentDir) {{
    $parts.Add(""${{blue}}${{currentDir}}${{rst}}"")
}}

# Git branch
if ($showBranch -eq '1') {{
    try {{
        $branch = (git branch --show-current 2>$null) -join ''
        if ($branch) {{ $parts.Add(""${{green}}⎇ ${{branch}}${{rst}}"") }}
    }} catch {{ }}
}}

# Usage + bar + reset
if ($showUsage -eq '1') {{
    if ($null -ne $pct) {{
        $uc   = Get-UsageColor $pct
        $text = ""Usage: ${{pct}}%""

        if ($showBar -eq '1') {{
            $filled = if ($pct -ge 100) {{ 10 }} elseif ($pct -le 0) {{ 0 }} else {{ [math]::Round($pct / 10) }}
            $empty  = 10 - $filled
            $bar    = ' ' + ('▓' * $filled) + ('░' * $empty)
            $text  += $bar
        }}

        if ($showReset -eq '1' -and $resetAt) {{
            try {{
                $rt    = [datetime]::Parse($resetAt).ToLocalTime()
                $h     = $rt.Hour; $m = $rt.Minute.ToString('D2')
                $ampm  = if ($h -ge 12) {{ 'PM' }} else {{ 'AM' }}
                $h12   = if ($h -eq 0) {{ 12 }} elseif ($h -gt 12) {{ $h - 12 }} else {{ $h }}
                $text += "" → Reset: ${{h12}}:${{m}} ${{ampm}}""
            }} catch {{ }}
        }}

        $parts.Add(""${{uc}}${{text}}${{rst}}"")
    }} else {{
        $parts.Add(""${{gray}}Usage: ~${{rst}}"")
    }}
}}

Write-Output ($parts -join $sep)
";
    }
}

/// <summary>Statusline component visibility settings (mirrored in statusline-config.txt).</summary>
public record StatuslineConfig
{
    public bool ShowDirectory   { get; init; } = true;
    public bool ShowBranch      { get; init; } = true;
    public bool ShowUsage       { get; init; } = true;
    public bool ShowProgressBar { get; init; } = true;
    public bool ShowResetTime   { get; init; } = true;
}
