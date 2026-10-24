namespace NinoBind.Input;

public sealed class EchoSuppressor
{
    public const uint Token = 0x4E424249;

    private readonly List<Pending> _pending = [];
    private readonly int _windowMilliseconds;

    public EchoSuppressor(int windowMilliseconds = 500)
    {
        _windowMilliseconds = windowMilliseconds;
    }

    public bool IsOwnInjection(bool injected, nuint extraInfo) =>
        injected && unchecked((uint)extraInfo) == Token;

    public void Remember(ushort virtualKey, bool isDown, long nowMilliseconds)
    {
        Prune(nowMilliseconds);
        _pending.Add(new Pending(virtualKey, isDown, nowMilliseconds));
    }

    public bool ConsumeRaw(ushort virtualKey, bool isDown, long nowMilliseconds)
    {
        Prune(nowMilliseconds);
        var index = _pending.FindIndex(item => item.VirtualKey == virtualKey && item.IsDown == isDown);
        if (index < 0)
            return false;
        _pending.RemoveAt(index);
        return true;
    }

    private void Prune(long nowMilliseconds)
    {
        _pending.RemoveAll(item => nowMilliseconds - item.At > _windowMilliseconds);
    }

    private readonly record struct Pending(ushort VirtualKey, bool IsDown, long At);
}
