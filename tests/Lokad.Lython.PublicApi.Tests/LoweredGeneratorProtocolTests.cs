using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class LoweredGeneratorProtocolTests
{
    public static TheoryData<string, string> Programs => new()
    {
        { "class Holder:\n values=(x+1 for x in range(3))\ng=Holder.values\nprint(callable(g.send),callable(g.throw),callable(g.close))\nprint(g.send(None))\ng.close()\nprint(list(g))", "True True True\n1\n[]\n" },
        { "class Holder:\n values=(x+1 for x in range(3))\ng=Holder.values\nprint(g.send(None))\ntry:g.throw(ValueError('stop'))\nexcept ValueError as error:print(str(error))\nprint(list(g))", "1\nstop\n[]\n" },
        { "def make():\n scale=10\n class Holder:\n  scale=99\n  values=(scale+x for x in range(2))\n scale=20\n return Holder.values\ng=make()\nprint(g.send(None),next(g))", "20 21\n" },
        { "scale=3\nclass Holder:\n scale=99\n values=(scale+x for x in range(2))\nprint(Holder.values.send(None),next(Holder.values))", "3 4\n" },
        { "class Holder:\n values=(((outer,inner) for inner in range(2)) for outer in range(3))\nfirst=next(Holder.values)\nsecond=next(Holder.values)\nprint(first.send(None),second.send(None))\nfirst.close()\nsecond.close()\nHolder.values.close()", "(1, 0) (1, 0)\n" },
        { "events=[]\nclass Source:\n def __iter__(self):\n  events.append('iter')\n  return iter((2,4))\nclass Holder:\n values=(value*3 for value in Source())\nevents.append('made')\nprint(events)\nprint(Holder.values.send(None))\nHolder.values.close()", "['iter', 'made']\n6\n" },
        { "class Holder:\n marker:(value+1 for value in range(2))\ng=Holder.__annotations__['marker']\nprint(g.send(None))\ng.close()\nprint(list(g))", "1\n[]\n" },
    };

    [Theory]
    [MemberData(nameof(Programs))]
    public async Task ClassAndAnnotationGeneratorsKeepPythonProtocolsAndScopes(string source, string expected)
    {
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        using (var script = new LythonEngine().Compile(source, budget))
        {
            Assert.True(script.IsValid, string.Join("; ", script.Diagnostics));
            var baseline = budget.ReservedBytes;
            using (var result = script.Run(new MockLythonHost()))
            {
                Assert.True(result.Success, result.Failure?.Message);
                Assert.Equal(expected, result.StandardOutput);
            }
            Assert.Equal(baseline, budget.ReservedBytes);
            using (var result = await script.RunAsync(new MockLythonHost()))
            {
                Assert.True(result.Success, result.Failure?.Message);
                Assert.Equal(expected, result.StandardOutput);
            }
            Assert.Equal(baseline, budget.ReservedBytes);
        }
        Assert.Equal(0, budget.ReservedBytes);
    }
}
