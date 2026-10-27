using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class NamedTupleConstructionTests
{
    [Fact]
    public async Task BindingDefaultsAndMakeMatchIndependentCpython313()
    {
        const string source = """
            from collections import namedtuple
            P=namedtuple('P','x y',defaults=[[]])
            first=P(1)
            second=P(x=2)
            print(first.y is second.y,first,second)
            for mode in range(4):
                try:
                    if mode==0: P()
                    elif mode==1: P(1,x=2)
                    elif mode==2: P(1,2,3)
                    else: P(z=1)
                except TypeError as e: print(type(e).__name__)
            events=[]
            def values(n):
                for i in range(n):
                    events.append(i)
                    yield i
            for n in [1,2,3]:
                try: print(P._make(values(n)))
                except TypeError as e: print(type(e).__name__,str(e))
            print(events)
            """;
        const string expected = "True P(x=1, y=[]) P(x=2, y=[])\nTypeError\nTypeError\nTypeError\nTypeError\nTypeError Expected 2 arguments, got 1\nP(x=0, y=1)\nTypeError Expected 2 arguments, got 3\n[0, 0, 1, 0, 1, 2]\n";
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
