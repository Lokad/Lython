namespace Lokad.Lython.PublicApi.Tests;

public sealed class ExpandedStringDiagnosticTests
{
    public static TheoryData<string, string, string> InvalidPrograms => new()
    {
        { "'a'.replace(1,'x')", "LA3094", "1" },
        { "'a'.replace('a',None)", "LA3094", "None" },
        { "'a'.split(1)", "LA3091", "1" },
        { "' a '.strip(1)", "LA3093", "1" },
        { "'a'.replace('a','b','bad')", "LA3095", "'bad'" },
        { "'a'.find('a','bad')", "LA3076", "'bad'" },
        { "'a'.startswith(1)", "LA3073", "1" },
        { "'a'.startswith((1,'a'))", "LA3074", "1" },
        { "','.join([1])", "LA3102", "1" },
        { "args=(1,'x')\n'a'.replace(*args)", "LA3094", "args" },
        { "args=('a',None)\n'a'.replace(*args)", "LA3094", "args" },
        { "args=(1,)\n'a'.split(*args)", "LA3091", "args" },
        { "args=(1,)\n' a '.strip(*args)", "LA3093", "args" },
        { "args={'count':'bad'}\n'a'.replace('a','b',**args)", "LA3095", "args" },
        { "args=('a','bad')\n'a'.find(*args)", "LA3076", "args" },
        { "args=(1,)\n'a'.startswith(*args)", "LA3073", "args" },
        { "args=([1],)\n','.join(*args)", "LA3102", "1" },
    };

    [Theory]
    [MemberData(nameof(InvalidPrograms))]
    public void ProvenInvalidArgumentsKeepTheirDiagnosticAndLocation(string source, string code, string marker)
    {
        var budget = new LythonMemoryBudget(512 * 1024);
        using (var script = new LythonEngine(Utf8RegexProvider.Instance).Compile(source, budget))
        {
            Assert.False(script.IsValid);
            var start = source.LastIndexOf(marker, StringComparison.Ordinal);
            Assert.Contains(script.Diagnostics, diagnostic => diagnostic.Code == code && diagnostic.Span?.Start == start);
        }
        Assert.Equal(0, budget.ReservedBytes);
    }
}
