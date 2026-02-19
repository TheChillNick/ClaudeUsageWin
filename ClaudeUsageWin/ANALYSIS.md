# ClaudeUsageWin -- Codebase Analysis

Generated: 2026-02-18

---

## 1. File-by-File Breakdown

### ClaudeUsageWin.csproj

- **Purpose**: MSBuild project file. Targets .NET 8.0 Windows, enables both WPF and WinForms (WinForms for `NotifyIcon` tray support).
- **Key settings**: `PublishSingleFile`, `SelfContained`, `RuntimeIdentifier=win-x64`, compression enabled, no PDB (`DebugType=None`).
- **Dependencies**: `System.Net.Http.WinHttpHandler 10.0.3` (used instead of default `SocketsHttpHandler` for better Windows auth / proxy support).
- **Assembly name**: `ClaudeUsage` (output exe is `ClaudeUsage.exe`).

---

### App.xaml

- Declares `ShutdownMode="OnExplicitShutdown"` so the app stays alive even when all windows are hidden (required for tray-only operation).
- No application-level resources defined.

---

### App.xaml.cs

- **Class**: `App : Application`
- **Enum**: `AuthMode { Auto, ManualKey }` -- determines whether OAuth (Claude Code) or cookie-based session key auth is used.
- **Fields**: `_tray` (NotifyIcon), `_popup` (MainWindow), `_timer` (DispatcherTimer), `_config` (AppConfig), `_client` (ClaudeApiClient?), `_authMode`, `_notifier` (ThresholdNotifier), `_lastPct` (int?).
- **Key methods**:
  - `OnStartup()` -- loads config, creates popup, sets up tray, timer, network change listener; picks auth mode (manual key > auto OAuth); calls `RefreshData()`.
  - `OnExit()` -- disposes tray icon.
  - `TryBuildClientFromClaudeCode()` -- reads `~/.claude/.credentials.json`, checks expiry, builds OAuth client. Returns false if missing/expired.
  - `ApplyWindowSettings()` -- sets popup opacity and topmost.
  - `BuildTrayIcon()` -- creates context menu with Show/Hide, Minimize, Resize sub-menu (280/340/420px), Refresh Now, Settings, Quit. Left-click toggles popup.
  - `UpdateTray(int pct)` -- regenerates tray icon and tooltip text.
  - `MakeIcon(int pct, string style)` / `MakePercentageIcon` / `MakeBarIcon` / `MakeDotIcon` -- GDI+ icon rendering at 32x32.
  - `TogglePopup()` -- shows/hides popup near tray.
  - `SetPopupWidth(int)` -- resizes popup from tray menu.
  - `OnShowRemainingToggled()` -- toggles Used/Remaining display and persists to config.
  - `RefreshData()` -- re-checks credentials in Auto mode, fetches org ID if missing, calls API on background thread, updates popup + tray + threshold notifier on UI thread.
  - `BuildTimer()` -- creates DispatcherTimer for periodic refresh.
  - `ShowSettings()` -- opens modal SettingsWindow; on save: updates config, rebuilds client, updates timer interval, applies startup registry, refreshes data.

---

### MainWindow.xaml

