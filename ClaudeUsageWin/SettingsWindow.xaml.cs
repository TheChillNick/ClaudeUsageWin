using System.IO;
using System.Windows;
using System.Windows.Media;
using ClaudeUsageWin.Services;

using WpfColor      = System.Windows.Media.Color;
using WpfColors     = System.Windows.Media.Colors;
using WpfBrush      = System.Windows.Media.SolidColorBrush;
using WpfMessageBox = System.Windows.MessageBox;

namespace ClaudeUsageWin;

public partial class SettingsWindow : Window
{
    private static readonly int[]    Intervals  = { 30, 60, 120, 300 };
    private static readonly string[] IconStyles = { "Percentage", "Bar", "Dot" };

    public event EventHandler<AppConfig>? Saved;
    public event EventHandler?            Cancelled;

    public AppConfig ResultConfig { get; private set; } = new();
    private readonly AppConfig _incomingConfig;
    private bool _keyVisible = false;

    public SettingsWindow(AppConfig config)
    {
        InitializeComponent();
        _incomingConfig = config;
        ResultConfig    = config;

        // Restore window size (clamped to a generous range)
        Width  = Math.Clamp(config.SettingsWidth,  MinWidth,  800);
        Height = Math.Clamp(config.SettingsHeight, MinHeight, 900);

        // ── Auth ──────────────────────────────────────────────────
        bool isManual = !string.IsNullOrWhiteSpace(config.SessionKey);
        AutoAuthRadio.IsChecked   = !isManual;
        ManualAuthRadio.IsChecked = isManual;
        ManualKeyPanel.Visibility = isManual ? Visibility.Visible : Visibility.Collapsed;

        SessionKeyBox.Password = config.SessionKey;
        UpdateKeyInfo(config.SessionKey);
        OrgIdBox.Text = config.OrgId;
        IntervalCombo.SelectedIndex = Math.Max(0, Array.IndexOf(Intervals, config.RefreshInterval));

        // ── Appearance ────────────────────────────────────────────
        OpacitySlider.Value          = config.OpacityPct;
        OpacityValueText.Text        = $"{config.OpacityPct}%";
        var popupScale = Math.Clamp(config.PopupScale, 0.75, 1.50);
        ScaleSlider.Value            = Math.Round(popupScale * 100.0 / 5.0) * 5.0; // snap to nearest 5
        ScaleValueText.Text          = $"{(int)ScaleSlider.Value}%";
        AlwaysOnTopCheck.IsChecked   = config.AlwaysOnTop;
        IconStyleCombo.SelectedIndex = Math.Max(0, Array.IndexOf(IconStyles, config.IconStyle));

        // ── Notifications ─────────────────────────────────────────
        NotifyFiveHourCheck.IsChecked = config.NotifyFiveHour;
        NotifyWeeklyCheck.IsChecked   = config.NotifyWeekly;
        if (config.NotifyThresholds.Length >= 3)
        {
            Threshold1Box.Text = config.NotifyThresholds[0].ToString();
            Threshold2Box.Text = config.NotifyThresholds[1].ToString();
            Threshold3Box.Text = config.NotifyThresholds[2].ToString();
        }

        // ── Behavior ──────────────────────────────────────────────
        LaunchAtStartupCheck.IsChecked = config.LaunchAtStartup;
        RefreshStatuslineStatus();
        ShowAutoAuthStatus();

        // ── About version ──────────────────────────────────────────────
        var ver = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
        VersionText.Text = ver is not null ? $"v{ver.Major}.{ver.Minor}.{ver.Build}" : "v1.1.0";
    }

    // ── Auto-auth banner ──────────────────────────────────────────────

