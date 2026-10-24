using System.Text;
namespace Antiraskladka.Core;

public enum Direction { LatinToCyrillic, CyrillicToLatin }

/// <summary>Перевод текста между двумя раскладками по положению клавиш.</summary>
public sealed class LayoutConverter
{
    public KeyboardLayout Latin { get; }
    public KeyboardLayout Cyrillic { get; }
    private readonly Dictionary<char, char> _l2c, _c2l;

    public LayoutConverter(KeyboardLayout latin, KeyboardLayout cyrillic)
    {
        Latin = latin; Cyrillic = cyrillic;
        _l2c = Build(latin, cyrillic); _c2l = Build(cyrillic, latin);
    }

    private static Dictionary<char, char> Build(KeyboardLayout from, KeyboardLayout to)
    {
        var d = new Dictionary<char, char>();
        void Add(string f, string t)
        {
            for (int i = 0; i < Math.Min(f.Length, t.Length); i++)
                if (f[i] != '\0' && t[i] != '\0') d.TryAdd(f[i], t[i]);
        }
        Add(from.Normal, to.Normal); Add(from.Shift, to.Shift);
        return d;
    }

    /// <summary>Определяет направление: какие буквы в тексте преобладают.</summary>
    public Direction Detect(string text)
    {
        int lat = 0, cyr = 0;
        foreach (var ch in text)
        {
            if (!char.IsLetter(ch)) continue;
            if (ch < 0x250) lat++; else if (ch >= 0x400 && ch <= 0x52F) cyr++;
        }
        if (lat == 0 && cyr == 0)
        {   // нет букв — смотрим, в какой раскладке есть больше символов
            int a = text.Count(_l2c.ContainsKey), b = text.Count(_c2l.ContainsKey);
            return b > a ? Direction.CyrillicToLatin : Direction.LatinToCyrillic;
        }
        return cyr > lat ? Direction.CyrillicToLatin : Direction.LatinToCyrillic;
    }

    public string Convert(string text, Direction dir)
    {
        var map = dir == Direction.LatinToCyrillic ? _l2c : _c2l;
        var sb = new StringBuilder(text.Length);
        foreach (var ch in text) sb.Append(map.TryGetValue(ch, out var r) ? r : ch);
        return sb.ToString();
    }

    public string Convert(string text) => Convert(text, Detect(text));
}
