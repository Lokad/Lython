using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class UnpackedKeywordCollisionTests
{
    [Fact]
    public async Task ConstructorsRejectCollidingKeywordsButAllowSourceOverrides()
    {
        var compiled = new LythonEngine().Compile("""
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
}
