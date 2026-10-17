using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class NativeJsonDecoderOptionTests
{
    // c0364867 behavior: explicit invalid callback types reject before parsing.
    // CPython-supported defaults/None/hooks also retain their independent suites.
    public static TheoryData<string, string, string> Programs => new()
    {
        { "defaults", "", "1\n" },
        { "strict_none", "strict=None", "1\n" },
        { "parse_int_none", "parse_int=None", "1\n" },
        { "bad_object_hook", "object_hook=7", "TypeError json option object_hook=... expects a callable or None.\n" },
        { "bad_parse_float", "parse_float=7", "TypeError json option parse_float=... expects a callable or None.\n" },
        { "bad_parse_int", "parse_int=7", "TypeError json option parse_int=... expects a callable or None.\n" },
        { "bad_constant", "parse_constant=7", "TypeError json option parse_constant=... expects a callable or None.\n" },
        { "bad_pairs", "object_pairs_hook=7", "TypeError json option object_pairs_hook=... expects a callable or None.\n" },
        { "unknown", "unknown=7", "TypeError JSONDecoder.__init__() got an unexpected keyword argument 'unknown'\n" },
        { "invalid_then_unknown", "parse_int=7,unknown=8", "TypeError json option parse_int=... expects a callable or None.\n" },
        { "callback_order", "object_hook=7,parse_float=8,parse_int=9", "TypeError json option object_hook=... expects a callable or None.\n" },
        { "pairs_error_order", "parse_constant=7,object_pairs_hook=8", "TypeError json option parse_constant=... expects a callable or None.\n" },
    };

    [Theory]
    [MemberData(nameof(Programs))]
    public async Task NativeOptionValidationOrderAndDefaultsArePreserved(string label, string options, string expected)
    {
        var source = "import json\ndef load(**options):return json.loads(\"1\",**options)\n"
            + "try:print(load(" + options + "))\nexcept Exception as error:print(type(error).__name__,str(error))";
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        using var script = new LythonEngine().Compile(source, budget);
        Assert.True(script.IsValid, label);
        var baseline = budget.ReservedBytes;
        foreach (var asynchronous in new[] { false, true })
        {
            using (var result = asynchronous ? await script.RunAsync(new MockLythonHost()) : script.Run(new MockLythonHost()))
            {
                Assert.True(result.Success, result.Failure?.Message);
                Assert.Equal(expected, result.StandardOutput);
            }
            Assert.Equal(baseline, budget.ReservedBytes);
        }
        script.Dispose();
        Assert.Equal(0, budget.ReservedBytes);
    }
}
