using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using ClaudeUsageWin.Models;
using ClaudeUsageWin.Services;

using WpfApp   = System.Windows.Application;
using WpfColor = System.Windows.Media.Color;

namespace ClaudeUsageWin;

public partial class MainWindow : Window
{
    private DateTime?       _lastUpdated;
    private DispatcherTimer _ticker;
    private bool            _offline;
    private bool            _showRemaining;
    private bool            _pinned;
    private UsageData?      _lastData;

    public event EventHandler? ShowRemainingToggled;

    public MainWindow()
    {
        InitializeComponent();

        _ticker = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _ticker.Tick += (_, _) => UpdateAgoLabel();
        _ticker.Start();
    }

    // ── Public API ───────────────────────────────────────────────────

    public void ApplyScale(double scale)
    {
        PopupScaleTransform.ScaleX = scale;
        PopupScaleTransform.ScaleY = scale;
    }

    public void UpdateData(UsageData data, DateTime lastUpdated, bool showRemaining)
    {
        _lastUpdated   = lastUpdated;
        _lastData      = data;
        _showRemaining = showRemaining;

        // ── Plan badge (Pro=gold, Max=royal red, Free=gray) ────────
        var planLower = data.Plan.ToLower();
        bool isMax    = planLower.Contains("max");
        bool isPro    = planLower.Contains("pro");

        PlanText.Text = isMax  ? "Max"
                      : isPro  ? "Pro"
                      : planLower == "free" ? "Free"
                      : (data.Plan.Length > 0
                            ? char.ToUpper(data.Plan[0]) + data.Plan[1..]
                            : "?");

        PlanBorder.Background = new SolidColorBrush(
            isMax ? WpfColor.FromRgb(139, 0,  40)   // Royal Red for Max
          : isPro ? WpfColor.FromRgb(193, 122, 58)  // Gold for Pro
                  : WpfColor.FromRgb( 60,  60, 60));  // Gray for Free

        // Brief pop-in animation on plan badge
        AnimatePlanBadge();

        // ── Local vs API data source ───────────────────────────────
        LocalDataBanner.Visibility = data.IsLocalOnly ? Visibility.Visible  : Visibility.Collapsed;

        // ── Populate sections ──────────────────────────────────────
        MessagesText.Text = data.TodayMessages.ToString();
        TokensText.Text   = FormatTokens(data.TodayTokens);

        if (data.IsLocalOnly)
        {
            // Use weekly message count in the percentage slots
            FiveHourLabel.Text     = "Today (local)";
            FiveHourPctText.Text   = $"{data.TodayMessages} msgs";
            FiveHourResetText.Text = "Synced from Claude Code sessions";

            WeeklyLabel.Text       = "This Week (local)";
            WeeklyPctText.Text     = $"{data.WeeklyMessages} msgs";
            WeeklyResetText.Text   = FormatTokens(data.WeeklyTokens) + " tokens";

            Dispatcher.InvokeAsync(() =>
            {
                FiveHourBarFill.Width = 0;
                WeeklyBarFill.Width   = 0;
            }, DispatcherPriority.Loaded);
        }
        else
        {
            FiveHourResetText.Text = FormatFiveHourReset(data.FiveHourResetAt);
            WeeklyResetText.Text   = FormatWeeklyReset(data.WeeklyResetAt);
            ApplyShowRemaining(showRemaining, data);
            SetOfflineMode(false);

            Dispatcher.InvokeAsync(() =>
            {
                SetBar(FiveHourBarFill, FiveHourBarGrid, data.FiveHourPct);
                SetBar(WeeklyBarFill,   WeeklyBarGrid,   data.WeeklyPct);
            }, DispatcherPriority.Loaded);
        }
    }

    public void ApplyShowRemaining(bool showRemaining)
    {
        _showRemaining = showRemaining;
        if (_lastData is not null && !_lastData.IsLocalOnly)
            ApplyShowRemaining(showRemaining, _lastData);
    }

