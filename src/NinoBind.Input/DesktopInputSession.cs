using System.Runtime.InteropServices;

namespace NinoBind.Input;

public sealed class DesktopInputSession : IDisposable
{
    private readonly EchoSuppressor _echo = new();
    private readonly object _gate = new();
    private NativeInput.HookProcedure? _keyboardProcedure;
    private NativeInput.HookProcedure? _mouseProcedure;
    private nint _keyboardHook;
    private nint _mouseHook;
    private nint _window;
    private bool _disposed;

    private bool _captureArmed;

    public IReadOnlyList<InputDevice> Devices { get; private set; } = [];

    public bool IsObserving { get; private set; }

    public bool IsEngineEnabled { get; private set; }

    public string? EnumerationError { get; private set; }

    public string? ObservationError { get; private set; }

    public event Action<ObservedInput>? InputObserved;

    public event Action? DevicesChanged;

    public event Action<PhysicalEdge>? Captured;

    public Func<PhysicalEdge, bool>? Decide { get; set; }

    public void RefreshDevices()
    {
        try
        {
            Devices = Enumerate();
            EnumerationError = null;
        }
        catch (Exception exception) when (exception is IOException or InvalidOperationException or EntryPointNotFoundException)
        {
            Devices = [];
            EnumerationError = exception.Message;
        }

        DevicesChanged?.Invoke();
    }

    public bool SetObserving(nint window, bool enabled)
    {
        var previous = IsObserving;
        IsObserving = enabled;
        if (!EnsureHooks(window))
        {
            IsObserving = previous;
            return false;
        }

        if (!enabled)
            ObservationError = null;
        return true;
    }

    public bool SetEngine(nint window, bool enabled)
    {
        var previous = IsEngineEnabled;
        IsEngineEnabled = enabled;
        if (EnsureHooks(window))
            return true;
        IsEngineEnabled = previous;
        return false;
    }

    public bool BeginCapture(nint window)
    {
        _captureArmed = true;
        return EnsureHooks(window);
    }

    public void EndCapture()
    {
        _captureArmed = false;
        if (_window != 0)
            _ = EnsureHooks(_window);
    }

    public bool TryStart(nint window) => SetObserving(window, true);

    public void Stop() => _ = SetObserving(_window == 0 ? 0 : _window, false);

    private bool EnsureHooks(nint window)
    {
        var needed = IsObserving || IsEngineEnabled || _captureArmed;
        if (!needed)
        {
            ReleaseHooks();
            return true;
        }

        if (window == 0)
        {
            ObservationError = "A janela ainda não tem identificador.";
            return false;
        }

        if (_keyboardHook != 0 && _window == window)
            return true;
        return InstallHooks(window);
    }

    private bool InstallHooks(nint window)
    {
        ReleaseHooks();
        _window = window;
        if (!Register(window, remove: false))
        {
            ObservationError = "O Windows recusou o registro Raw Input. " + Win32Error();
            return false;
        }

        _keyboardProcedure = KeyboardHook;
        _mouseProcedure = MouseHook;
        _keyboardHook = NativeInput.SetWindowsHookEx(NativeInput.WhKeyboardLl, _keyboardProcedure, 0, 0);
        _mouseHook = NativeInput.SetWindowsHookEx(NativeInput.WhMouseLl, _mouseProcedure, 0, 0);
        if (_keyboardHook == 0 || _mouseHook == 0)
        {
            ObservationError = "O hook de entrada não foi instalado. " + Win32Error();
            ReleaseHooks();
            return false;
        }

        ObservationError = null;
        return true;
    }

    private void ReleaseHooks()
    {
        if (_window != 0)
            _ = Register(_window, remove: true);
        if (_keyboardHook != 0)
            _ = NativeInput.UnhookWindowsHookEx(_keyboardHook);
        if (_mouseHook != 0)
            _ = NativeInput.UnhookWindowsHookEx(_mouseHook);
        _keyboardHook = 0;
        _mouseHook = 0;
        _keyboardProcedure = null;
        _mouseProcedure = null;
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        IsObserving = false;
        IsEngineEnabled = false;
        _captureArmed = false;
        ReleaseHooks();
        _disposed = true;
    }

