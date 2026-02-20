# Changelog

## [1.1.0] - 2026-02-20

### Bug Fixes
- **Scale slider now controls the usage popup**, not the Settings window — the slider in Settings live-previews and applies scale to the usage dashboard
- **Double red line at 100% usage** — replaced bar `Grid` elements with `Border + ClipToBounds` to properly clip fill at full width
- **Settings ding sound eliminated** — Settings is now a non-modal window (`Show()` instead of `ShowDialog()`); clicking the usage popup while Settings is open no longer triggers a Windows blocked-window sound
- **Usage dashboard now fully interactive while Settings is open** — both windows are independent and moveable simultaneously
- **Window positions remembered across sessions** — both the usage popup and Settings window restore to the exact position where you left them
- **Settings no longer opens in the center of the screen** — remembers and restores its last position
- **Drag Settings from maximized restores floating mode** — dragging the title bar of a maximized Settings window restores it to floating and begins dragging, matching Windows 11 snap behavior

### New Features
- **Contextual Insights panel** — click the 💡 button in the popup header to toggle an insights panel showing: usage rate (%/hr), estimated time to limit, and session peak usage
- **Multi-profile support** — manage multiple Claude accounts via the new Profiles tab in Settings; each profile has its own credentials; switch between profiles without restarting
- **Active profile shown in popup** — when a named profile is active, its name appears below the "Claude Usage" title in the popup header
- **First-run Setup Wizard** — new users (no Claude Code credentials and no local stats) see a friendly 3-step wizard (Welcome → Auth → Done) instead of the raw Settings dialog
- **About / Updates section** — the Settings Behavior tab now shows app version (v1.1.0) and a "Check for updates" button that queries GitHub Releases
- **Developer: Copy Log to Clipboard** — tray icon → Developer → Copy Log to Clipboard copies the full debug log to clipboard for easy inspection
- **Developer: Open Log File** — opens the log file directly in your default text editor
- **Developer: Export Log…** — save-dialog to export the log file anywhere
- **Dev log watcher script** — `scripts/dev-log.ps1` provides a live-tailing, color-coded log viewer for development use

### Performance
- **Reduced EXE size** — enabled IL partial trimming (`TrimMode=partial`) on publish, removing unused code from the app bundle while keeping WPF/WinForms assemblies intact

---

## [1.0.0] - 2026-02-18

### Added
- Initial release for Windows
- System tray icon with usage percentage display
- Dark-themed popup matching Claude Usage Tracker design
- 5-hour window and weekly usage tracking
- Auto-authentication via Claude Code credentials (`~/.claude/.credentials.json`)
- Manual session key fallback
- Configurable threshold notifications (75%, 90%, 95%)
- Opacity slider (20–100%)
- Always on top toggle
- Multiple icon styles: Percentage, Bar, Dot
- Launch at Windows startup option
- Remaining vs used percentage toggle
- Network connectivity monitoring
- Auto-refresh with configurable interval (30s to 5min)
- Single self-contained .exe — no installation required
