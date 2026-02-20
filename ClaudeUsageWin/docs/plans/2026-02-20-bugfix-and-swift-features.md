# ClaudeUsageWin — Bug Fixes + Swift Feature Parity

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Fix 7 active bugs in ClaudeUsageWin, then add four features ported from the macOS Swift reference app.

**Architecture:** WPF app (.NET 8), single-file self-contained EXE. Config stored in `%APPDATA%\ClaudeUsageWin\config.json`. Two windows: `MainWindow` (usage popup) and `SettingsWindow` (settings dialog). Orchestrated by `App.xaml.cs`. Features are added as new Services + XAML sections.

**Tech Stack:** C# 12, .NET 8, WPF, Windows Forms (NotifyIcon), System.Text.Json

**Phase structure:** Phase 1 (bugs) must land before Phase 2 (features). Within each phase, task groups A/B/C can run in parallel across agents.

---

## PHASE 1 — BUG FIXES

### Task 1: Fix UI Scale — should scale popup, not Settings window

**Files:**
- Modify: `Services/ConfigService.cs` — add `PopupScale` field
- Modify: `SettingsWindow.cs` — change scale slider to preview popup, not self
- Modify: `SettingsWindow.xaml` — update slider label; remove SettingsScaleTransform from SettingsWindow border
- Modify: `App.xaml.cs` — apply PopupScale to MainWindow content
- Modify: `MainWindow.xaml` — wrap inner StackPanel in a ScaleTransform

**Context:** Currently `ScaleSlider` in Settings applies `SettingsScaleTransform` to the Settings window's inner Grid, scaling the Settings UI. The user wants the slider to scale the **usage popup (MainWindow)** instead.

**Step 1: Add `PopupScale` to AppConfig in `Services/ConfigService.cs`**

Find the `AppConfig` record and add after `SettingsScale`:
```csharp
public double PopupScale  { get; init; } = 1.0;
```

Also remove `SettingsScale` from the record — it is no longer used.

**Step 2: Add RenderTransform to MainWindow inner content**

In `MainWindow.xaml`, the outermost `<Border Background="#2B2B2B" ...>` contains a `<StackPanel>`. Wrap that StackPanel's content with a ScaleTransform on the Border itself — or add a `LayoutTransform` to the inner StackPanel. The cleanest approach: add a named `ScaleTransform` as `LayoutTransform` on the StackPanel:

```xml
<!-- Inside the outer Border, replace the plain <StackPanel> opening with: -->
<StackPanel>
    <StackPanel.LayoutTransform>
        <ScaleTransform x:Name="PopupScaleTransform" ScaleX="1" ScaleY="1"/>
    </StackPanel.LayoutTransform>
    <!-- ... rest of existing content unchanged ... -->
</StackPanel>
```

**Step 3: Apply PopupScale in `App.xaml.cs` → `ApplyWindowSettings()`**

```csharp
private void ApplyWindowSettings()
{
    _popup.Opacity = _config.OpacityPct / 100.0;
    _popup.Topmost = _config.AlwaysOnTop;
    _popup.ApplyScale(_config.PopupScale);
}
```

**Step 4: Add `ApplyScale` method to `MainWindow.cs`**

```csharp
public void ApplyScale(double scale)
{
    PopupScaleTransform.ScaleX = scale;
    PopupScaleTransform.ScaleY = scale;
}
```

**Step 5: Update SettingsWindow — scale slider now controls popup scale**

In `SettingsWindow.xaml.cs` constructor, replace:
```csharp
var scale = Math.Clamp(config.SettingsScale, 0.75, 1.50);
SettingsScaleTransform.ScaleX = scale;
SettingsScaleTransform.ScaleY = scale;
// ...
ScaleSlider.Value = Math.Round(scale * 100.0 / 5.0) * 5.0;
```
With:
```csharp
var popupScale = Math.Clamp(config.PopupScale, 0.75, 1.50);
ScaleSlider.Value = Math.Round(popupScale * 100.0 / 5.0) * 5.0;
ScaleValueText.Text = $"{(int)ScaleSlider.Value}%";
```

Remove all references to `SettingsScaleTransform` from SettingsWindow.cs.

**Step 6: Remove SettingsScaleTransform from SettingsWindow.xaml**

Find the `<ScaleTransform x:Name="SettingsScaleTransform".../>` and its `<Grid.LayoutTransform>` block and delete them. The Settings window should not scale itself.

**Step 7: Update `ScaleSlider_ValueChanged` in SettingsWindow.cs**

Remove the lines that resize the Settings window proportionally (Width/Height based on scale). The slider now only updates the label — the actual scale is applied to the popup at save time.

```csharp
private void ScaleSlider_ValueChanged(object sender,
    System.Windows.RoutedPropertyChangedEventArgs<double> e)
{
    if (ScaleValueText is null) return;
    ScaleValueText.Text = $"{(int)ScaleSlider.Value}%";
    // Live preview: tell App to apply new scale to popup
    var scale = ScaleSlider.Value / 100.0;
    ((App)System.Windows.Application.Current).PreviewPopupScale(scale);
}
```

**Step 8: Add `PreviewPopupScale` to App.xaml.cs**

```csharp
public void PreviewPopupScale(double scale)
{
    _popup.ApplyScale(scale);
}
```

**Step 9: Update `SaveBtn_Click` in SettingsWindow.cs**

Replace `SettingsScale = ScaleSlider.Value / 100.0` with `PopupScale = ScaleSlider.Value / 100.0`.
Remove `SettingsWidth`/`SettingsHeight` from ResultConfig (those are now handled by position saving in Task 4).

**Step 10: Update the Settings XAML slider label**

Change label text from "UI Scale" or similar to "Usage Window Scale" so it's clear what it controls.

**Step 11: Build and verify**

```bash
cd "C:/Users/Štěpán/Claude Home/ClaudeUsageWin"
dotnet build 2>&1 | tail -20
```
Expected: 0 errors.

**Step 12: Commit**
```bash
git add -A
git commit -m "fix: scale slider now controls usage popup, not settings window"
```

---

