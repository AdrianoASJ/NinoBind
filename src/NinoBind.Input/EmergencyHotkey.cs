namespace NinoBind.Input;

public sealed class EmergencyHotkey : IDisposable
{
    private const int Id = 0x4E42;
    private const uint PauseKey = 0x13;
    private nint _window;

    public const string Label = "Ctrl + Alt + Pause";

    public bool IsRegistered { get; private set; }

    public bool Register(nint window)
    {
        Dispose();
        IsRegistered = NativeInput.RegisterHotKey(
            window,
            Id,
            NativeInput.ModControl | NativeInput.ModAlt | NativeInput.ModNoRepeat,
            PauseKey);
        _window = IsRegistered ? window : 0;
        return IsRegistered;
    }

    public bool IsTrigger(int message, nint wParam) =>
        IsRegistered && message == NativeInput.WmHotkey && wParam == Id;

    public void Dispose()
    {
        if (!IsRegistered)
            return;
        _ = NativeInput.UnregisterHotKey(_window, Id);
        IsRegistered = false;
        _window = 0;
    }
}
