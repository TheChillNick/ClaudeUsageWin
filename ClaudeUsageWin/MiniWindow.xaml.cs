using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

using WpfColor = System.Windows.Media.Color;

namespace ClaudeUsageWin;

public partial class MiniWindow : Window
{
    /// <summary>Fired when the user double-clicks the widget to request the main popup.</summary>
    public event EventHandler? OpenMainRequested;

    public MiniWindow()
    {
        InitializeComponent();
    }

    // ── Public API ───────────────────────────────────────────────────

    public void UpdateData(int pct, double costUSD, bool showRemaining)
    {
        var displayPct = showRemaining ? 100 - pct : pct;
        PctText.Text = $"{displayPct}%";

        int filled = (int)Math.Round(pct / 10.0);
        BarText.Text = new string('▓', filled) + new string('░', 10 - filled);

        var barColor = pct > 75 ? WpfColor.FromRgb(255,  87, 34)
                     : pct > 50 ? WpfColor.FromRgb(255, 193,  7)
                                : WpfColor.FromRgb( 76, 175, 80);
        BarText.Foreground = new SolidColorBrush(barColor);

        if (costUSD > 0.0001)
        {
            CostText.Text       = $"${costUSD:F3}";
            CostText.Visibility = Visibility.Visible;
        }
        else
        {
            CostText.Visibility = Visibility.Collapsed;
        }
    }

    public void PositionNearTray()
    {
        UpdateLayout();
        var wa = SystemParameters.WorkArea;
        Left = wa.Right  - ActualWidth  - 14;
        Top  = wa.Bottom - ActualHeight - 14;
    }

    // ── Drag / click ─────────────────────────────────────────────────

    private void Border_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount >= 2)
        {
            OpenMainRequested?.Invoke(this, EventArgs.Empty);
            e.Handled = true;
            return;
        }
        DragMove();
    }

    private void Window_Deactivated(object sender, EventArgs e)
    {
        // Mini window stays visible when deactivated (it's always-on-top by design)
    }

    // ── Context menu handlers ─────────────────────────────────────────

    private void ShowMain_Click(object sender, RoutedEventArgs e) =>
        OpenMainRequested?.Invoke(this, EventArgs.Empty);

    private void HideMini_Click(object sender, RoutedEventArgs e) => Hide();

    private void Quit_Click(object sender, RoutedEventArgs e) =>
        System.Windows.Application.Current.Shutdown();
}
