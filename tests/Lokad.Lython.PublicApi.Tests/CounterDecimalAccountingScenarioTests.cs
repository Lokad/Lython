using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

/// <summary>
/// MG11: accumulated decimal counts on Counter paths stay charged like other
/// small constructed values. Repeated decimal updates must exceed a 512KiB
/// budget in both modes.
/// </summary>
public sealed class CounterDecimalAccountingScenarioTests
{

    [Fact]
    public async Task ManyCounterDecimalUpdatesStayCharged()
    {
        var script = new LythonEngine().Compile(
            """
            import decimal
            from collections import Counter
            one = decimal.Decimal(1)
            c = Counter({"a": one})
            d = {"a": one}
            i = 0
            while i < 10000:
                c.update(d)
                i = i + 1
            return 0
            """);
        Assert.True(script.IsValid);
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = 524288 };
        var sync = script.Run(new MockLythonHost(), options);
        Assert.False(sync.Success);
        Assert.Equal("MemoryError", sync.Failure?.ExceptionType);

        var asyncResult = await script.RunAsync(new MockLythonHost(), options);
        Assert.False(asyncResult.Success);
        Assert.Equal("MemoryError", asyncResult.Failure?.ExceptionType);
    }

    [Fact]
    public async Task CounterDecimalBehaviorsStillProject()
    {
        var script = new LythonEngine().Compile(
            """
            import decimal
            from collections import Counter
            one = decimal.Decimal(1)
            two = decimal.Decimal(2)
            c = Counter({"a": one})
            d = Counter({"a": two})
            vals = []
            vals.append(str((c + d)["a"]))
            vals.append(str((d - c)["a"]))
            vals.append(str(len(-c)))
            vals.append(str(d.total()))
            vals.append(str(len(c)))
            __lython_file = open("/out.txt", "w")
            __lython_file.write("|".join(vals))
            __lython_file.close()
            """);
        Assert.True(script.IsValid);
        var syncHost = new MockLythonHost();
        var sync = script.Run(syncHost);
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal("3|1|0|2|1", syncHost.ReadText("/out.txt"));

        var asyncHost = new MockLythonHost();
        var asyncResult = await script.RunAsync(asyncHost);
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal("3|1|0|2|1", asyncHost.ReadText("/out.txt"));
    }
}