    private void ShowAutoAuthStatus()
    {
        var creds = CredentialsReader.TryRead();

        if (creds is not null && !CredentialsReader.IsExpired(creds))
        {
            AutoAuthBorder.Background = new WpfBrush(WpfColor.FromRgb(20, 50, 20));
            AutoAuthTitle.Foreground  = new WpfBrush(WpfColors.White);
            AutoAuthTitle.Text        = "Claude Code detected";
            AutoAuthSub.Text          = $"Signed in  ·  {creds.SubscriptionType} plan  ·  auto-refreshes";
            AutoAuthBadge.Text        = "\u2713";
            AutoAuthBadge.Foreground  = new WpfBrush(WpfColor.FromRgb(76, 175, 80));
        }
        else
        {
            AutoAuthBorder.Background = new WpfBrush(WpfColor.FromRgb(55, 30, 18));
            AutoAuthTitle.Foreground  = new WpfBrush(WpfColors.White);
            AutoAuthTitle.Text        = creds is null
                ? "Claude Code not found"
                : "Claude Code token expired";
            AutoAuthSub.Text          = "Use a manual session key, or re-login to Claude Code.";
            AutoAuthBadge.Text        = "\u26A0";
            AutoAuthBadge.Foreground  = new WpfBrush(WpfColor.FromRgb(255, 193, 7));
        }
    }

    // ── Auth radio ────────────────────────────────────────────────────

    private void AuthRadio_Checked(object sender, RoutedEventArgs e)
    {
        if (ManualKeyPanel is null) return;
        ManualKeyPanel.Visibility = ManualAuthRadio.IsChecked == true
            ? Visibility.Visible : Visibility.Collapsed;
    }

    // ── Session key eye toggle ────────────────────────────────────────

    private void EyeToggleBtn_Click(object sender, RoutedEventArgs e)
    {
        _keyVisible = !_keyVisible;

        if (_keyVisible)
        {
            SessionKeyText.Text      = SessionKeyBox.Password;
            SessionKeyBox.Visibility = Visibility.Collapsed;
            SessionKeyText.Visibility = Visibility.Visible;
            EyeToggleBtn.Content     = "\uD83D\uDEA7"; // 🚧 construction → use a text X or close eye
            EyeToggleBtn.Content     = "\uD83D\uDE48"; // 🙈 see-no-evil
            EyeToggleBtn.ToolTip     = "Hide session key";
        }
        else
        {
            SessionKeyBox.Password   = SessionKeyText.Text;
            SessionKeyText.Visibility = Visibility.Collapsed;
            SessionKeyBox.Visibility = Visibility.Visible;
            EyeToggleBtn.Content     = "\uD83D\uDC41";  // 👁
            EyeToggleBtn.ToolTip     = "Show session key";
        }

        UpdateKeyInfo(CurrentSessionKey);
    }

    private string CurrentSessionKey =>
        _keyVisible ? SessionKeyText.Text.Trim() : SessionKeyBox.Password.Trim();

    private void UpdateKeyInfo(string key)
    {
        if (KeyInfoText is null) return;
        if (string.IsNullOrEmpty(key))
        {
            KeyInfoText.Text = "";
            return;
        }
        var len     = key.Length;
        var preview = key.Length > 12
            ? key[..6] + "..." + key[^4..]
            : key;
        KeyInfoText.Text = $"{len} characters  ·  {preview}";
    }

    private void SessionKeyBox_Changed(object sender, RoutedEventArgs e)
    {
        if (OrgIdBox is not null) OrgIdBox.Text = "";
        UpdateKeyInfo(SessionKeyBox.Password);
    }

    private void SessionKeyText_Changed(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        if (OrgIdBox is not null) OrgIdBox.Text = "";
        UpdateKeyInfo(SessionKeyText.Text);
    }

    // ── Opacity slider ────────────────────────────────────────────────

    private void OpacitySlider_ValueChanged(object sender,
        System.Windows.RoutedPropertyChangedEventArgs<double> e)
    {
        if (OpacityValueText is not null)
            OpacityValueText.Text = $"{(int)OpacitySlider.Value}%";
    }

    // ── UI Scale slider ───────────────────────────────────────────

