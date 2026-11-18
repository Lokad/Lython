using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class UnpackedKeywordCollisionTests
{
    [Fact]
    public async Task ExpandedNamesMatchExactUnicodeAndPreserveOverflowKeyIdentity()
    {
        using var script = new LythonEngine().Compile("""
            class Receiver:
                def f(self, name, /, *, ζ='last', café='default', a='first', **kw):
                    print(name, a, café, ζ, list(kw.values()), next(iter(kw)) is unmatched)

            unmatched = ''.join(['cafe', '\u0301'])
            values = {'café': 'matched', unmatched: 42, 'name': 5, 'a': 'start', 'ζ': 'end'}
            Receiver().f('bound', **values)
            Receiver().f('bound', **{unmatched: 1})
            """);
        Assert.True(script.IsValid, string.Join("; ", script.Diagnostics.Select(d => d.Message)));
        foreach (var asynchronous in new[] { false, true })
        {
            using var result = asynchronous
                ? await script.RunAsync(new MockLythonHost())
                : script.Run(new MockLythonHost());
            Assert.True(result.Success, result.Failure?.Message);
            Assert.Equal("bound start matched end [42, 5] True\nbound first default last [1] True\n",
                result.StandardOutput);
        }
    }

    [Fact]
    public async Task ConstructorsRejectCollidingKeywordsButAllowSourceOverrides()
    {
        var compiled = new LythonEngine(Utf8RegexProvider.Instance).Compile("""
            import collections
            for call in [
                lambda: dict(x=1, **{'x': 2}),
                lambda: dict(**{'x': 1}, x=2),
                lambda: dict(**{'x': 1}, **{'x': 2}),
                lambda: collections.Counter(x=1, **{'x': 2}),
            ]:
                try:
                    call()
                    print('merged')
                except TypeError:
                    print('collision')
            print(dict({'x': 1}, x=2))
            print(dict(**{'x': 1}, y=2))
            """);
        Assert.True(compiled.IsValid, string.Join("; ", compiled.Diagnostics.Select(d => d.Message)));
        foreach (var result in new[] { compiled.Run(new MockLythonHost()), await compiled.RunAsync(new MockLythonHost()) })
        {
            Assert.True(result.Success, result.Failure?.Message);
            Assert.Equal("collision\ncollision\ncollision\ncollision\n{'x': 2}\n{'x': 1, 'y': 2}\n", result.StandardOutput);
        }
    }

    [Fact]
    public async Task ExactDictionaryPrefixChecksLaterMappingsBeforeReadingDuplicateItems()
    {
        using var compiled = new LythonEngine().Compile("""
            class Later:
                def __init__(self, keys):
                    self.names = keys
                def keys(self):
                    print('keys')
                    return self.names
                def __getitem__(self, name):
                    print('read', name)
                    return 3

            def forward(*args, **kwargs):
                print(args, list(kwargs), list(kwargs)[0] is original)

            original = ''.join(['café', '_key'])
            prefix = {original: 1}
            try:
                forward(5, **prefix, **Later(['extra', 'café_key']))
            except TypeError:
                print('collision')
            forward(6, **prefix, **Later(['extra']))
            """);
        Assert.True(compiled.IsValid, string.Join("; ", compiled.Diagnostics.Select(d => d.Message)));
        foreach (var asynchronous in new[] { false, true })
        {
            using var result = asynchronous
                ? await compiled.RunAsync(new MockLythonHost())
                : compiled.Run(new MockLythonHost());
            Assert.True(result.Success, result.Failure?.Message);
            Assert.Equal("keys\nread extra\ncollision\nkeys\nread extra\n(6,) ['café_key', 'extra'] True\n",
                result.StandardOutput);
        }
    }
}
