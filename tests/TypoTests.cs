using Antiraskladka.Core;
using Xunit;

public class TypoFixture { public TypoCorrector T { get; } = new(WordList.FromResource("ru"), WordList.FromResource("en")); }

public class TypoTests : IClassFixture<TypoFixture>
{
    readonly TypoCorrector _t;
    static readonly LayoutConverter Std = new(Layouts.UsQwerty, Layouts.Ru);
    public TypoTests(TypoFixture f) { _t = f.T; _t.User = new UserDictionary(); }

    [Theory]
    [InlineData("превет", "привет")]
    [InlineData("сегоня", "сегодня")]
    [InlineData("пожалуйта", "пожалуйста")]
    [InlineData("спасибл", "спасибо")]
    [InlineData("корова", "корова")]
    [InlineData("мололко", "молоко")]
    [InlineData("собака", "собака")]
    [InlineData("здраствуйте", "здравствуйте")]
    [InlineData("извините", "извините")]
    [InlineData("програма", "программа")]
    [InlineData("коммандой", "командой")]      // словоформа
    [InlineData("книгаии", "книгами")]
    [InlineData("работаэт", "работает")]
    [InlineData("репозиторий", "репозиторий")]
    [InlineData("репозитрий", "репозиторий")]  // термин
    [InlineData("антибиотк", "антибиотик")]
    [InlineData("кринжовй", "кринжовый")]      // сленг
    [InlineData("teh", "teh")]                 // короче 4 — не трогаем
    [InlineData("recieve", "receive")]
    [InlineData("becuase", "because")]
    [InlineData("definately", "definitely")]
    [InlineData("wierd", "weird")]
    [InlineData("helllo", "hello")]
    [InlineData("tommorow", "tomorrow")]
    [InlineData("kubernets", "kubernetes")]
    [InlineData("algoritm", "algorithm")]
    [InlineData("anesthesia", "anesthesia")]
    [InlineData("gaslightng", "gaslighting")]
    public void Word(string input, string expected) => Assert.Equal(expected, _t.Correct(input));

    [Theory]
    [InlineData("Превет, как дела?", "Привет, как дела?")]
    [InlineData("Я пошол домой.", "Я пошёл домой.")]
    [InlineData("This is realy good.", "This is really good.")]
    [InlineData("Recieve the file", "Receive the file")]
    public void Sentences(string input, string expected) => Assert.Equal(expected, _t.Correct(input));

    [Theory]
    [InlineData("https://exmaple.com/pateh")]
    [InlineData("user@exmaple.com")]
    [InlineData("getUsreName()")]
    [InlineData("my_varable = 5")]
    [InlineData("C:\\Progam Files\\Tset")]
    [InlineData("Встретил Ивонова вчера")]     // имя с заглавной внутри предложения
    [InlineData("NASA и ООНН")]
    [InlineData("v2 recieve2")]
    [InlineData("--verbos")]
    public void LeavesAlone(string text) => Assert.Equal(text, _t.Correct(text));

    [Fact]
    public void KeepsCase() => Assert.Equal("Привет", _t.Correct("Превет"));

    [Fact]
    public void UserDictionary()
    {
        _t.User = new UserDictionary();
        Assert.NotEqual("кракозябрик", _t.Correct("кракозябрик") is var r && r == "кракозябрик" ? "x" : "кракозябрик");
        _t.User.Add("гроквелл");
        Assert.Equal("гроквелл", _t.Correct("гроквелл"));
        Assert.Equal("гроквелл", _t.Correct("гроквел"));   // слово из личного словаря предлагается
        _t.User.Ignore("превет");
        Assert.Equal("превет", _t.Correct("превет"));
    }

    [Theory]
    [InlineData("ghbdtn", "привет")]              // просто раскладка
    [InlineData("ghtdtn", "привет")]              // раскладка + опечатка
    [InlineData("руддщ цщкдв", "hello world")]
    [InlineData("руддщ цщкдвб", "hello world,")]
    [InlineData("превет мир", "привет мир")]        // уже правильная раскладка — только опечатка
    [InlineData("this is realy good", "this is really good")]
    public void Combined(string input, string expected) => Assert.Equal(expected, TextFixer.Fix(input, Std, _t).Text);

    [Fact]
    public void ValidTextStillConvertsLikeBefore()
        => Assert.Equal("ghbdtn", TextFixer.Fix("привет", Std, _t).Text);

    [Fact]
    public void Speed()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        _t.Correct("Сегоня я пошол в магазен и купил мололко, хлеп и сыр. Програмисты любят рефакторнг.");
        Assert.True(sw.ElapsedMilliseconds < 2000, sw.ElapsedMilliseconds + " мс");
    }
}
