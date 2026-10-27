using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class NamedTupleReplacementTests
{
    [Fact]
    public async Task ReplacementMatchesCpython313AndReusesTheHostBudgetAfterCleanup()
    {
        // Independent CPython 3.13.2 output; neither API supplies the other's oracle.
        const string source = """
            from collections import namedtuple
            P=namedtuple('P','x y')
            original=P([1],2)
            changed=original._replace(y=3)
            print(original,changed,changed.x is original.x,type(changed) is P,changed is original)
            print(original._replace() is original)
            for bad in [True,False]:
                try:
                    if bad: original._replace(missing=4)
                    else: original._replace(0)
                except TypeError as e: print(type(e).__name__)
            print(original.y,changed.y)
            """;
        const string expected = "P(x=[1], y=2) P(x=[1], y=3) True True False\nFalse\nTypeError\nTypeError\n2 3\n";
        var root = new LythonMemoryBudget(2 * 1024 * 1024);
        using (var script = new LythonEngine().Compile(source, root))
        {
            Assert.True(script.IsValid, string.Join("; ", script.Diagnostics.Select(d => d.Message)));
            var baseline = root.ReservedBytes;
            foreach (var asynchronous in new[] { false, true })
            {
                using (var result = asynchronous ? await script.RunAsync(new MockLythonHost()) : script.Run(new MockLythonHost()))
                {
                    Assert.True(result.Success, result.Failure?.Message);
                    Assert.Equal(expected, result.StandardOutput);
                }
                Assert.Equal(baseline, root.ReservedBytes);
            }
        }
        Assert.Equal(0, root.ReservedBytes);
    }
}