    private void ApplyShowRemaining(bool showRemaining, UsageData data)
    {
        if (showRemaining)
        {
            FiveHourPctText.Text  = $"{100 - data.FiveHourPct}%";
            WeeklyPctText.Text    = $"{100 - data.WeeklyPct}%";
            FiveHourLabel.Text    = "5-Hour Remaining";
            WeeklyLabel.Text      = "Weekly Remaining";
            ToggleModeBtn.Content = "Remaining";
        }
        else
        {
            FiveHourPctText.Text  = $"{data.FiveHourPct}%";
            WeeklyPctText.Text    = $"{data.WeeklyPct}%";
            FiveHourLabel.Text    = "5-Hour Window";
            WeeklyLabel.Text      = "Weekly";
            ToggleModeBtn.Content = "Used";
        }
    }

    public void SetOfflineMode(bool offline)
    {
        _offline = offline;
        UpdateAgoLabel();
    }

    public void PositionNearTray()
    {
        UpdateLayout();
        var wa = SystemParameters.WorkArea;
        Left = wa.Right  - Width        - 12;
        Top  = wa.Bottom - ActualHeight - 12;
    }

    // ── Error banner ─────────────────────────────────────────────────

    public void SetError(string message)
    {
        ErrorText.Text              = message;
        ErrorBanner.Visibility      = Visibility.Visible;
        LocalDataBanner.Visibility  = Visibility.Collapsed;
    }

    public void ClearError()
    {
        ErrorBanner.Visibility = Visibility.Collapsed;
    }

    // ── Refresh animation ─────────────────────────────────────────────

    public void StartRefreshAnimation()
    {
        var anim = new DoubleAnimation(0, 360, new Duration(TimeSpan.FromSeconds(1.0)))
        {
            RepeatBehavior = RepeatBehavior.Forever
        };
        RefreshTransform.BeginAnimation(RotateTransform.AngleProperty, anim);
        RefreshBtn.IsEnabled = false;
    }

    public void StopRefreshAnimation()
    {
        RefreshTransform.BeginAnimation(RotateTransform.AngleProperty, null);
        RefreshTransform.Angle = 0;
        RefreshBtn.IsEnabled   = true;
    }

    // ── Plan badge pop-in ─────────────────────────────────────────────

    private void AnimatePlanBadge()
    {
        var sx = new DoubleAnimation(1.25, 1.0, TimeSpan.FromSeconds(0.25))
        {
            EasingFunction = new ElasticEase { Oscillations = 1, Springiness = 6,
                                               EasingMode = EasingMode.EaseOut }
        };
        PlanScale.BeginAnimation(ScaleTransform.ScaleXProperty, sx);
        PlanScale.BeginAnimation(ScaleTransform.ScaleYProperty, sx);
    }

    // ── Sparkline ────────────────────────────────────────────────────

    public void UpdateSparkline(List<HistoryPoint> pts)
    {
        SparklineCanvas.Children.Clear();
        if (pts.Count < 2) return;

        double w = SparklineCanvas.ActualWidth;
        double h = SparklineCanvas.ActualHeight;
        if (w <= 0) w = 308;

        // Background track line
        var track = new System.Windows.Shapes.Rectangle
        {
            Width  = w, Height = 1,
            Fill   = new SolidColorBrush(WpfColor.FromRgb(61, 61, 61))
        };
        System.Windows.Controls.Canvas.SetLeft(track, 0);
        System.Windows.Controls.Canvas.SetTop(track, h / 2);
        SparklineCanvas.Children.Add(track);

        var points = new PointCollection();
        for (int i = 0; i < pts.Count; i++)
        {
            double x = i * (w / (pts.Count - 1));
            double y = h - (pts[i].FiveHourPct / 100.0 * h);
            points.Add(new System.Windows.Point(x, y));
        }

        int lastPct  = pts[^1].FiveHourPct;
        var lineColor = lastPct > 75 ? WpfColor.FromRgb(255, 87,  34)
                       : lastPct > 50 ? WpfColor.FromRgb(255, 193,  7)
                                      : WpfColor.FromRgb( 76, 175, 80);

        var poly = new System.Windows.Shapes.Polyline
        {
            Points          = points,
            Stroke          = new SolidColorBrush(lineColor),
            StrokeThickness = 1.5,
            StrokeLineJoin  = PenLineJoin.Round
        };
        SparklineCanvas.Children.Add(poly);

        var dot = new System.Windows.Shapes.Ellipse
        {
            Width  = 5, Height = 5,
            Fill   = new SolidColorBrush(lineColor)
        };
        var lastPt = points[^1];
        System.Windows.Controls.Canvas.SetLeft(dot, lastPt.X - 2.5);
        System.Windows.Controls.Canvas.SetTop(dot, lastPt.Y - 2.5);
        SparklineCanvas.Children.Add(dot);
    }

