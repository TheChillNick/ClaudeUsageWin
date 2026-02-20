using System.Threading;
using System.Windows;
using System.Windows.Threading;
using System.Drawing;
using System.Windows.Forms;
using ClaudeUsageWin.Services;

using WpfApp = System.Windows.Application;

namespace ClaudeUsageWin;

public enum AuthMode { Auto, ManualKey }

public partial class App : WpfApp
{
    public static bool IsSettingsOpen { get; private set; }
    public bool IsAlwaysOnTop => _config?.AlwaysOnTop ?? true;

    private static Mutex? _singleInstanceMutex;

    private NotifyIcon        _tray           = null!;
    private MainWindow        _popup          = null!;
    private DispatcherTimer   _timer          = null!;
    private AppConfig         _config         = null!;
    private ThresholdNotifier _notifier       = new();
    private int?              _lastPct;
    private SettingsWindow?   _settingsWindow;

    // ── Startup ──────────────────────────────────────────────────────

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // ── Single-instance guard ─────────────────────────────────
        _singleInstanceMutex = new Mutex(true, "ClaudeUsageWin_SingleInstance_v1",
                                         out bool createdNew);
        if (!createdNew)
        {
            System.Windows.MessageBox.Show(
                "Claude Usage is already running.\nCheck the system tray.",
                "Claude Usage", MessageBoxButton.OK, MessageBoxImage.Information);
            Shutdown();
            return;
        }

        // ── Unhandled exception handlers ──────────────────────────
        DispatcherUnhandledException += (_, ev) =>
        {
            Logger.LogError("Unhandled UI exception", ev.Exception);
            ev.Handled = true;
        };
        AppDomain.CurrentDomain.UnhandledException += (_, ev) =>
            Logger.LogError("AppDomain unhandled", ev.ExceptionObject as Exception);

        _config = ConfigService.Load();

        // Overlay session key from active profile if present
        var activeProfile = ProfileService.GetActive();
        if (activeProfile is not null && !string.IsNullOrWhiteSpace(activeProfile.SessionKey))
            _config = _config with { SessionKey = activeProfile.SessionKey, OrgId = activeProfile.OrgId };

        _popup  = new MainWindow();
        _popup.Width = Math.Clamp(_config.PopupWidth, 260, 500);
        _popup.ShowRemainingToggled += OnShowRemainingToggled;

        // Remember popup width when user resizes
        _popup.SizeChanged += (_, _) =>
        {
            if (!_popup.IsLoaded) return;
            var w = (int)_popup.Width;
            if (w != _config.PopupWidth)
            {
                _config = _config with { PopupWidth = w };
                ConfigService.Save(_config);
            }
        };

        // Save popup position when it moves
        _popup.LocationChanged += (_, _) =>
        {
            if (!_popup.IsLoaded) return;
            _config = _config with { PopupLeft = _popup.Left, PopupTop = _popup.Top };
            ConfigService.Save(_config);
        };

        ApplyWindowSettings();
        BuildTrayIcon();
        BuildTimer();

        // Network monitoring
        System.Net.NetworkInformation.NetworkChange.NetworkAvailabilityChanged += (_, ev) =>
        {
            if (ev.IsAvailable) Dispatcher.Invoke(() => RefreshData());
            else                Dispatcher.Invoke(() => _popup.SetOfflineMode(true));
        };

        // Need at least one auth source or local stats
        var creds     = CredentialsReader.TryRead();
        bool hasAny   = !string.IsNullOrWhiteSpace(_config.SessionKey) || creds is not null;
        if (!hasAny)
        {
            var localStats = LocalStatsReader.TryRead();
            if (localStats is null)
            {
                Logger.Log("Startup: no credentials and no local stats, opening settings");
                ShowSettings();
                return;
            }
            Logger.Log("Startup: no credentials — will use local stats only");
        }

        Logger.Log($"Startup: hasSessionKey={!string.IsNullOrWhiteSpace(_config.SessionKey)}, " +
                   $"hasOAuth={creds is not null}");

        // Store subscription type from credentials for immediate plan display
        if (creds is not null && !string.IsNullOrEmpty(creds.SubscriptionType)
            && _config.SubscriptionType != creds.SubscriptionType)
        {
            _config = _config with { SubscriptionType = creds.SubscriptionType };
            ConfigService.Save(_config);
        }

