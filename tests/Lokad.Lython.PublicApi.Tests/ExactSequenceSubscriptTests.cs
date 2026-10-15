using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class ExactSequenceSubscriptTests
{
    public static TheoryData<string, string> Programs => new()
    {
        { "for data in ([11,22],(11,22),b'ab','ab',range(11,13)):\n print(data[0],data[-1],data[True])", "11 22 22\n11 22 22\n97 98 98\na b b\n11 12 12\n" },
        { "failures=0\nfor data in ([1,2],(1,2),b'ab','ab',range(2)):\n for key in (-3,2,1<<100):\n  value='before'\n  try: value=data[key]\n  except IndexError: failures+=value=='before'\nprint(failures)", "15\n" },
        { "text='a😀𝄞z'\nprint(text[1],text[-2],text[True])", "😀 𝄞 😀\n" },
        { "data=range(0,1<<102,2)\nprint(data[1<<100]==1<<101,data[-1]==(1<<102)-2)", "True True\n" },
        { "events=[]\nclass Items:\n def __getitem__(self,key):\n  events.append(key)\n  return key*2\ndata=Items()\nprint(data[1],data[True],events)", "2 2 [1, True]\n" },
    };

    [Theory]
    [MemberData(nameof(Programs))]
    public async Task ResultsErrorsAndExcludedTargetsMatchPython(string source, string expected)
    {
        var budget = new LythonMemoryBudget(2 * 1024 * 1024);
        using var script = new LythonEngine().Compile(source, budget);
        Assert.True(script.IsValid);
        var baseline = budget.ReservedBytes;
        foreach (var asynchronous in new[] { false, true })
        {
            using (var result = asynchronous ? await script.RunAsync(new MockLythonHost()) : script.Run(new MockLythonHost()))
            {
                Assert.True(result.Success, result.Failure?.Message);
                Assert.Equal(expected, result.StandardOutput);
            }
            Assert.Equal(baseline, budget.ReservedBytes);
        }
    }

    [Theory]
    [InlineData("[seed]")]
    [InlineData("(seed,)")]
    public async Task IndexedAliasesKeepTheirSurvivingOwner(string container)
    {
        var budget = new LythonMemoryBudget(2 * 1024 * 1024);
        using var script = new LythonEngine().Compile("holder=" + container + "\nreturn holder[-1]", budget);
        var baseline = budget.ReservedBytes;
        var seed = System.Numerics.BigInteger.One << 10000;
        var options = new LythonRunOptions { Globals = new Dictionary<string, object?> { ["seed"] = seed } };
        foreach (var asynchronous in new[] { false, true })
        {
            using (var retained = asynchronous ? await script.RunAsync(new MockLythonHost(), options) : script.Run(new MockLythonHost(), options))
            {
                Assert.True(retained.Success, retained.Failure?.Message);
                Assert.Equal(seed, Assert.IsType<System.Numerics.BigInteger>(retained.ReturnValue));
                var held = budget.ReservedBytes;
                using (var next = script.Run(new MockLythonHost(), options)) Assert.True(next.Success, next.Failure?.Message);
                Assert.Equal(held, budget.ReservedBytes);
                Assert.True(held > baseline);
            }
            Assert.Equal(baseline, budget.ReservedBytes);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CustomContainersStillSuspendAndCancel(bool cancel)
    {
        const string source = "class Items:\n def __getitem__(self,key):\n  with open('events.txt','a') as output: output.write('item')\n  return key+41\nprint(Items()[1])";
        var budget = new LythonMemoryBudget(2 * 1024 * 1024);
        using var script = new LythonEngine().Compile(source, budget);
        Assert.True(script.IsValid);
        var baseline = budget.ReservedBytes;
        var host = new DelayedLythonHost();
        host.SeedFile("/events.txt", "before");
        var entered = cancel ? host.PauseWriteUntilCancellation("/events.txt") : null;
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
                    Assert.Fail(early.Failure?.Message ?? "Execution completed before its item callback suspended.");
                }
                Assert.False(pending.IsCompleted);
                Assert.True(budget.ReservedBytes > baseline);
                cancellation.Cancel();
            }
            using var result = await pending.WaitAsync(TimeSpan.FromSeconds(10));
            if (cancel) Assert.False(result.Success);
            else Assert.True(result.Success, result.Failure?.Message);
            Assert.Equal(cancel ? "" : "42\n", result.StandardOutput);
            Assert.Equal(cancel ? "before" : "beforeitem", host.ReadText("/events.txt"));
            Assert.True(host.CompletedAsynchronously > 0);
        }
        finally
        {
            cancellation.Cancel();
            using var drained = await pending.WaitAsync(TimeSpan.FromSeconds(10));
        }
        Assert.Equal(baseline, budget.ReservedBytes);
    }
}
