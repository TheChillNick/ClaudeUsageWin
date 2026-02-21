# Statusline Feature Parity Design — 2026-02-21

## Goal

Port the terminal statusline feature from the macOS Claude-Usage-Tracker reference project to ClaudeUsageWin (Windows/WPF). Match the reference output format:

```
my-project │ ⎇ feature/new-ui │ Usage: 47% ▓▓▓▓▓░░░░░ → Reset: 4:15 PM
```

## Architecture

### Data flow

1. `App.xaml.cs → RefreshData()` calls `StatuslineService.WriteCache(pct, resetAt)` on every successful API refresh.
2. Cache written to `%APPDATA%\ClaudeUsageWin\statusline-cache.json` — no network calls from the script.
3. Claude Code invokes `powershell … ~/.claude/statusline-command.ps1` per terminal session start, passing `{"current_dir": "…"}` via stdin.
4. Script reads cache + `~/.claude/statusline-config.txt` and writes ANSI-colored output.

### Components (user-configurable)

| Toggle | Output segment |
|--------|---------------|
| Directory | `my-project` (basename of `current_dir`) |
| Git branch | `⎇ feature/new-ui` |
| Usage % | `Usage: 47%` with 10-level color gradient |
| Progress bar | `▓▓▓▓▓░░░░░` (10 blocks) |
| Reset time | `→ Reset: 4:15 PM` |

### Files

| File | Purpose |
|------|---------|
| `~/.claude/statusline-command.ps1` | PowerShell script, generated & managed by app |
| `~/.claude/statusline-config.txt` | Component toggles (`SHOW_DIRECTORY=1` etc.) |
| `~/.claude/settings.json` | Patched with `statusLine.command` when installed |
| `%APPDATA%\ClaudeUsageWin\statusline-cache.json` | Usage cache `{pct, resetAt}` |

### Color gradient (ported from reference bash script)

| Range | ANSI 256 color | Visual |
|-------|---------------|--------|
| 0–10% | 22 | dark green |
| 11–20% | 28 | soft green |
| 21–30% | 34 | medium green |
| 31–40% | 100 | green-yellow |
| 41–50% | 142 | olive |
| 51–60% | 178 | muted yellow |
| 61–70% | 172 | yellow-orange |
| 71–80% | 166 | darker orange |
| 81–90% | 160 | dark red |
| 91–100% | 124 | deep red |

## Changes

- `Services/StatuslineService.cs` — complete rewrite with `WriteCache()`, `ReadConfig()`, `WriteConfig()`, `Install()`, `Uninstall()`
- `App.xaml.cs` — `StatuslineService.WriteCache(data.FiveHourPct, data.FiveHourResetAt)` after API success
- `SettingsWindow.xaml` — 5 component checkboxes + preview example string
- `SettingsWindow.xaml.cs` — `LoadStatuslineComponentToggles()` + `StatuslineComponent_Changed()`
