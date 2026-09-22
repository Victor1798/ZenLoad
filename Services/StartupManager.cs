using Microsoft.Win32;
using System.Diagnostics;

namespace ZenLoad.Services;

public static class StartupManager
{
    private const string RunKeyPath = "Software\\Microsoft\\Windows\\CurrentVersion\\Run";
    private const string ApplicationName = "ZenLoad";

    public static bool IsEnabled
    {
        get
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: false);
            return key?.GetValue(ApplicationName) is string value
                && !string.IsNullOrWhiteSpace(value);
        }
    }

    public static void SetEnabled(bool enabled)
    {
        using var key = Registry.CurrentUser.CreateSubKey(RunKeyPath);
        if (key is null)
        {
            return;
        }

        if (enabled)
        {
            var executablePath = Environment.ProcessPath
                ?? Process.GetCurrentProcess().MainModule?.FileName;

            if (!string.IsNullOrWhiteSpace(executablePath))
            {
                key.SetValue(ApplicationName, $"\"{executablePath}\" --background");
            }
        }
        else
        {
            key.DeleteValue(ApplicationName, throwOnMissingValue: false);
        }
    }
}
