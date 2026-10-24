using Antiraskladka.Core;
using Xunit;

public class ConverterTests
{
    static readonly LayoutConverter Std = new(Layouts.UsQwerty, Layouts.Ru);

    [Theory]
    [InlineData("ghbdtn", "привет")]
    [InlineData("Ghbdtn? vbh!", "Привет, мир!")]
    [InlineData("`~", "ёЁ")]
    [InlineData("[]{};':\"<>,.", "хъХЪжэЖЭБЮбю")]
    [InlineData("!@#$%^&*()_+", "!\"№;%:?*()_+")]
    [InlineData("/?\\|", ".,\\/")]
    [InlineData("1234567890-=", "1234567890-=")]
    [InlineData("Ntcn 123", "Тест 123")]
    public void EnToRu(string en, string ru)
    {
        Assert.Equal(ru, Std.Convert(en, Direction.LatinToCyrillic));
    }

    [Theory]
    [InlineData("руддщ цщкдв", "hello world")]
    [InlineData("ёЁ", "`~")]
    [InlineData("хъХЪжэЖЭбюБЮ.,", "[]{};':\",.<>/?")]
    [InlineData("\"№;:?", "@#$^&")]
    public void RuToEn(string ru, string en)
    {
        Assert.Equal(en, Std.Convert(ru, Direction.CyrillicToLatin));
    }

    [Fact]
    public void EveryLayoutHas47UniqueKeys()
    {
        foreach (var l in Layouts.All)
        {
            Assert.Equal(KeyboardLayout.KeyCount, l.Normal.Length);
            Assert.Equal(KeyboardLayout.KeyCount, l.Shift.Length);
            Assert.Equal(KeyboardLayout.ScanCodes.Length, l.Normal.Length);
        }
    }

    [Fact]
    public void EveryKeyMapsByPosition()
    {
        foreach (var lat in Layouts.LatinLayouts)
        foreach (var cyr in Layouts.CyrillicLayouts)
        {
            var c = new LayoutConverter(lat, cyr);
            var first = new HashSet<char>();
            for (int i = 0; i < 47; i++)
                foreach (var (f, t) in new[] { (lat.Normal[i], cyr.Normal[i]), (lat.Shift[i], cyr.Shift[i]) })
                    if (first.Add(f)) Assert.Equal(t.ToString(), c.Convert(f.ToString(), Direction.LatinToCyrillic));
        }
    }

    [Theory]
    [InlineData("ghbdtn", "привет")]
    [InlineData("руддщ", "hello")]
    [InlineData("Ghbdtn vbh", "Привет мир")]
    public void AutoDetect(string input, string expected) => Assert.Equal(expected, Std.Convert(input));

    [Fact]
    public void RoundTripLetters()
    {
        var s = "Съешь же ещё этих мягких французских булок, да выпей чаю.";
        var en = Std.Convert(s);
        Assert.Equal(s, Std.Convert(en));
    }

    [Fact] public void Dvorak() => Assert.Equal("привет", new LayoutConverter(Layouts.Dvorak, Layouts.Ru).Convert("idxeyb"));
    [Fact] public void Colemak() => Assert.Equal("привет", new LayoutConverter(Layouts.Colemak, Layouts.Ru).Convert("dhbsgk"));
    [Fact] public void Phonetic() => Assert.Equal("привет", new LayoutConverter(Layouts.UsQwerty, Layouts.RuPhonetic).Convert("privet"));
    [Fact] public void Typewriter() => Assert.Equal("1|№", new LayoutConverter(Layouts.UsQwerty, Layouts.RuTypewriter).Convert("!`1", Direction.LatinToCyrillic));
    [Fact] public void Ukrainian() => Assert.Equal("їжак", new LayoutConverter(Layouts.UsQwerty, Layouts.Ukrainian).Convert("];fr"));
    [Fact] public void Belarusian() => Assert.Equal("ўсё", new LayoutConverter(Layouts.UsQwerty, Layouts.Belarusian).Convert("oc`"));
    [Fact] public void UkPound() => Assert.Equal("\"№", new LayoutConverter(Layouts.Uk, Layouts.Ru).Convert("\"£", Direction.LatinToCyrillic));
}
