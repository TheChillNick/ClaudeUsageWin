using Microsoft.Win32;

namespace ClaudeUsageWin.Services;

public static class StartupService
{
    private const string RegKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
    private const string AppName = "ClaudeUsage";

    public static bool IsEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RegKey);
        return key?.GetValue(AppName) is not null;
    }

    public static void Enable()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RegKey, true)!;
        key.SetValue(AppName, Environment.ProcessPath ?? "");
    }

    public static void Disable()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RegKey, true)!;
        key.DeleteValue(AppName, throwOnMissingValue: false);
    }
}
