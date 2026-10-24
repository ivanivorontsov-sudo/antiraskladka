using System.Text;
using System.Text.RegularExpressions;
namespace Antiraskladka.Core;

public sealed record Correction(int Index, string Original, string Replacement);

/// <summary>
/// Исправление опечаток (русский и английский). Кандидаты на расстоянии Дамерау—Левенштейна 1
/// (для длинных слов — 2), ранжирование по частоте слова и близости клавиш.
/// Не трогает: известные слова, короткие слова (≤3 букв), ВЕРХНИЙ РЕГИСТР, имена с заглавной
/// внутри предложения, идентификаторы кода (camelCase, snake_case, цифры), URL, почту, пути.
/// </summary>
public sealed class TypoCorrector
{
    const string RuAlpha = "абвгдеёжзийклмнопрстуфхцчшщъыьэюя", EnAlpha = "abcdefghijklmnopqrstuvwxyz";
    readonly WordList _ru, _en;
    public UserDictionary User { get; set; }

    public TypoCorrector(WordList ru, WordList en, UserDictionary? user = null)
    { _ru = ru; _en = en; User = user ?? new UserDictionary(); }

    static TypoCorrector? _default;
    static readonly object _lock = new();
    /// <summary>Встроенные словари (загружаются один раз, ~1–2 с).</summary>
    public static TypoCorrector LoadDefault(UserDictionary? user = null)
    {
        lock (_lock)
        {
            _default ??= new TypoCorrector(WordList.FromResource("ru"), WordList.FromResource("en"));
            if (user != null) _default.User = user;
            return _default;
        }
    }

    // ---------- раскладки для «соседних клавиш» ----------
    static readonly Dictionary<char, (int r, int c)> KeyPos = BuildPos();
    static Dictionary<char, (int, int)> BuildPos()
    {
        var d = new Dictionary<char, (int, int)>();
        string[][] rows = { new[] { "qwertyuiop[]", "asdfghjkl;'", "zxcvbnm,." }, new[] { "йцукенгшщзхъ", "фывапролджэ", "ячсмитьбю" } };
        foreach (var kb in rows)
            for (int r = 0; r < 3; r++)
                for (int c = 0; c < kb[r].Length; c++) d[kb[r][c]] = (r, c * 2 + r);
        d['ё'] = d['е'];
        return d;
    }
    static bool Adjacent(char a, char b)
    {
        if (!KeyPos.TryGetValue(a, out var p) || !KeyPos.TryGetValue(b, out var q)) return false;
        if ((a < 0x400) != (b < 0x400)) return false;
        int dr = Math.Abs(p.r - q.r), dc = Math.Abs(p.c - q.c);
        return dr == 0 ? dc == 2 : dr == 1 && dc <= 1;
    }

    // ---------- проверка слова ----------
    enum Lang { None, Ru, En }
    static Lang LangOf(string w)
    {
        bool ru = false, en = false;
        foreach (var ch in w)
        {
            if (ch is >= 'a' and <= 'z') en = true;
            else if (ch is >= 'а' and <= 'я' or 'ё') ru = true;
            else if (ch is not ('-' or '\'')) return Lang.None;
        }
        return ru == en ? Lang.None : ru ? Lang.Ru : Lang.En;
    }

    WordList Dict(Lang l) => l == Lang.Ru ? _ru : _en;

    /// <summary>Слово (в любом регистре) есть в словаре или личном словаре.</summary>
    public bool IsKnown(string word)
    {
        var w = Norm(word);
        var l = LangOf(w);
        if (User.IsWord(w) || User.IsIgnored(w)) return true;
        if (l == Lang.None) return false;
        if (Dict(l).Contains(w)) return true;
        // составные через дефис: «какой-нибудь», «well-known»
        if (w.Contains('-')) return w.Split('-').All(p => p.Length == 0 || Dict(l).Contains(p) || User.IsWord(p));
        return false;
    }

    static string Norm(string w) => w.ToLowerInvariant().Replace('’', '\'');

    int FreqOf(Lang l, string w)
    {
        int f = Dict(l).Freq(w);
        if (User.IsWord(w)) f = Math.Max(f, 40);
        return f;
    }

