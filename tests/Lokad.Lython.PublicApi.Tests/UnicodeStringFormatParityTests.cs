using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class UnicodeStringFormatParityTests
{
    // Independent CPython 3.13.2 outputs, including errors and scalar boundaries.
    public static IEnumerable<object[]> Cases()
    {
        yield return ["","","''\n''\n''\n"];
        yield return ["abc","","'abc'\n'abc'\n'abc'\n"];
        yield return ["café😀é","s","'café😀é'\n'café😀é'\n'café😀é'\n"];
        yield return ["café😀é",".0s","''\n''\n''\n"];
        yield return ["café😀é",".1s","'c'\n'c'\n'c'\n"];
        yield return ["café😀é",".4s","'café'\n'café'\n'café'\n"];
        yield return ["café😀é",".5s","'café😀'\n'café😀'\n'café😀'\n"];
        yield return ["café😀é",".6s","'café😀e'\n'café😀e'\n'café😀e'\n"];
        yield return ["café😀é",".7s","'café😀é'\n'café😀é'\n'café😀é'\n"];
        yield return ["café😀é","<12.5s","'café😀       '\n'café😀       '\n'café😀       '\n"];
        yield return ["café😀é",">12.5s","'       café😀'\n'       café😀'\n'       café😀'\n"];
        yield return ["café😀é","^12.5s","'   café😀    '\n'   café😀    '\n'   café😀    '\n"];
        yield return ["café😀é","😀^12.5s","'😀😀😀café😀😀😀😀😀'\n'😀😀😀café😀😀😀😀😀'\n'😀😀😀café😀😀😀😀😀'\n"];
        yield return ["café😀é","é>12.0s","'éééééééééééé'\n'éééééééééééé'\n'éééééééééééé'\n"];
        yield return ["ab","^3s","'ab '\n'ab '\n'ab '\n"];
        yield return ["","😀^3s","'😀😀😀'\n'😀😀😀'\n'😀😀😀'\n"];
        yield return ["abcdef",".2s","'ab'\n'ab'\n'ab'\n"];
        yield return ["x","05s","'x0000'\n'x0000'\n'x0000'\n"];
        yield return ["x","0>5s","'0000x'\n'0000x'\n'0000x'\n"];
        yield return ["x","0^6s","'00x000'\n'00x000'\n'00x000'\n"];
        yield return ["long","2s","'long'\n'long'\n'long'\n"];
        yield return ["x",">0s","'x'\n'x'\n'x'\n"];
        yield return ["\u0000é😀","🐈^8.2s","'🐈🐈🐈\\x00é🐈🐈🐈'\n'🐈🐈🐈\\x00é🐈🐈🐈'\n'🐈🐈🐈\\x00é🐈🐈🐈'\n"];
        yield return ["","0s","''\n''\n''\n"];
        yield return ["😀","*^4s","'*😀**'\n'*😀**'\n'*😀**'\n"];
        yield return ["x","\u0000<4s","'x\\x00\\x00\\x00'\n'x\\x00\\x00\\x00'\n'x\\x00\\x00\\x00'\n"];
        yield return ["x","+s","ValueError:Sign not allowed in string format specifier\nValueError:Sign not allowed in string format specifier\nValueError:Sign not allowed in string format specifier\n"];
        yield return ["x"," s","ValueError:Space not allowed in string format specifier\nValueError:Space not allowed in string format specifier\nValueError:Space not allowed in string format specifier\n"];
        yield return ["x","-s","ValueError:Sign not allowed in string format specifier\nValueError:Sign not allowed in string format specifier\nValueError:Sign not allowed in string format specifier\n"];
        yield return ["x","#s","ValueError:Alternate form (#) not allowed in string format specifier\nValueError:Alternate form (#) not allowed in string format specifier\nValueError:Alternate form (#) not allowed in string format specifier\n"];
        yield return ["x","=5s","ValueError:'=' alignment not allowed in string format specifier\nValueError:'=' alignment not allowed in string format specifier\nValueError:'=' alignment not allowed in string format specifier\n"];
        yield return ["x",",s","ValueError:Cannot specify ',' with 's'.\nValueError:Cannot specify ',' with 's'.\nValueError:Cannot specify ',' with 's'.\n"];
        yield return ["x","_s","ValueError:Cannot specify '_' with 's'.\nValueError:Cannot specify '_' with 's'.\nValueError:Cannot specify '_' with 's'.\n"];
        yield return ["x","5d","ValueError:Unknown format code 'd' for object of type 'str'\nValueError:Unknown format code 'd' for object of type 'str'\nValueError:Unknown format code 'd' for object of type 'str'\n"];
        yield return ["x",".s","ValueError:Format specifier missing precision\nValueError:Format specifier missing precision\nValueError:Format specifier missing precision\n"];
        yield return ["x","..2s","ValueError:Format specifier missing precision\nValueError:Format specifier missing precision\nValueError:Format specifier missing precision\n"];
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task StringFormatsPreservePythonAcrossEntryPoints(string value, string spec, string expected)
    {
        const string source = """
            for render in [lambda:format(value,spec),lambda:('{0:'+spec+'}').format(value),lambda:f'{value:{spec}}']:
             try: print(repr(render()))
             except (ValueError,TypeError) as error: print(type(error).__name__+':'+str(error))
            """;
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        using var script = new LythonEngine().Compile(source, budget);
        Assert.True(script.IsValid, string.Join("; ", script.Diagnostics));
        var baseline = budget.ReservedBytes;
        var options = new LythonRunOptions { Globals = new Dictionary<string, object?> { ["value"] = value, ["spec"] = spec } };
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
