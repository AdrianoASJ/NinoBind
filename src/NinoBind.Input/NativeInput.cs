using System.Runtime.InteropServices;
using System.Text;

namespace NinoBind.Input;

public static class NativeInputLayout
{
    public static int InputPacketSize => NativeInput.InputPacket.Size;
}

internal static class NativeInput
{
    public const int WmInput = 0x00FF;
    public const int WmInputDeviceChange = 0x00FE;
    public const uint RidInput = 0x10000003;
    public const uint RidiDeviceName = 0x20000007;
    public const uint RidiDeviceInfo = 0x2000000B;
    public const uint RimTypeMouse = 0;
    public const uint RimTypeKeyboard = 1;
    public const uint RidevInputSink = 0x00000100;
    public const uint RidevDevNotify = 0x00002000;
    public const uint RidevRemove = 0x00000001;
    public const int WhKeyboardLl = 13;
    public const int WhMouseLl = 14;
    public const uint LlkfInjected = 0x00000010;
    public const uint LlmhfInjected = 0x00000001;
    public const uint KeyeventfKeyUp = 0x0002;
    public const uint KeyeventfUnicode = 0x0004;
    public const uint InputKeyboard = 1;
    public const uint InputMouse = 0;
    public const uint MouseLeftDown = 0x0002;
    public const uint MouseLeftUp = 0x0004;
    public const uint MouseRightDown = 0x0008;
    public const uint MouseRightUp = 0x0010;
    public const uint MouseMiddleDown = 0x0020;
    public const uint MouseMiddleUp = 0x0040;
    public const uint MouseXDown = 0x0080;
    public const uint MouseXUp = 0x0100;
    public const ushort UsagePageGeneric = 0x01;
    public const ushort UsageKeyboard = 0x06;
    public const ushort UsageMouse = 0x02;

