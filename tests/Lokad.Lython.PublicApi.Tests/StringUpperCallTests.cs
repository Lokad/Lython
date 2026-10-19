using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class StringUpperCallTests
{
    // Complete stdout goldens from isolated CPython 3.13.2.
    public static TheoryData<string, string, string> Programs => new()
    {
        { "unicode", "for text in ['', 'ALREADY UPPER', 'alpha', 'Straße', 'ﬃ', 'iσ', 'İσ', 'ᾀ', '𐐨', 'a\\x00ß']:\n print(repr(text.upper()))", "''\n'ALREADY UPPER'\n'ALPHA'\n'STRASSE'\n'FFI'\n'IΣ'\n'İΣ'\n'ἈΙ'\n'𐐀'\n'A\\x00SS'\n" },
        { "method-metadata", "text='Straße'\nfirst=text.upper\nsecond=text.upper\nprint(first is second,first.__self__ is text,first.__name__,first.__qualname__,first.__module__)\ntext='changed'\nprint(first(),second(),text.upper())", "False True upper str.upper None\nSTRASSE STRASSE CHANGED\n" },
        { "positional-effects", "events=[]\ndef arg():\n events.append('arg')\n return 0\ntry:\n 'word'.upper(arg())\nexcept TypeError:\n print(events,'TypeError')", "['arg'] TypeError\n" },
        { "keyword-effects", "events=[]\ndef arg():\n events.append('arg')\n return 0\ntry:\n 'word'.upper(value=arg())\nexcept TypeError:\n print(events,'TypeError')", "['arg'] TypeError\n" },
        { "expanded", "events=[]\ndef args():\n events.append('args')\n return []\ntext='Straße'\nprint(text.upper(*args()),events)\ntry:\n text.upper(*[1])\nexcept TypeError:\n print('TypeError')", "STRASSE ['args']\nTypeError\n" },
        { "dynamic", "class Text:\n def upper(self): return 'custom'\nfor text in ['word',Text()]:\n print(text.upper())", "WORD\ncustom\n" },
        { "helper", "def normalize(text): return (text.upper)()\nprint([normalize(text) for text in ['é','aß','iς']])", "['É', 'ASS', 'IΣ']\n" },
        { "getattr", "text='Straße'\nmethod=getattr(text,'upper')\nprint(method(),getattr('é','upper')())", "STRASSE É\n" },
        { "empty-and-unchanged", "for text in ['', 'ABC', '123 😀', '\\x00']:\n method=text.upper\n print(repr(text.upper()),repr(method()),repr(text))", "'' '' ''\n'ABC' 'ABC' 'ABC'\n'123 😀' '123 😀' '123 😀'\n'\\x00' '\\x00' '\\x00'\n" },
    };

    [Theory]
    [MemberData(nameof(Programs))]
    public async Task ImmediateExposedAndDynamicCallsMatchPython(string label, string source, string expected)
    {
        var budget = new LythonMemoryBudget(512 * 1024);
        using var script = new LythonEngine(Utf8RegexProvider.Instance).Compile(source, budget);
        Assert.True(script.IsValid, label + ":" + string.Join(";", script.Diagnostics.Select(d => d.Message)));
        var baseline = budget.ReservedBytes;
        foreach (var asynchronous in new[] { false, true })
        {
            using var result = asynchronous ? await script.RunAsync(new MockLythonHost()) : script.Run(new MockLythonHost());
            Assert.True(result.Success, result.Failure?.Message);
            Assert.Equal(expected, result.StandardOutput);
            result.Dispose();
            Assert.Equal(baseline, budget.ReservedBytes);
        }
        script.Dispose();
        Assert.Equal(0, budget.ReservedBytes);
    }
}
