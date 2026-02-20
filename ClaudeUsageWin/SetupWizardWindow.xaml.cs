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
            WizardAutoAuthText.Text = $"\u2713 Claude Code detected \u2014 signed in as {creds.SubscriptionType}";
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
