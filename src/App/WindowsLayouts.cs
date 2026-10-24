using System.Text;
using Antiraskladka.Core;
namespace Antiraskladka;

/// <summary>Читает раскладки, установленные в Windows, и строит по ним таблицы клавиш.</summary>
static class WindowsLayouts
{
    public static IntPtr[] Installed()
    {
        int n = Native.GetKeyboardLayoutList(0, null);
        var arr = new IntPtr[Math.Max(n, 0)];
        if (n > 0) Native.GetKeyboardLayoutList(n, arr);
        return arr;
    }

    public static int LangId(IntPtr hkl) => (int)((long)hkl & 0xFFFF);
    public static bool IsCyrillicLang(int lang) => (lang & 0x3FF) is 0x19 or 0x22 or 0x23 or 0x02 or 0x1A or 0x2F or 0x3F or 0x40 or 0x44 or 0x50 or 0x28;

    public static KeyboardLayout Read(IntPtr hkl, string name, bool latin)
    {
        var normal = new StringBuilder(); var shift = new StringBuilder();
        foreach (var sc in KeyboardLayout.ScanCodes)
        {
            uint vk = Native.MapVirtualKeyEx((uint)sc, 1 /*VSC_TO_VK*/, hkl);
            normal.Append(Char(vk, (uint)sc, false, hkl));
            shift.Append(Char(vk, (uint)sc, true, hkl));
        }
        return new KeyboardLayout("win-" + ((long)hkl).ToString("X"), name, latin, normal.ToString(), shift.ToString());
    }

    private static char Char(uint vk, uint sc, bool shift, IntPtr hkl)
    {
        if (vk == 0) return '\0';
        var state = new byte[256];
        if (shift) state[0x10] = 0x80;
        var sb = new StringBuilder(8);
        // флаг 4: не менять состояние «мёртвых» клавиш (Windows 10 1607+)
        int r = Native.ToUnicodeEx(vk, sc, state, sb, sb.Capacity, 4, hkl);
        if (r < 0) { Native.ToUnicodeEx(vk, sc, state, sb, sb.Capacity, 4, hkl); return sb.Length > 0 ? sb[0] : '\0'; }
        return r >= 1 ? sb[0] : '\0';
    }

    /// <summary>Пара «латинская + кириллическая» из установленных в системе раскладок.</summary>
    public static (KeyboardLayout latin, IntPtr latHkl, KeyboardLayout cyr, IntPtr cyrHkl)? AutoPair()
    {
        var list = Installed();
        var lat = list.FirstOrDefault(h => !IsCyrillicLang(LangId(h)));
        var cyr = list.FirstOrDefault(h => IsCyrillicLang(LangId(h)));
        if (lat == IntPtr.Zero || cyr == IntPtr.Zero) return null;
        return (Read(lat, "Windows (латиница)", true), lat, Read(cyr, "Windows (кириллица)", false), cyr);
    }

    /// <summary>HKL для языка раскладки (en/ru/uk/be), если установлен.</summary>
    public static IntPtr Find(bool latin, string id)
    {
        int want = id.StartsWith("uk") ? 0x22 : id.StartsWith("be") ? 0x23 : latin ? 0x09 : 0x19;
        var list = Installed();
        var h = list.FirstOrDefault(x => (LangId(x) & 0x3FF) == want);
        if (h == IntPtr.Zero) h = list.FirstOrDefault(x => IsCyrillicLang(LangId(x)) != latin);
        return h;
    }
}
