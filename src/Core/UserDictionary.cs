namespace Antiraskladka.Core;

/// <summary>Личный словарь: «мои слова» (считаются правильными и предлагаются как исправления)
/// и «игнор» (никогда не исправляются, но и не предлагаются).</summary>
public sealed class UserDictionary
{
    readonly HashSet<string> _words = new(StringComparer.Ordinal), _ignore = new(StringComparer.Ordinal);
    public string? WordsPath { get; }
    public string? IgnorePath { get; }
    public IReadOnlyCollection<string> Words => _words;
    public IReadOnlyCollection<string> Ignored => _ignore;

    public UserDictionary() { }
    public UserDictionary(string wordsPath, string ignorePath)
    {
        WordsPath = wordsPath; IgnorePath = ignorePath;
        Load(wordsPath, _words); Load(ignorePath, _ignore);
    }

    static void Load(string path, HashSet<string> set)
    {
        try
        {
            if (!File.Exists(path)) return;
            foreach (var l in File.ReadAllLines(path))
            {
                var w = l.Trim();
                if (w.Length > 0 && !w.StartsWith('#')) set.Add(w.ToLowerInvariant());
            }
        }
        catch { }
    }

    public bool IsWord(string lower) => _words.Contains(lower);
    public bool IsIgnored(string lower) => _ignore.Contains(lower);
    public bool Add(string word) => Put(_words, WordsPath, word);
    public bool Ignore(string word) => Put(_ignore, IgnorePath, word);

    bool Put(HashSet<string> set, string? path, string word)
    {
        var w = word.Trim().ToLowerInvariant();
        if (w.Length == 0 || !set.Add(w)) return false;
        if (path != null)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                if (!File.Exists(path)) File.WriteAllText(path, "# одно слово на строку\n");
                File.AppendAllText(path, w + "\n");
            }
            catch { }
        }
        return true;
    }
}