- **Purpose**: The floating popup window that shows usage data.
- **Layout**: `WindowStyle="None"`, `AllowsTransparency="True"`, `Background="Transparent"` with a dark `Border` (#2B2B2B) with rounded corners and drop shadow.
- **Sections**:
  1. **Header** -- "Claude Usage" title, Used/Remaining toggle button, plan badge (e.g. "Pro").
  2. **5-Hour Window** -- percentage text, progress bar, reset countdown.
  3. **Weekly** -- same structure as 5-hour.
  4. **Today** -- message count and token count.
  5. **Footer** -- "Updated X ago" timestamp + refresh button; Settings + Quit links.
- **Behavior**: `Deactivated` event hides the window (unless settings dialog is open). `ResizeMode="CanResizeWithGrip"`.

---

### MainWindow.xaml.cs

- **Class**: `MainWindow : Window`
- **Fields**: `_lastUpdated`, `_ticker` (1-second timer for "Updated X ago"), `_offline`, `_showRemaining`, `_lastData`.
- **Event**: `ShowRemainingToggled` (raised on toggle button click).
- **Key methods**:
  - `UpdateData(UsageData, DateTime, bool)` -- populates all UI fields, triggers bar width animation via `DispatcherPriority.Loaded`.
  - `ApplyShowRemaining(bool)` / `ApplyShowRemaining(bool, UsageData)` -- swaps between "Used" and "Remaining" display.
  - `SetOfflineMode(bool)` -- sets offline flag and updates label.
  - `PositionNearTray()` -- places window at bottom-right of work area, 12px inset.
  - `UpdateAgoLabel()` -- formats "Updated X sec/min/h ago" or "Offline" text.
  - `SetBar()` -- sets fill width + color (green < 50, yellow 50-75, orange-red > 75).
  - `FormatFiveHourReset()` / `FormatWeeklyReset()` / `FormatTokens()` -- formatting helpers.
  - Event handlers: `RefreshBtn_Click`, `SettingsBtn_Click`, `QuitBtn_Click`, `ToggleModeBtn_Click`.

---

### SettingsWindow.xaml

- **Purpose**: Modal dialog for all app configuration.
- **Sections**: Authentication (auto-auth status banner, auth mode radio, manual session key, org ID with auto-detect), Refresh Interval, Appearance (opacity slider, always-on-top, icon style), Notifications (5-hour/weekly toggles, 3 threshold fields), Behavior (launch at startup), Save/Cancel buttons.
- **Styling**: Dark-themed via `Window.Resources` with styles for labels, text fields, buttons, checkboxes, radio buttons, combo box items, combo box shell.

---

### SettingsWindow.xaml.cs

- **Class**: `SettingsWindow : Window`
- **Property**: `ResultConfig` -- the config to return to App on save.
- **Key methods**:
  - Constructor -- populates all controls from existing `AppConfig`.
  - `ShowAutoAuthStatus()` -- reads credentials and shows green "detected" or orange "not found/expired" banner.
  - `AuthRadio_Checked()` -- toggles manual key panel visibility.
  - `SessionKeyBox_Changed()` -- clears org ID when key changes.
  - `OpacitySlider_ValueChanged()` -- updates percentage label.
  - `DetectBtn_Click()` -- async org ID auto-detection via API.
  - `SaveBtn_Click()` -- validates thresholds, builds new `AppConfig`, sets `DialogResult = true`.
  - `CancelBtn_Click()` -- closes without saving.

---

### Models/UsageData.cs

- **Record**: `UsageData` (immutable)
- **Properties**: `FiveHourPct` (int), `FiveHourResetAt` (DateTime?), `WeeklyPct` (int), `WeeklyResetAt` (DateTime?), `TodayMessages` (int), `TodayTokens` (long), `Plan` (string, default "free").

---

### Services/ClaudeApiClient.cs

- **Class**: `ClaudeApiClient`
- **Base URL**: `https://claude.ai/api`
- **Factory methods**:
  - `FromOAuth(string accessToken)` -- sets `Authorization: Bearer` header.
  - `FromSessionKey(string sessionKey)` -- sets `Cookie: sessionKey=...` header.
- **Private**: `BuildHttp()` -- creates `HttpClient` with `WinHttpHandler`, 15s timeout, Chrome-like headers (User-Agent, sec-ch-ua, Origin, Referer, etc.).
- **API methods**:
  - `GetOrgIdAsync()` -- `GET /api/auth/session`, extracts `account.memberships[0].organization.uuid`.
  - `GetUsageAsync(string orgId)` -- `GET /api/organizations/{orgId}/usage`, parses `five_hour_window`, `weekly`, `today` JSON nodes into `UsageData`.
- **Debug file**: Always writes to `%TEMP%/claude_usage_debug.txt` (see Known Issues).

---

### Services/CredentialsReader.cs

- **Record**: `ClaudeCredentials(AccessToken, RefreshToken, ExpiresAt, SubscriptionType, RateLimitTier)`
- **Class**: `CredentialsReader` (static)
- **Credential path**: `~/.claude/.credentials.json`, reads `claudeAiOauth` node.
- **Methods**:
  - `TryRead()` -- reads and parses credentials file. Returns null on any failure.
  - `IsExpired(ClaudeCredentials)` -- returns true if token expires within 5 minutes.

---

### Services/ConfigService.cs

- **Record**: `AppConfig` -- all app settings with defaults.
  - Fields: `SessionKey`, `OrgId`, `RefreshInterval` (60s), `SubscriptionType`, `NotifyThresholds` ([75,90,95]), `NotifyFiveHour`/`NotifyWeekly` (true), `OpacityPct` (95), `AlwaysOnTop` (true), `IconStyle` ("Percentage"), `LaunchAtStartup` (false), `ShowRemaining` (false).
- **Class**: `ConfigService` (static)
  - Config path: `%APPDATA%/ClaudeUsageWin/config.json`
  - `Load()` -- deserializes JSON or returns defaults.
  - `Save()` -- serializes with indentation.

---

### Services/ThresholdNotifier.cs

- **Class**: `ThresholdNotifier`
- **State**: `_notified` HashSet tracks which threshold+label combos have fired.
- **Methods**:
  - `Check(UsageData, AppConfig, NotifyIcon)` -- checks both 5-hour and weekly against thresholds.
  - `CheckThresholds(string label, int pct, int[] thresholds, NotifyIcon)` -- fires balloon tip when threshold crossed; clears notification when usage drops below threshold.

---

### Services/StartupService.cs

- **Class**: `StartupService` (static)
- **Registry key**: `HKCU\SOFTWARE\Microsoft\Windows\CurrentVersion\Run`, value name `ClaudeUsage`.
- **Methods**: `IsEnabled()`, `Enable()` (writes `Environment.ProcessPath`), `Disable()` (deletes value).

---

### Services/Logger.cs

- **Class**: `Logger` (static)
- **Log path**: `%APPDATA%/ClaudeUsageWin/logs/debug.log`
- **Behavior**: Only writes in `DEBUG` configuration (`#if DEBUG`).
- **Methods**: `Log(string)`, `LogError(string, Exception?)`, `LogsFolder` property.

---

## 2. Known Issues

| # | Issue | File(s) | Severity |
|---|-------|---------|----------|
| K1 | **Debug file always written to `%TEMP%`**. `ClaudeApiClient.DbgPath` writes `claude_usage_debug.txt` to `%TEMP%` unconditionally in both Debug and Release builds. This leaks API response bodies (including tokens/cookies in headers) to a world-readable temp directory. | `Services/ClaudeApiClient.cs:14-16,66-67,85-86,108` | High |
| K2 | **No unhandled exception handler**. No `AppDomain.UnhandledException` or `DispatcherUnhandledException` handler. Unhandled exceptions crash silently with no log. | `App.xaml.cs` | Medium |
| K3 | **Expired OAuth token is never refreshed**. `CredentialsReader` reads `RefreshToken` but never uses it. When the token expires, the app falls back to "no credentials" and opens settings. | `Services/CredentialsReader.cs`, `App.xaml.cs:87` | High |
| K4 | **No error UI feedback**. When `GetUsageAsync()` returns null (API failure, 401, network error), the popup simply doesn't update. No visual indicator tells the user something went wrong. | `App.xaml.cs:295-299`, `MainWindow.xaml` | Medium |
| K5 | **No "Open Claude.ai" action**. Users must manually open a browser; no quick link in popup or tray menu. | `MainWindow.xaml`, `App.xaml.cs` | Low |
| K6 | **No visible close button on popup**. Window can only be dismissed by clicking away (Deactivated event), tray menu, or Quit. No X button for discoverability. | `MainWindow.xaml` | Low |
| K7 | **No drag-to-move on popup**. The borderless window can only be positioned by PositionNearTray(). Users cannot reposition it. | `MainWindow.xaml.cs` | Low |
| K8 | **Settings window controls not fully dark-themed**. CheckBox and RadioButton checkmarks/circles use system default (white/light) rendering. Slider uses default Windows theme. ScrollViewer thumb is light. | `SettingsWindow.xaml` | Low |
| K9 | **GDI+ icon handle leak potential**. `MakePercentageIcon`, `MakeBarIcon`, `MakeDotIcon` call `Icon.FromHandle(bmp.GetHicon())` without calling `DestroyIcon` on the native handle. The old icon is disposed in `UpdateTray`, but the new icon's GDI handle is never released via `DestroyIcon`. | `App.xaml.cs:182,202,215` | Low |
| K10 | **`RefreshData` swallows all exceptions**. `Task.Run` has no try/catch around the outer lambda; if `Dispatcher.Invoke` throws, it goes unobserved. | `App.xaml.cs:273` | Medium |
| K11 | **`ShowRemaining` not persisted on save**. `SaveBtn_Click` creates a new `AppConfig` but doesn't copy over `ShowRemaining` from the previous config. | `SettingsWindow.xaml.cs:155-167` | Low |
| K12 | **Hardcoded Chrome version strings**. `sec-ch-ua` header references Chrome 131 which may become outdated and trigger bot detection. | `Services/ClaudeApiClient.cs:49-50` | Low |

---

## 3. TODO List

### T1: OAuth Token Auto-Refresh

**Goal**: When the access token from `~/.claude/.credentials.json` is expired (or within 5 min of expiry), use the `refreshToken` to obtain a new access token from Anthropic's OAuth endpoint, update the credentials file, and rebuild the `ClaudeApiClient`.

**Files to modify**:
- `Services/CredentialsReader.cs` -- add `async Task<ClaudeCredentials?> RefreshAsync(ClaudeCredentials creds)` method. This should POST to the Anthropic token refresh endpoint (likely `https://claude.ai/api/auth/refresh` or the OAuth2 token endpoint) with the refresh token. On success, write the updated token back to `~/.claude/.credentials.json` and return new `ClaudeCredentials`.
- `App.xaml.cs` -- in `TryBuildClientFromClaudeCode()`, when `IsExpired(creds)` is true, call `CredentialsReader.RefreshAsync(creds)` before returning false. Also add refresh logic in `RefreshData()` when auto-mode API calls return 401.

**Implementation notes**:
- The refresh endpoint needs to be discovered (inspect Claude Code CLI traffic or the credentials file format).
- Must handle refresh token itself being expired (fall back to opening settings).
- Write-back to `.credentials.json` should use file locking to avoid conflict with Claude Code CLI.
- Add a `_refreshing` flag to prevent concurrent refresh attempts.

---

### T2: Error State Banner in Popup (with Retry Button)

**Goal**: When the API call fails, show a visible error banner at the top of the popup with an error message and a "Retry" button.

**Files to modify**:
- `MainWindow.xaml` -- add a `Border` element (initially collapsed) between the header and 5-hour section. Contains a red/orange background with error icon, message `TextBlock`, and a "Retry" `Button`.
- `MainWindow.xaml.cs` -- add `ShowError(string message)` method that sets the banner visible and populates the message. Add `HideError()` method. Wire Retry button to call `((App)Application.Current).RefreshData()`.
- `App.xaml.cs` -- in `RefreshData()`, when `data is null`, call `_popup.ShowError("Failed to fetch usage data")` on the UI thread. When data succeeds, call `_popup.HideError()`.

**Implementation notes**:
- Error banner should have a distinct background (e.g. `#4A2020`) so it stands out.
- Show different messages for different failure modes (401 = "Authentication failed", timeout = "Network timeout", etc.). This requires `ClaudeApiClient` to return richer error info (enum or exception type) instead of just null.
- Consider adding an `ErrorKind` enum and returning `Result<UsageData, ErrorKind>` from the API client.

---

### T3: "Open Claude.ai" Button in Popup Footer and Tray Menu

**Goal**: Add a clickable link/button that opens `https://claude.ai` in the default browser.

**Files to modify**:
- `MainWindow.xaml` -- add a new `Button` in the footer grid (bottom row), styled as a text link, with content "Open Claude.ai".
- `MainWindow.xaml.cs` -- add click handler `OpenClaudeBtn_Click` that calls `Process.Start(new ProcessStartInfo("https://claude.ai") { UseShellExecute = true })`.
- `App.xaml.cs` -- in `BuildTrayIcon()`, add a menu item "Open Claude.ai" before the "Quit" separator, with the same `Process.Start` logic.

**Implementation notes**:
- Place the button between "Settings" and "Quit" in the popup footer.
- Use `System.Diagnostics.Process` -- requires `using System.Diagnostics;`.

---

### T4: Visible X Close Button on Popup + Drag-to-Move

**Goal**: Add an X button in the top-right corner of the popup to close/hide it. Allow the user to drag the popup window by its header area.

**Files to modify**:
- `MainWindow.xaml` -- add a close `Button` in the header `Grid` (column 3 or a new column after the plan badge). Style it as a small circle with "X" text.
- `MainWindow.xaml.cs` -- add `CloseBtn_Click` handler that calls `Hide()`. Add `MouseLeftButtonDown` handler on the header grid that calls `DragMove()`.

**Implementation notes**:
- The close button should be small (20x20) with subtle styling (#3D3D3D background, hover to #505050).
- `DragMove()` works on borderless windows and lets users reposition the popup.
- After drag, the popup won't snap back to tray position until next `TogglePopup()` call -- this is acceptable.

---

### T5: Settings Window Dark Mode (All Controls Themed)

**Goal**: Fully dark-theme every control in the settings window, including CheckBox checkmarks, RadioButton circles, Slider track/thumb, and ScrollViewer.

**Files to modify**:
- `SettingsWindow.xaml` -- add or update `Window.Resources` with complete `ControlTemplate` overrides:
  - **CheckBox**: Custom template with a dark border (#555), dark check background (#3D3D3D), white checkmark, hover effect.
  - **RadioButton**: Custom template with dark circle border, filled dot on selection.
  - **Slider**: Custom template with dark track (#3D3D3D), accent-colored thumb (#C17A3A), hover effect.
  - **ScrollViewer / ScrollBar**: Dark thumb (#555), dark track (#2B2B2B).
  - **PasswordBox**: Already partially styled inline but should match `Field` style.

**Implementation notes**:
- Use `ControlTemplate` overrides rather than trying to set individual properties, since WPF default templates use system colors.
- Test on Windows 10 and 11 (default themes differ).
- Consider extracting shared colors into `SolidColorBrush` resources (e.g. `DarkBg`, `DarkBorder`, `AccentColor`).

---

### T6: Usage History Sparkline (Last 20 Snapshots)

**Goal**: Show a mini line chart (sparkline) in the popup displaying the last 20 5-hour usage percentage values over time.

**Files to modify**:
- `Models/UsageData.cs` -- no changes needed (data already has `FiveHourPct`).
- **New file**: `Services/UsageHistory.cs` -- a service that stores the last 20 `(DateTime, int)` tuples in a circular buffer. Persists to `%APPDATA%/ClaudeUsageWin/history.json`. Methods: `Add(int pct)`, `GetHistory() -> List<(DateTime, int)>`, `Load()`, `Save()`.
- `MainWindow.xaml` -- add a `Canvas` or `Polyline` element between the 5-hour section and the weekly section (or below weekly). Height ~40px.
- `MainWindow.xaml.cs` -- add `UpdateSparkline(List<(DateTime, int)> history)` method that draws a polyline on the canvas. Color code: green to red gradient based on value.
- `App.xaml.cs` -- in `RefreshData()` success path, call `UsageHistory.Add(data.FiveHourPct)` and then `_popup.UpdateSparkline(UsageHistory.GetHistory())`.

**Implementation notes**:
- Use WPF `Polyline` element with `Points` bound to the data.
- Scale X axis evenly across canvas width, Y axis 0-100 mapped to canvas height.
- Add subtle grid lines or min/max labels for context.
- History file should be small (20 entries max, ~1KB).

---

### T7: Claude Code Statusline PowerShell Script Installer

**Goal**: Provide a PowerShell script (and an installer button in Settings) that adds a Claude usage status indicator to the user's PowerShell prompt, showing current 5-hour usage % in the terminal prompt line.

**Files to modify**:
- **New file**: `Scripts/claude-statusline.ps1` -- a PowerShell script that:
  1. Reads `%APPDATA%/ClaudeUsageWin/config.json` for last known usage data (or a separate `status.json` written by the app).
  2. Formats a prompt segment like `[Claude: 42%]` with ANSI color coding.
  3. Hooks into `$PROMPT` or provides a function to append to `$PROFILE`.
- `App.xaml.cs` -- in `RefreshData()` success path, write a small `status.json` file to `%APPDATA%/ClaudeUsageWin/` with `{ "fiveHourPct": 42, "weeklyPct": 15, "updatedAt": "..." }`.
- `SettingsWindow.xaml` -- add a "Statusline" section with an "Install to PowerShell Profile" button.
- `SettingsWindow.xaml.cs` -- add `InstallStatuslineBtn_Click()` that:
  1. Copies `claude-statusline.ps1` to `%APPDATA%/ClaudeUsageWin/`.
  2. Appends a `. "path\to\claude-statusline.ps1"` line to `$PROFILE` (with duplicate check).
  3. Shows confirmation message.

**Implementation notes**:
- The PS1 script should read the JSON file, check `updatedAt` freshness, and format the prompt segment.
- Use ANSI escape codes for color: green (<50%), yellow (50-75%), red (>75%).
- The script should gracefully handle missing/stale data (show `[Claude: --]`).
- Consider also supporting Oh-My-Posh / Starship integration via custom segment config.
- The `status.json` write should be lightweight (no file locking needed, atomic write via temp+rename).

---

## 4. Summary Table

| TODO | Priority | Complexity | Files |
|------|----------|-----------|-------|
| T1: OAuth auto-refresh | High | Medium | `CredentialsReader.cs`, `App.xaml.cs` |
| T2: Error banner + Retry | High | Low | `MainWindow.xaml`, `MainWindow.xaml.cs`, `App.xaml.cs`, `ClaudeApiClient.cs` |
| T3: Open Claude.ai button | Low | Low | `MainWindow.xaml`, `MainWindow.xaml.cs`, `App.xaml.cs` |
| T4: Close button + drag | Medium | Low | `MainWindow.xaml`, `MainWindow.xaml.cs` |
| T5: Settings dark mode | Medium | Medium | `SettingsWindow.xaml` |
| T6: Usage sparkline | Medium | Medium | New `UsageHistory.cs`, `MainWindow.xaml`, `MainWindow.xaml.cs`, `App.xaml.cs` |
| T7: PowerShell statusline | Low | Medium | New `claude-statusline.ps1`, `App.xaml.cs`, `SettingsWindow.xaml`, `SettingsWindow.xaml.cs` |