### Task 2: Fix Double Red Line at 100% Usage

**Files:**
- Modify: `MainWindow.xaml` — replace bar Grids with Border+ClipToBounds pattern
- Modify: `MainWindow.cs` — update `SetBar` to accept Border

**Context:** The `Rectangle` with `RadiusX=4` inside a plain `Grid` can produce a 1px anti-aliased artifact below the bar at 100% fill due to WPF subpixel rendering and lack of clipping. Wrapping the fill in a `Border` with `ClipToBounds="True"` and `CornerRadius="4"` clips correctly.

**Step 1: Replace FiveHourBarGrid in MainWindow.xaml**

Find:
```xml
<Grid x:Name="FiveHourBarGrid" Height="8" Margin="0,0,0,4">
    <Rectangle Fill="#3D3D3D" RadiusX="4" RadiusY="4"/>
    <Rectangle x:Name="FiveHourBarFill" Fill="#4CAF50" RadiusX="4" RadiusY="4"
               HorizontalAlignment="Left" Width="0"/>
</Grid>
```

Replace with:
```xml
<Border x:Name="FiveHourBarGrid" Height="8" Margin="0,0,0,4"
        Background="#3D3D3D" CornerRadius="4" ClipToBounds="True">
    <Rectangle x:Name="FiveHourBarFill" Fill="#4CAF50"
               HorizontalAlignment="Left" Width="0"/>
</Border>
```

**Step 2: Replace WeeklyBarGrid in MainWindow.xaml**

Find:
```xml
<Grid x:Name="WeeklyBarGrid" Height="8" Margin="0,0,0,4">
    <Rectangle Fill="#3D3D3D" RadiusX="4" RadiusY="4"/>
    <Rectangle x:Name="WeeklyBarFill" Fill="#4CAF50" RadiusX="4" RadiusY="4"
               HorizontalAlignment="Left" Width="0"/>
</Grid>
```

Replace with:
```xml
<Border x:Name="WeeklyBarGrid" Height="8" Margin="0,0,0,4"
        Background="#3D3D3D" CornerRadius="4" ClipToBounds="True">
    <Rectangle x:Name="WeeklyBarFill" Fill="#4CAF50"
               HorizontalAlignment="Left" Width="0"/>
</Border>
```

**Step 3: Update `SetBar` in MainWindow.cs**

