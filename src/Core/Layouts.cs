namespace Antiraskladka.Core;

/// <summary>Раскладка: символы 47 клавиш (в порядке скан-кодов 29,02..0D,10..1B,2B,1E..28,2C..35) без Shift и с Shift.</summary>
public sealed record KeyboardLayout(string Id, string Name, bool Latin, string Normal, string Shift)
{
    public const int KeyCount = 47;
    public static readonly int[] ScanCodes =
        new[] { 0x29 }.Concat(Enumerable.Range(0x02, 12)).Concat(Enumerable.Range(0x10, 12)).Append(0x2B)
        .Concat(Enumerable.Range(0x1E, 11)).Concat(Enumerable.Range(0x2C, 10)).ToArray();
}

public static class Layouts
{
    public static readonly KeyboardLayout UsQwerty = new("en-us", "Английская (США, QWERTY)", true,
        "`1234567890-=qwertyuiop[]\\asdfghjkl;'zxcvbnm,./",
        "~!@#$%^&*()_+QWERTYUIOP{}|ASDFGHJKL:\"ZXCVBNM<>?");
    public static readonly KeyboardLayout Uk = new("en-gb", "Английская (Великобритания)", true,
        "`1234567890-=qwertyuiop[]#asdfghjkl;'zxcvbnm,./",
        "¬!\"£$%^&*()_+QWERTYUIOP{}~ASDFGHJKL:@ZXCVBNM<>?");
    public static readonly KeyboardLayout Dvorak = new("en-dvorak", "Английская (Дворак)", true,
        "`1234567890[]',.pyfgcrl/=\\aoeuidhtns-;qjkxbmwvz",
        "~!@#$%^&*(){}\"<>PYFGCRL?+|AOEUIDHTNS_:QJKXBMWVZ");
    public static readonly KeyboardLayout Colemak = new("en-colemak", "Английская (Colemak)", true,
        "`1234567890-=qwfpgjluy;[]\\arstdhneio'zxcvbkm,./",
        "~!@#$%^&*()_+QWFPGJLUY:{}|ARSTDHNEIO\"ZXCVBKM<>?");
    public static readonly KeyboardLayout Ru = new("ru", "Русская (ЙЦУКЕН)", false,
        "ё1234567890-=йцукенгшщзхъ\\фывапролджэячсмитьбю.",
        "Ё!\"№;%:?*()_+ЙЦУКЕНГШЩЗХЪ/ФЫВАПРОЛДЖЭЯЧСМИТЬБЮ,");
    public static readonly KeyboardLayout RuTypewriter = new("ru-typewriter", "Русская (машинопись)", false,
        "|№-/\":,._?%!;йцукенгшщзхъ)фывапролджэячсмитьбюё",
        "+1234567890=\\ЙЦУКЕНГШЩЗХЪ(ФЫВАПРОЛДЖЭЯЧСМИТЬБЮЁ");
    public static readonly KeyboardLayout RuPhonetic = new("ru-phonetic", "Русская фонетическая (ЯВЕРТЫ)", false,
        "ё1234567890-ъяшертыуиопющэасдфгчйкльжзхцвбнм,./",
        "Ё!@#$%^&*()_ЪЯШЕРТЫУИОПЮЩЭАСДФГЧЙКЛЬЖЗХЦВБНМ<>?");
    public static readonly KeyboardLayout Ukrainian = new("uk", "Украинская", false,
        "'1234567890-=йцукенгшщзхї\\фівапролджєячсмитьбю.",
        "₴!\"№;%:?*()_+ЙЦУКЕНГШЩЗХЇ/ФІВАПРОЛДЖЄЯЧСМИТЬБЮ,");
    public static readonly KeyboardLayout Belarusian = new("be", "Белорусская", false,
        "ё1234567890-=йцукенгшўзх'\\фывапролджэячсмітьбю.",
        "Ё!\"№;%:?*()_+ЙЦУКЕНГШЎЗХ'/ФЫВАПРОЛДЖЭЯЧСМІТЬБЮ,");

    public static readonly KeyboardLayout[] LatinLayouts = { UsQwerty, Uk, Dvorak, Colemak };
    public static readonly KeyboardLayout[] CyrillicLayouts = { Ru, RuTypewriter, RuPhonetic, Ukrainian, Belarusian };
    public static IEnumerable<KeyboardLayout> All => LatinLayouts.Concat(CyrillicLayouts);
    public static KeyboardLayout? ById(string? id) => All.FirstOrDefault(l => l.Id == id);
}
