namespace NinoBind.Input;

public static class RawInputInterpreter
{
    public const ushort KeyBreak = 0x0001;
    public const ushort LeftDown = 0x0001;
    public const ushort LeftUp = 0x0002;
    public const ushort RightDown = 0x0004;
    public const ushort RightUp = 0x0008;
    public const ushort MiddleDown = 0x0010;
    public const ushort MiddleUp = 0x0020;
    public const ushort Button4Down = 0x0040;
    public const ushort Button4Up = 0x0080;
    public const ushort Button5Down = 0x0100;
    public const ushort Button5Up = 0x0200;

    public static bool TryReadKey(ReadOnlySpan<byte> keyboard, out ushort virtualKey, out bool isUp, out uint extraInformation)
    {
        virtualKey = 0;
        isUp = false;
        extraInformation = 0;
        if (keyboard.Length < 16)
            return false;
        virtualKey = BitConverter.ToUInt16(keyboard[6..]);
        isUp = (BitConverter.ToUInt16(keyboard[2..]) & KeyBreak) != 0;
        extraInformation = BitConverter.ToUInt32(keyboard[12..]);
        return true;
    }

    public static IReadOnlyList<(int Button, bool IsUp)> ReadMouseButtons(ReadOnlySpan<byte> mouse)
    {
        if (mouse.Length < 8)
            return [];
        var flags = BitConverter.ToUInt16(mouse[4..]);
        var found = new List<(int, bool)>(2);
        Add(found, flags, LeftDown, LeftUp, 1);
        Add(found, flags, RightDown, RightUp, 2);
        Add(found, flags, MiddleDown, MiddleUp, 3);
        Add(found, flags, Button4Down, Button4Up, 4);
        Add(found, flags, Button5Down, Button5Up, 5);
        return found;
    }

    private static void Add(List<(int Button, bool IsUp)> found, ushort flags, ushort down, ushort up, int button)
    {
        if ((flags & down) != 0)
            found.Add((button, false));
        if ((flags & up) != 0)
            found.Add((button, true));
    }
}
