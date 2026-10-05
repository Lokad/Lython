using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class JsonMutableInstanceTests
{
    [Fact]
    public async Task EncoderWritesControlSubsequentOperationsAndSnapshotIterators()
    {
        var compiled = new LythonEngine().Compile("""
            import json
            e = json.JSONEncoder()
            e.ensure_ascii = False
            print(e.encode('é'))
            e.sort_keys = True
            e.item_separator = '|'
            e.key_separator = '=>'
            print(e.encode({'b': 2, 'a': 1}))
            print(''.join(e.iterencode({'b': 2, 'a': 1})))
            e.sort_keys = False
            e.skipkeys = True
            print(e.encode({(1, 2): 0, 'x': 1}))
            e.allow_nan = False
            try:
                e.encode(float('nan'))
            except ValueError:
                print('nan rejected')
            e.default = lambda o: sorted(o)
            old = e.iterencode([{1}])
            e.default = lambda o: 42
            print(''.join(old), e.encode([{1}]))
            class E(json.JSONEncoder):
                def __init__(self):
                    super().__init__()
                    self.ensure_ascii = False
                    self.indent = 2
            child = E()
            print(child.encode('é'))
            print(child.encode([1, 2]) == ''.join(child.iterencode([1, 2])))
            print(child.ensure_ascii, child.indent)
            """);
        Assert.True(compiled.IsValid, string.Join("; ", compiled.Diagnostics.Select(d => d.Message)));
        foreach (var result in new[] { compiled.Run(new MockLythonHost()), await compiled.RunAsync(new MockLythonHost()) })
        {
            Assert.True(result.Success, result.Failure?.Message);
            Assert.Equal("\"é\"\n{\"a\"=>1|\"b\"=>2}\n{\"a\"=>1|\"b\"=>2}\n{\"x\"=>1}\nnan rejected\n[[1]] [42]\n\"é\"\nTrue\nFalse 2\n", result.StandardOutput);
        }
    }

    [Fact]
    public async Task DecoderAttributesRemainWritableWhileScannerCapturesConstructionOptions()
    {
        var compiled = new LythonEngine().Compile("""
            import json
            class D(json.JSONDecoder):
                pass
            for cls in [json.JSONDecoder, D]:
                d = cls(strict=False)
                print(d.strict, d.parse_int is int, d.object_hook is None)
                d.strict = True
                d.parse_int = lambda s: 42
                print(d.strict, d.decode('7'), repr(d.decode('"a' + chr(1) + 'b"')))
            """);
        Assert.True(compiled.IsValid);
        foreach (var result in new[] { compiled.Run(new MockLythonHost()), await compiled.RunAsync(new MockLythonHost()) })
        {
            Assert.True(result.Success, result.Failure?.Message);
            Assert.Equal("False True True\nTrue 7 'a\\x01b'\nFalse True True\nTrue 7 'a\\x01b'\n", result.StandardOutput);
        }
    }
}
