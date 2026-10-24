namespace NinoBind.Input;

public enum InputDeviceKind
{
    Keyboard,
    Mouse,
}

public enum InputEventKind
{
    KeyDown,
    KeyUp,
    MouseDown,
    MouseUp,
}

public enum InputOrigin
{
    PhysicalDevice,
    IgnoredEcho,
}

public sealed record InputDevice(
    nint Handle,
    InputDeviceKind Kind,
    string? Manufacturer,
    string? Product,
    string? DevicePath,
    int? ButtonCount,
    string DisplayName);

public sealed record ObservedInput(
    InputEventKind Kind,
    string Description,
    nint DeviceHandle,
    InputOrigin Origin);

public readonly record struct PhysicalEdge(bool IsKeyboard, int Code, bool IsDown);
