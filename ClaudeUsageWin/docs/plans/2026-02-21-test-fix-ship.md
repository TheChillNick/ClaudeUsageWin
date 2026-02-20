# ClaudeUsageWin — Test, Fix, and Ship v1.1.0

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Audit the codebase against TESTING.md, fix identified bugs, publish a production EXE, create the public GitHub repo `ClaudeUsageWin`, push the code, publish the v1.1.0 release, and write a developer-attractive README.

**Architecture:** WPF app (.NET 8), single-file self-contained EXE. GitHub repo created via the `mcp__plugin_github_github__*` MCP tools (gh CLI is not installed). README written in-place then pushed as part of the repo setup.

**Tech Stack:** C# 12 / .NET 8 / WPF / GitHub MCP API

---

## PHASE 1 — BUILD VERIFICATION

### Task 1: Confirm clean build

**Files:** none modified

**Step 1: Run build**

```bash
cd "C:/Users/Štěpán/Claude Home/ClaudeUsageWin"
dotnet build 2>&1 | tail -10
```

Expected output:
```
Vytváření sestavení bylo úspěšně dokončeno.
    0 upozornění
    Počet chyb: 0
```

**Step 2: If build fails, stop and report errors.** Do not proceed to Phase 2.

---

## PHASE 2 — BUG FIXES (2 confirmed bugs from static analysis)

### Task 2: Fix — TogglePopup ignores saved position

**Problem:** `TogglePopup()` in `App.xaml.cs` always calls `_popup.PositionNearTray()` when
re-showing the popup, overwriting the user's dragged position. The saved `PopupLeft`/`PopupTop`
in config is only used at startup.

**Files:**
- Modify: `App.xaml.cs` — `TogglePopup()` method (~line 308)

**Step 1: Read current TogglePopup**

```csharp
// Current (broken):
private void TogglePopup()
{
    Dispatcher.Invoke(() =>
    {
        if (_popup.IsVisible)
            _popup.Hide();
        else
        {
            _popup.Show();
            _popup.PositionNearTray();   // ← always overrides saved position
            _popup.Activate();
        }
    });
}
```

**Step 2: Replace with position-aware version**

Find in `App.xaml.cs`:
```csharp
        else
        {
            _popup.Show();
            _popup.PositionNearTray();
            _popup.Activate();
        }
```

Replace with:
```csharp
        else
        {
            _popup.Show();
            // Only snap to tray if the user hasn't manually positioned the window
            if (double.IsNaN(_config.PopupLeft) || double.IsNaN(_config.PopupTop))
                _popup.PositionNearTray();
            _popup.Activate();
        }
```

**Step 3: Build to verify**

```bash
cd "C:/Users/Štěpán/Claude Home/ClaudeUsageWin"
dotnet build 2>&1 | tail -5
```
Expected: 0 errors.

**Step 4: Commit**

```bash
cd "C:/Users/Štěpán/Claude Home/ClaudeUsageWin"
git add App.xaml.cs
git commit -m "fix: toggle popup respects saved position instead of always snapping to tray"
```

---

### Task 3: Fix — "(Default)" profile name shown for fresh installs