    // ── Internals ────────────────────────────────────────────────────

    private void UpdateAgoLabel()
    {
        if (_offline)
        {
            if (_lastUpdated is null)
            {
                UpdatedText.Text       = "Offline";
                UpdatedText.Foreground = new SolidColorBrush(WpfColor.FromRgb(255, 87, 34));
                return;
            }
            var s = (DateTime.Now - _lastUpdated.Value).TotalSeconds;
            var ago = s < 60    ? $"{(int)s} sec"
                    : s < 3600 ? $"{(int)(s / 60)} min"
                               : $"{(int)(s / 3600)}h";
            UpdatedText.Text       = $"Offline — last data from {ago} ago";
            UpdatedText.Foreground = new SolidColorBrush(WpfColor.FromRgb(255, 87, 34));
            return;
        }

        UpdatedText.Foreground = new SolidColorBrush(WpfColor.FromRgb(158, 158, 158));
        if (_lastUpdated is null) { UpdatedText.Text = "Updated --"; return; }
        var sec = (DateTime.Now - _lastUpdated.Value).TotalSeconds;
        UpdatedText.Text = sec < 60    ? $"Updated {(int)sec} sec ago"
                         : sec < 3600 ? $"Updated {(int)(sec / 60)} min ago"
                                      : $"Updated {(int)(sec / 3600)}h ago";
    }

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

    private static string FormatFiveHourReset(DateTime? resetAt)
    {
        if (resetAt is null) return "Resets in --";
        var delta = resetAt.Value - DateTime.Now;
        if (delta.TotalSeconds < 0) return "Reset now";
        int h = (int)delta.TotalHours, m = delta.Minutes;
        return h > 0 ? $"Resets in {h}h {m}m" : $"Resets in {m}m";
    }

    private static string FormatWeeklyReset(DateTime? resetAt) =>
        resetAt is null ? "Resets --" : $"Resets {resetAt.Value.ToLocalTime():ddd h:mm tt}";

    private static string FormatTokens(long n) =>
        n >= 1_000_000 ? $"{n / 1_000_000.0:F1}M"
      : n >= 1_000     ? $"{n / 1_000.0:F1}K"
                       : n.ToString();

    // ── Events ────────────────────────────────────────────────────────

    private void Window_Deactivated(object sender, EventArgs e)
    {
        if (!App.IsSettingsOpen && !_pinned) Hide();
    }

    private void Border_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left)
            DragMove();
    }

    private void PinBtn_Click(object sender, RoutedEventArgs e)
    {
        _pinned = !_pinned;
        Topmost = _pinned || ((App)WpfApp.Current).IsAlwaysOnTop;
        PinBtn.Foreground = new SolidColorBrush(
            _pinned ? WpfColor.FromRgb(255, 193, 7) : WpfColor.FromRgb(158, 158, 158));
        PinBtn.ToolTip = _pinned ? "Unpin window" : "Pin window (stay visible)";
    }

    private void RefreshBtn_Click(object sender, RoutedEventArgs e)     => ((App)WpfApp.Current).RefreshData();
    private void SettingsBtn_Click(object sender, RoutedEventArgs e)    => ((App)WpfApp.Current).ShowSettings();
    private void QuitBtn_Click(object sender, RoutedEventArgs e)        => WpfApp.Current.Shutdown();
    private void ClosePopupBtn_Click(object sender, RoutedEventArgs e)  => Hide();
    private void RetryBtn_Click(object sender, RoutedEventArgs e)       => ((App)WpfApp.Current).RefreshData();

    private void OpenClaudeBtn_Click(object sender, RoutedEventArgs e) =>
        System.Diagnostics.Process.Start(
            new System.Diagnostics.ProcessStartInfo("https://claude.ai") { UseShellExecute = true });

    private void ToggleModeBtn_Click(object sender, RoutedEventArgs e)
    {
        ShowRemainingToggled?.Invoke(this, EventArgs.Empty);
    }
}