    public void ProcessWindowMessage(int message, nint lParam)
    {
        if (message == NativeInput.WmInputDeviceChange)
        {
            RefreshDevices();
            return;
        }

        if (message != NativeInput.WmInput || !IsObserving)
            return;

        var headerSize = (uint)Marshal.SizeOf<NativeInput.RawInputHeader>();
        uint size = 0;
        _ = NativeInput.GetRawInputData(lParam, NativeInput.RidInput, null, ref size, headerSize);
        if (size < headerSize)
            return;
        var buffer = new byte[size];
        var read = NativeInput.GetRawInputData(lParam, NativeInput.RidInput, buffer, ref size, headerSize);
        if (read == uint.MaxValue || read < headerSize)
            return;

        var header = MemoryMarshal.Read<NativeInput.RawInputHeader>(buffer);
        var body = buffer.AsSpan((int)headerSize);
        if (header.Type == NativeInput.RimTypeKeyboard)
            PublishKey(header.Device, body);
        else if (header.Type == NativeInput.RimTypeMouse)
            PublishMouse(header.Device, body);
    }

    public int SendTestKey() => SendKeyTap(0x7C);

    public int SendKeyTap(ushort virtualKey)
    {
        var down = SendKey(virtualKey, down: true);
        var up = SendKey(virtualKey, down: false);
        return down + up;
    }

    public int SendKey(ushort virtualKey, bool down)
    {
        var packet = new NativeInput.InputPacket
        {
            Type = NativeInput.InputKeyboard,
            VirtualKey = virtualKey,
            Flags = down ? 0 : NativeInput.KeyeventfKeyUp,
            ExtraInfo = unchecked((nint)EchoSuppressor.Token),
        };
        return (int)NativeInput.SendInput(1, [packet], NativeInput.InputPacket.Size);
    }

    public int SendMouseButton(int button, bool down)
    {
        var packet = new NativeInput.InputPacket
        {
            Type = NativeInput.InputMouse,
            MouseFlags = MouseFlag(button, down),
            MouseData = button is 4 or 5 ? (uint)(button - 3) : 0,
            MouseExtra = unchecked((nint)EchoSuppressor.Token),
        };
        return (int)NativeInput.SendInput(1, [packet], NativeInput.InputPacket.Size);
    }

    public int SendUnicode(string text)
    {
        var sent = 0;
        foreach (var character in text)
        {
            sent += SendUnicodeUnit(character, keyUp: false);
            sent += SendUnicodeUnit(character, keyUp: true);
        }

        return sent;
    }

    private static int SendUnicodeUnit(char character, bool keyUp)
    {
        var packet = new NativeInput.InputPacket
        {
            Type = NativeInput.InputKeyboard,
            ScanCode = character,
            Flags = NativeInput.KeyeventfUnicode | (keyUp ? NativeInput.KeyeventfKeyUp : 0),
            ExtraInfo = unchecked((nint)EchoSuppressor.Token),
        };
        return (int)NativeInput.SendInput(1, [packet], NativeInput.InputPacket.Size);
    }

    private static uint MouseFlag(int button, bool down) => (button, down) switch
    {
        (1, true) => NativeInput.MouseLeftDown,
        (1, false) => NativeInput.MouseLeftUp,
        (2, true) => NativeInput.MouseRightDown,
        (2, false) => NativeInput.MouseRightUp,
        (3, true) => NativeInput.MouseMiddleDown,
        (3, false) => NativeInput.MouseMiddleUp,
        (4 or 5, true) => NativeInput.MouseXDown,
        _ => NativeInput.MouseXUp,
    };

    private void PublishKey(nint device, ReadOnlySpan<byte> body)
    {
        if (!RawInputInterpreter.TryReadKey(body, out var virtualKey, out var isUp, out var extra))
            return;
        var own = extra == EchoSuppressor.Token || _echo.ConsumeRaw(virtualKey, !isUp, Environment.TickCount64);
        Publish(new ObservedInput(
            isUp ? InputEventKind.KeyUp : InputEventKind.KeyDown,
            InputNames.Key(virtualKey),
            own ? 0 : device,
            own ? InputOrigin.IgnoredEcho : InputOrigin.PhysicalDevice));
    }

    private void PublishMouse(nint device, ReadOnlySpan<byte> body)
    {
        foreach (var (button, isUp) in RawInputInterpreter.ReadMouseButtons(body))
        {
            Publish(new ObservedInput(
                isUp ? InputEventKind.MouseUp : InputEventKind.MouseDown,
                InputNames.MouseButton(button),
                device,
                InputOrigin.PhysicalDevice));
        }
    }

    private nint KeyboardHook(int code, nint wParam, nint lParam)
    {
        if (code >= 0)
        {
            try
            {
                var info = Marshal.PtrToStructure<NativeInput.LowLevelKeyboard>(lParam);
                var injected = (info.Flags & NativeInput.LlkfInjected) != 0;
                var isDown = wParam == 0x0100 || wParam == 0x0104;
                if (_echo.IsOwnInjection(injected, info.ExtraInfo))
                {
                    _echo.Remember((ushort)info.VirtualKey, isDown, Environment.TickCount64);
                }
                else if (!injected && HandleEdge(new PhysicalEdge(true, (int)info.VirtualKey, isDown)))
                {
                    return 1;
                }
            }
            catch (Exception)
            {
            }
        }

        return NativeInput.CallNextHookEx(_keyboardHook, code, wParam, lParam);
    }

