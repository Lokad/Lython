using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class StringAffixBoundsSuspensionTests
{
    public static IEnumerable<object[]> Suspensions()
    {
        foreach (var method in new[] { "startswith", "endswith" })
        foreach (var descriptor in new[] { false, true })
        foreach (var pause in descriptor ? new string?[] { null, "bind-start", "call-start", "bind-end", "call-end" }
            : new string?[] { null, "call-start", "call-end" }) yield return [method, descriptor, pause!];
    }

    [Theory]
    [MemberData(nameof(Suspensions))]
    public async Task BoundHooksSuspendWithoutReplayAndKeepPendingStorageFunded(string method, bool descriptor, string? pause)
    {
        var source = Program(method, descriptor);
        var budget = new LythonMemoryBudget(2 * 1024 * 1024);
        using var script = new LythonEngine(Utf8RegexProvider.Instance).Compile(source, budget);
        Assert.True(script.IsValid);
        var baseline = budget.ReservedBytes;
        var host = new DelayedLythonHost();
        var stages = descriptor ? new[] { "bind-start", "call-start", "bind-end", "call-end" } : new[] { "call-start", "call-end" };
        foreach (var stage in stages) host.SeedFile("/" + stage + ".txt", "before:");
        var entered = pause is null ? null : host.PauseWriteUntilCancellation("/" + pause + ".txt");
        using var cancellation = new CancellationTokenSource();
        var pending = script.RunAsync(host, cancellationToken: cancellation.Token);
        try
        {
            if (entered is not null)
            {
                var first = await Task.WhenAny(entered, pending).WaitAsync(TimeSpan.FromSeconds(10));
                if (ReferenceEquals(first, pending))
                {
                    using var early = await pending;
                    Assert.Fail(early.Failure?.Message ?? "The bound callback did not suspend.");
                }
                Assert.False(pending.IsCompleted);
                Assert.True(budget.ReservedBytes > baseline);
                cancellation.Cancel();
            }
            using var result = await pending.WaitAsync(TimeSpan.FromSeconds(10));
            if (pause is null)
            {
                Assert.True(result.Success, result.Failure?.Message);
                Assert.Equal("True changed\n", result.StandardOutput);
            }
            else
            {
                Assert.False(result.Success);
                Assert.Equal(LythonFailureReason.Cancelled, result.FailureAttribution?.Reason);
                Assert.Empty(result.StandardOutput);
            }
            var completed = pause is null ? stages.Length : Array.IndexOf(stages, pause);
            for (var i = 0; i < stages.Length; i++)
                Assert.Equal(i < completed ? "before:" + stages[i] : "before:", host.ReadText("/" + stages[i] + ".txt"));
            Assert.True(host.CompletedAsynchronously > 0);
        }
        finally
        {
            cancellation.Cancel();
            using var drained = await pending.WaitAsync(TimeSpan.FromSeconds(10));
        }
        Assert.Equal(baseline, budget.ReservedBytes);
    }

    [Theory]
    [InlineData("startswith", false)]
    [InlineData("startswith", true)]
    [InlineData("endswith", false)]
    [InlineData("endswith", true)]
    public async Task ExposedMethodKeepsItsReceiverAndEscapingResultsFunded(string method, bool asynchronous)
    {
        var source = "class Bound:\n def __index__(self):return 0\ntext='aé😀z'*4096\nmethod=text." + method
            + "\ntext=None\nassert method('',Bound())\nreturn method.__self__";
        var budget = new LythonMemoryBudget(2 * 1024 * 1024);
        using var script = new LythonEngine(Utf8RegexProvider.Instance).Compile(source, budget);
        var baseline = budget.ReservedBytes;
        using var result = asynchronous ? await script.RunAsync(new MockLythonHost()) : script.Run(new MockLythonHost());
        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal(string.Concat(Enumerable.Repeat("aé😀z", 4096)), result.ReturnValue);
        Assert.True(budget.ReservedBytes > baseline);
        script.Dispose();
        Assert.True(budget.ReservedBytes > 0);
        result.Dispose();
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Theory]
    [InlineData("startswith", "start", false)]
    [InlineData("startswith", "start", true)]
    [InlineData("startswith", "end", false)]
    [InlineData("startswith", "end", true)]
    [InlineData("endswith", "start", false)]
    [InlineData("endswith", "start", true)]
    [InlineData("endswith", "end", false)]
    [InlineData("endswith", "end", true)]
    public async Task RefusedBoundAdmissionStopsLaterCallbacksAndReleasesOnlyItsOwner(string method, string denied, bool asynchronous)
    {
        var source = "class Bound:\n def __init__(self,name,value):self.name=name;self.value=value\n def __index__(self):\n  with open(self.name+'.txt','a') as out:out.write(self.name)\n  if self.name=='" + denied
            + "': value='x'*2000000\n  return self.value\ntry: result='aé😀z'." + method
            + "('',Bound('start',1),Bound('end',3))\nexcept MemoryError: print('recovered')\nprint('after')";
        var budget = new LythonMemoryBudget(512 * 1024);
        using var script = new LythonEngine(Utf8RegexProvider.Instance).Compile(source, budget);
        using var retainedScript = new LythonEngine(Utf8RegexProvider.Instance).Compile("return 'held'*1024", budget);
        using var held = retainedScript.Run(new MockLythonHost());
        Assert.True(held.Success, held.Failure?.Message);
        var baseline = budget.ReservedBytes;
        var immediate = new MockLythonHost();
        var delayed = new DelayedLythonHost();
        foreach (var name in new[] { "/start.txt", "/end.txt" })
        {
            immediate.SeedFile(name, "before:");
            delayed.SeedFile(name, "before:");
        }
        using (var result = asynchronous ? await script.RunAsync(delayed) : script.Run(immediate))
        {
            Assert.False(result.Success);
            Assert.NotEqual(0, result.ExitCode);
            Assert.Equal(LythonFailureReason.SharedMemoryAllowance, result.FailureAttribution?.Reason);
            Assert.Empty(result.StandardOutput);
            Assert.Equal("before:start", asynchronous ? delayed.ReadText("/start.txt") : immediate.ReadText("/start.txt"));
            Assert.Equal(denied == "start" ? "before:" : "before:end", asynchronous ? delayed.ReadText("/end.txt") : immediate.ReadText("/end.txt"));
            Assert.Equal(string.Concat(Enumerable.Repeat("held", 1024)), held.ReturnValue);
            if (asynchronous) Assert.True(delayed.CompletedAsynchronously > 0);
        }
        Assert.Equal(baseline, budget.ReservedBytes);
        script.Dispose();
        retainedScript.Dispose();
        Assert.True(budget.ReservedBytes > 0);
        held.Dispose();
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Theory]
    [InlineData("startswith", "start", false)]
    [InlineData("startswith", "start", true)]
    [InlineData("startswith", "end", false)]
    [InlineData("startswith", "end", true)]
    [InlineData("endswith", "start", false)]
    [InlineData("endswith", "start", true)]
    [InlineData("endswith", "end", false)]
    [InlineData("endswith", "end", true)]
    public async Task SuspendedOrdinaryErrorsRemainCatchableAndKeepEarlierEffects(string method, string failed, bool badReturn)
    {
        var fault = badReturn ? "return 'bad'" : "raise ValueError('bound')";
        var source = "class Bound:\n def __init__(self,name,value):self.name=name;self.value=value\n def __index__(self):\n  with open(self.name+'.txt','a') as out:out.write(self.name)\n  if self.name=='" + failed
            + "': " + fault + "\n  return self.value\ntry: result='aé😀z'." + method
            + "('',Bound('start',1),Bound('end',3))\nexcept (TypeError,ValueError) as error:print(type(error).__name__)\nprint('after')";
        var budget = new LythonMemoryBudget(2 * 1024 * 1024);
        using var script = new LythonEngine(Utf8RegexProvider.Instance).Compile(source, budget);
        Assert.True(script.IsValid);
        var baseline = budget.ReservedBytes;
        var host = new DelayedLythonHost();
        host.SeedFile("/start.txt", "before:");
        host.SeedFile("/end.txt", "before:");
        using (var result = await script.RunAsync(host))
        {
            Assert.True(result.Success, result.Failure?.Message);
            Assert.Equal((badReturn ? "TypeError" : "ValueError") + "\nafter\n", result.StandardOutput);
            Assert.Equal("before:start", host.ReadText("/start.txt"));
            Assert.Equal(failed == "start" ? "before:" : "before:end", host.ReadText("/end.txt"));
            Assert.True(host.CompletedAsynchronously > 0);
        }
        Assert.Equal(baseline, budget.ReservedBytes);
    }

    private static string Program(string method, bool descriptor)
    {
        var hook = descriptor
            ? " @property\n def __index__(self):\n  with open('bind-'+self.name+'.txt','a') as out:out.write('bind-'+self.name)\n  return self.convert\n"
            : " def __index__(self):return self.convert()\n";
        return "class Bound:\n def __init__(self,name,value):self.name=name;self.value=value\n"
            + hook + " def convert(self):\n  global text\n  with open('call-'+self.name+'.txt','a') as out:out.write('call-'+self.name)\n  text='changed'\n  return self.value\n"
            + "text='aé😀z'*1024\nresult=text." + method + "('" + (method == "startswith" ? "é" : "😀")
            + "',Bound('start',1),Bound('end',3))\nprint(result,text)";
    }
}