    /// <summary>Лучшее исправление слова (с сохранением регистра) или null.</summary>
    public string? Suggest(string word)
    {
        var w = Norm(word);
        var l = LangOf(w);
        if (l == Lang.None || w.Length < 4 || IsKnown(w)) return null;
        var alpha = l == Lang.Ru ? RuAlpha : EnAlpha;

        string? best = null; int bestScore = int.MinValue;
        void Consider(string cand, int dist, int bonus)
        {
            int f = FreqOf(l, cand);
            if (f < 0 || User.IsIgnored(cand) && !User.IsWord(cand)) return;
            if (w.Length <= 5 && f < 15) return;        // короткие: только частые слова
            if (dist == 2 && f < 20) return;            // двойная ошибка: только достаточно частые
            int score = f - 30 * dist + bonus;
            if (score > bestScore || score == bestScore && string.CompareOrdinal(cand, best) < 0) { bestScore = score; best = cand; }
        }

        var e1 = Edits(w, alpha);
        foreach (var (c, bonus) in e1) Consider(c, 1, bonus);
        if (best == null && w.Length >= 6)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var (c1, b1) in e1)
                foreach (var (c2, b2) in Edits(c1, alpha))
                    if (seen.Add(c2)) Consider(c2, 2, Math.Min(b1, b2));
        }
        if (best == null || best == w) return null;
        return MatchCase(word, best);
    }

    /// <summary>Все правки на расстоянии 1 (Дамерау): удаление, перестановка, замена, вставка. Бонус — за «вероятную» ошибку.</summary>
    static List<(string, int)> Edits(string w, string alpha)
    {
        var res = new List<(string, int)>(w.Length * (alpha.Length * 2 + 2));
        var sb = new StringBuilder(w.Length + 1);
        for (int i = 0; i < w.Length; i++)
        {
            // удаление (лишняя буква: удвоение или соседняя клавиша — вероятнее)
            int b = (i > 0 && (w[i - 1] == w[i] || Adjacent(w[i - 1], w[i]))) || (i + 1 < w.Length && Adjacent(w[i + 1], w[i])) ? 8 : 0;
            res.Add((w.Remove(i, 1), b));
            if (i + 1 < w.Length && w[i] != w[i + 1])
                res.Add((w[..i] + w[i + 1] + w[i] + w[(i + 2)..], 10)); // перестановка
            foreach (var ch in alpha)
            {
                if (ch == w[i]) continue;
                int sbonus = Adjacent(ch, w[i]) ? 10 : Similar(ch, w[i]) ? 12 : 0;
                sb.Clear().Append(w).Remove(i, 1).Insert(i, ch);
                res.Add((sb.ToString(), sbonus));
            }
        }
        for (int i = 0; i <= w.Length; i++)
            foreach (var ch in alpha)
                res.Add((w.Insert(i, ch.ToString()), i > 0 && w[i - 1] == ch || i < w.Length && w[i] == ch ? 12 : 3));
        return res;
    }

    /// <summary>Буквы, которые часто путают «на слух»: о/ё/е, и/е, а/о, а/я, е/э, ш/щ, и/ы, ь/ъ.</summary>
    static readonly string[] SimilarPairs = { "её", "оё", "ие", "ао", "ая", "еэ", "шщ", "иы", "ьъ", "ую", "ei", "ae", "ou", "ck", "sc", "sz" };
    static bool Similar(char a, char b) => SimilarPairs.Any(p => p[0] == a && p[1] == b || p[0] == b && p[1] == a);

    static string MatchCase(string orig, string cand)
    {
        if (orig.Length > 0 && char.IsUpper(orig[0]) && orig.Skip(1).All(c => !char.IsUpper(c)))
            return char.ToUpperInvariant(cand[0]) + cand[1..];
        return cand;
    }

    // ---------- текст ----------
    static readonly Regex Chunk = new(@"\S+", RegexOptions.Compiled);
    static readonly Regex WordRx = new(@"\p{L}+(?:['’-]\p{L}+)*", RegexOptions.Compiled);
    static readonly Regex SkipChunk = new(@"://|^www\.|@|[\\/_#$%^&*=<>{}\[\]|~`]|\d|\p{L}\.\p{L}|\p{Ll}\p{Lu}|^[-+]{1,2}\p{L}", RegexOptions.Compiled);

    /// <summary>Находит исправления в тексте.</summary>
    public List<Correction> Find(string text)
    {
        var res = new List<Correction>();
        bool sentenceStart = true;
        foreach (Match ch in Chunk.Matches(text))
        {
            var chunk = ch.Value;
            bool skip = SkipChunk.IsMatch(chunk);
            foreach (Match m in WordRx.Matches(chunk))
            {
                var word = m.Value;
                bool start = sentenceStart && m.Index == chunk.TakeWhile(c => !char.IsLetter(c)).Count();
                sentenceStart = false;
                if (skip || !Correctable(word, start)) continue;
                var s = Suggest(word);
                if (s != null) res.Add(new Correction(ch.Index + m.Index, word, s));
            }
            if (chunk.Length > 0 && ".!?…".Contains(chunk[^1])) sentenceStart = true;
        }
        return res;
    }

    static bool Correctable(string word, bool sentenceStart)
    {
        if (word.Length < 4) return false;
        int upper = word.Count(char.IsUpper);
        if (upper > 1) return false;                                   // АББРЕВИАТУРЫ, McDonald
        if (upper == 1 && (!char.IsUpper(word[0]) || !sentenceStart)) return false; // имена и названия
        return true;
    }

    public string Correct(string text) => Apply(text, Find(text));

    public static string Apply(string text, IEnumerable<Correction> fixes)
    {
        var sb = new StringBuilder(text);
        foreach (var c in fixes.OrderByDescending(c => c.Index))
            sb.Remove(c.Index, c.Original.Length).Insert(c.Index, c.Replacement);
        return sb.ToString();
    }

    /// <summary>Насколько текст похож на «нормальный» текст: 1.0 — все слова известны, 0 — ни одного.
    /// Исправимые опечатки дают половину балла. Используется, чтобы решить: менять раскладку или только исправить опечатки.</summary>
    public double Plausibility(string text)
    {
        int n = 0; double s = 0;
        foreach (Match m in WordRx.Matches(text))
        {
            var w = m.Value;
            if (w.Length < 2) continue;
            n++;
            if (IsKnown(w)) s += 1;
            else if (w.Length >= 4 && Suggest(w) != null) s += 0.5;
        }
        return n == 0 ? 0 : s / n;
    }
}
