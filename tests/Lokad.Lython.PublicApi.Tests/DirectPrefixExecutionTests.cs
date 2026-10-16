using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class DirectPrefixExecutionTests
{
    public static IEnumerable<object[]> ArgumentCases()
    {
        foreach (var form in new[] { "ordinary", "exposed", "unbound", "expanded" })
        foreach (var cancel in new[] { false, true }) yield return [form, cancel];
    }

    [Theory]
    [MemberData(nameof(ArgumentCases))]
    public async Task SuspendedArgumentKeepsReceiverAndTupleSemantics(string form, bool cancel)
    {
        var call = form switch
        {
            "ordinary" => "text[0].startswith(argument())",
            "exposed" => "callback(argument())",
            "unbound" => "str.startswith(text[0],argument())",
            _ => "callback(*[argument()])"
        };
        var source = "text=['alpha']\n" + (form is "exposed" or "expanded" ? "callback=text[0].startswith\n" : "")
            + "def argument():\n with open('argument.txt','a') as output: output.write('x')\n text[0]='other'\n return ('a',None)\nassert "
            + call + "\nassert text[0]=='other'\nprint('ok')";
        var budget = new LythonMemoryBudget(2 * 1024 * 1024);
        using var script = new LythonEngine().Compile(source, budget);
        Assert.True(script.IsValid, string.Join("; ", script.Diagnostics.Select(d => d.Message)));
        var baseline = budget.ReservedBytes;
        var host = new DelayedLythonHost();
        host.SeedFile("/argument.txt", "before");
        var entered = cancel ? host.PauseWriteUntilCancellation("/argument.txt") : null;
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
                    Assert.Fail(early.Failure?.Message ?? "Prefix call completed before argument suspension.");
                }
                Assert.False(pending.IsCompleted);
                Assert.True(budget.ReservedBytes > baseline);
                cancellation.Cancel();
            }
            using var result = await pending.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.Equal(!cancel, result.Success);
            Assert.Equal(cancel ? "" : "ok\n", result.StandardOutput);
            Assert.Equal(cancel ? "before" : "beforex", host.ReadText("/argument.txt"));
            if (!cancel) Assert.True(host.CompletedAsynchronously > 0);
            else Assert.True(entered!.IsCompletedSuccessfully);
        }
        finally
        {
            cancellation.Cancel();
            using var drained = await pending.WaitAsync(TimeSpan.FromSeconds(10));
        }
        Assert.Equal(baseline, budget.ReservedBytes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ExposedReceiverAliasesRemainFundedAcrossExecutions(bool asynchronous)
    {
        var budget = new LythonMemoryBudget(2 * 1024 * 1024);
        using var script = new LythonEngine().Compile("text='a'*4096\nold=text\ncallback=text.startswith\ntext='other'\nassert callback('a') and old.startswith('a')\nreturn old", budget);
        Assert.True(script.IsValid);
        var baseline = budget.ReservedBytes;
        using (var retained = asynchronous ? await script.RunAsync(new MockLythonHost()) : script.Run(new MockLythonHost()))
        {
            Assert.True(retained.Success, retained.Failure?.Message);
            Assert.Equal(new string('a', 4096), Assert.IsType<string>(retained.ReturnValue));
            var held = budget.ReservedBytes;
            using (var next = script.Run(new MockLythonHost())) Assert.True(next.Success, next.Failure?.Message);
            Assert.True(held > baseline);
            Assert.Equal(held, budget.ReservedBytes);
        }
        Assert.Equal(baseline, budget.ReservedBytes);
    }

    [Theory]
    [InlineData("None")]
    [InlineData("(None,)")]
    public async Task InvalidPrefixesKeepCallAndCallerSpans(string value)
    {
        using var script = new LythonEngine().Compile("def run(text,value):\n return text.startswith(value)\nrun('abc'," + value + ")");
        Assert.True(script.IsValid);
        foreach (var asynchronous in new[] { false, true })
        {
            using var result = asynchronous ? await script.RunAsync(new MockLythonHost()) : script.Run(new MockLythonHost());
            Assert.False(result.Success);
            var failure = Assert.IsType<LythonRuntimeFailure>(result.Failure);
            Assert.Equal("TypeError", failure.ExceptionType);
            Assert.Equal(2, failure.Span?.Line);
            Assert.Equal(9, failure.Span?.Column);
            Assert.Contains(failure.StackTrace, frame => frame.FunctionName == "run" && frame.Span?.Line == 3);
        }
    }
}
