namespace NinoBind.Input;

public sealed class EngineHotkey : IDisposable
{
    private const int Id = 0x4E43;
    private const uint ScrollLockKey = 0x91;
    private nint _window;

    public const string Label = "Scroll Lock";

    public bool IsRegistered { get; private set; }

    public bool Register(nint window)
    {
        Dispose();
        IsRegistered = NativeInput.RegisterHotKey(
            window,
            Id,
            NativeInput.ModNoRepeat,
            ScrollLockKey);
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
