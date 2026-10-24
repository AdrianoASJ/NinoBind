namespace NinoBind.Input;

public static class InputNames
{
    public static string Key(ushort virtualKey) => virtualKey switch
    {
        >= 0x41 and <= 0x5A => ((char)virtualKey).ToString(),
        >= 0x30 and <= 0x39 => ((char)virtualKey).ToString(),
        >= 0x70 and <= 0x87 => "F" + (virtualKey - 0x6F),
        0x08 => "Backspace",
        0x09 => "Tab",
        0x0D => "Enter",
        0x10 => "Shift",
        0x11 => "Ctrl",
        0x12 => "Alt",
        0x13 => "Pause",
        0x14 => "Caps Lock",
        0x91 => "Scroll Lock",
        0x1B => "Esc",
        0x20 => "Espaço",
        0x21 => "Page Up",
        0x22 => "Page Down",
        0x23 => "End",
        0x24 => "Home",
        0x25 => "Seta esquerda",
        0x26 => "Seta cima",
        0x27 => "Seta direita",
        0x28 => "Seta baixo",
        0x2C => "Print Screen",
        0x2D => "Insert",
        0x2E => "Delete",
        0x5B => "Win",
        0x5C => "Win",
        0x60 => "Num 0",
        0x61 => "Num 1",
        0x62 => "Num 2",
        0x63 => "Num 3",
        0x64 => "Num 4",
        0x65 => "Num 5",
        0x66 => "Num 6",
        0x67 => "Num 7",
        0x68 => "Num 8",
        0x69 => "Num 9",
        0xA0 => "Shift esquerdo",
        0xA1 => "Shift direito",
        0xA2 => "Ctrl esquerdo",
        0xA3 => "Ctrl direito",
        0xA4 => "Alt esquerdo",
        0xA5 => "Alt direito",
        _ => $"VK 0x{virtualKey:X2}",
    };

    public static string MouseButton(int button) => button switch
    {
        1 => "Botão esquerdo",
        2 => "Botão direito",
        3 => "Botão do meio",
        4 => "Botão 4",
        5 => "Botão 5",
        _ => $"Botão {button}",
    };
}
