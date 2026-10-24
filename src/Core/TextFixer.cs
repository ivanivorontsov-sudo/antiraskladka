namespace Antiraskladka.Core;

public enum FixKind { None, Layout, Typos, LayoutAndTypos }
public sealed record FixResult(string Text, FixKind Kind, Direction? Dir);

/// <summary>Что делать с выделенным текстом по горячей клавише.
/// Без исправления опечаток — как раньше: всегда смена раскладки.
/// С ним: если текст уже в правильной раскладке и в нём есть опечатки — только исправляем их;
/// иначе меняем раскладку и затем исправляем опечатки в результате.</summary>
public static class TextFixer
{
    public static FixResult Fix(string text, LayoutConverter conv, TypoCorrector? typos)
    {
        var dir = conv.Detect(text);
        var converted = conv.Convert(text, dir);
        if (typos == null) return new(converted, converted == text ? FixKind.None : FixKind.Layout, dir);

        var fixedOrig = typos.Correct(text);
        if (fixedOrig != text)
        {
            double po = typos.Plausibility(text), pc = typos.Plausibility(converted);
            if (po >= pc) return new(fixedOrig, FixKind.Typos, null);
        }
        var fixedConv = typos.Correct(converted);
        var kind = fixedConv == converted ? (converted == text ? FixKind.None : FixKind.Layout) : FixKind.LayoutAndTypos;
        return new(fixedConv, kind, dir);
    }
}
