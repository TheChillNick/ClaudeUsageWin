# Claude Usage Win

**Track your Claude AI usage limits from the Windows system tray.**

A Windows port inspired by [Claude Usage Tracker](https://github.com/hamed-elfayome/Claude-Usage-Tracker) for macOS. Monitor your Claude Pro/Team 5-hour window and weekly usage at a glance, directly from your system tray.

![Windows 11](https://img.shields.io/badge/Windows-11-blue) ![.NET 8](https://img.shields.io/badge/.NET-8.0-purple) ![Version](https://img.shields.io/badge/version-1.1.0-orange) ![License](https://img.shields.io/badge/license-MIT-green)

---

## Features

### Usage Tracking
- **Real-time usage tracking** — 5-hour window and weekly usage displayed in a dark-themed popup
- **Color-coded progress bars** — green → yellow → red as usage climbs
- **Remaining vs used toggle** — switch between "% used" and "% remaining" display
- **Sparkline chart** — recent usage history plotted in the popup
- **Contextual Insights panel** — click 💡 to see usage rate/hr, estimated time to limit, and session peak

### Authentication
- **Auto-authentication** — reads Claude Code credentials (`~/.claude/.credentials.json`) with zero configuration
- **Manual session key fallback** — paste your `sessionKey` cookie if Claude Code is not installed
- **Multi-profile support** — manage multiple Claude accounts in Settings → Profiles; switch without restarting
- **First-run Setup Wizard** — friendly 3-step onboarding for new users

### Notifications
- **Threshold notifications** — configurable alerts at 75%, 90%, and 95% usage (5-hour and weekly, independently)
- **Network awareness** — gracefully handles offline/online transitions; shows last known data

### Appearance & Behavior
- **Multiple tray icon styles** — Percentage, Bar, or Dot
- **Opacity control** — adjustable popup transparency (20–100%)
- **Usage window scale** — resize the popup content independently of the window chrome
- **Always on top** — pin the popup above all other windows
- **Pin button** — keep the popup open without it auto-closing
- **Launch at Windows startup** — optional auto-start via the Windows registry

### Window Management
- **Position memory** — both the usage popup and Settings window remember their last position and size
- **Non-modal Settings** — Settings and the usage popup are independent; interact with both simultaneously
- **Snap restore** — drag the Settings title bar from a maximized state to restore it to a floating window
- **Resize grip** — drag the corner of the popup to resize it

### Developer Tools
- **Copy Log to Clipboard** — tray → Developer → Copy Log to Clipboard
- **Open Log File** — opens `%AppData%\ClaudeUsageWin\logs\debug.log` in your text editor
- **Export Log…** — save the log file anywhere via a save dialog
- **Live log watcher** — `scripts/dev-log.ps1` (see [Development](#development))

---

## Installation

### Option A: Download the executable (recommended)

1. Go to the [Releases](../../releases) page.
2. Download `ClaudeUsage.exe`.
3. Run it — no dependencies or installation required.

### Option B: Build from source

Requires the [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0).

```bash
git clone https://github.com/stepantech/claude-usage-win.git
cd claude-usage-win/ClaudeUsageWin
dotnet publish -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -o dist
```

The compiled executable will be in `ClaudeUsageWin/dist/ClaudeUsage.exe`.

---

## Getting Started

1. **Download and run** `ClaudeUsage.exe`.
2. **If Claude Code is installed** — the app auto-authenticates. You are done.
3. **If Claude Code is not installed** — the Setup Wizard appears on first run; paste your `sessionKey` cookie (see below).
4. **Click the tray icon** to view your current usage.

### How to Get Your Session Key (manual auth)

1. Open [claude.ai](https://claude.ai) in your browser and log in.
2. Open DevTools (`F12` or `Ctrl+Shift+I`).
3. Go to the **Application** tab (Chrome) or **Storage** tab (Firefox).
4. Under **Cookies**, select `https://claude.ai`.
5. Find the cookie named `sessionKey` and copy its value.
6. Open Settings → Authentication and paste it into the Session Key field.

---

## Settings Reference

| Setting | Description |
|---------|-------------|
| **Authentication** | Auto (Claude Code) or manual session key |
| **Org ID** | Auto-detected; override if needed |
| **Profiles** | Add/switch/delete named credential profiles |
| **Refresh interval** | 30s / 1min / 2min / 5min |
| **Opacity** | Popup transparency (20–100%) |
| **Usage Window Scale** | Scale the popup content (75–150%) |
| **Always on top** | Keep popup above all other windows |
| **Tray icon style** | Percentage / Bar / Dot |
| **Notifications** | Thresholds for 5-hour and weekly usage |
| **Launch at startup** | Auto-start with Windows |
| **Claude Code Statusline** | Install/remove the statusline integration |
| **About** | Version display and update check |

---

## Known Issues

| Issue | Details | Workaround |
|-------|---------|------------|
| **Profile "Add" uses auto-generated names** | The Add button in the Profiles tab creates a profile named "Profile N" with no inline name input. | Rename via the profile's JSON file in `%AppData%\ClaudeUsageWin\profiles\`. A proper input dialog is planned. |
| **Update checker URL is a placeholder** | The "Check for updates" button points to `stepantech/claude-usage-win` on GitHub — this may 404 until the repo is published there. | The button fails gracefully with a red status message. |
| **EXE size is ~69 MB** | Self-contained .NET 8 bundles the entire runtime. IL partial trimming is enabled but WPF/WinForms assemblies cannot be trimmed further. | Expected for self-contained .NET apps. Framework-dependent builds (~5 MB) require .NET 8 to be installed on the target machine. |
| **Contextual Insights shows "not enough data" on first run** | Insights require at least 2 history data points. History is written to disk on each successful API fetch. | Wait for a second refresh cycle (default: 1 minute). |
| **Setup Wizard: skipping session key with no Claude Code** | If the user skips the session key field and has no Claude Code credentials, the app starts but immediately shows an API error in the popup. | Click Refresh or open Settings to add credentials. |
| **Drag-from-maximized positioning** | When dragging Settings from a maximized state, the restored window position may be slightly off from the cursor if the DPI scaling is not 100%. | Drag slightly to reposition after restore. |

---

## Development

### Building

```bash
cd ClaudeUsageWin
dotnet build          # debug build (fast)
dotnet publish -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -o dist   # release EXE
```

### Watching Logs

The app writes to `%AppData%\ClaudeUsageWin\logs\debug.log`. Use the included script to watch it live:

```powershell
# Live tail (color-coded INF/ERR)
.\scripts\dev-log.ps1

# Show last 50 lines then exit
.\scripts\dev-log.ps1 -Last 50

# Show only errors
.\scripts\dev-log.ps1 -Errors

# Copy full log to clipboard (paste into Claude Code, issue tracker, etc.)
.\scripts\dev-log.ps1 -Copy

# Clear the log
.\scripts\dev-log.ps1 -Clear
```

You can also use the tray icon → **Developer** menu to copy, open, or export the log without leaving the app.

### Project Structure

```
ClaudeUsageWin/
  App.xaml / App.xaml.cs              Application entry point, tray icon, orchestration
  MainWindow.xaml / .xaml.cs          Usage popup window
  SettingsWindow.xaml / .xaml.cs      Settings window (non-modal)
  SetupWizardWindow.xaml / .xaml.cs   First-run setup wizard
  Models/
    UsageData.cs                      API response models
  Services/
    ClaudeApiClient.cs                HTTP client for Claude usage API
    ConfigService.cs                  Persistent config (JSON in %AppData%)
    CredentialsReader.cs              Reads Claude Code OAuth credentials
    LocalStatsReader.cs               Reads local ~/.claude/stats-cache.json
    Logger.cs                         Debug log writer (%AppData%\ClaudeUsageWin\logs\)
    ProfileService.cs                 Multi-profile CRUD (%AppData%\ClaudeUsageWin\profiles\)
    StartupService.cs                 Windows startup registry entry
    StatuslineService.cs              Claude Code statusline integration
    ThresholdNotifier.cs              Usage threshold tray notifications
    UsageHistory.cs                   Sparkline history (JSON in %AppData%)
  scripts/
    dev-log.ps1                       Development log watcher / exporter
  docs/plans/
    2026-02-20-bugfix-and-swift-features.md   Implementation plan
  .github/workflows/
    build.yml                         CI/CD release workflow (tag → EXE artifact)
```

### Data Files

| File | Path | Description |
|------|------|-------------|
| Config | `%AppData%\ClaudeUsageWin\config.json` | All app settings |
| Profiles | `%AppData%\ClaudeUsageWin\profiles\<id>.json` | Per-profile credentials |
| Active profile | `%AppData%\ClaudeUsageWin\active-profile.txt` | Active profile ID |
| Usage history | `%AppData%\ClaudeUsageWin\history.json` | Sparkline data points |
| Debug log | `%AppData%\ClaudeUsageWin\logs\debug.log` | Rolling log (2 MB max) |
| Log archive | `%AppData%\ClaudeUsageWin\logs\debug.log.old` | Previous log file |

---

## GitHub Actions

Push a version tag to trigger an automatic release build:

```bash
git tag v1.1.0
git push origin v1.1.0
```

This runs `.github/workflows/build.yml` and publishes `ClaudeUsage.exe` as a release artifact.

---

## Privacy

- All data stays on your machine. The app contacts only `api.anthropic.com` (OAuth path) or `api.claude.ai` (session key path) to fetch your usage.
- Zero telemetry, analytics, or tracking of any kind.
- Credentials and config are stored locally in `%AppData%\ClaudeUsageWin\`.

---

## License

[MIT](LICENSE)
