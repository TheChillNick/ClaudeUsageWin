# ClaudeUsageWin - Manual Test Checklist

## Build Verification
- [ ] `dotnet build ClaudeUsageWin.csproj` completes with 0 errors, 0 warnings

## 1. Tray Icon & Basic Popup
- [ ] App starts and shows a tray icon in the system tray
- [ ] Left-clicking the tray icon toggles the popup open/closed
- [ ] Right-clicking the tray icon shows context menu with: Show/Hide, Minimize, Resize, Open Claude.ai, Refresh Now, Settings, Quit
- [ ] "Quit" menu item shuts down the app
- [ ] "Refresh Now" triggers a data refresh
- [ ] "Open Claude.ai" opens the browser to claude.ai
- [ ] Resize submenu offers Small (280px), Normal (340px), Large (420px) and resizes the popup

## 2. Data Display
- [ ] Popup shows plan name (capitalized first letter)
- [ ] 5-Hour window percentage and bar are displayed
- [ ] Weekly percentage and bar are displayed
- [ ] 5-Hour reset countdown shows time remaining (e.g. "Resets in 2h 15m")
- [ ] Weekly reset shows day and time (e.g. "Resets Mon 12:00 AM")
- [ ] Today's messages count is shown
- [ ] Today's tokens count is shown (formatted as K/M for large numbers)
- [ ] "Updated X sec/min ago" label updates in real-time

## 3. Authentication
### Auto (OAuth via Claude Code)
- [ ] With Claude Code installed and logged in, app auto-detects credentials
- [ ] Settings shows "Claude Code detected" with green badge
- [ ] Token auto-refreshes when expired

### Manual (Session Key)
- [ ] Entering a session key in Settings and saving uses it for API calls
- [ ] Switching to Manual auth mode shows the session key input panel
- [ ] Switching back to Auto auth mode clears the session key
- [ ] "Auto-detect" button in Settings detects the Organization ID

## 4. Settings Window (Dark Mode UI)
- [ ] Settings window has dark background with proper contrast
- [ ] All text is readable (white/light on dark backgrounds)
- [ ] Tab navigation or sections work correctly
- [ ] Radio buttons for auth mode are visible and functional
- [ ] Sliders, checkboxes, and combo boxes all render with dark theme
- [ ] Save and Cancel buttons work correctly
- [ ] Window can be dragged by the title bar
- [ ] Close (X) button works

## 5. Threshold Notifications
- [ ] Default thresholds are 75%, 90%, 95%
- [ ] Custom thresholds can be set in Settings (3 text boxes)
- [ ] When 5-hour usage crosses a threshold, a Windows notification appears (if enabled)
- [ ] When weekly usage crosses a threshold, a Windows notification appears (if enabled)
- [ ] Notifications can be toggled on/off independently for 5-hour and weekly
- [ ] Invalid threshold values (0, negative, >100) are ignored and defaults are used

## 6. Appearance Settings
### Opacity
- [ ] Opacity slider in Settings changes the popup window opacity
- [ ] Slider shows current percentage value (e.g. "95%")
- [ ] Opacity persists across app restarts

### Always On Top
- [ ] Checkbox toggles the popup's Topmost property
- [ ] When enabled, popup stays above other windows
- [ ] Setting persists across restarts

### Icon Styles
- [ ] "Percentage" icon style shows the usage % number in the tray icon
- [ ] "Bar" icon style shows a filled bar in the tray icon
- [ ] "Dot" icon style shows a colored dot in the tray icon
- [ ] Icon color changes based on usage level: green (<50%), yellow (50-75%), red (>75%)

## 7. Launch at Startup
- [ ] Checkbox in Settings enables/disables Windows startup entry
- [ ] When enabled, the app registry key is written to `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`
- [ ] When disabled, the registry key is removed
- [ ] Setting persists in config

## 8. Remaining vs Used Toggle
- [ ] "Used"/"Remaining" button in popup toggles the display mode
- [ ] In "Used" mode: shows usage percentage (e.g. "72%"), labels say "5-Hour Window" / "Weekly"
- [ ] In "Remaining" mode: shows remaining percentage (e.g. "28%"), labels say "5-Hour Remaining" / "Weekly Remaining"
- [ ] Tray icon tooltip reflects the current mode
- [ ] Toggle state persists across Settings saves (ShowRemaining is preserved)

## 9. Sparkline (Usage History)
- [ ] A sparkline chart appears in the popup below the main stats
- [ ] Sparkline shows historical 5-hour usage data points
- [ ] Line color reflects the current usage level (green/yellow/red)
- [ ] A dot marks the latest data point
- [ ] Background track line is visible
- [ ] Sparkline updates on each data refresh

## 10. Drag & Pin
- [ ] Popup window can be dragged by clicking and holding on the main border area
- [ ] Pin button (pin icon) toggles pin state
- [ ] When pinned, popup stays visible even when it loses focus (Window_Deactivated does not hide)
- [ ] When pinned, pin button turns yellow
- [ ] When unpinned, popup hides when clicking elsewhere (loses focus)
- [ ] Pin button tooltip changes: "Pin window (stay visible)" / "Unpin window"

## 11. Error Handling
- [ ] When API call fails, an error banner appears in the popup with a message
- [ ] Error banner has a "Retry" button that triggers a refresh
- [ ] On successful refresh, the error banner is cleared
- [ ] "Open Claude.ai" button works from the popup

## 12. Offline / Network Handling
- [ ] When network is lost, popup shows "Offline" or "Offline -- last data from X ago"
- [ ] Offline text is colored orange
- [ ] When network is restored, a refresh is triggered automatically
- [ ] If data was previously loaded, it continues to show with the "offline" indicator

## 13. Claude Code Statusline Integration
- [ ] Settings shows Statusline section with install/remove buttons
- [ ] "Install" button creates the statusline hook
- [ ] Status indicator shows checkmark when installed, warning when not
- [ ] "Remove" button uninstalls the statusline hook

## 14. Config Persistence
- [ ] Config is saved to `%APPDATA%\ClaudeUsageWin\config.json`
- [ ] All settings round-trip correctly through save/load
- [ ] ShowRemaining, SubscriptionType, and StatuslineInstalled are NOT lost when saving Settings
- [ ] OrgId is cleared when saving Settings (to re-detect on next refresh)

## 15. Logging
- [ ] Log file is written (check Logger.cs for location)
- [ ] Key events are logged: startup, auth mode, API calls, errors
