using System.Numerics;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class CounterHashabilityTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CounterUsesDictionaryHashabilityWithoutChangingNumericTwins(bool asynchronous)
    {
        var script = new LythonEngine(Utf8RegexProvider.Instance).Compile("""
            from collections import Counter
            def count(ctor, values):
                try:
                    ctor(values)
                except TypeError:
                    return True
                return False
            for bad in ([], {}, set(), ([],)):
                assert count(Counter, [bad])
                c = Counter()
                try:
                    c.update([bad])
                    raise AssertionError('unhashable update accepted')
                except TypeError:
                    pass
                try:
                    c.subtract([bad])
                    raise AssertionError('unhashable subtract accepted')
                except TypeError:
                    pass
            c = Counter([True, 1, False, 0, (1,), (1,)])
            assert c[True] == 2 and c[0] == 2 and c[(1,)] == 2
            return len(c)
            """);
        Assert.True(script.IsValid, string.Join("|", script.Diagnostics));
        var result = asynchronous ? await script.RunAsync(new MockLythonHost()) : script.Run(new MockLythonHost());
        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal(new BigInteger(3), result.ReturnValue);
    }
}
