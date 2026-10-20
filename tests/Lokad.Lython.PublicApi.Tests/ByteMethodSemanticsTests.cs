using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class ByteMethodSemanticsTests
{
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task GroupingDirectionAndIdentityTranslationMatchPython(bool asynchronous)
    {
        using var script = new LythonEngine().Compile("""
            for group in [-2, 2, 0, -2147483648]:
                print(b'ABCDE'.hex('-', group))
            print(b'ABCDE'.hex(b':', -2))
            print(b'banana'.translate(None, b'a'))
            print(b'banana'.translate(None))
            print(b'banana'.translate(bytes.maketrans(b'an',b'xy'), b'b'))
            for table in [b'', b'a', 1, 'x']:
                try: b'a'.translate(table)
                except Exception as error: print(type(error).__name__)
            """);
        Assert.True(script.IsValid);
        using var result = asynchronous ? await script.RunAsync(new MockLythonHost()) : script.Run(new MockLythonHost());
        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("4142-4344-45\n41-4243-4445\n4142434445\n4142434445\n4142:4344:45\nb'bnn'\nb'banana'\nb'xyxyx'\nValueError\nValueError\nTypeError\nTypeError\n", result.StandardOutput);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task ByteSlicesAndEscapingPreservePythonValues(bool asynchronous)
    {
        using var script = new LythonEngine().Compile("""
            value=b'ab\x00\xff\n\'"'
            for selection in [value[:], value[1:4], value[::-1], value[::2], value[2:2], value[-100:100]]:
                print(repr(selection))
            print(b'\t.\t\n\t'.expandtabs(4))
            """);
        Assert.True(script.IsValid);
        using var result = asynchronous ? await script.RunAsync(new MockLythonHost()) : script.Run(new MockLythonHost());
        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("b'ab\\x00\\xff\\n\\\'\"'\nb'b\\x00\\xff'\nb'\"\\\'\\n\\xff\\x00ba'\nb'a\\x00\\n\"'\nb''\nb'ab\\x00\\xff\\n\\\'\"'\nb'    .   \\n    '\n", result.StandardOutput);
    }
}
