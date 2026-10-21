using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class OptionTruthProtocolTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FlagsRetainPythonTruthEffectsAndEvaluationPoints(bool asynchronous)
    {
        using var script = new LythonEngine().Compile("""
            import shlex,statistics
            effects=[]
            class F:
                def __bool__(self): effects.append('flag'); return False
            print(shlex.split('a #b',comments=F()))
            print(len(effects))
            effects.clear()
            print(shlex.split("'a b'",posix=F()))
            print(len(effects))
            effects.clear()
            r=statistics.linear_regression([1,2,3],[3,5,7],proportional=F())
            print(r,len(effects))
            manual=statistics.LinearRegression(2,1)
            print(manual,manual==statistics.LinearRegression(2.,1.),hash(manual)==hash(statistics.LinearRegression(2.,1.)))
            print(type(manual.slope).__name__,type(r.slope).__name__)
            """);
        Assert.True(script.IsValid);
        using var result = asynchronous ? await script.RunAsync(new MockLythonHost()) : script.Run(new MockLythonHost());
        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("['a', '#b']\n1\n[\"'a b'\"]\n10\nLinearRegression(slope=2.0, intercept=1.0) 2\nLinearRegression(slope=2, intercept=1) True True\nint float\n", result.StandardOutput);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RegressionRetestsMutableProportionalAndPreservesItsErrors(bool asynchronous)
    {
        using var script = new LythonEngine().Compile("""
            import statistics
            class Switch:
                def __init__(self,values): self.values=iter(values)
                def __bool__(self): print('flag'); return next(self.values)
            print(statistics.linear_regression([1,2,3],[3,5,7],proportional=Switch([False,True])))
            try: statistics.linear_regression([1,2,3],[3,5,7],proportional=Switch([True,False]))
            except UnboundLocalError: print('unbound')
            class Bad:
                def __bool__(self): raise ValueError('bad flag')
            try: statistics.linear_regression([1,2],[3,5],proportional=Bad())
            except ValueError as e: print(str(e))
            """);
        using var result = asynchronous ? await script.RunAsync(new MockLythonHost()) : script.Run(new MockLythonHost());
        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("flag\nflag\nLinearRegression(slope=2.0, intercept=0.0)\nflag\nflag\nunbound\nbad flag\n", result.StandardOutput);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FirstRegressionTruthRunsBeforeReadingInputValues(bool asynchronous)
    {
        using var script = new LythonEngine().Compile("""
            import statistics
            x=[1.,2.,3.]; y=[3.,5.,7.]
            effects=[]
            class Flag:
                def __bool__(self):
                    if not effects: x[0]=2.; y[0]=10.
                    effects.append('flag')
                    return False
            r=statistics.linear_regression(x,y,proportional=Flag())
            print(round(r.slope,6),round(r.intercept,6),effects)
            """);
        using var result = asynchronous ? await script.RunAsync(new MockLythonHost()) : script.Run(new MockLythonHost());
        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("-0.5 8.5 ['flag', 'flag']\n", result.StandardOutput);
    }

    public static TheoryData<string, string> AsyncRoutes => new()
    {
        { "return shlex.split(\"'a b'\",posix=Flag())", "/marker" },
        { "return shlex.split('a #b',comments=Flag())", "/marker" },
        { "return list(shlex.shlex(\"'a b'\",posix=Flag()))", "/marker" },
        { "lexer=shlex.shlex(\"'a b'\")\nlexer.posix=Flag()\nreturn lexer.get_token()", "/marker" },
        { "lexer=shlex.shlex(\"'a b'\")\nlexer.posix=Flag()\nreturn lexer.read_token()", "/marker" },
        { "return statistics.linear_regression([1,2,3],[3,5,7],proportional=Flag())", "/marker" },
        { "return statistics.linear_regression(range(2000),range(2000),proportional=LaterFlag()).slope", "/marker" },
        { "with open('/input') as source:\n return list(shlex.shlex(source,posix=True))", "/input" },
    };

    [Theory]
    [MemberData(nameof(AsyncRoutes))]
    public async Task FlagAndLexerInputIoSuspendWithFundedOwnersUntilCleanup(string statement, string marker)
    {
        var source = """
            import shlex,statistics
            class Flag:
                def __bool__(self):
                    with open('/marker') as source: source.read()
                    return False
            class LaterFlag:
                def __init__(self): self.calls=0
                def __bool__(self):
                    self.calls+=1
                    if self.calls==2:
                        with open('/marker') as source: source.read()
                    return False
            """ + "\n" + statement;
        var budget = new LythonMemoryBudget(1024 * 1024);
        using var script = new LythonEngine().Compile(source, budget);
        Assert.True(script.IsValid);
        var baseline = budget.ReservedBytes;
        foreach (var cancel in new[] { false, true })
        {
            var host = new DelayedLythonHost();
            host.SeedFile("/marker", "data");
            host.SeedFile("/input", "one 'two words' three");
            var entered = cancel ? host.PauseReadUntilCancellation(marker) : null;
            using var cancellation = new CancellationTokenSource();
            var pending = script.RunAsync(host, cancellationToken: cancellation.Token);
            try
            {
                if (entered is not null)
                {
                    var first = await Task.WhenAny(entered, pending).WaitAsync(TimeSpan.FromSeconds(5));
                    Assert.Same(entered, first);
                    Assert.False(pending.IsCompleted);
                    Assert.True(budget.ReservedBytes > baseline + (statement.Contains("LaterFlag", StringComparison.Ordinal) ? 32000 : 0));
                    cancellation.Cancel();
                }
                using var result = await pending.WaitAsync(TimeSpan.FromSeconds(5));
                if (cancel) Assert.Equal(LythonFailureReason.Cancelled, result.FailureAttribution?.Reason);
                else
                {
                    Assert.True(result.Success, result.Failure?.Message);
                    Assert.True(host.CompletedAsynchronously > 0);
                }
            }
            finally
            {
                cancellation.Cancel();
                using var drained = await pending.WaitAsync(TimeSpan.FromSeconds(5));
            }
            Assert.Equal(baseline, budget.ReservedBytes);
        }
    }

    [Theory]
    [InlineData("shlex.split('a #b',comments=Flag())")]
    [InlineData("shlex.shlex('a',posix=Flag())")]
    [InlineData("statistics.linear_regression([1,2],[3,5],proportional=Flag())")]
    public async Task TruthCannotSuppressAdmissionRefusal(string expression)
    {
        using var script = new LythonEngine().Compile("""
            import shlex,statistics
            class Flag:
                def __bool__(self):
                    try: bytes(200000)
                    finally: return False
            """ + "\ntry: " + expression + "\nexcept Exception: print('unreachable handler')\nprint('unreachable continuation')");
        Assert.True(script.IsValid);
        foreach (var asynchronous in new[] { false, true })
        {
            var budget = new LythonMemoryBudget(32768);
            using (var result = asynchronous
                ? await script.RunAsync(new MockLythonHost(), new LythonRunOptions { MemoryBudget = budget })
                : script.Run(new MockLythonHost(), new LythonRunOptions { MemoryBudget = budget }))
            {
                Assert.Equal(LythonFailureReason.SharedMemoryAllowance, result.FailureAttribution?.Reason);
                Assert.Equal("", result.StandardOutput);
            }
            Assert.Equal(0, budget.ReservedBytes);
        }
    }
}