    private nint MouseHook(int code, nint wParam, nint lParam)
    {
        if (code >= 0)
        {
            try
            {
                var info = Marshal.PtrToStructure<NativeInput.LowLevelMouse>(lParam);
                var button = MouseButtonFromMessage(wParam, info.MouseData);
                var injected = (info.Flags & NativeInput.LlmhfInjected) != 0;
                var isDown = wParam is 0x0201 or 0x0204 or 0x0207 or 0x020B;
                if (button > 0 && _echo.IsOwnInjection(injected, info.ExtraInfo))
                {
                    _echo.Remember((ushort)button, isDown, Environment.TickCount64);
                }
                else if (button > 0 && !injected && HandleEdge(new PhysicalEdge(false, button, isDown)))
                {
                    return 1;
                }
            }
            catch (Exception)
            {
            }
        }

        return NativeInput.CallNextHookEx(_mouseHook, code, wParam, lParam);
    }

    private bool HandleEdge(PhysicalEdge edge)
    {
        if (_captureArmed && edge.IsDown)
        {
            _captureArmed = false;
            Captured?.Invoke(edge);
            return true;
        }

        if (!IsEngineEnabled || Decide is null)
            return false;
        try
        {
            return Decide(edge);
        }
        catch (Exception)
        {
            return false;
        }
    }

    private static int MouseButtonFromMessage(nint message, uint mouseData) => message switch
    {
        0x0201 or 0x0202 => 1,
        0x0204 or 0x0205 => 2,
        0x0207 or 0x0208 => 3,
        0x020B or 0x020C => (mouseData >> 16) == 2 ? 5 : 4,
        _ => 0,
    };

    private void Publish(ObservedInput input)
    {
        lock (_gate)
            InputObserved?.Invoke(input);
    }

    private static bool Register(nint window, bool remove)
    {
        var flags = remove ? NativeInput.RidevRemove : NativeInput.RidevInputSink | NativeInput.RidevDevNotify;
        var target = remove ? 0 : window;
        NativeInput.RawInputDevice[] devices =
        [
            new()
            {
                UsagePage = NativeInput.UsagePageGeneric,
                Usage = NativeInput.UsageKeyboard,
                Flags = flags,
                Target = target,
            },
            new()
            {
                UsagePage = NativeInput.UsagePageGeneric,
                Usage = NativeInput.UsageMouse,
                Flags = flags,
                Target = target,
            },
        ];
        return NativeInput.RegisterRawInputDevices(
            devices,
            (uint)devices.Length,
            (uint)Marshal.SizeOf<NativeInput.RawInputDevice>());
    }

    private static List<InputDevice> Enumerate()
    {
        uint count = 0;
        var itemSize = (uint)Marshal.SizeOf<NativeInput.RawInputDeviceList>();
        _ = NativeInput.GetRawInputDeviceList(null, ref count, itemSize);
        if (count == 0)
            return [];
        var list = new NativeInput.RawInputDeviceList[count];
        var copied = NativeInput.GetRawInputDeviceList(list, ref count, itemSize);
        if (copied == uint.MaxValue)
            throw new InvalidOperationException("GetRawInputDeviceList falhou. " + Win32Error());

        var devices = new List<InputDevice>();
        foreach (var item in list.Take((int)copied))
        {
            if (item.Type != NativeInput.RimTypeKeyboard && item.Type != NativeInput.RimTypeMouse)
                continue;
            var kind = item.Type == NativeInput.RimTypeKeyboard ? InputDeviceKind.Keyboard : InputDeviceKind.Mouse;
            var path = NativeInput.ReadDevicePath(item.Device);
            var names = NativeInput.ReadHidNames(path);
            int? buttons = null;
            var info = NativeInput.ReadDeviceInfo(item.Device);
            if (info is { Type: NativeInput.RimTypeMouse } mouse && mouse.Mouse.ButtonCount > 0)
                buttons = (int)mouse.Mouse.ButtonCount;
            devices.Add(new InputDevice(
                item.Device,
                kind,
                names.Manufacturer,
                names.Product,
                path,
                buttons,
                HidPathIdentity.DisplayName(kind, names.Manufacturer, names.Product, path)));
        }

        return devices;
    }

    private static string Win32Error() => new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error()).Message;
}
