using System.Windows.Forms;
using ClaudeUsageWin.Models;

namespace ClaudeUsageWin.Services;

public class ThresholdNotifier
{
    private readonly HashSet<string> _notified = new();

    public void Check(UsageData data, AppConfig config, NotifyIcon tray)
    {
        if (config.NotifyFiveHour)
            CheckThresholds("5-Hour", data.FiveHourPct, config.NotifyThresholds, tray);

        if (config.NotifyWeekly)
            CheckThresholds("Weekly", data.WeeklyPct, config.NotifyThresholds, tray);
    }

    private void CheckThresholds(string label, int pct, int[] thresholds, NotifyIcon tray)
    {
        foreach (var t in thresholds)
        {
            var key = $"{label}_{t}";
            if (pct >= t && _notified.Add(key))
            {
                var icon = t >= 95 ? ToolTipIcon.Error
                         : t >= 90 ? ToolTipIcon.Warning
                                   : ToolTipIcon.Info;
                tray.ShowBalloonTip(5000, "Claude Usage",
                    $"{label} window at {pct}%!", icon);
            }
            else if (pct < t)
            {
                _notified.Remove(key);
            }
        }
    }
}
