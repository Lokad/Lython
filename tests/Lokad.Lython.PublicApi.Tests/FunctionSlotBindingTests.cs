using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class FunctionSlotBindingTests
{
    [Theory]
    [InlineData(4)]
    [InlineData(65)]
    [InlineData(130)]
    public async Task NoneOmissionMutableDefaultsAndFailedBindsStayIndependent(int count)
    {
        var parameters = string.Join(",", Enumerable.Range(0, count).Select(i => $"p{i}={i}"));
        var last = count - 1;
        var source = $"""
            def f({parameters}, *, option='default'):
                return p0, p{last}, option
            print(f(None, p{last}=None, option=None))
            print(f())
            try: f(*range({count}), p0=None)
            except TypeError: print('duplicate')
            f.__defaults__ = None
            try: f()
            except TypeError: print('missing')
            f.__defaults__ = tuple(range({count}))
            f.__kwdefaults__ = dict(option=None)
            print(f())
            """;
        var expected = $"(None, None, None)\n(0, {last}, 'default')\nduplicate\nmissing\n(0, {last}, None)\n";
        var budget = new LythonMemoryBudget(8 * 1024 * 1024);
        using (var script = new LythonEngine().Compile(source, budget))
        {
            Assert.True(script.IsValid, string.Join(';', script.Diagnostics.Select(d => d.Message)));
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
        }
        Assert.Equal(0, budget.ReservedBytes);
    }
}
