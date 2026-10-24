using System.Text.RegularExpressions;

namespace NinoBind.Input;

public static partial class HidPathIdentity
{
    public static (ushort VendorId, ushort ProductId)? TryReadVidPid(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return null;
        var match = VidPid().Match(path);
        if (!match.Success)
            return null;
        return (
            Convert.ToUInt16(match.Groups[1].Value, 16),
            Convert.ToUInt16(match.Groups[2].Value, 16));
    }

    public static string DisplayName(
        InputDeviceKind kind,
        string? manufacturer,
        string? product,
        string? path)
    {
        var parts = new[] { manufacturer, product }
            .Where(part => !string.IsNullOrWhiteSpace(part))
            .Select(part => part!.Trim())
            .ToArray();
        if (parts.Length > 0)
            return string.Join(" ", parts);

        var ids = TryReadVidPid(path);
        var fallback = kind == InputDeviceKind.Keyboard ? "Teclado" : "Mouse";
        return ids is { } value
            ? $"{fallback} VID_{value.VendorId:X4} PID_{value.ProductId:X4}"
            : fallback;
    }

    [GeneratedRegex(@"VID_([0-9A-Fa-f]{4}).*PID_([0-9A-Fa-f]{4})", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex VidPid();
}
