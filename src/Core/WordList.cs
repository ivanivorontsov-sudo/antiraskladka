using System.IO.Compression;
namespace Antiraskladka.Core;

/// <summary>
/// Компактный словарь (≈1,3 млн словоформ занимают ~25 МБ): слова кодируются по байту на букву
/// и лежат в одном массиве, поиск — открытая адресация по хешу. Частота — zipf×10 (0..80).
/// </summary>
public sealed class WordList
{
    // 0 — «нет символа»; a-z, а-я, ё, дефис, апостроф
    static readonly byte[] Code = BuildCodes();
    static byte[] BuildCodes()
    {
        var c = new byte[0x460]; byte n = 1;
        for (char ch = 'a'; ch <= 'z'; ch++) c[ch] = n++;
        for (char ch = 'а'; ch <= 'я'; ch++) c[ch] = n++;
        c['ё'] = n++; c['-'] = n++; c['\''] = n++;
        return c;
    }
    static byte CodeOf(char ch) => ch < Code.Length ? Code[ch] : (byte)0;

    byte[] _blob = new byte[1 << 20]; int _used;
    int[] _table = new int[1 << 16]; int _count;
    public int Count => _count;

    public static WordList FromResource(string lang)
    {
        var asm = typeof(WordList).Assembly;
        using var s = asm.GetManifestResourceStream("dict." + lang) ?? throw new InvalidOperationException("нет словаря " + lang);
        using var gz = new GZipStream(s, CompressionMode.Decompress);
        return FromReader(new StreamReader(gz, System.Text.Encoding.UTF8));
    }

    public static WordList FromReader(TextReader r)
    {
        var wl = new WordList();
        string? line;
        while ((line = r.ReadLine()) != null)
        {
            int tab = line.IndexOf('\t');
            if (tab <= 0) continue;
            wl.Add(line.AsSpan(0, tab), byte.TryParse(line.AsSpan(tab + 1), out var f) ? f : (byte)5);
        }
        return wl;
    }

    static uint Hash(ReadOnlySpan<byte> k)
    {
        uint h = 2166136261;
        foreach (var b in k) { h ^= b; h *= 16777619; }
        return h;
    }

    static bool Encode(ReadOnlySpan<char> w, Span<byte> dst)
    {
        for (int i = 0; i < w.Length; i++) { var b = CodeOf(w[i]); if (b == 0) return false; dst[i] = b; }
        return true;
    }

    public void Add(ReadOnlySpan<char> word, byte freq)
    {
        if (word.Length == 0 || word.Length > 60) return;
        Span<byte> k = stackalloc byte[word.Length];
        if (!Encode(word, k)) return;
        int slot = Find(k);
        if (_table[slot] != 0) { int off = _table[slot] - 1; ref byte f = ref _blob[off + 1 + k.Length]; if (freq > f) f = freq; return; }
        if (_used + k.Length + 2 > _blob.Length) Array.Resize(ref _blob, _blob.Length * 2);
        _blob[_used] = (byte)k.Length; k.CopyTo(_blob.AsSpan(_used + 1)); _blob[_used + 1 + k.Length] = freq;
        _table[slot] = _used + 1; _used += k.Length + 2; _count++;
        if (_count * 2 > _table.Length) Rehash();
    }

    void Rehash()
    {
        var old = _table; _table = new int[old.Length * 2];
        foreach (var e in old)
        {
            if (e == 0) continue;
            int off = e - 1; var k = _blob.AsSpan(off + 1, _blob[off]);
            _table[Find(k)] = e;
        }
    }

    int Find(ReadOnlySpan<byte> k)
    {
        int mask = _table.Length - 1, i = (int)(Hash(k) & mask);
        while (true)
        {
            int e = _table[i];
            if (e == 0) return i;
            int off = e - 1;
            if (_blob[off] == k.Length && _blob.AsSpan(off + 1, k.Length).SequenceEqual(k)) return i;
            i = (i + 1) & mask;
        }
    }

    /// <summary>Частота слова (в нижнем регистре) или -1, если его нет.</summary>
    public int Freq(ReadOnlySpan<char> word)
    {
        if (word.Length == 0 || word.Length > 60) return -1;
        Span<byte> k = stackalloc byte[word.Length];
        if (!Encode(word, k)) return -1;
        int e = _table[Find(k)];
        return e == 0 ? -1 : _blob[e - 1 + 1 + k.Length];
    }

    public bool Contains(ReadOnlySpan<char> word) => Freq(word) >= 0;
}
