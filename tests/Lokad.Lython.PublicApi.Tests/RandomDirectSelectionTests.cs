using System.Numerics;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

// N07, part 1: single picks and small takes serve indexed positions without
// draining the whole population. choice/choices keep their exact draws;
// small-k sample draws Floyd positions (documented stream change from the
// full shuffle); larger takes keep the legacy shuffle path and its draws.
public sealed class RandomDirectSelectionTests
{
    private static LythonCompiledScript Compile(string source)
    {
        var script = new LythonEngine().Compile(source);
        Assert.True(script.IsValid, string.Join("|", script.Diagnostics.Select(d => d.Code + ":" + d.Message)));
        return script;
    }

    [Fact]
    public async Task ChoiceThousandCalls_SucceedsUnderSmallBudget()
    {
        var script = Compile("""
            import random
            for i in range(1000):
                random.choice(range(1000))
            return "ok"
            """);
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = 524288 };
        var sync = script.Run(new MockLythonHost(), options);
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal("ok", sync.ReturnValue);
        var asyncResult = await script.RunAsync(new MockLythonHost(), options);
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal("ok", asyncResult.ReturnValue);
    }

    [Fact]
    public async Task ChoiceReseed_ReproducesSequence()
    {
        var script = Compile("""
            import random
            random.seed(123)
            first = [random.choice(["a", "b", "c"]) for _ in range(5)]
            random.seed(123)
            second = [random.choice(["a", "b", "c"]) for _ in range(5)]
            return [first == second, len(first)]
            """);
        Assert.True(script.IsValid, string.Join(" | ", script.Diagnostics.Select(d => d.Code + ": " + d.Message)));
        var expected = new List<object?> { true, new BigInteger(5) };
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);
        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }

    [Fact]
    public async Task ChoicesReseed_ReproducesSequence()
    {
        var script = Compile("""
            import random
            random.seed(9)
            first = random.choices([10, 20, 30], k=4)
            random.seed(9)
            second = random.choices([10, 20, 30], k=4)
            weighted = random.choices([10, 20], [0, 1], None, 2)
            return [first == second, first, weighted]
            """);
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.True(((List<object?>)sync.ReturnValue!)[0] is true);
        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(sync.ReturnValue, asyncResult.ReturnValue);
    }

    [Fact]
    public async Task SampleSmallTake_SucceedsWithoutFullDrain()
    {
        var script = Compile("""
            import random
            random.seed(11)
            r = random.sample(range(100000), 3)
            return [len(r), len(set(r)) == 3, all(0 <= v < 100000 for v in r)]
            """);
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = 1048576 };
        var expected = new List<object?> { new BigInteger(3), true, true };
        var sync = script.Run(new MockLythonHost(), options);
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);
        var asyncResult = await script.RunAsync(new MockLythonHost(), options);
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }

    [Fact]
    public async Task SampleLargeTake_ReseedReproduces()
    {
        var script = Compile("""
            import random
            random.seed(21)
            first = random.sample([1, 2, 3, 4], 2)
            random.seed(21)
            second = random.sample([1, 2, 3, 4], 2)
            return [first == second, len(first)]
            """);
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(new List<object?> { true, new BigInteger(2) }, sync.ReturnValue);
        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(sync.ReturnValue, asyncResult.ReturnValue);
    }

    [Fact]
    public async Task SelectionErrors_KeepContracts()
    {
        var script = Compile("""
            import random
            errors = []
            try:
                random.sample([1, 2, 3], 5)
            except ValueError as e:
                errors.append(str(e))
            try:
                random.choice([])
            except IndexError as e:
                errors.append(str(e))
            return errors
            """);
        var expected = new List<object?>
        {
            "Sample larger than population or is negative",
            "Cannot choose from an empty sequence",
        };
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);
        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }
}
