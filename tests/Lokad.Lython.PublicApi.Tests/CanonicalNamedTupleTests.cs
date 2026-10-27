using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class CanonicalNamedTupleTests
{
    public static TheoryData<string, string> AdmissionCases => new()
    {
        { "render", "T=NamedTuple('T',[('x',str)])\nt=T('x'*262144)\ntime.time()\nr=repr(t)" },
        { "factory", "fields=[('f'+str(i),int) for i in range(4096)]\ntime.time()\nT=NamedTuple('T',fields)" },
        { "replace", "T=NamedTuple('T',[('f'+str(i),int) for i in range(8192)])\nt=T(*([0]*8192))\ntime.time()\nr=t._replace()" },
    };

    [Theory, MemberData(nameof(AdmissionCases))]
    public async Task SavedStorageWitnessesRefuseAndReturnTheSharedBudget(string name, string operation)
    {
        using var script = new LythonEngine().Compile("import time\nfrom typing import NamedTuple\n" + operation);
        Assert.True(script.IsValid, name);
        foreach (var asynchronous in new[] { false, true })
        {
            var budget = new AdmissionGateBudget();
            var host = new AdmissionGateHost(budget, 32768);
            using (var result = asynchronous ? await script.RunAsync(host, new LythonRunOptions { MemoryBudget = budget })
                : script.Run(host, new LythonRunOptions { MemoryBudget = budget }))
            {
                Assert.False(result.Success, name);
                Assert.Equal(LythonFailureReason.SharedMemoryAllowance, result.FailureAttribution?.Reason);
                Assert.True(budget.AtGate > 0);
            }
            Assert.Equal(0, budget.Root.ReservedBytes);
        }
    }

    [Fact]
    public async Task FunctionalClassAndCollectionsFormsMatchIndependentCpython313()
    {
        const string source = """
            from typing import NamedTuple
            from collections import namedtuple
            class C(NamedTuple):
                x: int
                y: str = 'default'
            F=NamedTuple('F',[('x',int),('y',str)])
            N=namedtuple('N','x y')
            for T in [C,F,N]:
                p=T(1,'a')
                print(type(p) is T,isinstance(p,T),isinstance(p,tuple),p==(1,'a'),hash(p)==hash((1,'a')))
                print(p,p._replace(x=2),T._make([3,'b']),p._asdict(),T._fields is p._fields)
                for mode in range(2):
                    try:
                        if mode==0: T(1,x=2)
                        else: T()
                    except TypeError as e: print(type(e).__name__)
                annotations=T.__annotations__
                print(annotations is T.__annotations__,annotations is T.__new__.__annotations__)
                if T is not N: print(annotations['x'] is int,annotations['y'] is str)
                annotations['extra']=42
                print(T.__annotations__['extra'])
            print(C(4))
            for fields in [[('x',int),('x',str)],[('_x',int)],[('for',int)],[('bad name',int)],[('x',)],[('x',int,str)]]:
                try: NamedTuple('Bad',fields)
                except (ValueError,TypeError) as e: print(type(e).__name__)
            events=[]
            class Fields:
                def __iter__(self):
                    events.append('iter')
                    yield ('x',int)
            R=NamedTuple('R',Fields())
            print(events,R.__annotations__['x'] is int)
            def once():
                yield ('x',int)
            G=NamedTuple('G',once())
            print(G(5),G.__annotations__)
            E=NamedTuple('E',[])
            print(E(),E.__annotations__)
            """ + "\n";
        const string expected = """
            True True True True True
            C(x=1, y='a') C(x=2, y='a') C(x=3, y='b') {'x': 1, 'y': 'a'} True
            TypeError
            TypeError
            True True
            True True
            42
            True True True True True
            F(x=1, y='a') F(x=2, y='a') F(x=3, y='b') {'x': 1, 'y': 'a'} True
            TypeError
            TypeError
            True True
            True True
            42
            True True True True True
            N(x=1, y='a') N(x=2, y='a') N(x=3, y='b') {'x': 1, 'y': 'a'} True
            TypeError
            TypeError
            True False
            42
            C(x=4, y='default')
            ValueError
            ValueError
            ValueError
            ValueError
            ValueError
            ValueError
            ['iter', 'iter'] True
            G(x=5) {}
            E() {}
            """ + "\n";
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
                    Assert.Equal(expected.ReplaceLineEndings("\n"), result.StandardOutput);
                }
                Assert.Equal(baseline, root.ReservedBytes);
            }
        }
        Assert.Equal(0, root.ReservedBytes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task EvaluatedAnnotationAliasesStayFundedUntilResultCleanup(bool classForm)
    {
        var declaration = classForm ? "class P(NamedTuple):\n x: annotation\n" : "P=NamedTuple('P',[('x',annotation)])\n";
        var root = new LythonMemoryBudget(2 * 1024 * 1024);
        using (var script = new LythonEngine().Compile("from typing import NamedTuple\nannotation=['x'*32768]\n" + declaration + """
            metadata=P.__annotations__
            assert metadata['x'] is annotation
            assert metadata is P.__annotations__
            assert metadata is P.__new__.__annotations__
            metadata['extra']=annotation
            assert metadata['extra'] is annotation
            return [metadata,metadata['extra']]
            """, root))
        {
            Assert.True(script.IsValid, string.Join("; ", script.Diagnostics.Select(d => d.Message)));
            var baseline = root.ReservedBytes;
            foreach (var asynchronous in new[] { false, true })
            {
                using (var result = asynchronous ? await script.RunAsync(new MockLythonHost()) : script.Run(new MockLythonHost()))
                {
                    Assert.True(result.Success, result.Failure?.Message);
                    var values = Assert.IsType<List<object?>>(result.ReturnValue);
                    var map = Assert.IsType<Dictionary<object, object?>>(values[0]);
                    // Projection materializes CLR containers; guest identity is
                    // asserted above, before crossing that value boundary.
                    foreach (var value in new[] { map["x"], map["extra"], values[1] })
                    {
                        var annotation = Assert.IsType<List<object?>>(value);
                        Assert.Equal(new string('x', 32768), Assert.Single(annotation));
                    }
                    Assert.True(root.ReservedBytes >= baseline + 32768);
                }
                Assert.Equal(baseline, root.ReservedBytes);
            }
        }
        Assert.Equal(0, root.ReservedBytes);
    }

    [Fact]
    public async Task FieldsAwaitHostIoWithoutLosingAnnotationsOrCleanup()
    {
        const string source = """
            from typing import NamedTuple
            class Fields:
                def __iter__(self):
                    with open('/field') as source: name=source.read()
                    yield (name,int)
            P=NamedTuple('P',Fields())
            print(P(7),P.__annotations__['x'] is int)
            """;
        var root = new LythonMemoryBudget(2 * 1024 * 1024);
        using (var script = new LythonEngine().Compile(source, root))
        {
            Assert.True(script.IsValid, string.Join("; ", script.Diagnostics.Select(d => d.Message)));
            var baseline = root.ReservedBytes;
            var host = new DelayedLythonHost();
            host.SeedFile("/field", "x");
            using (var result = await script.RunAsync(host))
            {
                Assert.True(result.Success, result.Failure?.Message);
                Assert.Equal("P(x=7) True\n", result.StandardOutput);
                Assert.True(host.CompletedAsynchronously >= 2);
            }
            Assert.Equal(baseline, root.ReservedBytes);
        }
        Assert.Equal(0, root.ReservedBytes);
    }
}
