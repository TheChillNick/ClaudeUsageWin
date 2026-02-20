<div align="center">

# Claude Usage Win

**Real-time Claude AI usage monitoring from your Windows system tray.**

[![Windows 11](https://img.shields.io/badge/Windows-11-0078D4?logo=windows11&logoColor=white)](https://www.microsoft.com/windows)
[![.NET 8](https://img.shields.io/badge/.NET-8.0-512BD4?logo=dotnet&logoColor=white)](https://dotnet.microsoft.com)
[![Version](https://img.shields.io/github/v/release/stepantech/ClaudeUsageWin?color=orange&label=version)](https://github.com/stepantech/ClaudeUsageWin/releases)
[![License](https://img.shields.io/badge/license-MIT-22c55e)](LICENSE)

*A Windows port of [Claude Usage Tracker](https://github.com/hamed-elfayome/Claude-Usage-Tracker) — inspired by the macOS original, built for the Windows desktop.*

</div>

---

## Why This Exists

Claude Pro and Team subscriptions have a 5-hour rolling usage window. There's no built-in indicator anywhere, so you never know how close you are to the limit until you hit it mid-conversation. Claude Usage Win puts that number in your system tray — always visible, always up to date, zero maintenance.

---

## Features

### Usage Tracking
| Feature | Description |
|---------|-------------|
| **Real-time monitoring** | 5-hour window and weekly usage, auto-refreshed every 30s–5min |
| **Color-coded progress bars** | Green → yellow → red as you approach your limit |
| **Remaining / Used toggle** | Switch between "% used" and "% remaining" with one click |
| **Sparkline chart** | Mini history graph shows your usage trend at a glance |
| **Contextual Insights panel** | Click 💡 to see usage rate/hr, estimated time to limit, and session peak |
| **Today's stats** | Message count and token count for the current day |

### Authentication
| Feature | Description |
|---------|-------------|
| **Zero-config OAuth** | Reads Claude Code credentials (`~/.claude/.credentials.json`) automatically — no setup needed if you use Claude Code |
| **Manual session key** | Paste your `sessionKey` cookie if Claude Code isn't installed |
| **Auto token refresh** | OAuth tokens are refreshed automatically before expiry |
| **Multi-profile support** | Manage multiple Claude accounts (personal + work); switch without restarting |
| **First-run Setup Wizard** | Friendly 3-step onboarding guides new users through account connection |

### Notifications
| Feature | Description |
|---------|-------------|
| **Threshold alerts** | Windows notifications at configurable thresholds (default: 75%, 90%, 95%) |
| **Independent toggles** | Enable/disable notifications for the 5-hour and weekly windows separately |
| **Network resilience** | Shows last-known data when offline; auto-refreshes when reconnected |

### Appearance & Window Management
| Feature | Description |
|---------|-------------|
| **3 tray icon styles** | Percentage number, filled bar, or colored dot |
| **Opacity control** | Adjustable popup transparency (20–100%) |
| **Usage window scale** | Resize popup content independently of window chrome (75%–150%) |
| **Always on top** | Pin above all windows, or let it sit behind your work |
| **Pin button** | Keep the popup open while you work without it auto-closing |
| **Position memory** | Both popup and Settings window remember their last position and size |
| **Non-modal Settings** | Interact with the popup and Settings window simultaneously |
| **Snap restore** | Drag a maximized Settings window to restore it to floating mode |
| **Launch at startup** | Optional auto-start via Windows registry |

### Developer Tools
| Feature | Description |
|---------|-------------|
| **Copy Log to Clipboard** | Tray → Developer → Copy Log |
| **Open Log File** | Opens `%AppData%\ClaudeUsageWin\logs\debug.log` in your default text editor |
| **Export Log** | Save-dialog to export the log anywhere on disk |
| **Live log watcher** | `scripts/dev-log.ps1` — color-coded, live-tailing PowerShell log viewer |

---

## Installation

### Option A — Download the executable *(recommended)*

1. Go to the **[Releases](../../releases)** page
2. Download `ClaudeUsage.exe` from the latest release
3. Run it — no installation, no dependencies, no admin rights required

The app stores its config in `%AppData%\ClaudeUsageWin\config.json` and logs to `%AppData%\ClaudeUsageWin\logs\debug.log`.

### Option B — Build from source

**Prerequisites:** [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) · Windows 10 or 11

```bash
git clone https://github.com/stepantech/ClaudeUsageWin.git
cd ClaudeUsageWin

# Run in development
dotnet run

# Publish single-file self-contained EXE
dotnet publish -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -o dist
# → dist/ClaudeUsage.exe (~65 MB, no runtime required)
```

---

## Configuration

Config lives at `%AppData%\ClaudeUsageWin\config.json` and is managed through the Settings window. All fields have sensible defaults.

| Key | Default | Description |
|-----|---------|-------------|
| `refreshInterval` | `60` | Seconds between auto-refreshes (30, 60, 120, or 300) |
| `opacityPct` | `95` | Popup window opacity, 20–100% |
| `alwaysOnTop` | `true` | Keep popup above all other windows |
| `iconStyle` | `"Percentage"` | Tray icon style: `"Percentage"`, `"Bar"`, or `"Dot"` |
| `notifyFiveHour` | `true` | Enable notifications for the 5-hour window |
| `notifyWeekly` | `true` | Enable notifications for the weekly window |
| `notifyThresholds` | `[75, 90, 95]` | Usage percentages that trigger a notification |
| `launchAtStartup` | `false` | Register app in Windows startup (HKCU Run key) |
| `showRemaining` | `false` | Show remaining % instead of used % |
| `popupScale` | `1.0` | Content scale factor for the popup (0.75–1.50) |

---

## Tech Stack

| Technology | Purpose | Notes |
|-----------|---------|-------|
| **C# 12 / .NET 8** | Application runtime | Targets `net8.0-windows`; single-file self-contained publish |
| **WPF** | Popup and Settings UI | XAML layout, hardware-accelerated rendering, smooth animations |
| **Windows Forms NotifyIcon** | System tray icon | WPF has no native tray icon support |
| **System.Text.Json** | Config and profile serialization | Built-in, no extra dependencies |
| **WinHttpHandler** | HTTP transport | Better Windows proxy and auth handling than the default `SocketsHttpHandler` |
| **GDI+** | Tray icon rendering | Draws percentage/bar/dot bitmaps at runtime into 32×32 icons |
| **IL trimming (partial)** | Binary size reduction | `TrimMode=partial` removes unused code while keeping WPF/WinForms intact |

---

## What Changed

### v1.1.0 *(2026-02-20)*

**Bug fixes:**
- Scale slider now controls the **usage popup**, not the Settings window itself
- Fixed double red line artifact at 100% on progress bars (WPF subpixel clipping)
- Eliminated the Settings "ding" sound — Settings is now non-modal (`Show()` not `ShowDialog()`)
- Usage popup is fully interactive while Settings is open
- Window positions remembered and restored across sessions
- Drag Settings from maximized → restores to floating window (matches Windows 11 snap behavior)
- Toggle popup via tray icon now respects the user's last dragged position

**New features:**
- 💡 **Contextual Insights panel** — usage rate (%/hr), estimated time to limit, session peak
- 👥 **Multi-profile support** — manage and switch multiple Claude accounts from Settings
- 🧙 **First-run Setup Wizard** — 3-step onboarding (Welcome → Auth → Done) for new users
- ℹ️ **About / Updates section** — version display and "Check for updates" in Settings
- 🛠️ **Developer log tools** — copy, open, and export the debug log from the tray menu
- 📋 **Live log watcher** — `scripts/dev-log.ps1` PowerShell live-tail viewer

**Performance:**
- Enabled IL partial trimming on publish; self-contained EXE reduced in size

### v1.0.0 *(2026-02-18)*

Initial release. System tray usage monitor with OAuth auto-auth via Claude Code, manual session key fallback, dark-themed floating popup, color-coded 5-hour and weekly progress bars, threshold notifications, sparkline history chart, opacity and always-on-top controls, tray icon style selector, and launch-at-startup support.

---

## Contributing

Issues and PRs welcome. Please open an issue before submitting a large PR so we can discuss the approach.

```bash
git clone https://github.com/stepantech/ClaudeUsageWin.git
cd ClaudeUsageWin
dotnet build       # verify it compiles
dotnet run         # run in dev mode
```

See [CONTRIBUTING.md](CONTRIBUTING.md) for full guidelines and the manual test checklist.

---

## License

MIT — see [LICENSE](LICENSE) for details.

---

<div align="center">

Built for the Windows Claude users who live in the system tray.

</div>
