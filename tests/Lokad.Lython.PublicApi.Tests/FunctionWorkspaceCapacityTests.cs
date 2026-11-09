using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class FunctionWorkspaceCapacityTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task NestedFramesGeneratorsAndDataclassesUseLogicalBindings(bool asynchronous)
    {
        const string source = """
            from dataclasses import dataclass
            @dataclass
            class Record:
                value:int
            def small(x, y='!'):
                with open('value') as source:
                    data=source.read()
                return [sorted(locals()), x+y, data]
            def large(x):
                a0=x;a1=x;a2=x;a3=x;a4=x;a5=x;a6=x;a7=x
                a8=x;a9=x;a10=x;a11=x;a12=x;a13=x;a14=x;a15=x
                return [small(x), [a0,a1,a2,a3,a4,a5,a6,a7]]
            def stream(value):
                yield value
            first=large('a')
            saved=stream(first)
            second=large('b')
            record=Record(3)
            print(next(saved))
            print(second)
            print(str(record),record==Record(3))
            return next(stream('held'))
            """;
        const string expected = "[[['data', 'source', 'x', 'y'], 'a!', 'ok'], ['a', 'a', 'a', 'a', 'a', 'a', 'a', 'a']]\n[[['data', 'source', 'x', 'y'], 'b!', 'ok'], ['b', 'b', 'b', 'b', 'b', 'b', 'b', 'b']]\nRecord(value=3) True\n";
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        using (var script = new LythonEngine().Compile(source, budget))
        {
            Assert.True(script.IsValid, string.Join(';', script.Diagnostics));
            var baseline = budget.ReservedBytes;
            var mock = new MockLythonHost(); mock.SeedFile("/value", "ok");
            var delayed = new DelayedLythonHost(); delayed.SeedFile("/value", "ok");
            using (var result = asynchronous ? await script.RunAsync(delayed) : script.Run(mock))
            {
                Assert.True(result.Success, result.Failure?.Message);
                Assert.Equal(expected, result.StandardOutput);
                Assert.Equal("held", result.ReturnValue);
                Assert.True(budget.ReservedBytes > baseline);
                if (asynchronous) Assert.True(delayed.CompletedAsynchronously > 0);
            }
            if (asynchronous) Assert.Equal(2, delayed.ClosedFileCount);
            Assert.Equal(baseline, budget.ReservedBytes);
        }
        Assert.Equal(0, budget.ReservedBytes);
    }
}
