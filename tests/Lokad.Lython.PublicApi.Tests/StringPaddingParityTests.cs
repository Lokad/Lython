using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class StringPaddingParityTests
{
    // Independent CPython outputs; center's odd-padding rule differs from format ^.
    public static IEnumerable<object[]> Cases()
    {
        yield return ["ab",3," ","'ab '\n' ab'\n' ab'\n"];
        yield return ["😀",4,"*","'😀***'\n'***😀'\n'*😀**'\n"];
        yield return ["café😀",8,"🧮","'café😀🧮🧮🧮'\n'🧮🧮🧮café😀'\n'🧮café😀🧮🧮'\n"];
        yield return ["é",3,"é","'éé'\n'éé'\n'éé'\n"];
        yield return ["",3,"🐈","'🐈🐈🐈'\n'🐈🐈🐈'\n'🐈🐈🐈'\n"];
        yield return ["é",0,"*","'é'\n'é'\n'é'\n"];
        yield return ["abc",-2,"x","'abc'\n'abc'\n'abc'\n"];
        yield return ["\u0000😀",5,"\u0000","'\\x00😀\\x00\\x00\\x00'\n'\\x00\\x00\\x00\\x00😀'\n'\\x00\\x00\\x00😀\\x00'\n"];
        yield return ["x",5,"","TypeError:The fill character must be exactly one character long\nTypeError:The fill character must be exactly one character long\nTypeError:The fill character must be exactly one character long\n"];
        yield return ["x",0,"too long","TypeError:The fill character must be exactly one character long\nTypeError:The fill character must be exactly one character long\nTypeError:The fill character must be exactly one character long\n"];
        yield return ["x",5,"ab","TypeError:The fill character must be exactly one character long\nTypeError:The fill character must be exactly one character long\nTypeError:The fill character must be exactly one character long\n"];
        yield return ["x",16,".","'x...............'\n'...............x'\n'.......x........'\n"];
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task PaddingPreservesScalarWidthsAndRetainedResults(string value, int width, string fill, string expected)
    {
        const string source = """
            for render in [lambda:value.ljust(width,fill),lambda:value.rjust(width,fill),lambda:value.center(width,fill)]:
             try: print(repr(render()))
             except (TypeError,ValueError) as error: print(type(error).__name__+':'+str(error))
            """;
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        using var script = new LythonEngine().Compile(source, budget);
        Assert.True(script.IsValid, string.Join("; ", script.Diagnostics));
        var baseline = budget.ReservedBytes;
        var options = new LythonRunOptions { Globals = new Dictionary<string, object?> { ["value"] = value, ["width"] = width, ["fill"] = fill } };
        foreach (var asynchronous in new[] { false, true })
        {
            using (var result = asynchronous ? await script.RunAsync(new MockLythonHost(), options) : script.Run(new MockLythonHost(), options))
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
