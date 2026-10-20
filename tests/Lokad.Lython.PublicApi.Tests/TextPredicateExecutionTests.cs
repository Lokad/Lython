using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class TextPredicateExecutionTests
{
    // CPython 3.13.2; order matches the direct calls and names below.
    public static TheoryData<string, string, string> Inputs => new()
    {
        { "empty", "", "0000010001" },
        { "lower", "abc", "1011010001" },
        { "upper", "ABC", "0111010001" },
        { "mixed", "Ab1", "0001010001" },
        { "decimal", "123", "0001011111" },
        { "superscript", "\u00b2", "0001000111" },
        { "arabic", "\u0661", "0001001111" },
        { "ideograph", "\u56db", "0011000011" },
        { "space", "\u2003", "0000100000" },
        { "control", "\u001c", "0000110000" },
        { "astral", "\ud801\udc28", "1011000001" },
        { "titlecase", "\u01c5", "0011000001" },
        { "joining", "a\u200d", "1000000000" },
        { "combining", "a\u0301", "1000000001" },
        { "early-false", "aaaaaaaaaaaaaaaa!", "1000010001" },
        { "boundary-astral", new string('a', 63) + "\ud801\udc28a", "1011000001" },
        { "boundary-ideograph", new string('a', 63) + "\u56dba", "1011000001" },
        { "boundary-digit", new string('1', 63) + "\u00b21", "0001000111" },
    };

    [Theory]
    [MemberData(nameof(Inputs))]
    public async Task DirectAndBoundPredicatesPreserveUnicodeResultsAfterHostIo(string name, string input, string expected)
    {
        const string direct = "values=[text.islower(),text.isupper(),text.isalpha(),text.isalnum(),text.isspace(),text.isascii(),text.isdecimal(),text.isdigit(),text.isnumeric(),text.isprintable()]";
        const string bound = "names=['islower','isupper','isalpha','isalnum','isspace','isascii','isdecimal','isdigit','isnumeric','isprintable']\nvalues=[getattr(text,name)() for name in names]";
        var budget = new LythonMemoryBudget(2 * 1024 * 1024);
        foreach (var statements in new[] { direct, bound })
        {
            var source = "with open('checkpoint.txt') as marker:marker.read()\n" + statements + "\nprint(''.join('1' if value else '0' for value in values))";
            using var script = new LythonEngine().Compile(source, budget);
            Assert.True(script.IsValid);
            var baseline = budget.ReservedBytes;
            var options = new LythonRunOptions { Globals = new Dictionary<string, object?> { ["text"] = input } };
            foreach (var asynchronous in new[] { false, true })
            {
                var mock = asynchronous ? null : new MockLythonHost();
                var delayed = asynchronous ? new DelayedLythonHost() : null;
                if (delayed is not null) delayed.SeedFile("/checkpoint.txt", "ready");
                else mock!.SeedFile("/checkpoint.txt", "ready");
                using (var result = asynchronous ? await script.RunAsync(delayed!, options) : script.Run(mock!, options))
                {
                    Assert.True(result.Success, $"{name}: {result.Failure?.Message}");
                    Assert.Equal(expected + "\n", result.StandardOutput);
                    if (delayed is not null) Assert.True(delayed.CompletedAsynchronously > 0);
                }
                Assert.Equal(baseline, budget.ReservedBytes);
            }
        }
        Assert.Equal(0, budget.ReservedBytes);
    }
}
