# Claude Usage Win

**Track your Claude AI usage limits from the Windows system tray.**

A Windows port inspired by [Claude Usage Tracker](https://github.com/hamed-elfayome/Claude-Usage-Tracker) for macOS. Monitor your Claude Pro/Team 5-hour window and weekly usage at a glance, directly from your system tray.

![Windows 11](https://img.shields.io/badge/Windows-11-blue) ![.NET 8](https://img.shields.io/badge/.NET-8.0-purple) ![License](https://img.shields.io/badge/license-MIT-green)

## Features

- **Real-time usage tracking** -- 5-hour window and weekly usage displayed in a dark-themed popup
- **Auto-authentication** -- reads Claude Code credentials (`~/.claude/.credentials.json`) with zero configuration
- **Color-coded progress bars** -- green, yellow, and red indicating usage severity
- **Configurable threshold notifications** -- get notified at 75%, 90%, and 95% usage
- **Multiple icon styles** -- Percentage, Bar, or Dot display in the system tray
- **Opacity control** -- adjustable popup transparency (20--100%)
- **Always on top** -- keep the usage popup visible above other windows
- **Launch at Windows startup** -- optional auto-start via the Windows registry
- **Network connectivity awareness** -- gracefully handles offline/online transitions
- **Remaining vs used toggle** -- show percentage remaining or percentage used
- **Manual session key fallback** -- paste your `sessionKey` cookie if Claude Code is not installed

## Installation

### Option A: Download the executable (recommended)

1. Go to the [Releases](../../releases) page.
2. Download `ClaudeUsage.exe`.
3. Run it -- no dependencies or installation required.

### Option B: Build from source

Requires the [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0).

```bash
git clone https://github.com/user/claude-usage-win.git
cd claude-usage-win
dotnet publish -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -o dist
```

The compiled executable will be in the `dist/` folder.

## Getting Started

1. **Download and run** `ClaudeUsage.exe`.
2. **If Claude Code is installed** -- the app auto-authenticates using your existing credentials. You are done.
3. **If Claude Code is not installed** -- open Settings from the tray icon context menu, then paste your `sessionKey` cookie (see below).
4. **Click the tray icon** to view your current usage.

## How to Get Your Session Key

If you do not have Claude Code installed, you can authenticate manually:

1. Open [claude.ai](https://claude.ai) in your browser and log in.
2. Open DevTools (`F12` or `Ctrl+Shift+I`).
3. Go to the **Application** tab (Chrome) or **Storage** tab (Firefox).
4. Under **Cookies**, select `https://claude.ai`.
5. Find the cookie named `sessionKey` and copy its value.
6. Open the app's **Settings** and paste the value into the Session Key field.

## GitHub Actions

The repository includes a GitHub Actions workflow that automatically builds a self-contained `.exe` when you push a version tag:

```bash
git tag v1.0.0
git push origin v1.0.0
```

This triggers a build that publishes the artifact to the release.

## Project Structure

```
ClaudeUsageWin/
  App.xaml / App.xaml.cs          Application entry point and tray icon setup
  MainWindow.xaml / .xaml.cs      Usage popup window
  SettingsWindow.xaml / .xaml.cs   Settings dialog
  Models/
    UsageData.cs                  API response models
  Services/
    ClaudeApiClient.cs            HTTP client for the Claude usage API
    ConfigService.cs              Persistent settings (JSON in %AppData%)
    CredentialsReader.cs          Reads Claude Code credentials
    Logger.cs                     Debug logging
    StartupService.cs             Windows startup registration
    ThresholdNotifier.cs          Usage threshold notifications
  .github/workflows/
    build.yml                     CI/CD release workflow
```

## Privacy

- All data stays on your machine. The app only contacts `api.claude.ai` to fetch your usage.
- Zero telemetry, analytics, or tracking.
- Credentials are stored locally in `%AppData%`.

## License

[MIT](LICENSE)
