using NinoBind.Input;
using Xunit;

namespace NinoBind.Input.Tests;

public sealed class InputInterpretationTests
{
    [Fact]
    public void Vid_and_pid_come_from_the_device_path()
    {
        var ids = HidPathIdentity.TryReadVidPid(@"\\?\HID#VID_046D&PID_C52B&MI_00#1#{guid}");

        Assert.NotNull(ids);
        Assert.Equal((ushort)0x046D, ids.Value.VendorId);
        Assert.Equal((ushort)0xC52B, ids.Value.ProductId);
    }

    [Fact]
    public void Missing_product_name_uses_vid_pid_instead_of_a_guessed_brand()
    {
        var name = HidPathIdentity.DisplayName(
            InputDeviceKind.Keyboard,
            manufacturer: null,
            product: null,
            path: "HID\\VID_1234&PID_ABCD");

        Assert.Equal("Teclado VID_1234 PID_ABCD", name);
    }

    [Fact]
    public void Key_break_flag_is_a_key_up()
    {
        var bytes = new byte[16];
        BitConverter.TryWriteBytes(bytes.AsSpan(2), RawInputInterpreter.KeyBreak);
        BitConverter.TryWriteBytes(bytes.AsSpan(6), (ushort)0x7C);
        BitConverter.TryWriteBytes(bytes.AsSpan(12), EchoSuppressor.Token);

        var read = RawInputInterpreter.TryReadKey(bytes, out var virtualKey, out var isUp, out var extra);

        Assert.True(read);
        Assert.Equal((ushort)0x7C, virtualKey);
        Assert.True(isUp);
        Assert.Equal(EchoSuppressor.Token, extra);
        Assert.Equal("F13", InputNames.Key(virtualKey));
    }

    [Fact]
    public void One_mouse_packet_can_carry_two_button_edges()
    {
        var bytes = new byte[24];
        var flags = (ushort)(RawInputInterpreter.LeftDown | RawInputInterpreter.RightUp);
        BitConverter.TryWriteBytes(bytes.AsSpan(4), flags);

        var edges = RawInputInterpreter.ReadMouseButtons(bytes);

        Assert.Equal([(1, false), (2, true)], edges);
    }

    [Fact]
    public void Raw_echo_is_consumed_once()
    {
        var echo = new EchoSuppressor();
        echo.Remember(0x7C, isDown: true, nowMilliseconds: 1_000);

        Assert.True(echo.ConsumeRaw(0x7C, isDown: true, nowMilliseconds: 1_100));
        Assert.False(echo.ConsumeRaw(0x7C, isDown: true, nowMilliseconds: 1_100));
    }

    [Fact]
    public void Own_injection_requires_the_token()
    {
        var echo = new EchoSuppressor();

        Assert.True(echo.IsOwnInjection(injected: true, EchoSuppressor.Token));
        Assert.False(echo.IsOwnInjection(injected: true, extraInfo: 1));
        Assert.False(echo.IsOwnInjection(injected: false, EchoSuppressor.Token));
    }

    [Fact]
    public void SendInput_packet_matches_the_64_bit_layout()
    {
        Assert.True(Environment.Is64BitProcess);
        Assert.Equal(40, NativeInputLayout.InputPacketSize);
    }
}