    [StructLayout(LayoutKind.Sequential)]
    internal struct RawInputDeviceList
    {
        public nint Device;
        public uint Type;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct RawInputDevice
    {
        public ushort UsagePage;
        public ushort Usage;
        public uint Flags;
        public nint Target;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct RawInputHeader
    {
        public uint Type;
        public uint Size;
        public nint Device;
        public nuint WParam;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct DeviceInfoMouse
    {
        public uint Id;
        public uint ButtonCount;
        public uint SampleRate;
        public int HasHorizontalWheel;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct DeviceInfoKeyboard
    {
        public uint Type;
        public uint SubType;
        public uint KeyboardMode;
        public uint FunctionKeys;
        public uint Indicators;
        public uint KeysTotal;
    }

    [StructLayout(LayoutKind.Explicit, Size = 32)]
    internal struct DeviceInfo
    {
        [FieldOffset(0)] public uint Size;
        [FieldOffset(4)] public uint Type;
        [FieldOffset(8)] public DeviceInfoMouse Mouse;
        [FieldOffset(8)] public DeviceInfoKeyboard Keyboard;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct LowLevelKeyboard
    {
        public uint VirtualKey;
        public uint ScanCode;
        public uint Flags;
        public uint Time;
        public nuint ExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct LowLevelMouse
    {
        public int X;
        public int Y;
        public uint MouseData;
        public uint Flags;
        public uint Time;
        public nuint ExtraInfo;
    }

    [StructLayout(LayoutKind.Explicit, Size = 40)]
    internal struct InputPacket
    {
        [FieldOffset(0)] public uint Type;
        [FieldOffset(8)] public ushort VirtualKey;
        [FieldOffset(10)] public ushort ScanCode;
        [FieldOffset(12)] public uint Flags;
        [FieldOffset(16)] public uint Time;
        [FieldOffset(24)] public nint ExtraInfo;
        [FieldOffset(8)] public int MouseX;
        [FieldOffset(12)] public int MouseY;
        [FieldOffset(16)] public uint MouseData;
        [FieldOffset(20)] public uint MouseFlags;
        [FieldOffset(32)] public nint MouseExtra;

        public static int Size => Marshal.SizeOf<InputPacket>();
    }

    internal delegate nint HookProcedure(int code, nint wParam, nint lParam);

    public const int WmHotkey = 0x0312;
    public const uint ModAlt = 0x0001;
    public const uint ModControl = 0x0002;
    public const uint ModNoRepeat = 0x4000;

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool RegisterHotKey(nint window, int id, uint modifiers, uint virtualKey);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool UnregisterHotKey(nint window, int id);

    [DllImport("user32.dll", SetLastError = true)]
    internal static extern uint GetRawInputDeviceList(
        [Out] RawInputDeviceList[]? devices,
        ref uint deviceCount,
        uint itemSize);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    internal static extern uint GetRawInputDeviceInfo(
        nint device,
        uint command,
        char[]? data,
        ref uint size);

    [DllImport("user32.dll", SetLastError = true)]
    internal static extern uint GetRawInputDeviceInfo(
        nint device,
        uint command,
        ref DeviceInfo data,
        ref uint size);

    [DllImport("user32.dll", SetLastError = true)]
    internal static extern bool RegisterRawInputDevices(
        RawInputDevice[] devices,
        uint deviceCount,
        uint size);

    [DllImport("user32.dll", SetLastError = true)]
    internal static extern uint GetRawInputData(
        nint rawInput,
        uint command,
        byte[]? data,
        ref uint size,
        uint headerSize);

    [DllImport("user32.dll", SetLastError = true)]
    internal static extern nint SetWindowsHookEx(int idHook, HookProcedure procedure, nint module, uint threadId);

    [DllImport("user32.dll")]
    internal static extern bool UnhookWindowsHookEx(nint hook);

    [DllImport("user32.dll")]
    internal static extern nint CallNextHookEx(nint hook, int code, nint wParam, nint lParam);

    [DllImport("user32.dll", SetLastError = true)]
    internal static extern uint SendInput(uint count, InputPacket[] inputs, int size);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    internal static extern nint CreateFile(
        string fileName,
        uint access,
        uint share,
        nint security,
        uint creation,
        uint flags,
        nint template);

    [DllImport("kernel32.dll", SetLastError = true)]
    internal static extern bool CloseHandle(nint handle);

    [DllImport("hid.dll", CharSet = CharSet.Unicode)]
    internal static extern bool HidD_GetManufacturerString(nint device, byte[] buffer, uint length);

    [DllImport("hid.dll", CharSet = CharSet.Unicode)]
    internal static extern bool HidD_GetProductString(nint device, byte[] buffer, uint length);

    internal static string? ReadDevicePath(nint device)
    {
        uint characters = 0;
        _ = GetRawInputDeviceInfo(device, RidiDeviceName, null, ref characters);
        if (characters < 2)
            return null;
        var buffer = new char[characters];
        var written = GetRawInputDeviceInfo(device, RidiDeviceName, buffer, ref characters);
        if (written == 0 || written == uint.MaxValue)
            return null;
        return new string(buffer).TrimEnd('\0');
    }

    internal static DeviceInfo? ReadDeviceInfo(nint device)
    {
        var info = new DeviceInfo { Size = (uint)Marshal.SizeOf<DeviceInfo>() };
        var size = info.Size;
        var read = GetRawInputDeviceInfo(device, RidiDeviceInfo, ref info, ref size);
        return read == uint.MaxValue ? null : info;
    }

    internal static (string? Manufacturer, string? Product) ReadHidNames(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return (null, null);
        var handle = CreateFile(path, 0x80000000, 3, 0, 3, 0, 0);
        if (handle == 0 || handle == -1)
            return (null, null);
        try
        {
            return (ReadHidString(handle, manufacturer: true), ReadHidString(handle, manufacturer: false));
        }
        finally
        {
            _ = CloseHandle(handle);
        }
    }

    private static string? ReadHidString(nint handle, bool manufacturer)
    {
        var buffer = new byte[512];
        var ok = manufacturer
            ? HidD_GetManufacturerString(handle, buffer, (uint)buffer.Length)
            : HidD_GetProductString(handle, buffer, (uint)buffer.Length);
        if (!ok)
            return null;
        var text = Encoding.Unicode.GetString(buffer).TrimEnd('\0').Trim();
        return string.IsNullOrWhiteSpace(text) ? null : text;
    }
}