**Problem:** `ProfileService.GetActive()` returns a synthetic `Profile { Name = "Default" }` even
when the user has never set up any profiles (the profiles directory doesn't exist). This causes
`(Default)` to appear in the popup header for all new users, which is confusing.

**Files:**
- Modify: `App.xaml.cs` — startup block and `ReloadProfile()` (~line 153, ~line 614)

**Step 1: Fix in OnStartup**

Find in `App.xaml.cs`:
```csharp
        _popup.SetProfileName(ProfileService.GetActive()?.Name ?? "");
```

Replace with:
```csharp
        // Only show profile name if the user has explicitly set up profiles
        var activeProfile = ProfileService.GetActive();
        var profilesExist = Directory.Exists(
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                         "ClaudeUsageWin", "profiles"));
        _popup.SetProfileName(profilesExist ? (activeProfile?.Name ?? "") : "");
```

**Step 2: Fix in ReloadProfile()**

Find in `App.xaml.cs`:
```csharp
    public void ReloadProfile()
    {
        var profile = ProfileService.GetActive();
        if (profile is not null)
        {
            _config = _config with { SessionKey = profile.SessionKey, OrgId = profile.OrgId };
            _popup.SetProfileName(profile.Name);
            RefreshData();
        }
    }
```

Replace with:
```csharp
    public void ReloadProfile()
    {
        var profile = ProfileService.GetActive();
        if (profile is not null)
        {
            _config = _config with { SessionKey = profile.SessionKey, OrgId = profile.OrgId };
            var profilesExist = Directory.Exists(
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                             "ClaudeUsageWin", "profiles"));
            _popup.SetProfileName(profilesExist ? profile.Name : "");
            RefreshData();
        }
    }
```

**Step 3: Build to verify**

```bash
cd "C:/Users/Štěpán/Claude Home/ClaudeUsageWin"
dotnet build 2>&1 | tail -5
```
Expected: 0 errors.

**Step 4: Commit**

```bash
cd "C:/Users/Štěpán/Claude Home/ClaudeUsageWin"
git add App.xaml.cs
git commit -m "fix: hide profile name in popup for users without any saved profiles"
```

---

### Task 4: Fix minor — dead assignment in EyeToggleBtn_Click

**Problem:** Line 127 in `SettingsWindow.xaml.cs` sets `EyeToggleBtn.Content` and line 128
immediately overwrites it. Dead code, confusing to readers.

**Files:**
- Modify: `SettingsWindow.xaml.cs` — `EyeToggleBtn_Click` (~line 127)

**Step 1: Find the duplicate lines**

In `SettingsWindow.xaml.cs`, inside `EyeToggleBtn_Click`, find:
```csharp
            EyeToggleBtn.Content     = "\uD83D\uDE47"; // 🚧 construction → use a text X or close eye
            EyeToggleBtn.Content     = "\uD83D\uDE48"; // 🙈 see-no-evil
```

**Step 2: Remove the dead first line**

Replace those two lines with just:
```csharp
            EyeToggleBtn.Content     = "\uD83D\uDE48"; // 🙈 see-no-evil (hide key)
```

**Step 3: Build to verify**

```bash
cd "C:/Users/Štěpán/Claude Home/ClaudeUsageWin"
dotnet build 2>&1 | tail -5
```

**Step 4: Commit**

```bash
cd "C:/Users/Štěpán/Claude Home/ClaudeUsageWin"
git add SettingsWindow.xaml.cs
git commit -m "fix: remove dead assignment in eye toggle button handler"
```

---

## PHASE 3 — PUBLISH PRODUCTION EXE

### Task 5: Build and publish production EXE

**Files:** none modified — publishing existing code

**Step 1: Publish single-file self-contained EXE**

```bash
cd "C:/Users/Štěpán/Claude Home/ClaudeUsageWin"
dotnet publish -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -o dist 2>&1 | tail -15
```

Expected: no errors, EXE created in `dist/`.

**Step 2: Verify EXE exists and check size**

```bash
ls -lh "C:/Users/Štěpán/Claude Home/ClaudeUsageWin/dist/ClaudeUsage.exe"
```

Expected: file exists, size ~50–80 MB.

**Step 3: Commit the dist EXE is NOT committed to git** (it's a binary). Verify `.gitignore` covers it:

```bash
cd "C:/Users/Štěpán/Claude Home/ClaudeUsageWin"
cat .gitignore 2>/dev/null || echo "no .gitignore"
```

If `dist/` is not in `.gitignore`, create/update `.gitignore`:

```
bin/
obj/
dist/
*.user
.vs/
```

Then:
```bash
git add .gitignore
git commit -m "chore: add .gitignore" 2>/dev/null || true
```

---

## PHASE 4 — WRITE ATTRACTIVE README

### Task 6: Write production-quality README.md

**Files:**
- Modify: `README.md` (overwrite completely)

The README must be visually compelling to developers — similar to polished open-source projects.
Structure:

1. **Hero block**: app name, tagline, badge row (Windows 11, .NET 8, version, license), screenshot placeholder
2. **Why this exists** (2–3 sentences, problem/solution framing)
3. **Feature showcase** — grouped with icons, each feature clearly named
4. **Installation** — two paths: download EXE, build from source
5. **Screenshots** section (placeholder)
6. **Configuration** — where config lives, what fields do
7. **Tech stack** — table with technology + purpose + why chosen
8. **What changed** — version history inline (v1.0.0 and v1.1.0 with bullet points)
9. **Contributing** — brief
10. **License**

**Step 1: Write the README**

Write the following content to `README.md` (overwrite the existing file):

````markdown
<div align="center">

# Claude Usage Win

**Real-time Claude AI usage monitoring from your Windows system tray.**

[![Windows 11](https://img.shields.io/badge/Windows-11-0078D4?logo=windows11&logoColor=white)](https://www.microsoft.com/windows)
[![.NET 8](https://img.shields.io/badge/.NET-8.0-512BD4?logo=dotnet&logoColor=white)](https://dotnet.microsoft.com)
[![Version](https://img.shields.io/github/v/release/stepantech/ClaudeUsageWin?color=orange)](https://github.com/stepantech/ClaudeUsageWin/releases)
[![License](https://img.shields.io/badge/license-MIT-22c55e)](LICENSE)

*A Windows port of [Claude Usage Tracker](https://github.com/hamed-elfayome/Claude-Usage-Tracker) — inspired by the macOS original, built for the Windows desktop.*

</div>

---

## Why This Exists

Claude Pro and Team subscriptions have a 5-hour rolling usage window. There's no built-in indicator, so you never know how close you are to hitting the limit until it's too late. Claude Usage Win puts that number in your system tray — always visible, always up to date.

---

## Features

### Usage Tracking
| Feature | Description |
|---------|-------------|
| **Real-time monitoring** | 5-hour window and weekly usage, refreshed automatically (30s–5min interval) |
| **Color-coded progress bars** | Green → yellow → red as you approach limits |
| **Remaining / Used toggle** | Switch between "% used" and "% remaining" with one click |
| **Sparkline chart** | Mini history graph in the popup shows your usage trend |
| **Contextual Insights panel** | Click 💡 to see usage rate/hr, estimated time to limit, and session peak |
| **Today's stats** | Message and token counts for the current day |

### Authentication
| Feature | Description |
|---------|-------------|
| **Zero-config OAuth** | Reads Claude Code credentials (`~/.claude/.credentials.json`) automatically — no setup needed if you use Claude Code |
| **Manual session key** | Paste your `sessionKey` cookie if Claude Code isn't installed |
| **Auto token refresh** | OAuth tokens are refreshed automatically before expiry |
| **Multi-profile support** | Manage multiple Claude accounts; switch without restarting |
| **First-run Setup Wizard** | Friendly 3-step onboarding guides new users to connect their account |

### Notifications
| Feature | Description |
|---------|-------------|
| **Threshold alerts** | Windows notifications at configurable thresholds (default: 75%, 90%, 95%) |
| **Independent toggles** | Enable/disable notifications for 5-hour and weekly windows separately |
| **Network resilience** | Shows last-known data when offline; auto-refreshes when reconnected |

### Appearance & Window Management
| Feature | Description |
|---------|-------------|
| **3 tray icon styles** | Percentage number, filled bar, or colored dot |
| **Opacity control** | Adjustable popup transparency (20–100%) |
| **Usage window scale** | Resize the popup content independently of the window chrome |
| **Always on top** | Pin above all windows; or let it sit behind your work |
| **Pin button** | Keep the popup open while you work |
| **Position memory** | Both popup and Settings window remember their last position and size |
| **Non-modal Settings** | Use the popup and Settings simultaneously — no modal blocking |
| **Snap restore** | Drag a maximized Settings window to restore it to floating mode |
| **Launch at startup** | Optional auto-start via Windows registry |

### Developer Tools
| Feature | Description |
|---------|-------------|
| **Copy Log to Clipboard** | Tray → Developer → Copy Log |
| **Open Log File** | Opens `%AppData%\ClaudeUsageWin\logs\debug.log` in your default editor |
| **Export Log** | Save-dialog to export the log anywhere |
| **Live log watcher** | `scripts/dev-log.ps1` — color-coded, live-tailing terminal log viewer |

---

## Installation

### Option A — Download the executable *(recommended)*

1. Go to the **[Releases](../../releases)** page
2. Download `ClaudeUsage.exe` from the latest release
3. Run it — no installation, no dependencies, no admin rights required

The app stores its config in `%AppData%\ClaudeUsageWin\config.json` and writes logs to `%AppData%\ClaudeUsageWin\logs\`.

### Option B — Build from source

**Prerequisites:** [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0), Windows 10/11

```bash
git clone https://github.com/stepantech/ClaudeUsageWin.git
cd ClaudeUsageWin

# Run in development
dotnet run

# Publish single-file EXE
dotnet publish -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -o dist
```

The published EXE will be at `dist/ClaudeUsage.exe` (~65 MB, fully self-contained).

---

## Configuration

Config file location: `%AppData%\ClaudeUsageWin\config.json`

| Key | Default | Description |
|-----|---------|-------------|
| `refreshInterval` | `60` | Seconds between automatic refreshes (30, 60, 120, 300) |
| `opacityPct` | `95` | Popup window opacity (20–100) |
| `alwaysOnTop` | `true` | Keep popup above all windows |
| `iconStyle` | `"Percentage"` | Tray icon: `"Percentage"`, `"Bar"`, or `"Dot"` |
| `notifyFiveHour` | `true` | Enable notifications for 5-hour window |
| `notifyWeekly` | `true` | Enable notifications for weekly window |
| `notifyThresholds` | `[75, 90, 95]` | Usage percentages that trigger a notification |
| `launchAtStartup` | `false` | Auto-start with Windows |
| `showRemaining` | `false` | Show remaining % instead of used % |
| `popupScale` | `1.0` | Content scale factor (0.75–1.50) |

---

## Tech Stack

| Technology | Purpose | Why |
|-----------|---------|-----|
| **C# 12 / .NET 8** | Application runtime | Native Windows performance, single-file publish |
| **WPF** | Popup and Settings UI | Rich XAML styling, smooth animations, hardware-accelerated |
| **Windows Forms NotifyIcon** | System tray icon | WPF doesn't support tray icons natively |
| **System.Text.Json** | Config and profile serialization | Built-in, zero dependencies |
| **WinHttpHandler** | HTTP client transport | Better Windows proxy/auth handling than SocketsHttpHandler |
| **GDI+** | Tray icon rendering | Draw percentage/bar/dot icons at runtime into 32×32 bitmaps |

---

## What Changed

### v1.1.0 *(2026-02-20)*

**Bug fixes:**
- Scale slider now controls the **usage popup** (not the Settings window)
- Fixed double red line artifact at 100% on progress bars
- Eliminated Settings "ding" sound — Settings is now non-modal
- Usage popup is fully interactive while Settings is open
- Window positions remembered and restored across sessions
- Drag Settings from maximized → restores floating window
- Toggle popup (tray click) respects the user's last dragged position

**New features:**
- 💡 **Contextual Insights panel** — usage rate, time to limit, session peak
- 👥 **Multi-profile support** — manage and switch between multiple Claude accounts
- 🧙 **First-run Setup Wizard** — 3-step onboarding for new users
- ℹ️ **About / Updates** in Settings — version display + "Check for updates" button
- 🛠️ **Developer tools** in tray — copy/open/export log file
- 📋 **Live log watcher** — `scripts/dev-log.ps1`

**Performance:**
- IL partial trimming on publish — removes unused code from the self-contained bundle

### v1.0.0 *(2026-02-18)*

Initial release — system tray usage monitor with OAuth auto-auth, manual session key fallback, dark-themed popup, color-coded bars, threshold notifications, opacity/topmost/icon-style settings, and launch-at-startup support.

---

## Contributing

Contributions are welcome. Please open an issue before submitting a large PR so we can discuss the approach.

```bash
git clone https://github.com/stepantech/ClaudeUsageWin.git
cd ClaudeUsageWin
dotnet build        # verify it compiles
dotnet run          # run in dev mode
```

See [CONTRIBUTING.md](CONTRIBUTING.md) for full guidelines.

---

## License

MIT — see [LICENSE](LICENSE) for details.

---

<div align="center">

Made with ☕ for the Windows Claude users who live in the system tray.

</div>
````

**Step 2: Build to verify nothing broke**

```bash
cd "C:/Users/Štěpán/Claude Home/ClaudeUsageWin"
dotnet build 2>&1 | tail -5
```

**Step 3: Commit README**

```bash
cd "C:/Users/Štěpán/Claude Home/ClaudeUsageWin"
git add README.md
git commit -m "docs: rewrite README with feature showcase, tech stack, and version history"
```

---

## PHASE 5 — GITHUB REPO SETUP

### Task 7: Create GitHub repo and push code

**Tools used:** `mcp__plugin_github_github__get_me`, `mcp__plugin_github_github__create_repository`,
then `git` to push.

**Step 1: Get authenticated GitHub username**

Call `mcp__plugin_github_github__get_me` to retrieve the authenticated user's login name.

**Step 2: Check if repo already exists**

Call `mcp__plugin_github_github__search_repositories` with query `ClaudeUsageWin user:<login>`.
If a repo named `ClaudeUsageWin` already exists for this user, skip creation.

**Step 3: Create repo if it doesn't exist**

Call `mcp__plugin_github_github__create_repository` with:
```json
{
  "name": "ClaudeUsageWin",
  "description": "Real-time Claude AI usage monitoring from your Windows system tray",
  "private": false,
  "autoInit": false
}
```

**Step 4: Add remote and push**

```bash
cd "C:/Users/Štěpán/Claude Home/ClaudeUsageWin"
git remote remove origin 2>/dev/null || true
git remote add origin https://github.com/<login>/ClaudeUsageWin.git
git branch -M main
git push -u origin main
```

Replace `<login>` with the actual username from Step 1.

**Step 5: Update the "Check for updates" URL in SettingsWindow.xaml.cs**

The current URL is hardcoded as `stepantech/claude-usage-win`. Update it to match the actual repo:

Find in `SettingsWindow.xaml.cs`:
```csharp
            var json = await http.GetStringAsync(
                "https://api.github.com/repos/stepantech/claude-usage-win/releases/latest");
```

Replace with (using the correct `<login>/ClaudeUsageWin`):
```csharp
            var json = await http.GetStringAsync(
                "https://api.github.com/repos/<login>/ClaudeUsageWin/releases/latest");
```

**Step 6: Also update CONTRIBUTING.md clone URL**

Find in `CONTRIBUTING.md`:
```
git clone https://github.com/<your-username>/claude-usage-win.git
```

Replace with:
```
git clone https://github.com/<login>/ClaudeUsageWin.git
```

**Step 7: Build to verify after URL changes**

```bash
cd "C:/Users/Štěpán/Claude Home/ClaudeUsageWin"
dotnet build 2>&1 | tail -5
```

**Step 8: Commit URL fixes**

```bash
cd "C:/Users/Štěpán/Claude Home/ClaudeUsageWin"
git add SettingsWindow.xaml.cs CONTRIBUTING.md
git commit -m "fix: update GitHub repo URLs to match actual repository"
```

**Step 9: Push updated commits**

```bash
cd "C:/Users/Štěpán/Claude Home/ClaudeUsageWin"
git push origin main
```

---

### Task 8: Re-publish EXE with final repo URLs, create GitHub Release

**Step 1: Re-publish EXE (URLs changed in Task 7)**

```bash
cd "C:/Users/Štěpán/Claude Home/ClaudeUsageWin"
dotnet publish -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -o dist 2>&1 | tail -10
```

**Step 2: Verify EXE**

```bash
ls -lh "C:/Users/Štěpán/Claude Home/ClaudeUsageWin/dist/ClaudeUsage.exe"
```

**Step 3: Create GitHub Release via MCP**

Call `mcp__plugin_github_github__create_repository` — wait, wrong tool. There's no direct
"create release" MCP tool. Use the `gh` CLI — but gh is not installed. Use the GitHub REST API
via `mcp__plugin_github_github__*` tools or instruct the user to upload manually.

**Actually:** The MCP plugin doesn't have a "create release" tool. Handle as follows:

- Create the release via the GitHub web UI, OR
- Use the GitHub API via a direct curl call if available:

```bash
# Create the release (tag v1.1.0)
cd "C:/Users/Štěpán/Claude Home/ClaudeUsageWin"
git tag v1.1.0 2>/dev/null || true
git push origin v1.1.0

# Then upload the EXE via GitHub web UI:
# Releases → Draft a new release → Tag: v1.1.0 → Upload ClaudeUsage.exe
```

**Step 4: Report to user**

After pushing the tag, inform the user:
> Tag v1.1.0 has been pushed. To complete the release: go to **https://github.com/<login>/ClaudeUsageWin/releases/new?tag=v1.1.0**, fill in the release notes from the CHANGELOG, and upload `dist/ClaudeUsage.exe` as an asset.

---

## PHASE 6 — FINAL VERIFICATION

### Task 9: Final build + checklist summary

**Step 1: Clean rebuild**

```bash
cd "C:/Users/Štěpán/Claude Home/ClaudeUsageWin"
dotnet build 2>&1 | tail -5
```

Expected: 0 errors, 0 warnings.

**Step 2: Confirm all commits pushed**

```bash
cd "C:/Users/Štěpán/Claude Home/ClaudeUsageWin"
git log --oneline -10
git status
```

Expected: clean working tree, all commits visible.

**Step 3: Confirm repo is public on GitHub**

Call `mcp__plugin_github_github__search_repositories` for `ClaudeUsageWin` and confirm it appears.

**Step 4: Report to user**

Provide a summary of:
- Bugs fixed (Tasks 2, 3, 4)
- EXE size
- GitHub repo URL
- What the user must do manually (upload EXE asset to the release)
- Items from TESTING.md that require manual UI testing (tray, animations, threshold notifications)

---

## Testing Checklist Audit

Items from `TESTING.md` verified statically (code review):

| # | Test | Status | Notes |
|---|------|--------|-------|
| 1 | Build: 0 errors | ✅ PASS | `dotnet build` confirmed |
| 2 | Tray icon appears | ✅ CODE OK | `BuildTrayIcon()` creates `NotifyIcon` |
| 3 | Left-click toggles popup | ✅ CODE OK | `MouseClick` handler → `TogglePopup()` |
| 4 | Right-click context menu | ✅ CODE OK | `ContextMenuStrip` with all items |
| 5 | Data display (pcts, bars, reset) | ✅ CODE OK | `UpdateData()` covers all fields |
| 6 | OAuth auto-auth | ✅ CODE OK | `CredentialsReader.TryRead()` path |
| 7 | Manual session key | ✅ CODE OK | `FromSessionKey()` path |
| 8 | Settings UI | ✅ CODE OK | All fields wired |
| 9 | Non-modal settings | ✅ CODE OK | `Show()` not `ShowDialog()` |
| 10 | Scale slider → popup | ✅ CODE OK | `PreviewPopupScale()` + `ApplyScale()` |
| 11 | Window positions saved | ✅ CODE OK | `LocationChanged` handler |
| 12 | Threshold notifications | ✅ CODE OK | `ThresholdNotifier.Check()` |
| 13 | Icon styles (3) | ✅ CODE OK | `MakePercentageBitmap/Bar/Dot` |
| 14 | Launch at startup | ✅ CODE OK | `StartupService.Enable/Disable()` |
| 15 | Remaining vs Used toggle | ✅ CODE OK | `ApplyShowRemaining()` |
| 16 | Sparkline chart | ✅ CODE OK | `UpdateSparkline()` |
| 17 | Insights panel | ✅ CODE OK | `InsightsBtn_Click()` + `UpdateInsights()` |
| 18 | Multi-profile UI | ✅ CODE OK | `ProfileService` + Settings Profiles section |
| 19 | About / version display | ✅ CODE OK | Assembly version reflection |
| 20 | First-run wizard | ✅ CODE OK | `SetupWizardWindow` shown when no creds |
| 21 | Dev log tools | ✅ CODE OK | Tray Developer submenu |
| 22 | **Toggle popup position** | 🐛 **FIXED** | Was: always snaps to tray. Fixed in Task 2 |
| 23 | **Profile name in popup** | 🐛 **FIXED** | Was: shows "(Default)". Fixed in Task 3 |
| 24 | **Dead code in eye toggle** | 🐛 **FIXED** | Removed in Task 4 |
| 25 | Visual rendering/animations | ⚠️ MANUAL | Requires running the app with display |
| 26 | Tray icon color transitions | ⚠️ MANUAL | GDI+ rendering, verify on-screen |
| 27 | Threshold notification popups | ⚠️ MANUAL | Requires hitting 75%+ usage |
| 28 | Drag from maximized Settings | ⚠️ MANUAL | Window state interaction |
| 29 | Network offline/online | ⚠️ MANUAL | Requires toggling network |

Items marked ⚠️ MANUAL require the user to run the app and verify visually.
