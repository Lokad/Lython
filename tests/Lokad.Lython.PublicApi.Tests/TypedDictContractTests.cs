using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class TypedDictContractTests
{
    [Theory]
    [InlineData("from typing import TypedDict\nT=TypedDict('T',{'x':int},total=1)", "NotImplementedError")]
    [InlineData("from typing import TypedDict\nclass T(TypedDict,total=1):\n x:int", "NotImplementedError")]
    [InlineData("from typing import TypedDict\nclass T(TypedDict,unknown=True):\n x:int", "TypeError")]
    [InlineData("from typing import TypedDict\nclass T(TypedDict,object):\n x:int", "TypeError")]
    [InlineData("from typing import TypedDict\nclass T(TypedDict):\n def method(self):return 1", "TypeError")]
    [InlineData("from typing import TypedDict\nT=TypedDict('T',{'x':int})\nT.__required_keys__", "NotImplementedError")]
    [InlineData("from typing import TypedDict\nT=TypedDict('T',{'x':int})\nT.__optional_keys__", "NotImplementedError")]
    public async Task UnsupportedDeclarationsAndInspectionFailExplicitly(string source, string error)
    {
        var root = new LythonMemoryBudget(2 * 1024 * 1024);
        using (var script = new LythonEngine().Compile(source,root))
        {
            Assert.True(script.IsValid,string.Join("; ",script.Diagnostics.Select(d=>d.Message)));
            var baseline = root.ReservedBytes;
            foreach (var asynchronous in new[] { false,true })
            {
                using (var result = asynchronous ? await script.RunAsync(new MockLythonHost()) : script.Run(new MockLythonHost()))
                {
                    Assert.False(result.Success);
                    Assert.Equal(error,result.Failure?.ExceptionType);
                }
                Assert.Equal(baseline,root.ReservedBytes);
            }
        }
        Assert.Equal(0,root.ReservedBytes);
    }

    [Fact]
    public async Task RuntimeSchemaChecksRaiseTypeErrorAsInCpython313()
    {
        const string source = """
            from typing import TypedDict
            T=TypedDict('T',{'x':int})
            for mode in range(3):
                try:
                    if mode==0: isinstance({},T)
                    elif mode==1: issubclass(dict,T)
                    else:
                        match {}:
                            case T(): pass
                except TypeError as e: print(type(e).__name__)
            """;
        using var script = new LythonEngine().Compile(source);
        Assert.True(script.IsValid);
        foreach (var asynchronous in new[] { false,true })
        {
            using var result = asynchronous ? await script.RunAsync(new MockLythonHost()) : script.Run(new MockLythonHost());
            Assert.True(result.Success,result.Failure?.Message);
            Assert.Equal("TypeError\nTypeError\nTypeError\n",result.StandardOutput);
        }
    }

    [Fact]
    public async Task DeclarationsAndDictConstructionMatchIndependentCpython313()
    {
        const string source = """
            from typing import TypedDict
            marker=[]
            source={'x':marker,'name':str}
            F=TypedDict('F',source)
            class C(TypedDict):
                x: marker
                name: str
            class O(TypedDict,total=False):
                x: int
            for T in [F,C,O]:
                print(T.__name__,T.__total__,T.__annotations__ is T.__annotations__)
                print(type(T()) is dict,T(),T(x='unchecked',extra=3),T([('x',1)],x=2))
                try:T({'x':1},{'y':2})
                except TypeError as e: print(type(e).__name__)
                annotations=T.__annotations__
                if T is not O: print(annotations['x'] is marker)
                annotations['extra']=marker
                print(T.__annotations__['extra'] is marker)
            source['later']=int
            print('later' in F.__annotations__)
            events=[]
            def pairs():
                events.append('first')
                yield ('x',1)
                events.append('tail')
                yield ('extra',2)
            print(F(pairs()),events)
            """;
        const string expected = "F True True\nTrue {} {'x': 'unchecked', 'extra': 3} {'x': 2}\nTypeError\nTrue\nTrue\nC True True\nTrue {} {'x': 'unchecked', 'extra': 3} {'x': 2}\nTypeError\nTrue\nTrue\nO False True\nTrue {} {'x': 'unchecked', 'extra': 3} {'x': 2}\nTypeError\nTrue\nFalse\n{'x': 1, 'extra': 2} ['first', 'tail']\n";
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

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DelayedPairIterationAndRetainedMetadataKeepTheirOwners(bool classForm)
    {
        var declaration = classForm ? "class T(TypedDict):\n x:original\n" : "T=TypedDict('T',{'x':original})\n";
        var source = "from typing import TypedDict\noriginal=['x'*32768]\n" + declaration + """
            metadata=T.__annotations__
            assert metadata is T.__annotations__
            assert metadata['x'] is original
            metadata['extra']=original
            assert T.__annotations__['extra'] is original
            def pairs():
                with open('/value') as source: value=int(source.read())
                yield ('x',value)
            print(T(pairs(),extra=3))
            return metadata['extra']
            """;
        var root = new LythonMemoryBudget(2 * 1024 * 1024);
        using (var script = new LythonEngine().Compile(source,root))
        {
            Assert.True(script.IsValid,string.Join("; ",script.Diagnostics.Select(d=>d.Message)));
            var baseline=root.ReservedBytes;
            foreach(var asynchronous in new[]{false,true})
            {
                var immediate=new MockLythonHost();immediate.SeedFile("/value","7");
                var delayed=new DelayedLythonHost();delayed.SeedFile("/value","7");
                using(var result=asynchronous?await script.RunAsync(delayed):script.Run(immediate))
                {
                    Assert.True(result.Success,result.Failure?.Message);
                    Assert.Equal("{'x': 7, 'extra': 3}\n",result.StandardOutput);
                    Assert.Equal(new string('x',32768),Assert.Single(Assert.IsType<List<object?>>(result.ReturnValue)));
                    Assert.True(root.ReservedBytes >= baseline+32768);
                    if(asynchronous)Assert.True(delayed.CompletedAsynchronously>0);
                }
                Assert.Equal(baseline,root.ReservedBytes);
            }
        }
        Assert.Equal(0,root.ReservedBytes);
    }

    [Fact]
    public async Task SavedFactoryWitnessRequiresSharedAdmission()
    {
        using var script = new LythonEngine().Compile("""
            import time
            from typing import TypedDict
            fields={'f'+str(i):int for i in range(4096)}
            time.time()
            T=TypedDict('T',fields)
            """);
        Assert.True(script.IsValid);
        foreach (var asynchronous in new[] { false, true })
        {
            var budget = new AdmissionGateBudget();
            var host = new AdmissionGateHost(budget,32768);
            using (var result = asynchronous ? await script.RunAsync(host,new LythonRunOptions { MemoryBudget=budget })
                : script.Run(host,new LythonRunOptions { MemoryBudget=budget }))
            {
                Assert.False(result.Success);
                Assert.Equal(LythonFailureReason.SharedMemoryAllowance,result.FailureAttribution?.Reason);
            }
            Assert.Equal(0,budget.Root.ReservedBytes);
        }
    }
}