The signature uses `FrameworkElement` for the container which covers both Grid and Border. No signature change needed. But remove the `RadiusX`/`RadiusY` property set if any (there isn't one in code). Verify the method still works:

```csharp
private static void SetBar(System.Windows.Shapes.Rectangle fill,
                            FrameworkElement container, int pct)
{
    container.UpdateLayout();
    fill.Width = container.ActualWidth * Math.Clamp(pct, 0, 100) / 100.0;
    fill.Fill  = new SolidColorBrush(
        pct > 75 ? WpfColor.FromRgb(255, 87,  34)
      : pct > 50 ? WpfColor.FromRgb(255, 193,  7)
                 : WpfColor.FromRgb( 76, 175, 80));
}
```

No changes needed — `FrameworkElement` covers `Border`.

**Step 4: Build and verify**
```bash
dotnet build 2>&1 | tail -5
```

**Step 5: Commit**
```bash
git add MainWindow.xaml MainWindow.xaml.cs
git commit -m "fix: clip progress bars to prevent double-line artifact at 100%"
```

---

### Task 3: Fix Settings Modal Sound + Allow Dashboard Interaction

**Files:**
- Modify: `App.xaml.cs` — change `ShowDialog()` to `Show()`, handle via events
- Modify: `SettingsWindow.cs` — expose Saved/Cancelled events; handle close

**Context:** `ShowDialog()` creates a WPF application modal. Clicking another window while a modal is open triggers a Windows "ding" sound. Making Settings a non-modal window (`Show()`) removes this entirely and allows the user to move the dashboard while Settings is open.

**Step 1: Add events to SettingsWindow.cs**

Add at the top of the class:
```csharp
public event EventHandler<AppConfig>? Saved;
public event EventHandler? Cancelled;
```

**Step 2: Change `SaveBtn_Click` to fire event instead of closing as dialog**

Replace:
```csharp
DialogResult = true;
Close();
```
With:
```csharp
Saved?.Invoke(this, ResultConfig);
Close();
```

**Step 3: Change `CancelBtn_Click`**

Replace:
```csharp
DialogResult = false;
Close();
```
With:
```csharp
Cancelled?.Invoke(this, EventArgs.Empty);
Close();
```

**Step 4: Update `ShowSettings` in App.xaml.cs**

Replace the entire `ShowSettings` method:
```csharp
public void ShowSettings()
{
    // If already open, bring to front
    if (IsSettingsOpen)
    {
        _settingsWindow?.Activate();
        return;
    }

    IsSettingsOpen = true;
    _settingsWindow = new SettingsWindow(_config);
    _settingsWindow.Saved += (_, newConfig) =>
    {
        bool credChanged = newConfig.SessionKey != _config.SessionKey;
        _config = newConfig with { OrgId = credChanged ? "" : newConfig.OrgId };
        ConfigService.Save(_config);

        _timer.Interval = TimeSpan.FromSeconds(_config.RefreshInterval);
        ApplyWindowSettings();

        if (_config.LaunchAtStartup) StartupService.Enable();
        else                         StartupService.Disable();

        RefreshData();
    };
    _settingsWindow.Closed += (_, _) =>
    {
        IsSettingsOpen = false;
        _settingsWindow = null;
    };
    _settingsWindow.Show();
}
```

**Step 5: Add `_settingsWindow` field to App.xaml.cs**

```csharp
private SettingsWindow? _settingsWindow;
```

**Step 6: Remove `SettingsWindow` constructor's `DialogResult` usage**

Since it's no longer shown as dialog, remove any `DialogResult` property references from SettingsWindow.

**Step 7: Build and verify**
```bash
dotnet build 2>&1 | tail -5
```

**Step 8: Commit**
```bash
git add App.xaml.cs SettingsWindow.cs
git commit -m "fix: make settings non-modal to eliminate ding sound and allow popup interaction"
```

---

### Task 4: Remember and Restore Window Positions

**Files:**
- Modify: `Services/ConfigService.cs` — add position fields
- Modify: `App.xaml.cs` — save/restore popup position
- Modify: `SettingsWindow.cs` — save/restore settings position on open/close

**Context:** Currently only popup width and settings size are persisted. Add Left/Top for both windows. On open, restore last position; on close/move, save it.

**Step 1: Add position fields to AppConfig**

In `Services/ConfigService.cs`, add to the `AppConfig` record:
```csharp
// Window positions (double.NaN = not yet set = use default)
public double PopupLeft      { get; init; } = double.NaN;
public double PopupTop       { get; init; } = double.NaN;
public double SettingsLeft   { get; init; } = double.NaN;
public double SettingsTop    { get; init; } = double.NaN;
public int    SettingsWidth  { get; init; } = 460;
public int    SettingsHeight { get; init; } = 560;
```

Note: `SettingsWidth`/`SettingsHeight` were previously saved from SettingsWindow — keep them here now since we removed them from SettingsWindow's SaveBtn.

**Step 2: Restore popup position in App.xaml.cs `OnStartup`**

After `_popup.Show()`, instead of always calling `PositionNearTray()`, restore saved position if valid:
```csharp
if (!double.IsNaN(_config.PopupLeft) && !double.IsNaN(_config.PopupTop))
{
    _popup.Left = _config.PopupLeft;
    _popup.Top  = _config.PopupTop;
    // Clamp to screen bounds
    var wa = SystemParameters.WorkArea;
    _popup.Left = Math.Clamp(_popup.Left, wa.Left, wa.Right  - _popup.Width);
    _popup.Top  = Math.Clamp(_popup.Top,  wa.Top,  wa.Bottom - _popup.ActualHeight);
}
else
{
    _popup.PositionNearTray();
}
```

**Step 3: Save popup position when it moves**

In App.xaml.cs `OnStartup`, after the `SizeChanged` handler, add:
```csharp
_popup.LocationChanged += (_, _) =>
{
    if (!_popup.IsLoaded) return;
    _config = _config with { PopupLeft = _popup.Left, PopupTop = _popup.Top };
    ConfigService.Save(_config);
};
```

**Step 4: Restore and save Settings position**

In `ShowSettings()`, after creating `_settingsWindow`, restore position:
```csharp
_settingsWindow = new SettingsWindow(_config);

// Restore position
if (!double.IsNaN(_config.SettingsLeft) && !double.IsNaN(_config.SettingsTop))
{
    _settingsWindow.WindowStartupLocation = System.Windows.WindowStartupLocation.Manual;
    _settingsWindow.Left = _config.SettingsLeft;
    _settingsWindow.Top  = _config.SettingsTop;
    var wa = SystemParameters.WorkArea;
    _settingsWindow.Left = Math.Clamp(_settingsWindow.Left, wa.Left, wa.Right  - _settingsWindow.Width);
    _settingsWindow.Top  = Math.Clamp(_settingsWindow.Top,  wa.Top,  wa.Bottom - _settingsWindow.Height);
}

// Save position on close
_settingsWindow.Closed += (_, _) =>
{
    _config = _config with
    {
        SettingsLeft   = _settingsWindow!.Left,
        SettingsTop    = _settingsWindow!.Top,
        SettingsWidth  = (int)_settingsWindow!.Width,
        SettingsHeight = (int)_settingsWindow!.Height,
    };
    ConfigService.Save(_config);
    IsSettingsOpen = false;
    _settingsWindow = null;
};
```

Note: The SettingsWindow.xaml still has `WindowStartupLocation="CenterScreen"` as default; overriding `.Left`/`.Top` only works after also setting `WindowStartupLocation = Manual`.

**Step 5: Remove position-restoring code from SettingsWindow.cs constructor**

The constructor currently restores `SettingsWidth`/`SettingsHeight` from config. Remove those lines — they're now handled by App.cs.

**Step 6: Build and verify**
```bash
dotnet build 2>&1 | tail -5
```

**Step 7: Commit**
```bash
git add Services/ConfigService.cs App.xaml.cs SettingsWindow.cs
git commit -m "feat: remember and restore window positions across sessions"
```

---

### Task 5: Settings — Drag from Maximized Restores Floating

**Files:**
- Modify: `SettingsWindow.cs` — handle drag-from-maximized in TitleBar handler

**Context:** When Settings is maximized (via system snap or double-click title) and the user drags the title bar, Windows standard behavior restores the window and starts dragging it. This works automatically for standard `WindowStyle` windows, but our Settings uses `WindowStyle="None"` with a custom title, so we must implement it manually.

**Step 1: Update `TitleBar_PreviewMouseDown` in SettingsWindow.cs**

Replace the existing method:
```csharp
private void TitleBar_PreviewMouseDown(object sender,
    System.Windows.Input.MouseButtonEventArgs e)
{
    if (e.ChangedButton != System.Windows.Input.MouseButton.Left) return;

    // Don't capture drag if a button was clicked
    var src = e.OriginalSource as System.Windows.DependencyObject;
    while (src is not null)
    {
        if (src is System.Windows.Controls.Button) return;
        src = System.Windows.Media.VisualTreeHelper.GetParent(src);
    }

    if (WindowState == WindowState.Maximized)
    {
        // Restore to normal, then let the user drag from current cursor position
        var mousePos = e.GetPosition(this);
        WindowState = WindowState.Normal;

        // Re-position so the cursor is over the title bar area (proportionally)
        var screenPos = System.Windows.Forms.Control.MousePosition;
        Left = screenPos.X - (Width * (mousePos.X / ActualWidth));
        Top  = screenPos.Y - (mousePos.Y / 2.0); // keep near cursor
    }

    DragMove();
}
```

**Step 2: Build and verify**
```bash
dotnet build 2>&1 | tail -5
```

**Step 3: Commit**
```bash
git add SettingsWindow.cs
git commit -m "feat: drag settings from maximized state restores floating window"
```

---

### Task 6: Reduce App Size — IL Trimming

**Files:**
- Modify: `ClaudeUsageWin.csproj`

**Context:** Self-contained .NET 8 bundles the entire runtime (~70MB). IL trimming removes unused code and can cut size by 30-50% for WPF apps (with caveats — WPF uses reflection heavily, so `TrimmerRootDescriptor` or `TrimMode=partial` is needed).

**Step 1: Add trimming flags to .csproj**

In `ClaudeUsageWin.csproj`, inside the `<PropertyGroup>`:
```xml
<PublishTrimmed>true</PublishTrimmed>
<TrimMode>partial</TrimMode>
<!-- WPF requires keeping all assemblies; partial trim still removes unused methods -->
```

**Step 2: Test publish and check size**
```bash
cd "C:/Users/Štěpán/Claude Home/ClaudeUsageWin"
dotnet publish -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -o dist 2>&1 | tail -10
ls -lh dist/ClaudeUsage.exe
```

**Step 3: Run the published exe briefly to verify it launches**
```bash
start "" "C:/Users/Štěpán/Claude Home/ClaudeUsageWin/dist/ClaudeUsage.exe"
```
Wait 5 seconds, check it appears in system tray.

**Step 4: Kill test process**
```bash
taskkill /IM ClaudeUsage.exe /F 2>/dev/null || true
```

**Step 5: Commit**
```bash
git add ClaudeUsageWin.csproj
git commit -m "perf: enable IL partial trimming to reduce EXE size"
```

---

## PHASE 2 — SWIFT FEATURE PARITY

> Start Phase 2 only after all Phase 1 tasks are committed.

---

### Task 7: Contextual Insights Panel

**Files:**
- Modify: `MainWindow.xaml` — add collapsible Insights section
- Modify: `MainWindow.cs` — populate insights from UsageData + history
- Modify: `Services/UsageHistory.cs` — add helpers for rate calculation

**Context:** The Swift app has a "Contextual Insights" panel that expands from the footer. It shows usage rate per hour, estimated time to limit, and peak usage. We add a toggleable panel at the bottom of the popup.

**Step 1: Add Insights toggle button to MainWindow header**

In `MainWindow.xaml`, add a `💡` button next to the existing header buttons (after Pin, before Close):
```xml
<Button x:Name="InsightsBtn" Grid.Column="3" Content="&#x1F4A1;"
        FontSize="11" Cursor="Hand" Width="22" Height="22" Margin="4,0,0,0"
        Click="InsightsBtn_Click" VerticalAlignment="Center"
        ToolTip="Toggle insights">
    <Button.Template><!-- same template as PinBtn --></Button.Template>
    <Button.Foreground><SolidColorBrush Color="#9E9E9E"/></Button.Foreground>
</Button>
```

**Step 2: Add Insights panel XAML above the footer**

Before the footer `<Grid>` (the one with UpdatedText and RefreshBtn), add:
```xml
<!-- ── Insights Panel ── -->
<Border x:Name="InsightsPanel" Background="#1E1E1E" CornerRadius="6" Padding="10,8"
        Margin="0,0,0,8" Visibility="Collapsed">
    <StackPanel>
        <TextBlock Text="INSIGHTS" FontSize="9" FontWeight="SemiBold"
                   Foreground="#5A5A5A" Margin="0,0,0,6" LetterSpacing="1"/>
        <Grid Margin="0,0,0,4">
            <Grid.ColumnDefinitions>
                <ColumnDefinition Width="*"/><ColumnDefinition Width="Auto"/>
            </Grid.ColumnDefinitions>
            <TextBlock Text="Usage rate" FontSize="11" Foreground="#9E9E9E"/>
            <TextBlock x:Name="InsightRateText" Grid.Column="1"
                       FontSize="11" Foreground="White" Text="--"/>
        </Grid>
        <Grid Margin="0,0,0,4">
            <Grid.ColumnDefinitions>
                <ColumnDefinition Width="*"/><ColumnDefinition Width="Auto"/>
            </Grid.ColumnDefinitions>
            <TextBlock Text="Time to limit" FontSize="11" Foreground="#9E9E9E"/>
            <TextBlock x:Name="InsightTimeText" Grid.Column="1"
                       FontSize="11" Foreground="White" Text="--"/>
        </Grid>
        <Grid>
            <Grid.ColumnDefinitions>
                <ColumnDefinition Width="*"/><ColumnDefinition Width="Auto"/>
            </Grid.ColumnDefinitions>
            <TextBlock Text="Session peak" FontSize="11" Foreground="#9E9E9E"/>
            <TextBlock x:Name="InsightPeakText" Grid.Column="1"
                       FontSize="11" Foreground="White" Text="--"/>
        </Grid>
    </StackPanel>
</Border>
```

**Step 3: Add insights toggle logic in MainWindow.cs**

```csharp
private bool _insightsVisible = false;

private void InsightsBtn_Click(object sender, RoutedEventArgs e)
{
    _insightsVisible = !_insightsVisible;
    InsightsPanel.Visibility = _insightsVisible ? Visibility.Visible : Visibility.Collapsed;
    InsightsBtn.Foreground = new SolidColorBrush(
        _insightsVisible ? WpfColor.FromRgb(255, 193, 7) : WpfColor.FromRgb(158, 158, 158));
}
```

**Step 4: Populate insights when data updates**

In `UpdateData()`, after the existing updates, add:
```csharp
UpdateInsights(data);
```

Add a new `UpdateInsights` method:
```csharp
private void UpdateInsights(UsageData data)
{
    var history = UsageHistory.Load();
    if (history.Count < 2)
    {
        InsightRateText.Text = "not enough data";
        InsightTimeText.Text = "--";
        InsightPeakText.Text = "--";
        return;
    }

    // Rate: pct change per hour based on last two points
    var last  = history[^1];
    var prev  = history[^2];
    double dtHours = (last.Timestamp - prev.Timestamp).TotalHours;
    double rate = dtHours > 0 ? (last.FiveHourPct - prev.FiveHourPct) / dtHours : 0;
    InsightRateText.Text = rate >= 0 ? $"+{rate:F1}%/hr" : $"{rate:F1}%/hr";

    // Time to limit
    if (rate > 0)
    {
        double hoursLeft = (100 - last.FiveHourPct) / rate;
        InsightTimeText.Text = hoursLeft < 1
            ? $"{(int)(hoursLeft * 60)}m"
            : $"{hoursLeft:F1}h";
    }
    else
    {
        InsightTimeText.Text = "stable";
    }

    // Peak
    int peak = history.Max(h => h.FiveHourPct);
    InsightPeakText.Text = $"{peak}%";
}
```

**Step 5: Ensure `HistoryPoint` has a `Timestamp` field**

Check `Services/UsageHistory.cs`. If `HistoryPoint` does not have a `Timestamp` property, add one (DateTime UTC). Update `Append` to set it.

**Step 6: Build and verify**
```bash
dotnet build 2>&1 | tail -5
```

**Step 7: Commit**
```bash
git add MainWindow.xaml MainWindow.xaml.cs Services/UsageHistory.cs
git commit -m "feat: add contextual insights panel to usage popup"
```

---

### Task 8: Multi-Profile Support

**Files:**
- Create: `Services/ProfileService.cs` — profile CRUD, active profile
- Modify: `Services/ConfigService.cs` — per-profile config path
- Modify: `App.xaml.cs` — load active profile config
- Modify: `MainWindow.xaml` — add profile indicator in header
- Modify: `MainWindow.cs` — show active profile name
- Modify: `SettingsWindow.xaml` — add Profiles tab/section
- Modify: `SettingsWindow.cs` — profile management UI logic

**Context:** Allow multiple Claude accounts (e.g., personal Pro + work Team). Each profile has its own session key / OAuth binding. The active profile is shown in the popup header.

**Step 1: Create `Services/ProfileService.cs`**

```csharp
using System.IO;
using System.Text.Json;

namespace ClaudeUsageWin.Services;

public record Profile
{
    public string Id          { get; init; } = Guid.NewGuid().ToString();
    public string Name        { get; init; } = "Default";
    public string SessionKey  { get; init; } = "";
    public string OrgId       { get; init; } = "";
    // OAuth is always read from ~/.claude/.credentials.json for the active profile
}

public static class ProfileService
{
    private static readonly string ProfilesDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "ClaudeUsageWin", "profiles");

    private static readonly string ActiveFile = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "ClaudeUsageWin", "active-profile.txt");

    public static List<Profile> LoadAll()
    {
        if (!Directory.Exists(ProfilesDir)) return [new Profile()];
        var profiles = Directory.GetFiles(ProfilesDir, "*.json")
            .Select(f =>
            {
                try { return JsonSerializer.Deserialize<Profile>(File.ReadAllText(f)); }
                catch { return null; }
            })
            .Where(p => p is not null)
            .Cast<Profile>()
            .ToList();
        return profiles.Count > 0 ? profiles : [new Profile()];
    }

    public static string GetActiveId()
    {
        try { return File.Exists(ActiveFile) ? File.ReadAllText(ActiveFile).Trim() : ""; }
        catch { return ""; }
    }

    public static Profile? GetActive()
    {
        var id = GetActiveId();
        var all = LoadAll();
        return all.FirstOrDefault(p => p.Id == id) ?? all.FirstOrDefault();
    }

    public static void Save(Profile profile)
    {
        Directory.CreateDirectory(ProfilesDir);
        File.WriteAllText(
            Path.Combine(ProfilesDir, $"{profile.Id}.json"),
            JsonSerializer.Serialize(profile, new JsonSerializerOptions { WriteIndented = true }));
    }

    public static void SetActive(string id)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(ActiveFile)!);
        File.WriteAllText(ActiveFile, id);
    }

    public static void Delete(string id)
    {
        var path = Path.Combine(ProfilesDir, $"{id}.json");
        if (File.Exists(path)) File.Delete(path);
    }
}
```

**Step 2: Update App.xaml.cs to load active profile's credentials**

In `OnStartup`, after loading `_config`, overlay session key from active profile:
```csharp
var profile = ProfileService.GetActive();
if (profile is not null && !string.IsNullOrWhiteSpace(profile.SessionKey))
    _config = _config with { SessionKey = profile.SessionKey, OrgId = profile.OrgId };
```

**Step 3: Add profile name to popup header**

In `MainWindow.xaml`, add a small text below "Claude Usage" title:
```xml
<TextBlock x:Name="ProfileNameText" Text="" FontSize="10"
           Foreground="#6A6A6A" Margin="0,2,0,0"
           Visibility="Collapsed"/>
```

**Step 4: Expose `SetProfileName` in MainWindow.cs**

```csharp
public void SetProfileName(string name)
{
    ProfileNameText.Text       = string.IsNullOrEmpty(name) ? "" : $"({name})";
    ProfileNameText.Visibility = string.IsNullOrEmpty(name) ? Visibility.Collapsed : Visibility.Visible;
}
```

**Step 5: Call `SetProfileName` in App.xaml.cs after startup and profile switch**

```csharp
var profile = ProfileService.GetActive();
_popup.SetProfileName(profile?.Name ?? "");
```

**Step 6: Add Profiles section to SettingsWindow**

In `SettingsWindow.xaml`, add a new expander/section below the Auth section:
```xml
<!-- ── Profiles ── -->
<TextBlock Text="PROFILES" Style="{StaticResource Label}" Margin="0,16,0,5"/>
<Border Background="#1A1A1A" CornerRadius="6" Padding="12">
    <StackPanel>
        <ListBox x:Name="ProfilesList" Background="Transparent" BorderThickness="0"
                 MaxHeight="120" Foreground="White"/>
        <StackPanel Orientation="Horizontal" Margin="0,8,0,0" HorizontalAlignment="Right">
            <Button Content="+ Add" Style="{StaticResource SmallBtn}"
                    Background="#3D3D3D" Margin="0,0,6,0"
                    Click="AddProfileBtn_Click"/>
            <Button Content="Switch" Style="{StaticResource SmallBtn}"
                    Background="#3D6B4A"  Margin="0,0,6,0"
                    Click="SwitchProfileBtn_Click"/>
            <Button Content="Delete" Style="{StaticResource SmallBtn}"
                    Background="#6B3D3D"
                    Click="DeleteProfileBtn_Click"/>
        </StackPanel>
    </StackPanel>
</Border>
```

**Step 7: Populate and wire up ProfilesList in SettingsWindow.cs constructor**

```csharp
RefreshProfilesList();
```

```csharp
private void RefreshProfilesList()
{
    if (ProfilesList is null) return;
    var profiles = ProfileService.LoadAll();
    var activeId = ProfileService.GetActiveId();
    ProfilesList.Items.Clear();
    foreach (var p in profiles)
    {
        var marker = p.Id == activeId ? " ✓" : "";
        ProfilesList.Items.Add($"{p.Name}{marker}  ({(string.IsNullOrEmpty(p.SessionKey) ? "OAuth" : "key")})");
    }
    ProfilesList.SelectedIndex = profiles.FindIndex(p => p.Id == activeId);
    _profilesList = profiles;
}
private List<Profile> _profilesList = [];
```

**Step 8: Implement Add/Switch/Delete handlers**

```csharp
private void AddProfileBtn_Click(object sender, RoutedEventArgs e)
{
    var name = Microsoft.VisualBasic.Interaction.InputBox("Profile name:", "New Profile", "Work");
    if (string.IsNullOrWhiteSpace(name)) return;
    var p = new Profile { Name = name.Trim() };
    ProfileService.Save(p);
    RefreshProfilesList();
}

private void SwitchProfileBtn_Click(object sender, RoutedEventArgs e)
{
    if (ProfilesList.SelectedIndex < 0 || _profilesList.Count == 0) return;
    var selected = _profilesList[ProfilesList.SelectedIndex];
    ProfileService.SetActive(selected.Id);
    RefreshProfilesList();
    // Tell App to reload
    ((App)System.Windows.Application.Current).ReloadProfile();
}

private void DeleteProfileBtn_Click(object sender, RoutedEventArgs e)
{
    if (ProfilesList.SelectedIndex < 0 || _profilesList.Count <= 1) return;
    var selected = _profilesList[ProfilesList.SelectedIndex];
    if (selected.Id == ProfileService.GetActiveId()) return; // can't delete active
    ProfileService.Delete(selected.Id);
    RefreshProfilesList();
}
```

**Step 9: Add `ReloadProfile` to App.xaml.cs**

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

Note: `Microsoft.VisualBasic.Interaction.InputBox` requires adding `Microsoft.VisualBasic` reference. Alternative: create a simple input dialog window instead.

**Step 10: Build and verify**
```bash
dotnet build 2>&1 | tail -10
```

**Step 11: Commit**
```bash
git add Services/ProfileService.cs App.xaml.cs MainWindow.xaml MainWindow.xaml.cs SettingsWindow.xaml SettingsWindow.cs
git commit -m "feat: multi-profile support with profile switcher in settings"
```

---

### Task 9: About / Updates Tab in Settings

**Files:**
- Modify: `SettingsWindow.xaml` — add About section at bottom
- Modify: `SettingsWindow.cs` — version display + GitHub releases check
- Modify: `ClaudeUsageWin.csproj` — set assembly version

**Context:** Swift app has an About/Updates section. Add a collapsible "About" section at the bottom of Settings showing version, update check button, and a link to the repo.

**Step 1: Set version in .csproj**

In `ClaudeUsageWin.csproj` `<PropertyGroup>`:
```xml
<Version>1.1.0</Version>
<AssemblyVersion>1.1.0.0</AssemblyVersion>
```

**Step 2: Add About section to SettingsWindow.xaml**

At the very bottom of the settings content (before the Cancel/Save button row), add:
```xml
<!-- ── About ── -->
<TextBlock Text="ABOUT" Style="{StaticResource Label}" Margin="0,16,0,5"/>
<Border Background="#1A1A1A" CornerRadius="6" Padding="12">
    <Grid>
        <Grid.ColumnDefinitions>
            <ColumnDefinition Width="*"/>
            <ColumnDefinition Width="Auto"/>
        </Grid.ColumnDefinitions>
        <StackPanel>
            <TextBlock Text="Claude Usage Win" Foreground="White" FontWeight="SemiBold" FontSize="13"/>
            <TextBlock x:Name="VersionText" Text="v1.1.0" Foreground="#7A7A7A" FontSize="11" Margin="0,2,0,0"/>
            <TextBlock x:Name="UpdateStatusText" Text="" Foreground="#7A7A7A" FontSize="11" Margin="0,4,0,0"
                       TextWrapping="Wrap"/>
        </StackPanel>
        <Button x:Name="CheckUpdateBtn" Grid.Column="1" Content="Check for updates"
                Style="{StaticResource SmallBtn}" Background="#3D3D3D"
                VerticalAlignment="Top" Click="CheckUpdateBtn_Click"/>
    </Grid>
</Border>
```

**Step 3: Set version text in SettingsWindow constructor**

```csharp
var ver = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
VersionText.Text = ver is not null ? $"v{ver.Major}.{ver.Minor}.{ver.Build}" : "v1.1.0";
```

**Step 4: Implement `CheckUpdateBtn_Click` in SettingsWindow.cs**

```csharp
private async void CheckUpdateBtn_Click(object sender, RoutedEventArgs e)
{
    CheckUpdateBtn.IsEnabled = false;
    CheckUpdateBtn.Content   = "Checking…";
    UpdateStatusText.Text    = "";

    try
    {
        using var http = new System.Net.Http.HttpClient();
        http.DefaultRequestHeaders.Add("User-Agent", "ClaudeUsageWin");
        var json = await http.GetStringAsync(
            "https://api.github.com/repos/your-username/claude-usage-win/releases/latest");
        var doc  = System.Text.Json.JsonDocument.Parse(json);
        var tag  = doc.RootElement.GetProperty("tag_name").GetString() ?? "";
        var cur  = VersionText.Text.TrimStart('v');
        var latest = tag.TrimStart('v');

        if (latest == cur)
        {
            UpdateStatusText.Text       = "✓ You are on the latest version.";
            UpdateStatusText.Foreground = new System.Windows.Media.SolidColorBrush(
                System.Windows.Media.Color.FromRgb(76, 175, 80));
        }
        else
        {
            UpdateStatusText.Text       = $"Update available: {tag}";
            UpdateStatusText.Foreground = new System.Windows.Media.SolidColorBrush(
                System.Windows.Media.Color.FromRgb(255, 193, 7));
        }
    }
    catch
    {
        UpdateStatusText.Text       = "Could not check for updates.";
        UpdateStatusText.Foreground = new System.Windows.Media.SolidColorBrush(
            System.Windows.Media.Color.FromRgb(255, 87, 34));
    }
    finally
    {
        CheckUpdateBtn.IsEnabled = true;
        CheckUpdateBtn.Content   = "Check for updates";
    }
}
```

**Note:** Replace `your-username` with the actual GitHub repo path before shipping.

**Step 5: Build and verify**
```bash
dotnet build 2>&1 | tail -5
```

**Step 6: Commit**
```bash
git add SettingsWindow.xaml SettingsWindow.cs ClaudeUsageWin.csproj
git commit -m "feat: add About section with version display and update checker"
```

---

### Task 10: First-Run Setup Wizard

**Files:**
- Create: `SetupWizardWindow.xaml` + `SetupWizardWindow.xaml.cs`
- Modify: `App.xaml.cs` — show wizard on first launch instead of settings

**Context:** When no credentials and no local stats exist, instead of opening the raw Settings, show a friendly wizard with steps: Welcome → Auth → Done.

**Step 1: Create `SetupWizardWindow.xaml`**

```xml
<Window x:Class="ClaudeUsageWin.SetupWizardWindow"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        Title="Claude Usage — Setup"
        Width="420" Height="420"
        WindowStyle="None" AllowsTransparency="True"
        Background="Transparent"
        WindowStartupLocation="CenterScreen"
        ResizeMode="NoResize">

    <Border Background="#2B2B2B" CornerRadius="12" Padding="28,24">
        <Border.Effect>
            <DropShadowEffect Color="Black" Opacity="0.5" BlurRadius="20" ShadowDepth="4"/>
        </Border.Effect>

        <StackPanel>
            <!-- Step indicators -->
            <StackPanel Orientation="Horizontal" HorizontalAlignment="Center" Margin="0,0,0,24">
                <Ellipse x:Name="Step1Dot" Width="10" Height="10" Margin="4,0"
                         Fill="White"/>
                <Ellipse x:Name="Step2Dot" Width="10" Height="10" Margin="4,0"
                         Fill="#444"/>
                <Ellipse x:Name="Step3Dot" Width="10" Height="10" Margin="4,0"
                         Fill="#444"/>
            </StackPanel>

            <!-- Step 1: Welcome -->
            <StackPanel x:Name="Step1Panel">
                <TextBlock Text="👋 Welcome to Claude Usage" FontSize="18" FontWeight="Bold"
                           Foreground="White" HorizontalAlignment="Center" Margin="0,0,0,12"/>
                <TextBlock Text="Monitor your Claude AI usage limits right from the system tray."
                           Foreground="#9E9E9E" FontSize="13" TextWrapping="Wrap"
                           HorizontalAlignment="Center" TextAlignment="Center" Margin="0,0,0,24"/>
                <Button Content="Get Started →" Background="#4CAF50" Foreground="White"
                        FontSize="14" Padding="24,10" HorizontalAlignment="Center"
                        Cursor="Hand" Click="NextBtn_Click">
                    <Button.Template>
                        <ControlTemplate TargetType="Button">
                            <Border Background="{TemplateBinding Background}" CornerRadius="8"
                                    Padding="{TemplateBinding Padding}">
                                <ContentPresenter HorizontalAlignment="Center" VerticalAlignment="Center"/>
                            </Border>
                        </ControlTemplate>
                    </Button.Template>
                </Button>
            </StackPanel>

            <!-- Step 2: Auth -->
            <StackPanel x:Name="Step2Panel" Visibility="Collapsed">
                <TextBlock Text="🔑 Connect Your Account" FontSize="16" FontWeight="Bold"
                           Foreground="White" HorizontalAlignment="Center" Margin="0,0,0,12"/>
                <Border x:Name="WizardAutoAuthBorder" CornerRadius="6" Padding="12" Margin="0,0,0,12">
                    <TextBlock x:Name="WizardAutoAuthText" Foreground="White" FontSize="12"
                               TextWrapping="Wrap"/>
                </Border>
                <TextBlock Text="Or enter your session key manually:"
                           Foreground="#9E9E9E" FontSize="12" Margin="0,0,0,6"/>
                <PasswordBox x:Name="WizardKeyBox" Background="#1A1A1A" Foreground="White"
                             FontSize="13" Padding="10,7" Height="36" BorderThickness="1"
                             BorderBrush="#444"/>
                <StackPanel Orientation="Horizontal" HorizontalAlignment="Right" Margin="0,12,0,0">
                    <Button Content="Back" Background="#3D3D3D" Foreground="White"
                            Padding="16,8" Margin="0,0,8,0" Cursor="Hand" Click="BackBtn_Click">
                        <Button.Template>
                            <ControlTemplate TargetType="Button">
                                <Border Background="{TemplateBinding Background}" CornerRadius="6"
                                        Padding="{TemplateBinding Padding}">
                                    <ContentPresenter HorizontalAlignment="Center" VerticalAlignment="Center"/>
                                </Border>
                            </ControlTemplate>
                        </Button.Template>
                    </Button>
                    <Button Content="Continue →" Background="#4CAF50" Foreground="White"
                            Padding="16,8" Cursor="Hand" Click="NextBtn_Click">
                        <Button.Template>
                            <ControlTemplate TargetType="Button">
                                <Border Background="{TemplateBinding Background}" CornerRadius="6"
                                        Padding="{TemplateBinding Padding}">
                                    <ContentPresenter HorizontalAlignment="Center" VerticalAlignment="Center"/>
                                </Border>
                            </ControlTemplate>
                        </Button.Template>
                    </Button>
                </StackPanel>
            </StackPanel>

            <!-- Step 3: Done -->
            <StackPanel x:Name="Step3Panel" Visibility="Collapsed">
                <TextBlock Text="✅ You're all set!" FontSize="18" FontWeight="Bold"
                           Foreground="White" HorizontalAlignment="Center" Margin="0,0,0,12"/>
                <TextBlock Text="Claude Usage is now tracking your limits from the system tray. Click the tray icon to view your usage."
                           Foreground="#9E9E9E" FontSize="13" TextWrapping="Wrap"
                           HorizontalAlignment="Center" TextAlignment="Center" Margin="0,0,0,24"/>
                <Button Content="Start Monitoring" Background="#4CAF50" Foreground="White"
                        FontSize="14" Padding="24,10" HorizontalAlignment="Center"
                        Cursor="Hand" Click="FinishBtn_Click">
                    <Button.Template>
                        <ControlTemplate TargetType="Button">
                            <Border Background="{TemplateBinding Background}" CornerRadius="8"
                                    Padding="{TemplateBinding Padding}">
                                <ContentPresenter HorizontalAlignment="Center" VerticalAlignment="Center"/>
                            </Border>
                        </ControlTemplate>
                    </Button.Template>
                </Button>
            </StackPanel>
        </StackPanel>
    </Border>
</Window>
```

**Step 2: Create `SetupWizardWindow.xaml.cs`**

```csharp
using System.Windows;
using ClaudeUsageWin.Services;

namespace ClaudeUsageWin;

public partial class SetupWizardWindow : Window
{
    private int _step = 1;
    public string? ResultSessionKey { get; private set; }

    public SetupWizardWindow()
    {
        InitializeComponent();
        ShowStep(1);
        // Show auto-auth status
        var creds = CredentialsReader.TryRead();
        if (creds is not null && !CredentialsReader.IsExpired(creds))
        {
            WizardAutoAuthBorder.Background =
                new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromRgb(20, 50, 20));
            WizardAutoAuthText.Text = $"✓ Claude Code detected — signed in as {creds.SubscriptionType}";
        }
        else
        {
            WizardAutoAuthBorder.Background =
                new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromRgb(50, 30, 15));
            WizardAutoAuthText.Text = "Claude Code not found. Enter your session key below.";
        }
    }

    private void ShowStep(int step)
    {
        _step = step;
        Step1Panel.Visibility = step == 1 ? Visibility.Visible : Visibility.Collapsed;
        Step2Panel.Visibility = step == 2 ? Visibility.Visible : Visibility.Collapsed;
        Step3Panel.Visibility = step == 3 ? Visibility.Visible : Visibility.Collapsed;
        Step1Dot.Fill = DotColor(step >= 1);
        Step2Dot.Fill = DotColor(step >= 2);
        Step3Dot.Fill = DotColor(step >= 3);
    }

    private static System.Windows.Media.SolidColorBrush DotColor(bool active) =>
        new(active
            ? System.Windows.Media.Color.FromRgb(76, 175, 80)
            : System.Windows.Media.Color.FromRgb(68, 68, 68));

    private void NextBtn_Click(object sender, RoutedEventArgs e)
    {
        if (_step == 1) ShowStep(2);
        else if (_step == 2)
        {
            ResultSessionKey = WizardKeyBox.Password.Trim();
            ShowStep(3);
        }
    }

    private void BackBtn_Click(object sender, RoutedEventArgs e) => ShowStep(_step - 1);

    private void FinishBtn_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }
}
```

**Step 3: Update App.xaml.cs first-run check**

Replace the first-run block in `OnStartup`:
```csharp
var creds     = CredentialsReader.TryRead();
bool hasAny   = !string.IsNullOrWhiteSpace(_config.SessionKey) || creds is not null;
if (!hasAny)
{
    var localStats = LocalStatsReader.TryRead();
    if (localStats is null)
    {
        // First run — show wizard instead of raw settings
        var wizard = new SetupWizardWindow();
        if (wizard.ShowDialog() != true)
        {
            Shutdown();
            return;
        }
        if (!string.IsNullOrWhiteSpace(wizard.ResultSessionKey))
        {
            _config = _config with { SessionKey = wizard.ResultSessionKey };
            ConfigService.Save(_config);
        }
    }
}
```

**Step 4: Build and verify**
```bash
dotnet build 2>&1 | tail -10
```

**Step 5: Commit**
```bash
git add SetupWizardWindow.xaml SetupWizardWindow.xaml.cs App.xaml.cs
git commit -m "feat: add first-run setup wizard for new users"
```

---

## Agent Team Split

For parallel execution, split across 3 agents:

| Agent | Tasks | Files owned |
|-------|-------|-------------|
| **Agent-Bugs** | 1, 2, 3, 4, 5, 6 | ConfigService.cs, App.xaml.cs, SettingsWindow.cs, SettingsWindow.xaml, MainWindow.xaml, MainWindow.cs, .csproj |
| **Agent-Features-A** | 7 (Insights), 9 (About), 10 (Wizard) | MainWindow.xaml+cs, SettingsWindow.xaml+cs, SetupWizardWindow.* |
| **Agent-Features-B** | 8 (Multi-profile) | ProfileService.cs, App.xaml.cs, MainWindow.xaml+cs, SettingsWindow.xaml+cs |

> **Important:** Agent-Features-A and Agent-Features-B BOTH touch SettingsWindow and MainWindow. Run Agent-Bugs first (commits), then Features-A and Features-B sequentially or with careful merge coordination.

## Final Build + Release

After all tasks committed:

```bash
cd "C:/Users/Štěpán/Claude Home/ClaudeUsageWin"
dotnet publish -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -o dist
ls -lh dist/ClaudeUsage.exe
```

Tag and release:
```bash
git tag v1.1.0
git push origin v1.1.0
```
