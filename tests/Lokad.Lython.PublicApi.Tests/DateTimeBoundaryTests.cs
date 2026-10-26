using System.Text.Json;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class DateTimeBoundaryTests
{
    // Independent CPython 3.13.2 goldens, including malformed input controls.
    public static IEnumerable<object[]> Cases()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", "DateTimeBoundary", "cases.json");
        foreach (var item in JsonSerializer.Deserialize<BoundaryCase[]>(File.ReadAllText(path))!)
            yield return [item.Name, item];
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task NativeValuesFollowPythonContracts(string name, BoundaryCase item)
    {
        Assert.Equal(name, item.Name);
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        using (var script = new LythonEngine().Compile(item.Source, budget))
        {
            Assert.True(script.IsValid);
            var baseline = budget.ReservedBytes;
            foreach (var asynchronous in new[] { false, true })
            {
                var mock = new MockLythonHost();
                var delayed = new DelayedLythonHost();
                if (item.Input is not null)
                {
                    mock.SeedFile("/value", item.Input);
                    delayed.SeedFile("/value", item.Input);
                }
                using (var result = asynchronous ? await script.RunAsync(delayed) : script.Run(mock))
                {
                    Assert.True(result.Success, result.Failure?.Message);
                    Assert.Equal(item.StandardOutput, result.StandardOutput);
                }
                Assert.Equal(baseline, budget.ReservedBytes);
                if (asynchronous && item.Input is not null)
                    Assert.True(delayed.CompletedAsynchronously > 0);
            }
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Theory]
    [InlineData("time")]
    [InlineData("datetime")]
    public async Task LongIsoFractionsUseBoundedWorkingStorage(string type)
    {
        var prefix = type == "datetime" ? "2024-01-02T" : "";
        using var script = new LythonEngine().Compile("import datetime,time\npayload='" + prefix +
            "03:04:05.'+'1'*262144\ntime.time()\nvalue=datetime." + type +
            ".fromisoformat(payload)\nreturn value.microsecond");
        foreach (var asynchronous in new[] { false, true })
        {
            var budget = new AdmissionGateBudget();
            var host = new AdmissionGateHost(budget, 32768);
            using (var result = asynchronous ? await script.RunAsync(host, new LythonRunOptions { MemoryBudget = budget })
                : script.Run(host, new LythonRunOptions { MemoryBudget = budget }))
            {
                Assert.True(result.Success, result.Failure?.Message);
                Assert.Equal(new System.Numerics.BigInteger(111111), result.ReturnValue);
            }
            Assert.Equal(0, budget.Root.ReservedBytes);
        }
    }

    [Theory]
    [InlineData("str(z)", true)]
    [InlineData("z.tzname(None)", true)]
    [InlineData("repr(z)", false)]
    [InlineData("repr(datetime.time(tzinfo=z))", false)]
    [InlineData("repr(datetime.datetime(2024,1,2,tzinfo=z))", false)]
    public async Task TimezoneNamesAliasFundedStorageAndAdmitNewOutput(string expression, bool succeeds)
    {
        using var script = new LythonEngine().Compile("import datetime,time\nlabel='x'*262144\n" +
            "z=datetime.timezone(datetime.timedelta(seconds=1),label)\ntime.time()\nvalue=" + expression + "\nreturn value is label");
        foreach (var asynchronous in new[] { false, true })
        {
            var budget = new AdmissionGateBudget();
            var host = new AdmissionGateHost(budget, 32768);
            using (var result = asynchronous ? await script.RunAsync(host, new LythonRunOptions { MemoryBudget = budget })
                : script.Run(host, new LythonRunOptions { MemoryBudget = budget }))
            {
                Assert.Equal(succeeds, result.Success);
                if (succeeds) Assert.Equal(true, result.ReturnValue);
                else Assert.Equal(LythonFailureReason.SharedMemoryAllowance, result.FailureAttribution?.Reason);
            }
            Assert.Equal(0, budget.Root.ReservedBytes);
        }
    }

    [Fact]
    public async Task CalendarTuplesKeepMethodsAndProjectThroughThePublicBoundary()
    {
        using var script = new LythonEngine().Compile("import datetime\na=datetime.date(2024,1,2).isocalendar()\nassert a != 2024 and a != [2024,1,2]\nassert a.count(1)==1 and a.index(2)==2\nassert a+(3,)==(2024,1,2,3) and a*2==(2024,1,2,2024,1,2)\nreturn a");
        using var result = await script.RunAsync(new MockLythonHost());
        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal(new System.Numerics.BigInteger[] { 2024, 1, 2 }, Assert.IsAssignableFrom<IEnumerable<object>>(result.ReturnValue).Cast<System.Numerics.BigInteger>());
    }

    [Fact]
    public async Task RefusedConversionRemainsTerminalAfterSuspension()
    {
        using var script = new LythonEngine().Compile("import datetime,time\nclass Index:\n def __index__(self):\n  with open('value') as f: value=int(f.read())\n  time.time()\n  try: payload='x'*262144\n  finally: return value\nvalue=datetime.date(Index(),1,2)\nprint('after')");
        Assert.True(script.IsValid);
        var budget = new AdmissionGateBudget();
        var delayed = new DelayedLythonHost();
        delayed.SeedFile("/value", "2024");
        var host = new AdmissionGateHost(budget, 32768, delayed);
        using (var result = await script.RunAsync(host, new LythonRunOptions { MemoryBudget = budget }))
        {
            Assert.False(result.Success);
            Assert.Equal(LythonFailureReason.SharedMemoryAllowance, result.FailureAttribution?.Reason);
            Assert.Equal("", result.StandardOutput);
            Assert.True(delayed.CompletedAsynchronously > 0);
        }
        Assert.Equal(0, budget.Root.ReservedBytes);
    }

    [Fact]
    public async Task ConversionCancellationAwaitsCleanupAndReusesBudget()
    {
        using var script = new LythonEngine().Compile("import datetime\nclass Index:\n def __index__(self):\n  with open('value') as f:return int(f.read())\nreturn datetime.date(Index(),1,2).year");
        Assert.True(script.IsValid);
        var budget = new LythonMemoryBudget(1024 * 1024);
        var delayed = new DelayedLythonHost();
        delayed.SeedFile("/value", "2024");
        var reached = delayed.PauseReadUntilCancellation("/value");
        using var cancellation = new CancellationTokenSource();
        var pending = script.RunAsync(delayed, new LythonRunOptions { MemoryBudget = budget, CancellationToken = cancellation.Token });
        await reached.WaitAsync(TimeSpan.FromSeconds(10));
        cancellation.Cancel();
        using (var result = await pending)
        {
            Assert.False(result.Success);
            Assert.Equal(LythonFailureReason.Cancelled, result.FailureAttribution?.Reason);
        }
        Assert.Equal(0, budget.ReservedBytes);
        var immediate = new MockLythonHost();
        immediate.SeedFile("/value", "2024");
        using (var result = script.Run(immediate, new LythonRunOptions { MemoryBudget = budget }))
            Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal(0, budget.ReservedBytes);
    }

    public sealed record BoundaryCase(string Name, string Source, string StandardOutput, string? Input);
}
