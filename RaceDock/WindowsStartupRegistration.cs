// SPDX-License-Identifier: GPL-3.0-or-later
using System.Runtime.InteropServices;
using System.Security;
using System.Text;
using Microsoft.Win32;

namespace RaceDock.App;

internal static class WindowsStartupRegistration
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "RaceDock";
    private const int AppModelErrorNoPackage = 15700;

    public static string? LegacySettingsPath
    {
        get
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: false);
                var value = key?.GetValue(ValueName) as string;
                if (string.IsNullOrWhiteSpace(value)) return null;
                var executable = value.Trim().Trim('"');
                var directory = Path.GetDirectoryName(executable);
                return string.IsNullOrWhiteSpace(directory) ? null : Path.Combine(directory, "settings.json");
            }
            catch (Exception failure) when (failure is IOException or UnauthorizedAccessException or SecurityException)
            {
                return null;
            }
        }
    }

    public static bool IsPackaged
    {
        get
        {
            uint length = 0;
            return GetCurrentPackagePath(ref length, null) != AppModelErrorNoPackage;
        }
    }

    public static void Apply(bool enabled)
    {
        if (IsPackaged) return;
        if (!enabled)
        {
            using var existingKey = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true);
            existingKey?.DeleteValue(ValueName, throwOnMissingValue: false);
            return;
        }

        using var key = Registry.CurrentUser.CreateSubKey(RunKeyPath, writable: true)
            ?? throw new InvalidOperationException("Could not open the current-user Windows startup registry key.");
        var processPath = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(processPath))
            throw new InvalidOperationException("Could not determine the RaceDock executable path.");

        key.SetValue(ValueName, $"\"{processPath}\"", RegistryValueKind.String);
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern int GetCurrentPackagePath(ref uint packagePathLength, StringBuilder? packagePath);
}