        _popup.Show();
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
        _popup.Activate();
        _popup.SetProfileName(ProfileService.GetActive()?.Name ?? "");

        RefreshData();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _singleInstanceMutex?.ReleaseMutex();
        _singleInstanceMutex?.Dispose();
        _tray?.Dispose();
        base.OnExit(e);
    }

    // ── Window settings ──────────────────────────────────────────────

    private void ApplyWindowSettings()
    {
        _popup.Opacity = _config.OpacityPct / 100.0;
        _popup.Topmost = _config.AlwaysOnTop;
        _popup.ApplyScale(_config.PopupScale);
    }

    public void PreviewPopupScale(double scale)
    {
        _popup.ApplyScale(scale);
    }

    // ── Tray icon ────────────────────────────────────────────────────

    private void BuildTrayIcon()
    {
        var menu = new ContextMenuStrip();
        menu.Items.Add("Show / Hide", null, (_, _) => TogglePopup());
        menu.Items.Add("Minimize",    null, (_, _) => Dispatcher.Invoke(() => _popup.Hide()));
        menu.Items.Add(new ToolStripSeparator());

        var resizeMenu = new ToolStripMenuItem("Resize");
        resizeMenu.DropDownItems.Add("Small  (280px)", null, (_, _) => SetPopupWidth(280));
        resizeMenu.DropDownItems.Add("Normal (340px)", null, (_, _) => SetPopupWidth(340));
        resizeMenu.DropDownItems.Add("Large  (420px)", null, (_, _) => SetPopupWidth(420));
        menu.Items.Add(resizeMenu);

        menu.Items.Add("Open Claude.ai", null, (_, _) =>
            System.Diagnostics.Process.Start(
                new System.Diagnostics.ProcessStartInfo("https://claude.ai") { UseShellExecute = true }));
        menu.Items.Add("Refresh Now",  null, (_, _) => RefreshData());
        menu.Items.Add("Settings",     null, (_, _) => ShowSettings());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Quit",         null, (_, _) => Shutdown());

        var (initIcon, initHandle) = MakeIconWithHandle(0, _config.IconStyle);
        _lastIconHandle = initHandle;
        _tray = new NotifyIcon
        {
            Visible          = true,
            Text             = "Claude Usage",
            Icon             = initIcon,
            ContextMenuStrip = menu,
        };
        _tray.MouseClick += (_, e) =>
        {
            if (e.Button == MouseButtons.Left)
                TogglePopup();
        };
    }

    private IntPtr _lastIconHandle = IntPtr.Zero;

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool DestroyIcon(IntPtr hIcon);

    private void UpdateTray(int pct)
    {
        var displayPct = _config.ShowRemaining ? (100 - pct) : pct;
        var label = _config.ShowRemaining ? "Remaining" : "Used";

        var (icon, handle) = MakeIconWithHandle(pct, _config.IconStyle);
        _tray.Icon = icon;
        _tray.Text = $"Claude Usage — {displayPct}% {label}";

        if (_lastIconHandle != IntPtr.Zero)
            DestroyIcon(_lastIconHandle);
        _lastIconHandle = handle;
    }

    private static (System.Drawing.Icon icon, IntPtr handle) MakeIconWithHandle(int pct, string style)
    {
        var bmp = style switch
        {
            "Bar" => MakeBarBitmap(pct),
            "Dot" => MakeDotBitmap(pct),
            _     => MakePercentageBitmap(pct),
        };
        var h   = bmp.GetHicon();
        var ico = System.Drawing.Icon.FromHandle(h);
        bmp.Dispose();
        return (ico, h);
    }

    private static Bitmap MakePercentageBitmap(int pct)
    {
        var bmp = new Bitmap(32, 32);
        using var g = Graphics.FromImage(bmp);
        g.SmoothingMode     = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
        using var bg = new SolidBrush(Color.FromArgb(220, 35, 35, 35));
        g.FillRectangle(bg, 0, 7, 32, 18);
        var fg = pct > 75 ? Color.OrangeRed : pct > 50 ? Color.Gold : Color.White;
        using var font  = new Font("Segoe UI", 8.5f, System.Drawing.FontStyle.Bold);
        using var brush = new SolidBrush(fg);
        var text = $"{pct}%";
        var sz   = g.MeasureString(text, font);
        g.DrawString(text, font, brush, (32 - sz.Width) / 2f, (32 - sz.Height) / 2f);
        return bmp;
    }

    private static Bitmap MakeBarBitmap(int pct)
    {
        var bmp = new Bitmap(32, 32);
        using var g = Graphics.FromImage(bmp);
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        using var bg = new SolidBrush(Color.FromArgb(220, 35, 35, 35));
        g.FillRectangle(bg, 0, 0, 32, 32);
        using var track = new SolidBrush(Color.FromArgb(80, 80, 80));
        g.FillRectangle(track, 4, 12, 24, 8);
        var fillColor = pct > 75 ? Color.OrangeRed : pct > 50 ? Color.Gold : Color.FromArgb(76, 175, 80);
        using var fill = new SolidBrush(fillColor);
        var w = (int)(24 * Math.Clamp(pct, 0, 100) / 100.0);
        if (w > 0) g.FillRectangle(fill, 4, 12, w, 8);
        return bmp;
    }

    private static Bitmap MakeDotBitmap(int pct)
    {
        var bmp = new Bitmap(32, 32);
        using var g = Graphics.FromImage(bmp);
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        var dotColor = pct > 75 ? Color.OrangeRed : pct > 50 ? Color.Gold : Color.FromArgb(76, 175, 80);
        using var brush = new SolidBrush(dotColor);
        g.FillEllipse(brush, 2, 2, 28, 28);
        return bmp;
    }

    // ── Popup ────────────────────────────────────────────────────────

    private void TogglePopup()
    {
        Dispatcher.Invoke(() =>
        {
            if (_popup.IsVisible)
                _popup.Hide();
            else
            {
                _popup.Show();
                _popup.PositionNearTray();
                _popup.Activate();
            }
        });
    }

    private void SetPopupWidth(int width)
    {
        Dispatcher.Invoke(() =>
        {
            _popup.Width = width;
            if (_popup.IsVisible)
                _popup.PositionNearTray();
        });
    }

    // ── ShowRemaining toggle ─────────────────────────────────────────

    private void OnShowRemainingToggled(object? sender, EventArgs e)
    {
        _config = _config with { ShowRemaining = !_config.ShowRemaining };
        ConfigService.Save(_config);
        _popup.ApplyShowRemaining(_config.ShowRemaining);
        if (_lastPct.HasValue)
            UpdateTray(_lastPct.Value);
    }

    // ── Data refresh ──────────────────────────────────────────────────
    //
    // Auth priority:
    //   1. OAuth Bearer token (Claude Code credentials) — bypasses Cloudflare cookie challenge
    //   2. Manual session key (cookie)
    //   3. Local ~/.claude/stats-cache.json (always available, no network needed)

    public void RefreshData()
    {
        Task.Run(async () =>
        {
            try
            {
                Dispatcher.Invoke(() => _popup.StartRefreshAnimation());

                // ── 1. Try to build API client (OAuth first) ──────
                ClaudeApiClient? client   = null;
                ClaudeCredentials? creds  = null;

                var rawCreds = CredentialsReader.TryRead();
                if (rawCreds is not null)
                {
                    if (CredentialsReader.IsExpired(rawCreds))
                    {
                        Logger.Log("RefreshData: token expired, attempting refresh");
                        rawCreds = await CredentialsReader.TryRefreshAsync(rawCreds);
                    }
                    if (rawCreds is not null)
                    {
                        client = ClaudeApiClient.FromOAuth(rawCreds.AccessToken);
                        creds  = rawCreds;
                        Logger.Log("RefreshData: using OAuth Bearer token");
                    }
                }

                if (client is null && !string.IsNullOrWhiteSpace(_config.SessionKey))
                {
                    client = ClaudeApiClient.FromSessionKey(_config.SessionKey);
                    Logger.Log("RefreshData: using manual session key");
                }

                // ── 2. Try API ────────────────────────────────────
                Models.UsageData? data = null;

                // OAuth path: use api.anthropic.com directly — no Cloudflare, no org ID
                if (client is not null && creds is not null)
                {
                    Logger.Log("RefreshData: trying OAuth direct endpoint (api.anthropic.com)");
                    data = await client.GetOAuthUsageAsync(creds.SubscriptionType);
                }

                // Session key path: use claude.ai with org ID
                if (data is null && client is not null && creds is null)
                {
                    var orgId = _config.OrgId;
                    if (string.IsNullOrEmpty(orgId))
                    {
                        Logger.Log("RefreshData: OrgId empty, auto-detecting");
                        orgId = await client.GetOrgIdAsync();
                        if (orgId is not null)
                        {
                            _config = _config with { OrgId = orgId };
                            ConfigService.Save(_config);
                            Logger.Log($"RefreshData: OrgId detected: {orgId}");
                        }
                    }
                    if (orgId is not null)
                        data = await client.GetUsageAsync(orgId);
                }

                // Overlay today's local stats (messages + tokens) on API data
                if (data is not null)
                {
                    var local = LocalStatsReader.TryRead();
                    if (local is not null)
                        data = data with { TodayMessages = local.TodayMessages, TodayTokens = local.TodayTokens };
                }

                // ── 3a. API success ───────────────────────────────
                if (data is not null)
                {
                    // Overlay plan from credentials if available
                    if (creds is not null && !string.IsNullOrEmpty(creds.SubscriptionType))
                        data = data with { Plan = creds.SubscriptionType };

                    Logger.Log($"RefreshData: API success — 5h={data.FiveHourPct}%, plan={data.Plan}");

                    Dispatcher.Invoke(() =>
                    {
                        _popup.ClearError();
                        _popup.SetOfflineMode(false);
                        _popup.UpdateData(data, DateTime.Now, _config.ShowRemaining);
                        UsageHistory.Append(data);
                        _popup.UpdateSparkline(UsageHistory.Load());
                        _lastPct = data.FiveHourPct;
                        UpdateTray(data.FiveHourPct);
                        _notifier.Check(data, _config, _tray);
                        _popup.StopRefreshAnimation();
                    });
                    return;
                }

                // ── 3b. API failed — fall back to local stats ─────
                Logger.Log("RefreshData: API unavailable, trying local stats-cache.json");
                var localData = LocalStatsReader.TryRead();
                if (localData is not null)
                {
                    Dispatcher.Invoke(() =>
                    {
                        _popup.ClearError();
                        _popup.SetOfflineMode(false);
                        _popup.UpdateData(localData, DateTime.Now, _config.ShowRemaining);
                        _popup.StopRefreshAnimation();
                    });
                    return;
                }

                // ── 3c. Everything failed ─────────────────────────
                Logger.LogError("RefreshData: both API and local stats unavailable");
                Dispatcher.Invoke(() =>
                {
                    _popup.SetError(
                        "Could not load usage — API blocked and no local stats found.");
                    _popup.StopRefreshAnimation();
                });
            }
            catch (Exception ex)
            {
                Logger.LogError("RefreshData: unhandled exception", ex);
                Dispatcher.Invoke(() =>
                {
                    _popup.SetError($"Unexpected error: {ex.Message}");
                    _popup.StopRefreshAnimation();
                });
            }
        });
    }

    private void BuildTimer()
    {
        _timer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(_config.RefreshInterval)
        };
        _timer.Tick += (_, _) => RefreshData();
        _timer.Start();
    }

    // ── Settings ─────────────────────────────────────────────────────

    public void ShowSettings()
    {
        // If already open, bring to front
        if (IsSettingsOpen)
        {
            _settingsWindow?.Activate();
            return;
        }

        IsSettingsOpen  = true;
        _settingsWindow = new SettingsWindow(_config);

        // Restore settings position
        if (!double.IsNaN(_config.SettingsLeft) && !double.IsNaN(_config.SettingsTop))
        {
            _settingsWindow.WindowStartupLocation = System.Windows.WindowStartupLocation.Manual;
            _settingsWindow.Left = _config.SettingsLeft;
            _settingsWindow.Top  = _config.SettingsTop;
            var wa = SystemParameters.WorkArea;
            _settingsWindow.Left = Math.Clamp(_settingsWindow.Left, wa.Left, wa.Right  - _settingsWindow.Width);
            _settingsWindow.Top  = Math.Clamp(_settingsWindow.Top,  wa.Top,  wa.Bottom - _settingsWindow.Height);
        }

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
            _config = _config with
            {
                SettingsLeft   = _settingsWindow!.Left,
                SettingsTop    = _settingsWindow!.Top,
                SettingsWidth  = (int)_settingsWindow!.Width,
                SettingsHeight = (int)_settingsWindow!.Height,
            };
            ConfigService.Save(_config);
            IsSettingsOpen  = false;
            _settingsWindow = null;
        };

        _settingsWindow.Show();
    }

    // ── Profile ──────────────────────────────────────────────────────

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
}
