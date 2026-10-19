using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class LoweredMatchBodyTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task CaseAssignmentsEvaluateTheValueBeforeTargetAndBounds(bool asynchronous, bool slice)
    {
        var source = "events=[]\ndata=[0,0]\n" +
            "def value():events.append('value');return " + (slice ? "[7]" : "7") + "\n" +
            "def target():events.append('target');return data\n" +
            "def index():events.append('index');return 0\n" +
            "def end():events.append('end');return 1\n" +
            "class Owner:\n match 1:\n  case 1:target()[index()" + (slice ? ":end()" : "") + "]=value()\n" +
            "return '|'.join(events)+':'+str(data)";
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        using var script = new LythonEngine().Compile(source, budget);
        Assert.True(script.IsValid, string.Join(";", script.Diagnostics.Select(d => d.Message)));
        var baseline = budget.ReservedBytes;
        using (var result = asynchronous ? await script.RunAsync(new MockLythonHost()) : script.Run(new MockLythonHost()))
        {
            Assert.True(result.Success, result.Failure?.Message);
            Assert.Equal("value|target|index" + (slice ? "|end" : "") + ":[7, 0]", result.ReturnValue);
        }
        Assert.Equal(baseline, budget.ReservedBytes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ImportedCasesKeepGuardsClosuresLoopControlAndFinally(bool asynchronous)
    {
        const string module = """
            events=[]
            def mark(value):
                events.append('subject:'+str(value))
                return value
            def run():
                for i in range(3):
                    try:
                        match mark(i):
                            case n if (seen := n)==0:continue
                            case 1:break
                    finally:
                        events.append('finally:'+str(i))
                else:events.append('wrong-else')
                def captured():return seen
                with open('/payload.txt') as source:text=source.read()
                return '|'.join(events)+';'+str(captured())+';'+text
            """;
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        using var script = new LythonEngine().Compile("import selected\nreturn selected.run()", budget);
        Assert.True(script.IsValid);
        var baseline = budget.ReservedBytes;
        var delayed = asynchronous ? new DelayedLythonHost() : null;
        var mock = asynchronous ? null : new MockLythonHost();
        if (delayed is not null)
        {
            delayed.SeedFile("/selected.py", module);
            delayed.SeedFile("/payload.txt", "payload");
        }
        else
        {
            mock!.SeedFile("/selected.py", module);
            mock.SeedFile("/payload.txt", "payload");
        }
        var options = new LythonRunOptions { AllowedLocalModules = new HashSet<string> { "selected" } };
        using (var result = asynchronous ? await script.RunAsync(delayed!, options) : script.Run(mock!, options))
        {
            Assert.True(result.Success, result.Failure?.Message);
            Assert.Equal("subject:0|finally:0|subject:1|finally:1;1;payload", result.ReturnValue);
            if (delayed is not null) Assert.True(delayed.CompletedAsynchronously > 0);
        }
        Assert.Equal(baseline, budget.ReservedBytes);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task CaseFailureCannotResumeGuestWorkAndReleasesItsOwner(bool asynchronous, bool refusal)
    {
        var source = "try:\n class Owner:\n  match 1:\n   case 1:\n    print('entered')\n    " +
            (refusal ? "value='x'*1000000" : "while True:pass") +
            "\nexcept BaseException:print('caught')\nprint('after')";
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        using var script = new LythonEngine().Compile(source, budget);
        Assert.True(script.IsValid);
        var baseline = budget.ReservedBytes;
        using var cancellation = new CancellationTokenSource();
        var host = new MockLythonHost();
        if (!refusal) host.OnStandardOutputWrite = () => cancellation.Cancel();
        var options = new LythonRunOptions { CancellationToken = cancellation.Token, MemoryBudget = new LythonMemoryBudget(65536) };
        var pending = asynchronous ? script.RunAsync(host, options)
            : Task.Factory.StartNew(() => script.Run(host, options), CancellationToken.None,
                TaskCreationOptions.LongRunning, TaskScheduler.Default);
        LythonExecutionResult? result = null;
        try
        {
            result = await pending.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.False(result.Success);
            Assert.Equal(refusal ? LythonFailureReason.SharedMemoryAllowance : LythonFailureReason.Cancelled,
                result.FailureAttribution?.Reason);
            Assert.StartsWith("entered", result.StandardOutput);
            Assert.DoesNotContain("caught", result.StandardOutput);
            Assert.DoesNotContain("after", result.StandardOutput);
            if (refusal) Assert.True(result.DeniedReservationBytes > 0);
        }
        finally
        {
            cancellation.Cancel();
            result ??= await pending.WaitAsync(TimeSpan.FromSeconds(5));
            result.Dispose();
        }
        Assert.Equal(baseline, budget.ReservedBytes);
    }
}
