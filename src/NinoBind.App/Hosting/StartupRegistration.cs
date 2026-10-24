using Microsoft.Win32;

namespace NinoBind.Hosting;

public enum StartupState { Off, On, OtherPath, Unknown }

public sealed record StartupStatus(StartupState State, string? RegisteredCommand, string? Error);

public static class StartupRegistration
{
    public const string TrayArgument = "--tray";
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "NinoBind";

    public static string? ExpectedCommand =>
        Environment.ProcessPath is { } path ? $"\"{path}\" {TrayArgument}" : null;

    public static StartupStatus Read()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: false);
            if (key?.GetValue(ValueName) is not string command)
                return new StartupStatus(StartupState.Off, null, null);
            var state = string.Equals(command, ExpectedCommand, StringComparison.OrdinalIgnoreCase)
                ? StartupState.On
                : StartupState.OtherPath;
            return new StartupStatus(state, command, null);
        }
        catch (Exception exception) when (exception is UnauthorizedAccessException or System.Security.SecurityException or System.IO.IOException)
        {
            return new StartupStatus(StartupState.Unknown, null, exception.Message);
        }
    }

    public static string? Set(bool enabled)
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(RunKey, writable: true);
            if (enabled)
            {
                if (ExpectedCommand is not { } command)
                    return "O caminho do executável não é conhecido.";
                key.SetValue(ValueName, command, RegistryValueKind.String);
            }
            else
            {
                key.DeleteValue(ValueName, throwOnMissingValue: false);
            }

            return null;
        }
        catch (Exception exception) when (exception is UnauthorizedAccessException or System.Security.SecurityException or System.IO.IOException)
        {
            return exception.Message;
        }
    }
}
