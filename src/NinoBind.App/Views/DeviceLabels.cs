using NinoBind.Input;

namespace NinoBind.Views;

internal static class DeviceLabels
{
    public static string KeyOf(InputDevice device) =>
        string.IsNullOrEmpty(device.DevicePath) ? device.Handle.ToString() : device.DevicePath;

    public static string Friendly(
        InputDevice device,
        IReadOnlyList<InputDevice> all,
        IReadOnlyDictionary<string, string> nicknames)
    {
        if (nicknames.TryGetValue(KeyOf(device), out var nick) && !string.IsNullOrWhiteSpace(nick))
            return nick;
        if (!string.IsNullOrWhiteSpace(device.Manufacturer) || !string.IsNullOrWhiteSpace(device.Product))
            return device.DisplayName;
        var index = all.Where(item => item.Kind == device.Kind).ToList()
            .FindIndex(item => item.Handle == device.Handle) + 1;
        return device.Kind == InputDeviceKind.Mouse ? $"Mouse {index}" : $"Teclado {index}";
    }
}
