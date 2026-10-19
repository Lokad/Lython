using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class JsonIntegerEmissionTests
{
    [Theory]
    [MemberData(nameof(IntegerRenderingTests.Numbers), MemberType = typeof(IntegerRenderingTests))]
    public async Task IntegerValuesAndKeysUseTheSameExactText(string expression, string expected)
    {
        var source = "import json\nvalue=" + expression + "\nencoder=json.JSONEncoder()\nprint(json.dumps(value))\nprint(encoder.encode(value))\nprint(''.join(encoder.iterencode(value)))\nmapping={value:None}\nprint(json.dumps(mapping))\nprint(encoder.encode(mapping))\nprint(''.join(encoder.iterencode(mapping)))";
        var key = "{\"" + expected + "\": null}";
        await AssertBoth(source, string.Concat(Enumerable.Repeat(expected + "\n", 3)) + string.Concat(Enumerable.Repeat(key + "\n", 3)));
    }

    public static TheoryData<string, string> Controls => new()
    {
        { "mapping={10:'ten',2:'two'}\noptions={'sort_keys':True}", "{\"2\": \"two\", \"10\": \"ten\"}" },
        { "mapping={True:1,None:2,1.5:3,4:False}\noptions={'ensure_ascii':False,'separators':(',',':')}", "{\"true\":1,\"null\":2,\"1.5\":3,\"4\":false}" },
        { "mapping={(1,):'skip',2:'two'}\noptions={'skipkeys':True}", "{\"2\": \"two\"}" },
    };

    [Theory]
    [MemberData(nameof(Controls))]
    public async Task SortingSkippingAndOtherNativeKeysKeepTheirBehavior(string setup, string expected)
        => await AssertBoth("import json\n" + setup + "\nencoder=json.JSONEncoder(**options)\nprint(json.dumps(mapping,**options))\nprint(encoder.encode(mapping))\nprint(''.join(encoder.iterencode(mapping)))",
            string.Concat(Enumerable.Repeat(expected + "\n", 3)));

    private static async Task AssertBoth(string source, string expected)
    {
        using var compiled = new LythonEngine(Utf8RegexProvider.Instance).Compile(source);
        Assert.True(compiled.IsValid, string.Join("; ", compiled.Diagnostics.Select(d => d.Message)));
        foreach (var asynchronous in new[] { false, true })
        {
            using var result = asynchronous ? await compiled.RunAsync(new MockLythonHost()) : compiled.Run(new MockLythonHost());
            Assert.True(result.Success, result.Failure?.Message);
            Assert.Equal(expected, result.StandardOutput);
        }
    }
}