    private void ScaleSlider_ValueChanged(object sender,
        System.Windows.RoutedPropertyChangedEventArgs<double> e)
    {
        if (ScaleValueText is null) return;
        ScaleValueText.Text = $"{(int)ScaleSlider.Value}%";
        // Live preview: tell App to apply new scale to popup
        var scale = ScaleSlider.Value / 100.0;
        ((App)System.Windows.Application.Current).PreviewPopupScale(scale);
    }

    // ── Auto-detect Org ID ────────────────────────────────────────────

    private async void DetectBtn_Click(object sender, RoutedEventArgs e)
    {
        DetectBtn.Content   = "Detecting…";
        DetectBtn.IsEnabled = false;

        try
        {
            ClaudeApiClient client;

            if (ManualAuthRadio.IsChecked == true && !string.IsNullOrWhiteSpace(CurrentSessionKey))
            {
                client = ClaudeApiClient.FromSessionKey(CurrentSessionKey);
            }
            else
            {
                var creds = CredentialsReader.TryRead();
                if (creds is null || CredentialsReader.IsExpired(creds))
                {
                    WpfMessageBox.Show(
                        "No valid Claude Code credentials found.\nEnter a session key or re-login to Claude Code.",
                        "Claude Usage", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                client = ClaudeApiClient.FromOAuth(creds.AccessToken);
            }

            var orgId = await client.GetOrgIdAsync();
            if (orgId is not null)
            {
                OrgIdBox.Text        = orgId;
                DetectBtn.Content    = "\u2713 Found";
                DetectBtn.Background = new WpfBrush(WpfColor.FromRgb(30, 80, 30));
                await Task.Delay(1800);
                DetectBtn.Background = new WpfBrush(WpfColor.FromRgb(61, 61, 61));
            }
            else
            {
                WpfMessageBox.Show(
                    "Could not detect Organization ID.\nCheck your credentials or network connection.",
                    "Claude Usage", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
        finally
        {
            DetectBtn.Content   = "Auto-detect";
            DetectBtn.IsEnabled = true;
        }
    }

    // ── Save ──────────────────────────────────────────────────────────

    private void SaveBtn_Click(object sender, RoutedEventArgs e)
    {
        var thresholds = new List<int>();
        if (int.TryParse(Threshold1Box.Text, out var t1) && t1 is > 0 and <= 100) thresholds.Add(t1);
        if (int.TryParse(Threshold2Box.Text, out var t2) && t2 is > 0 and <= 100) thresholds.Add(t2);
        if (int.TryParse(Threshold3Box.Text, out var t3) && t3 is > 0 and <= 100) thresholds.Add(t3);
        if (thresholds.Count == 0) thresholds.AddRange([75, 90, 95]);

        var iconStyleItem = IconStyleCombo.SelectedItem as System.Windows.Controls.ComboBoxItem;
        var iconStyle     = iconStyleItem?.Tag?.ToString() ?? "Percentage";

        var sessionKey = ManualAuthRadio.IsChecked == true ? CurrentSessionKey : "";

        ResultConfig = new AppConfig
        {
            SessionKey       = sessionKey,
            OrgId            = OrgIdBox.Text.Trim(),
            RefreshInterval  = Intervals[Math.Max(0, IntervalCombo.SelectedIndex)],
            OpacityPct       = (int)OpacitySlider.Value,
            AlwaysOnTop      = AlwaysOnTopCheck.IsChecked == true,
            IconStyle        = iconStyle,
            NotifyThresholds = thresholds.ToArray(),
            NotifyFiveHour   = NotifyFiveHourCheck.IsChecked == true,
            NotifyWeekly     = NotifyWeeklyCheck.IsChecked == true,
            LaunchAtStartup  = LaunchAtStartupCheck.IsChecked == true,
            // Preserve fields managed elsewhere
            ShowRemaining       = _incomingConfig.ShowRemaining,
            SubscriptionType    = _incomingConfig.SubscriptionType,
            StatuslineInstalled = _incomingConfig.StatuslineInstalled,
            PopupWidth          = _incomingConfig.PopupWidth,
            PopupScale          = ScaleSlider.Value / 100.0,
            // Window positions preserved from incoming config (App.cs saves them on close)
            PopupLeft      = _incomingConfig.PopupLeft,
            PopupTop       = _incomingConfig.PopupTop,
            SettingsLeft   = _incomingConfig.SettingsLeft,
            SettingsTop    = _incomingConfig.SettingsTop,
            SettingsWidth  = _incomingConfig.SettingsWidth,
            SettingsHeight = _incomingConfig.SettingsHeight,
        };

        Saved?.Invoke(this, ResultConfig);
        Close();
    }

    // ── Cancel ────────────────────────────────────────────────────────

    private void CancelBtn_Click(object sender, RoutedEventArgs e)
    {
        Cancelled?.Invoke(this, EventArgs.Empty);
        Close();
    }

    // ── Statusline ────────────────────────────────────────────────────

    private void RefreshStatuslineStatus()
    {
        if (StatuslineStatusText is null) return;
        bool installed = StatuslineService.IsInstalled;
        StatuslineStatusText.Text = installed ? "\u2713  Installed and active" : "Not installed";
        StatuslineStatusText.Foreground = installed
            ? new WpfBrush(WpfColor.FromRgb(76, 175, 80))
            : new WpfBrush(WpfColor.FromRgb(255, 138, 138));
    }

    private void StatuslineInstallBtn_Click(object sender, RoutedEventArgs e)
    {
        StatuslineService.Install();
        RefreshStatuslineStatus();
        StatuslineInstallBtn.Content    = "\u2713 Installed";
        StatuslineInstallBtn.Background = new WpfBrush(WpfColor.FromRgb(30, 80, 30));
    }

    private void StatuslineRemoveBtn_Click(object sender, RoutedEventArgs e)
    {
        StatuslineService.Uninstall();
        RefreshStatuslineStatus();
        StatuslineInstallBtn.Content    = "Install";
        StatuslineInstallBtn.Background = new WpfBrush(WpfColor.FromRgb(76, 175, 80));
    }

    // ── Logs ──────────────────────────────────────────────────────────

    private void ViewLogsBtn_Click(object sender, RoutedEventArgs e)
    {
        var folder = Logger.LogsFolder;
        Directory.CreateDirectory(folder);
        System.Diagnostics.Process.Start(
            new System.Diagnostics.ProcessStartInfo("explorer.exe", folder)
            { UseShellExecute = true });
    }

    // ── Update check ──────────────────────────────────────────────────

    private async void CheckUpdateBtn_Click(object sender, RoutedEventArgs e)
    {
        CheckUpdateBtn.IsEnabled = false;
        CheckUpdateBtn.Content   = "Checking\u2026";
        UpdateStatusText.Text    = "";

        try
        {
            using var http = new System.Net.Http.HttpClient();
            http.DefaultRequestHeaders.Add("User-Agent", "ClaudeUsageWin");
            var json = await http.GetStringAsync(
                "https://api.github.com/repos/stepantech/claude-usage-win/releases/latest");
            var doc    = System.Text.Json.JsonDocument.Parse(json);
            var tag    = doc.RootElement.GetProperty("tag_name").GetString() ?? "";
            var cur    = VersionText.Text.TrimStart('v');
            var latest = tag.TrimStart('v');

            if (latest == cur)
            {
                UpdateStatusText.Text       = "\u2713 You are on the latest version.";
                UpdateStatusText.Foreground = new WpfBrush(WpfColor.FromRgb(76, 175, 80));
            }
            else
            {
                UpdateStatusText.Text       = $"Update available: {tag}";
                UpdateStatusText.Foreground = new WpfBrush(WpfColor.FromRgb(255, 193, 7));
            }
        }
        catch
        {
            UpdateStatusText.Text       = "Could not check for updates.";
            UpdateStatusText.Foreground = new WpfBrush(WpfColor.FromRgb(255, 87, 34));
        }
        finally
        {
            CheckUpdateBtn.IsEnabled = true;
            CheckUpdateBtn.Content   = "Check for updates";
        }
    }

    // ── Drag to move ──────────────────────────────────────────────────

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
}
